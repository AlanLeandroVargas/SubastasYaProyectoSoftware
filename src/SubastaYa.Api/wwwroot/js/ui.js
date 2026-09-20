/**
 * Utilidades de interfaz compartidas por todas las vistas: formato, avisos flotantes,
 * indicadores de carga, contador regresivo y barra de navegación.
 *
 * Todo el texto que ve el usuario está en español a propósito: el código es inglés, el producto no.
 */
import { getUser, signOut, isAuthenticated } from "./session.js";

const CRITICAL_ZONE_SECONDS = 60;
const WARNING_ZONE_SECONDS = 300;

/** Etiquetas en español para los estados que devuelve la API. */
const STATUS_LABELS = {
    ACTIVE: "Activa",
    SCHEDULED: "Próxima",
    COMPLETED: "Finalizada",
    UNSOLD: "Desierta"
};

/** Etiquetas en español para los tipos de asiento del libro mayor. */
export const LEDGER_TYPE_LABELS = {
    DEPOSIT: "Depósito",
    HOLD: "Retención",
    RELEASE: "Liberación",
    PAYMENT: "Pago",
    PAYOUT: "Cobro"
};

const currencyFormatter = new Intl.NumberFormat("es-AR", {
    style: "currency",
    currency: "ARS",
    maximumFractionDigits: 0
});

const timeFormatter = new Intl.DateTimeFormat("es-AR", {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit"
});

const dateTimeFormatter = new Intl.DateTimeFormat("es-AR", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit"
});

export function formatCurrency(value) {
    return currencyFormatter.format(Number(value ?? 0));
}

export function formatTime(isoDate) {
    return timeFormatter.format(new Date(isoDate));
}

export function formatDateTime(isoDate) {
    return dateTimeFormatter.format(new Date(isoDate));
}

/** Escapa el texto antes de insertarlo en el DOM, para evitar inyección de HTML. */
export function escapeHtml(text) {
    const element = document.createElement("div");
    element.textContent = text ?? "";
    return element.innerHTML;
}

export function statusLabel(status) {
    return STATUS_LABELS[status] ?? status ?? "";
}

export function statusClass(status) {
    return `sy-status sy-status-${(status ?? "").toLowerCase()}`;
}

// ---------------------------------------------------------------------------
// Avisos flotantes
// ---------------------------------------------------------------------------

const TOAST_ICONS = {
    success: "bi-check-circle-fill",
    error: "bi-x-octagon-fill",
    warning: "bi-exclamation-triangle-fill",
    info: "bi-info-circle-fill"
};

function toastContainer() {
    let container = document.querySelector(".sy-toasts");

    if (!container) {
        container = document.createElement("div");
        container.className = "sy-toasts";
        document.body.appendChild(container);
    }

    return container;
}

/**
 * Muestra un aviso que no bloquea. Es el único canal de retroalimentación de la aplicación, así
 * que toda confirmación o error se comunica siempre de la misma manera.
 */
export function notify(type, message, milliseconds = 5000) {
    const toast = document.createElement("div");
    toast.className = `sy-toast sy-toast-${type}`;
    toast.setAttribute("role", type === "error" ? "alert" : "status");
    toast.innerHTML = `<i class="bi ${TOAST_ICONS[type]}"></i><div>${escapeHtml(message)}</div>`;

    toastContainer().appendChild(toast);
    setTimeout(() => toast.remove(), milliseconds);
}

/**
 * Convierte un ApiError en un aviso cuyo tono corresponde al código HTTP.
 * Un conflicto o un saldo insuficiente no son fallas del sistema: son respuestas esperables del
 * negocio, y se muestran como advertencia y no como error.
 */
export function notifyError(error) {
    if (error?.status === 409 || error?.status === 422) {
        notify("warning", error.message, 7000);
        return;
    }

    notify("error", error?.message ?? "Ocurrió un error inesperado.");
}

// ---------------------------------------------------------------------------
// Indicadores de estado
// ---------------------------------------------------------------------------

export function showLoading(container, message = "Cargando...") {
    container.innerHTML = `
        <div class="sy-loading">
            <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">${escapeHtml(message)}</span>
            </div>
            <span>${escapeHtml(message)}</span>
        </div>`;
}

export function showEmpty(container, message, icon = "bi-inbox") {
    container.innerHTML = `
        <div class="sy-empty">
            <i class="bi ${icon}"></i>
            <p class="mb-0">${escapeHtml(message)}</p>
        </div>`;
}

/**
 * Ejecuta una acción manteniendo el botón deshabilitado mientras dura, para impedir envíos
 * duplicados al backend.
 */
export async function runWithButton(button, busyLabel, action) {
    const originalContent = button.innerHTML;
    button.disabled = true;
    button.innerHTML = `<span class="spinner-border spinner-border-sm me-2"></span>${escapeHtml(busyLabel)}`;

    try {
        return await action();
    } finally {
        button.disabled = false;
        button.innerHTML = originalContent;
    }
}

// ---------------------------------------------------------------------------
// Contador regresivo
// ---------------------------------------------------------------------------

export function secondsRemaining(endsAtIso) {
    return Math.floor((new Date(endsAtIso).getTime() - Date.now()) / 1000);
}

export function formatCountdown(seconds) {
    if (seconds <= 0) {
        return "Cerrada";
    }

    const days = Math.floor(seconds / 86400);
    const hours = Math.floor((seconds % 86400) / 3600);
    const minutes = Math.floor((seconds % 3600) / 60);
    const rest = seconds % 60;
    const pad = (value) => String(value).padStart(2, "0");

    if (days > 0) {
        return `${days}d ${pad(hours)}:${pad(minutes)}:${pad(rest)}`;
    }

    return `${pad(hours)}:${pad(minutes)}:${pad(rest)}`;
}

/** El último minuto se pinta en rojo y los últimos cinco minutos en ámbar. */
export function countdownClass(seconds) {
    if (seconds <= 0) {
        return "sy-countdown sy-countdown-normal";
    }

    if (seconds <= CRITICAL_ZONE_SECONDS) {
        return "sy-countdown sy-countdown-critical";
    }

    if (seconds <= WARNING_ZONE_SECONDS) {
        return "sy-countdown sy-countdown-warning";
    }

    return "sy-countdown sy-countdown-normal";
}

/**
 * Refresca cada segundo todos los elementos que lleven [data-ends-at].
 * Un único temporizador global evita multiplicar intervalos por cada tarjeta del catálogo.
 */
export function startCountdowns(onReachingZero) {
    const refresh = () => {
        document.querySelectorAll("[data-ends-at]").forEach((element) => {
            const seconds = secondsRemaining(element.dataset.endsAt);
            element.textContent = formatCountdown(seconds);
            element.className = countdownClass(seconds);

            if (seconds <= 0 && element.dataset.notified !== "yes") {
                element.dataset.notified = "yes";
                onReachingZero?.(element);
            }
        });
    };

    refresh();
    return setInterval(refresh, 1000);
}

// ---------------------------------------------------------------------------
// Barra de navegación
// ---------------------------------------------------------------------------

const NAVIGATION_LINKS = [
    { label: "Catálogo", path: "/index.html", icon: "bi-grid" },
    { label: "Publicar", path: "/publish.html", icon: "bi-plus-square", requiresSession: true },
    { label: "Mi billetera", path: "/wallet.html", icon: "bi-wallet2", requiresSession: true },
    { label: "Mis actividades", path: "/my-activity.html", icon: "bi-person-lines-fill", requiresSession: true }
];

export function renderNavigation() {
    const container = document.querySelector("[data-navigation]");

    if (!container) {
        return;
    }

    const authenticated = isAuthenticated();
    const user = getUser();
    const currentPath = window.location.pathname;

    const links = NAVIGATION_LINKS
        .filter((link) => !link.requiresSession || authenticated)
        .map((link) => {
            const activeClass = currentPath === link.path ? "active" : "";
            return `<li class="nav-item">
                        <a class="nav-link ${activeClass}" href="${link.path}">
                            <i class="bi ${link.icon} me-1"></i>${link.label}
                        </a>
                    </li>`;
        })
        .join("");

    const sessionBlock = authenticated
        ? `<div class="d-flex align-items-center gap-3">
               <span class="sy-muted d-none d-md-inline">
                   <i class="bi bi-person-circle me-1"></i>${escapeHtml(user?.pseudonym ?? "")}
               </span>
               <button class="btn btn-outline-secondary btn-sm" data-sign-out>Salir</button>
           </div>`
        : `<a class="btn btn-primary btn-sm" href="/login.html">Iniciar sesión</a>`;

    container.innerHTML = `
        <nav class="navbar navbar-expand-lg sy-navbar sticky-top">
            <div class="container">
                <a class="navbar-brand sy-brand" href="/index.html">Subasta<span>Ya</span></a>
                <button class="navbar-toggler" type="button" data-bs-toggle="collapse"
                        data-bs-target="#mainMenu" aria-label="Abrir menú">
                    <span class="navbar-toggler-icon"></span>
                </button>
                <div class="collapse navbar-collapse" id="mainMenu">
                    <ul class="navbar-nav me-auto mb-2 mb-lg-0">${links}</ul>
                    ${sessionBlock}
                </div>
            </div>
        </nav>`;

    container.querySelector("[data-sign-out]")?.addEventListener("click", () => {
        signOut();
        window.location.href = "/index.html";
    });
}
