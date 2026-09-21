#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# Prueba de estrés de concurrencia optimista con curl (variante Bash).
#
# FASE 1 - Ráfaga: dispara N pujas idénticas en paralelo sobre la misma subasta y comprueba
# que la base registre EXACTAMENTE UNA y rechace el resto con HTTP 409.
#
# FASE 2 - Integridad: verifica que la ráfaga no haya descuadrado el dinero. Sobre todas las
# cuentas de prueba comprueba que el libro mayor reconstruya cada saldo, que la garantía viva
# de la subasta coincida con el importe líder y que el dinero del sistema se conserve.
#
# Uso:     ./concurrency-test.sh [url_base] [id_subasta] [cantidad_peticiones]
# Ejemplo: ./concurrency-test.sh http://localhost:5080 1 12
#
# Requiere python en el PATH para leer las respuestas JSON sin depender de jq.
# ---------------------------------------------------------------------------
set -euo pipefail

BASE_URL="${1:-http://localhost:5080}"
AUCTION_ID="${2:-1}"
REQUESTS="${3:-12}"
PASSWORD="Password123!"
ACCOUNTS=("comprador1@test.com" "comprador2@test.com")
ALL_ACCOUNTS=("vendedor@test.com" "comprador1@test.com" "comprador2@test.com" "sinfondos@test.com")

TEMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TEMP_DIR"' EXIT

read_field() {
    # Extrae un campo simple de un JSON sin depender de jq.
    python -c "import sys,json;print(json.load(sys.stdin)[sys.argv[1]])" "$1"
}

get_access_token() {
    curl -s -X POST "$BASE_URL/api/v1/sessions" \
        -H "Content-Type: application/json" \
        -d "{\"email\":\"$1\",\"password\":\"$PASSWORD\"}" | read_field token
}

echo "== Prueba de concurrencia optimista =="

AUCTION_JSON="$(curl -s "$BASE_URL/api/v1/auctions/$AUCTION_ID")"
STATUS="$(echo "$AUCTION_JSON" | read_field status)"

if [[ "$STATUS" != "ACTIVE" ]]; then
    echo "La subasta $AUCTION_ID esta en estado '$STATUS'. Elegi una ACTIVA." >&2
    exit 1
fi

AMOUNT="$(echo "$AUCTION_JSON" | read_field minimumNextBid)"
BIDS_BEFORE="$(echo "$AUCTION_JSON" | read_field bidCount)"

# Se elige la cuenta que no lidera: si liderara, la regla de negocio rechazaria todas las
# peticiones y la prueba no demostraria nada sobre concurrencia.
TOKEN=""
for account in "${ACCOUNTS[@]}"; do
    candidate_token="$(get_access_token "$account")"
    detail="$(curl -s "$BASE_URL/api/v1/auctions/$AUCTION_ID" -H "Authorization: Bearer $candidate_token")"
    is_leading="$(echo "$detail" | read_field isLeading)"
    is_seller="$(echo "$detail" | read_field isSeller)"

    if [[ "$is_leading" == "False" && "$is_seller" == "False" ]]; then
        TOKEN="$candidate_token"
        echo "Postor  : $account"
        break
    fi
done

if [[ -z "$TOKEN" ]]; then
    echo "Ninguna de las cuentas indicadas puede pujar en la subasta $AUCTION_ID." >&2
    exit 1
fi

# Se acreditan fondos si hacen falta, para que el script pueda repetirse varias veces seguidas.
AVAILABLE="$(curl -s "$BASE_URL/api/v1/wallets/me" -H "Authorization: Bearer $TOKEN" | read_field available)"
MISSING="$(python -c "import math,sys;print(max(0, math.ceil(float(sys.argv[1]) - float(sys.argv[2]))))" "$AMOUNT" "$AVAILABLE")"

if [[ "$MISSING" -gt 0 ]]; then
    echo "Acreditando $MISSING para que la cuenta pueda respaldar la puja..."
    curl -s -X POST "$BASE_URL/api/v1/wallets/me/deposits" \
        -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
        -d "{\"amount\":$MISSING}" > /dev/null
fi

echo "Subasta : $(echo "$AUCTION_JSON" | read_field title)"
echo "Oferta  : se enviaran $REQUESTS pujas de $AMOUNT en paralelo"

# --- FASE 1: rafaga --------------------------------------------------------

for index in $(seq 1 "$REQUESTS"); do
    curl -s -o "$TEMP_DIR/body_$index" -w "%{http_code}" \
        -X POST "$BASE_URL/api/v1/auctions/$AUCTION_ID/bids" \
        -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
        -d "{\"amount\":$AMOUNT}" > "$TEMP_DIR/code_$index" &
done
wait

ACCEPTED=0
CONFLICTS=0

echo
echo "Resultado de la rafaga:"
for index in $(seq 1 "$REQUESTS"); do
    code="$(cat "$TEMP_DIR/code_$index")"

    if [[ "$code" == "201" ]]; then
        ACCEPTED=$((ACCEPTED + 1))
        reason="PUJA ACEPTADA"
    else
        CONFLICTS=$((CONFLICTS + 1))
        reason="$(read_field title < "$TEMP_DIR/body_$index" 2>/dev/null || echo "sin detalle")"
    fi

    echo "  HTTP $code  $reason"
done

# --- FASE 2: integridad ----------------------------------------------------

echo
echo "Verificacion de integridad:"

TOKENS_FILE="$TEMP_DIR/tokens"
: > "$TOKENS_FILE"
for account in "${ALL_ACCOUNTS[@]}"; do
    echo "$account $(get_access_token "$account")" >> "$TOKENS_FILE"
done

AFTER_JSON="$(curl -s "$BASE_URL/api/v1/auctions/$AUCTION_ID")"

# Se desactiva errexit alrededor del bloque: su codigo de salida es el veredicto de la fase,
# no un fallo que deba abortar el script antes de imprimir el resumen.
set +e
BASE_URL="$BASE_URL" AUCTION_ID="$AUCTION_ID" ACCEPTED="$ACCEPTED" BIDS_BEFORE="$BIDS_BEFORE" \
TOKENS_FILE="$TOKENS_FILE" AFTER_JSON="$AFTER_JSON" python - <<'PY'
import json, os, sys, urllib.request

base = os.environ['BASE_URL']
auction_id = int(os.environ['AUCTION_ID'])
accepted = int(os.environ['ACCEPTED'])
bids_before = int(os.environ['BIDS_BEFORE'])
after = json.loads(os.environ['AFTER_JSON'])

def get(path, token):
    request = urllib.request.Request(base + path, headers={'Authorization': 'Bearer ' + token})
    return json.loads(urllib.request.urlopen(request).read().decode('utf-8'))

problems = []

new_bids = after['bidCount'] - bids_before
if new_bids != accepted:
    problems.append(f"Se registraron {new_bids} puja(s) nueva(s) pero se aceptaron {accepted}.")

total_deposited = total_balance = escrow_for_auction = 0.0

for line in open(os.environ['TOKENS_FILE'], encoding='utf-8'):
    email, token = line.split()
    wallet = get('/api/v1/wallets/me', token)
    entries = get('/api/v1/wallets/me/transactions?count=200', token)

    rebuilt_total = sum(
        e['amount'] if e['type'] in ('DEPOSIT', 'PAYOUT') else -e['amount'] if e['type'] == 'PAYMENT' else 0
        for e in entries)
    rebuilt_held = sum(
        e['amount'] if e['type'] == 'HOLD' else -e['amount'] if e['type'] in ('RELEASE', 'PAYMENT') else 0
        for e in entries)

    if abs(rebuilt_total - wallet['total']) > 1e-3:
        problems.append(f"El libro mayor de {email} no reconstruye su saldo total.")
    if abs(rebuilt_held - wallet['held']) > 1e-3:
        problems.append(f"El libro mayor de {email} no reconstruye su saldo retenido.")

    escrow_for_auction += sum(
        e['amount'] if e['type'] == 'HOLD' else -e['amount'] if e['type'] in ('RELEASE', 'PAYMENT') else 0
        for e in entries if e['auctionId'] == auction_id)

    total_deposited += sum(e['amount'] for e in entries if e['type'] == 'DEPOSIT')
    total_balance += wallet['total']

    print(f"  {email:<22} total {wallet['total']:>14,.2f}  retenido {wallet['held']:>12,.2f}")

# Solo el lider puede tener fondos congelados por esta subasta, y por el importe exacto.
expected_escrow = after['currentAmount'] if after['bidCount'] > 0 else 0.0

if abs(escrow_for_auction - expected_escrow) > 1e-3:
    problems.append(
        f"La garantia viva de la subasta ({escrow_for_auction}) no coincide con el importe lider ({expected_escrow}).")

if abs(total_deposited - total_balance) > 1e-3:
    problems.append(
        f"El dinero del sistema no se conserva: depositado {total_deposited} contra saldos {total_balance}.")

print()
print(f"  garantia viva de la subasta {auction_id} : {escrow_for_auction:,.2f}  (importe lider {expected_escrow:,.2f})")
print(f"  depositado en el sistema        : {total_deposited:,.2f}")
print(f"  suma de saldos                  : {total_balance:,.2f}")

for problem in problems:
    print(f"FALLO de integridad: {problem}")

sys.exit(1 if problems else 0)
PY
INTEGRITY_OK=$?
set -e

# --- Veredicto -------------------------------------------------------------

echo
if [[ "$ACCEPTED" -eq 1 && "$CONFLICTS" -eq $((REQUESTS - 1)) && "$INTEGRITY_OK" -eq 0 ]]; then
    echo "OK: se registro exactamente 1 puja, se rechazaron $CONFLICTS con HTTP 409"
    echo "    y las invariantes economicas se mantienen intactas."
    exit 0
fi

if [[ "$ACCEPTED" -ne 1 || "$CONFLICTS" -ne $((REQUESTS - 1)) ]]; then
    echo "FALLO en la rafaga: aceptadas=$ACCEPTED, conflictos=$CONFLICTS (se esperaba 1 y $((REQUESTS - 1)))."
fi

exit 1
