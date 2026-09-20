/**
 * Sala de subasta en vivo.
 *
 * Sincroniza el estado con el backend por WebSockets (SignalR) y mantiene un sondeo de respaldo
 * cada 5 segundos por si el canal en tiempo real no está disponible, de modo que la vista nunca
 * termine mostrando información vieja.
 */
import { api, ApiError } from "./api.js";
import { isAuthenticated, getUser, requireSession } from "./session.js";
import {
    renderNavigation, showLoading, showEmpty, notify, notifyError, formatCurrency,
    formatTime, formatDateTime, escapeHtml, statusClass, statusLabel, secondsRemaining,
    formatCountdown, countdownClass, runWithButton
} from "./ui.js";

const HUB_PATH = "/hubs/auctions";
const POLLING_INTERVAL_MS = 5000;

const auctionId = Number(new URLSearchParams(window.location.search).get("id"));
const content = document.getElementById("roomContent");
const template = document.getElementById("roomTemplate");

let auction = null;
let history = [];
let connection = null;
let clockTimer = null;
let pollingTimer = null;

// ---------------------------------------------------------------------------
// Dibujado
// ---------------------------------------------------------------------------

function mountLayout() {
    content.innerHTML = "";
    content.appendChild(template.content.cloneNode(true));

    document.getElementById("bidForm").addEventListener("submit", submitBid);
}

function refreshProductDetails() {
    const image = document.getElementById("productImage");
    image.src = auction.imageUrl;
    image.alt = auction.title;
    image.onerror = () => { image.style.display = "none"; };

    document.getElementById("auctionTitle").textContent = auction.title;
    document.getElementById("auctionDescription").textContent = auction.description;
    document.getElementById("auctionCategory").textContent = auction.category;
    document.getElementById("auctionSeller").textContent = auction.seller;
    document.getElementById("startingPrice").textContent = formatCurrency(auction.startingPrice);
    document.getElementById("minimumIncrement").textContent = formatCurrency(auction.minimumIncrement);
    document.getElementById("closingDate").textContent = formatDateTime(auction.endsAtUtc);

    const status = document.getElementById("auctionStatus");
    status.className = statusClass(auction.status);
    status.textContent = statusLabel(auction.status);

    document.getElementById("clockLabel").textContent =
        auction.status === "SCHEDULED" ? "Comienza en" : "Tiempo restante";
}

function refreshFinancialPanel() {
    document.getElementById("currentAmount").textContent = formatCurrency(auction.currentAmount);
    document.getElementById("bidCount").textContent = auction.bidCount;
    document.getElementById("leadingBidder").textContent = auction.leadingBidder ?? "sin ofertas";

    refreshBidderIndicator();
    refreshBidConsole();
}

/**
 * Indicador de situación del postor. Es lo primero que mira quien entra a la sala: si va
 * ganando, si lo superaron, o si todavía no participó.
 */
function refreshBidderIndicator() {
    const indicator = document.getElementById("bidderIndicator");

    if (!isAuthenticated()) {
        indicator.className = "sy-indicator sy-indicator-neutral";
        indicator.innerHTML = `<i class="bi bi-info-circle"></i> Iniciá sesión para participar de la subasta.`;
        return;
    }

    if (auction.isSeller) {
        indicator.className = "sy-indicator sy-indicator-neutral";
        indicator.innerHTML = `<i class="bi bi-shop"></i> Sos el vendedor de esta publicación.`;
        return;
    }

    if (auction.isLeading) {
        indicator.className = "sy-indicator sy-indicator-leading";
        indicator.innerHTML = `<i class="bi bi-trophy-fill"></i> Estás liderando la subasta.`;
        return;
    }

    // Haber ofertado y no liderar significa exactamente una cosa: te superaron, y la garantía
    // que estaba congelada volvió a estar disponible.
    const hasParticipated = history.some((bid) => bid.isMine);

    if (hasParticipated) {
        indicator.className = "sy-indicator sy-indicator-outbid";
        indicator.innerHTML = `<i class="bi bi-arrow-down-circle-fill"></i> Te superaron: tu garantía fue liberada.`;
        return;
    }

    indicator.className = "sy-indicator sy-indicator-neutral";
    indicator.innerHTML = `<i class="bi bi-hammer"></i> Todavía no ofertaste en esta subasta.`;
}

/**
 * Habilita o bloquea la consola según el estado de la subasta y el rol del usuario.
 * Impedir el envío cuando la operación es imposible evita llamadas inútiles al backend y, sobre
 * todo, evita que el usuario reciba un error por algo que la pantalla ya sabía de antemano.
 */
function refreshBidConsole() {
    const form = document.getElementById("bidForm");
    const amountField = document.getElementById("bidAmount");
    const button = document.getElementById("bidButton");
    const help = document.getElementById("bidHelp");
    const blockedMessage = document.getElementById("blockedMessage");

    const blockingReason = getBlockingReason();

    // Sólo se fija el mínimo. El incremento no se usa como "step" del campo porque el dominio
    // no exige que la oferta sea múltiplo de él: basta con alcanzar el mínimo, y una oferta
    // intermedia como 52.000 sobre un mínimo de 50.000 es perfectamente válida.
    amountField.min = auction.minimumNextBid;

    // Sugerencia automática: la oferta líder más el incremento mínimo. No se pisa el valor
    // mientras el usuario está escribiendo en el campo.
    if (document.activeElement !== amountField) {
        amountField.value = auction.minimumNextBid;
    }

    help.innerHTML = `Oferta mínima admitida: <strong>${formatCurrency(auction.minimumNextBid)}</strong>
                      (líder ${formatCurrency(auction.currentAmount)} + incremento ${formatCurrency(auction.minimumIncrement)}).`;

    form.classList.toggle("d-none", Boolean(blockingReason));
    button.disabled = Boolean(blockingReason);
    blockedMessage.textContent = blockingReason ?? "";
}

/** Devuelve el motivo por el que no se puede ofertar, o null si la puja está habilitada. */
function getBlockingReason() {
    if (!isAuthenticated()) {
        return "Iniciá sesión para poder ofertar.";
    }

    if (auction.isSeller) {
        return "Un vendedor no puede ofertar en su propia subasta.";
    }

    if (auction.status === "SCHEDULED") {
        return `Las ofertas se habilitan el ${formatDateTime(auction.startsAtUtc)}`;
    }

    if (auction.status !== "ACTIVE") {
        return "La subasta ya está cerrada.";
    }

    if (auction.isLeading) {
        return "Ya sos el postor líder: esperá a que alguien te supere.";
    }

    return null;
}

function refreshHistory(highlightedBidId = null) {
    const container = document.getElementById("bidHistory");

    if (history.length === 0) {
        showEmpty(container, "Todavía no hay ofertas. Podés ser el primero.", "bi-hammer");
        return;
    }

    container.innerHTML = history
        .map((bid) => `
            <div class="sy-bid ${bid.isMine ? "sy-bid-own" : "sy-bid-other"}
                        ${bid.id === highlightedBidId ? "sy-bid-new" : ""}">
                <div>
                    <div class="fw-semibold">
                        ${escapeHtml(bid.bidder)}${bid.isMine ? ' <span class="badge bg-primary ms-1">Vos</span>' : ""}
                    </div>
                    <small class="sy-muted"><i class="bi bi-clock me-1"></i>${formatTime(bid.placedAtUtc)}</small>
                </div>
                <div class="fw-bold">${formatCurrency(bid.amount)}</div>
            </div>`)
        .join("");
}

// ---------------------------------------------------------------------------
// Reloj
// ---------------------------------------------------------------------------

function startClock() {
    clearInterval(clockTimer);

    const update = () => {
        const clock = document.getElementById("auctionClock");

        if (!clock) {
            return;
        }

        const reference = auction.status === "SCHEDULED" ? auction.startsAtUtc : auction.endsAtUtc;
        const remaining = secondsRemaining(reference);

        clock.textContent = auction.status === "ACTIVE" || auction.status === "SCHEDULED"
            ? formatCountdown(remaining)
            : "Cerrada";
        clock.className = `sy-clock-large ${countdownClass(remaining)}`;
    };

    update();
    clockTimer = setInterval(update, 1000);
}

// ---------------------------------------------------------------------------
// Carga de datos
// ---------------------------------------------------------------------------

async function loadAuction({ highlightBidId = null, silent = false } = {}) {
    if (!silent) {
        showLoading(content, "Entrando a la sala...");
    }

    try {
        const detail = await api.getAuction(auctionId);
        const isFirstLoad = auction === null;

        auction = detail;
        history = detail.history;

        if (isFirstLoad) {
            mountLayout();
            startClock();
        }

        refreshProductDetails();
        refreshFinancialPanel();
        refreshHistory(highlightBidId);

        if (isAuthenticated()) {
            await loadBalance();
        }
    } catch (error) {
        if (!silent) {
            showEmpty(content, "No se pudo cargar la subasta solicitada.", "bi-exclamation-triangle");
        }

        notifyError(error);
    }
}

async function loadBalance() {
    try {
        const balance = await api.getBalance();
        document.getElementById("balanceBlock").classList.remove("d-none");
        document.getElementById("availableBalance").textContent = formatCurrency(balance.available);
        document.getElementById("heldBalance").textContent = formatCurrency(balance.held);
    } catch {
        // El saldo es informativo: si falla, la sala sigue siendo usable y el backend volverá a
        // validarlo de todas formas cuando se envíe la oferta.
    }
}

// ---------------------------------------------------------------------------
// Envío de la oferta
// ---------------------------------------------------------------------------

async function submitBid(event) {
    event.preventDefault();

    if (!requireSession()) {
        return;
    }

    const amountField = document.getElementById("bidAmount");
    const amount = Number(amountField.value);

    // Validación en pantalla antes de gastar una llamada al backend.
    if (!Number.isFinite(amount) || amount < auction.minimumNextBid) {
        notify("warning", `La oferta debe ser de al menos ${formatCurrency(auction.minimumNextBid)}.`);
        amountField.focus();
        return;
    }

    const button = document.getElementById("bidButton");

    try {
        const result = await runWithButton(button, "Enviando", () => api.placeBid(auctionId, amount));

        notify("success", `Oferta de ${formatCurrency(result.amount)} confirmada. Sos el nuevo líder.`);

        if (result.wasExtended) {
            notify("info", "Regla anti-sniping: el cierre se extendió 2 minutos para dar tiempo a responder.", 8000);
        }

        await loadAuction({ highlightBidId: result.bidId, silent: true });
    } catch (error) {
        handleBidError(error);

        // Se recarga igual: si la oferta fue rechazada por un conflicto, la sala cambió y hay que
        // mostrar el estado nuevo en lugar de dejar el anterior en pantalla.
        await loadAuction({ silent: true });
    }
}

function handleBidError(error) {
    if (!(error instanceof ApiError)) {
        notifyError(error);
        return;
    }

    if (error.isInsufficientFunds) {
        notify("warning", `${error.message} Cargá saldo en tu billetera para seguir participando.`, 8000);
        return;
    }

    if (error.isConflict) {
        notify("warning", `${error.message} Se actualizó el estado de la sala.`, 8000);
        return;
    }

    notifyError(error);
}

// ---------------------------------------------------------------------------
// Tiempo real
// ---------------------------------------------------------------------------

function markConnection(label, icon, colorClass) {
    const indicator = document.getElementById("liveConnection");

    if (indicator) {
        indicator.innerHTML = `<i class="bi ${icon} me-1 ${colorClass}"></i>${escapeHtml(label)}`;
    }
}

async function connectToLiveFeed() {
    if (typeof signalR === "undefined") {
        // La librería no cargó: el sondeo de respaldo mantiene la sala utilizable igual.
        markConnection("Sondeo periódico", "bi-arrow-repeat", "text-warning");
        return;
    }

    connection = new signalR.HubConnectionBuilder()
        .withUrl(HUB_PATH)
        .withAutomaticReconnect()
        .build();

    connection.on("BidPlaced", async (notification) => {
        // Se recarga el estado desde la API en lugar de confiar sólo en el payload: así el
        // historial y los saldos quedan siempre consistentes con la base de datos.
        await loadAuction({ silent: true });

        const user = getUser();

        if (user && notification.outbidBidderId === user.id) {
            notify("warning", `${notification.bidderPseudonym} te superó con ${formatCurrency(notification.currentAmount)}.`, 8000);
        } else if (!user || notification.bidderId !== user.id) {
            notify("info", `Nueva oferta de ${notification.bidderPseudonym}: ${formatCurrency(notification.currentAmount)}.`);
        }

        if (notification.wasExtended) {
            notify("info", "El cierre se extendió 2 minutos por la regla anti-sniping.", 8000);
        }
    });

    connection.on("AuctionClosed", async (notification) => {
        await loadAuction({ silent: true });

        const message = notification.winnerPseudonym
            ? `Subasta finalizada. Ganador: ${notification.winnerPseudonym} con ${formatCurrency(notification.finalAmount)}.`
            : "La subasta se declaró DESIERTA: venció sin recibir ofertas.";

        notify("info", message, 10000);
    });

    connection.onreconnecting(() => markConnection("Reconectando...", "bi-arrow-repeat", "text-warning"));

    connection.onreconnected(async () => {
        // Al reconectar hay que volver a unirse al grupo: la pertenencia vive en la conexión,
        // y además pudo haber pasado cualquier cosa mientras el canal estuvo caído.
        await connection.invoke("JoinRoom", auctionId);
        markConnection("En vivo", "bi-broadcast", "text-success");
        await loadAuction({ silent: true });
    });

    connection.onclose(() => markConnection("Sin conexión en vivo", "bi-wifi-off", "text-danger"));

    try {
        await connection.start();
        await connection.invoke("JoinRoom", auctionId);
        markConnection("En vivo", "bi-broadcast", "text-success");
    } catch {
        markConnection("Sondeo periódico", "bi-arrow-repeat", "text-warning");
    }
}

/**
 * Respaldo del canal en vivo: mantiene la vista fresca aunque el WebSocket falle.
 * Sólo consulta con la pestaña visible, para no golpear la API en segundo plano.
 */
function startFallbackPolling() {
    pollingTimer = setInterval(() => {
        if (document.visibilityState === "visible") {
            loadAuction({ silent: true });
        }
    }, POLLING_INTERVAL_MS);
}

window.addEventListener("beforeunload", () => {
    clearInterval(clockTimer);
    clearInterval(pollingTimer);
    connection?.stop();
});

// ---------------------------------------------------------------------------
// Arranque
// ---------------------------------------------------------------------------

renderNavigation();

if (!Number.isInteger(auctionId) || auctionId <= 0) {
    showEmpty(content, "El identificador de subasta no es válido.", "bi-exclamation-triangle");
} else {
    await loadAuction();
    await connectToLiveFeed();
    startFallbackPolling();
}
