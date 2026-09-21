<#
.SYNOPSIS
    Prueba de estrés de concurrencia optimista exigida por la consigna.

.DESCRIPTION
    Consta de dos fases.

    FASE 1 - Ráfaga
    Dispara N pujas idénticas en paralelo, desde un mismo proceso y sobre una conexión ya
    establecida, de modo que las peticiones lleguen al servidor prácticamente en el mismo
    milisegundo.

    El resultado esperado es que se persista EXACTAMENTE UNA y que el resto se rechace con
    HTTP 409. Los rechazos provienen de dos mecanismos complementarios, ambos con código 409:

      * "Conflicto de concurrencia": dos transacciones leyeron la misma versión de la fila y el
        token rowversion invalidó a la más lenta. Esto es bloqueo optimista puro.
      * "Conflicto con el estado actual": la petición perdedora alcanzó a leer el estado ya
        actualizado y la regla de negocio la rechazó antes de escribir.

    FASE 2 - Integridad
    Que gane una sola puja no alcanza: hay que comprobar que la ráfaga no dejó el dinero
    descuadrado. Se verifica, sobre todas las cuentas de prueba, que el libro mayor reconstruya
    cada saldo, que la garantía viva de la subasta coincida con el importe líder (sólo el líder
    tiene fondos congelados) y que el dinero del sistema se conserve.

    El script se configura solo: elige la cuenta que no lidera la subasta y acredita fondos si
    hacen falta, así puede ejecutarse varias veces seguidas.

.EXAMPLE
    ./concurrency-test.ps1
    ./concurrency-test.ps1 -BaseUrl http://localhost:5080 -AuctionId 1 -Requests 15
#>
[CmdletBinding()]
param(
    [string]   $BaseUrl = "http://localhost:5080",
    [int]      $AuctionId = 1,
    [int]      $Requests = 12,
    [string[]] $Accounts = @("comprador1@test.com", "comprador2@test.com"),
    [string[]] $AllAccounts = @("vendedor@test.com", "comprador1@test.com", "comprador2@test.com", "sinfondos@test.com"),
    [string]   $Password = "Password123!"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

function Get-AccessToken {
    param([string] $Email)

    $body = @{ email = $Email; password = $Password } | ConvertTo-Json
    return (Invoke-RestMethod -Uri "$BaseUrl/api/v1/sessions" -Method Post -Body $body -ContentType "application/json").token
}

function Get-AuthorizationHeader {
    param([string] $Token)
    return @{ Authorization = "Bearer $Token" }
}

Write-Host "== Prueba de concurrencia optimista ==" -ForegroundColor Cyan

$auction = Invoke-RestMethod -Uri "$BaseUrl/api/v1/auctions/$AuctionId" -Method Get

if ($auction.status -ne "ACTIVE") {
    Write-Warning "La subasta $AuctionId esta en estado '$($auction.status)'. Indica una ACTIVA con -AuctionId."
    exit 1
}

# Se elige una cuenta que no lidere la subasta: si liderara, la regla de negocio rechazaria las
# N peticiones y la prueba no demostraria nada sobre concurrencia.
$bidder = $null
foreach ($email in $Accounts) {
    $candidateToken = Get-AccessToken -Email $email
    $detail = Invoke-RestMethod -Uri "$BaseUrl/api/v1/auctions/$AuctionId" -Method Get `
        -Headers (Get-AuthorizationHeader $candidateToken)

    if (-not $detail.isLeading -and -not $detail.isSeller) {
        $bidder = [pscustomobject]@{ Email = $email; Token = $candidateToken }
        break
    }
}

if ($null -eq $bidder) {
    Write-Warning "Ninguna de las cuentas indicadas puede pujar en la subasta $AuctionId."
    exit 1
}

$amount = $auction.minimumNextBid
$headers = Get-AuthorizationHeader $bidder.Token
$balance = Invoke-RestMethod -Uri "$BaseUrl/api/v1/wallets/me" -Method Get -Headers $headers

if ($balance.available -lt $amount) {
    $missing = [math]::Ceiling($amount - $balance.available)
    Write-Host "Acreditando $missing para que la cuenta pueda respaldar la puja..." -ForegroundColor DarkGray
    $deposit = @{ amount = $missing } | ConvertTo-Json
    Invoke-RestMethod -Uri "$BaseUrl/api/v1/wallets/me/deposits" -Method Post -Headers $headers `
        -Body $deposit -ContentType "application/json" | Out-Null
}

$bidsBefore = $auction.bidCount

Write-Host "Subasta : $($auction.title)"
Write-Host "Postor  : $($bidder.Email)"
Write-Host "Oferta  : $($auction.currentAmount) -> se enviaran $Requests pujas de $amount en paralelo" -ForegroundColor Yellow

# --- FASE 1: rafaga ------------------------------------------------------------------------

$client = [System.Net.Http.HttpClient]::new()
$client.DefaultRequestHeaders.Authorization =
    [System.Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $bidder.Token)

# Peticion de calentamiento: evita que el costo del handshake TCP escalone la rafaga.
$client.GetAsync("$BaseUrl/api/v1/categories").Result | Out-Null

$bidBody = "{""amount"":$amount}"
$tasks = [System.Collections.Generic.List[System.Threading.Tasks.Task]]::new()

foreach ($index in 1..$Requests) {
    $content = [System.Net.Http.StringContent]::new($bidBody, [System.Text.Encoding]::UTF8, "application/json")
    $tasks.Add($client.PostAsync("$BaseUrl/api/v1/auctions/$AuctionId/bids", $content))
}

[System.Threading.Tasks.Task]::WaitAll($tasks.ToArray())

$results = foreach ($task in $tasks) {
    $response = $task.Result
    $body = $response.Content.ReadAsStringAsync().Result

    if ($response.IsSuccessStatusCode) {
        $reason = "PUJA ACEPTADA"
    }
    else {
        $reason = ($body | ConvertFrom-Json).title
    }

    [pscustomobject]@{
        StatusCode = [int] $response.StatusCode
        Reason     = $reason
    }
}

$client.Dispose()

Write-Host ""
Write-Host "Resultado de la rafaga:" -ForegroundColor Cyan
$results | Group-Object StatusCode, Reason | ForEach-Object {
    Write-Host ("  {0,3} peticion(es) -> HTTP {1}" -f $_.Count, $_.Name)
}

# Se fuerza el arreglo: PowerShell 5.1 devuelve un escalar sin .Count cuando hay un solo elemento.
$accepted = @($results | Where-Object { $_.StatusCode -eq 201 }).Count
$conflicts = @($results | Where-Object { $_.StatusCode -eq 409 }).Count

# --- FASE 2: integridad --------------------------------------------------------------------

Write-Host ""
Write-Host "Verificacion de integridad:" -ForegroundColor Cyan

$afterAuction = Invoke-RestMethod -Uri "$BaseUrl/api/v1/auctions/$AuctionId" -Method Get
$newBids = $afterAuction.bidCount - $bidsBefore

$problems = [System.Collections.Generic.List[string]]::new()

if ($newBids -ne $accepted) {
    $problems.Add("Se registraron $newBids puja(s) nueva(s) pero se aceptaron $accepted.")
}

$totalDeposited = 0.0
$totalBalance = 0.0
$escrowForAuction = 0.0

foreach ($email in $AllAccounts) {
    $token = Get-AccessToken -Email $email
    $accountHeaders = Get-AuthorizationHeader $token

    $wallet = Invoke-RestMethod -Uri "$BaseUrl/api/v1/wallets/me" -Method Get -Headers $accountHeaders
    # Se asigna antes de envolver: @(Invoke-RestMethod ...) devolveria un arreglo de UN
    # elemento que contiene al arreglo real, en lugar de enumerarlo.
    $ledgerResponse = Invoke-RestMethod -Uri "$BaseUrl/api/v1/wallets/me/transactions?count=200" -Method Get -Headers $accountHeaders
    $entries = @($ledgerResponse)

    $rebuiltTotal = 0.0
    $rebuiltHeld = 0.0

    foreach ($entry in $entries) {
        switch ($entry.type) {
            "DEPOSIT" { $rebuiltTotal += $entry.amount }
            "PAYOUT"  { $rebuiltTotal += $entry.amount }
            "PAYMENT" { $rebuiltTotal -= $entry.amount }
        }

        switch ($entry.type) {
            "HOLD"    { $rebuiltHeld += $entry.amount }
            "RELEASE" { $rebuiltHeld -= $entry.amount }
            "PAYMENT" { $rebuiltHeld -= $entry.amount }
        }

        if ($entry.auctionId -eq $AuctionId) {
            switch ($entry.type) {
                "HOLD"    { $escrowForAuction += $entry.amount }
                "RELEASE" { $escrowForAuction -= $entry.amount }
                "PAYMENT" { $escrowForAuction -= $entry.amount }
            }
        }

        if ($entry.type -eq "DEPOSIT") {
            $totalDeposited += $entry.amount
        }
    }

    $totalBalance += $wallet.total

    if ([math]::Abs($rebuiltTotal - $wallet.total) -gt 0.001) {
        $problems.Add("El libro mayor de $email no reconstruye su saldo total.")
    }

    if ([math]::Abs($rebuiltHeld - $wallet.held) -gt 0.001) {
        $problems.Add("El libro mayor de $email no reconstruye su saldo retenido.")
    }

    Write-Host ("  {0,-22} total {1,14:N2}  retenido {2,12:N2}" -f $email, $wallet.total, $wallet.held)
}

# Solo el lider puede tener fondos congelados por esta subasta, y por el importe exacto.
if ($afterAuction.bidCount -gt 0) {
    $expectedEscrow = $afterAuction.currentAmount
}
else {
    $expectedEscrow = 0.0
}

if ([math]::Abs($escrowForAuction - $expectedEscrow) -gt 0.001) {
    $problems.Add("La garantia viva de la subasta ($escrowForAuction) no coincide con el importe lider ($expectedEscrow).")
}

if ([math]::Abs($totalDeposited - $totalBalance) -gt 0.001) {
    $problems.Add("El dinero del sistema no se conserva: depositado $totalDeposited contra saldos $totalBalance.")
}

Write-Host ""
Write-Host ("  garantia viva de la subasta {0} : {1:N2}  (importe lider {2:N2})" -f $AuctionId, $escrowForAuction, $expectedEscrow)
Write-Host ("  depositado en el sistema        : {0:N2}" -f $totalDeposited)
Write-Host ("  suma de saldos                  : {0:N2}" -f $totalBalance)

# --- Veredicto -----------------------------------------------------------------------------

Write-Host ""

$burstOk = ($accepted -eq 1) -and ($conflicts -eq ($Requests - 1))

if ($burstOk -and $problems.Count -eq 0) {
    Write-Host "OK: se registro exactamente 1 puja, se rechazaron $conflicts con HTTP 409" -ForegroundColor Green
    Write-Host "    y las invariantes economicas se mantienen intactas." -ForegroundColor Green
    exit 0
}

if (-not $burstOk) {
    Write-Host "FALLO en la rafaga: aceptadas=$accepted, conflictos=$conflicts (se esperaba 1 y $($Requests - 1))." -ForegroundColor Red
}

foreach ($problem in $problems) {
    Write-Host "FALLO de integridad: $problem" -ForegroundColor Red
}

exit 1
