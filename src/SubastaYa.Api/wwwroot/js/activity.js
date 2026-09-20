/**
 * Panel "mis actividades": una solapa con las participaciones como comprador y otra con las
 * publicaciones como vendedor, incluyendo sus métricas de recaudación y adjudicación.
 */
import { api } from "./api.js";
import { requireSession } from "./session.js";
import {
    renderNavigation, showLoading, showEmpty, notifyError, formatCurrency,
    formatDateTime, escapeHtml, statusClass, statusLabel
} from "./ui.js";

const participationsContainer = document.getElementById("participationsPanel");
const publicationsContainer = document.getElementById("publicationsPanel");

function renderTable(headers, rows) {
    return `
        <div class="table-responsive">
            <table class="table align-middle mb-0">
                <thead>
                    <tr class="sy-muted small">
                        ${headers.map((header) => `<th>${escapeHtml(header)}</th>`).join("")}
                    </tr>
                </thead>
                <tbody>${rows.join("")}</tbody>
            </table>
        </div>`;
}

function renderAuctionCell(item) {
    return `
        <td>
            <a href="/auction.html?id=${item.auctionId}" class="fw-semibold">${escapeHtml(item.title)}</a>
            <div class="sy-muted small">Cierre: ${formatDateTime(item.endsAtUtc)}</div>
        </td>`;
}

/**
 * Desenlace de la participación desde el punto de vista del comprador.
 * "Superado" sólo tiene sentido con la subasta todavía viva: una vez cerrada sin haber ganado,
 * lo que corresponde decir es que no se la adjudicó.
 */
function renderOutcomeBadge(participation) {
    if (participation.hasWon) {
        return '<span class="badge bg-success"><i class="bi bi-trophy me-1"></i>Ganaste</span>';
    }

    if (participation.isLeading) {
        return '<span class="badge bg-primary"><i class="bi bi-lightning-charge me-1"></i>Liderando</span>';
    }

    if (participation.status === "ACTIVE") {
        return '<span class="badge bg-warning text-dark"><i class="bi bi-arrow-down me-1"></i>Superado</span>';
    }

    return '<span class="badge bg-secondary">No adjudicada</span>';
}

function renderParticipationRow(participation) {
    return `
        <tr>
            ${renderAuctionCell(participation)}
            <td><span class="${statusClass(participation.status)}">${escapeHtml(statusLabel(participation.status))}</span></td>
            <td class="text-nowrap">${formatCurrency(participation.myHighestBid)}</td>
            <td class="text-nowrap fw-semibold">${formatCurrency(participation.currentAmount)}</td>
            <td>${renderOutcomeBadge(participation)}</td>
        </tr>`;
}

function renderPublicationRow(publication) {
    const winner = publication.winner
        ? escapeHtml(publication.winner)
        : '<span class="sy-muted">-</span>';

    return `
        <tr>
            ${renderAuctionCell(publication)}
            <td><span class="${statusClass(publication.status)}">${escapeHtml(statusLabel(publication.status))}</span></td>
            <td class="text-nowrap">${formatCurrency(publication.startingPrice)}</td>
            <td class="text-nowrap fw-semibold">${formatCurrency(publication.currentAmount)}</td>
            <td class="text-center">${publication.bidCount}</td>
            <td class="text-nowrap ${publication.revenue > 0 ? "text-success fw-semibold" : "sy-muted"}">
                ${formatCurrency(publication.revenue)}
            </td>
            <td>${winner}</td>
        </tr>`;
}

async function loadParticipations() {
    showLoading(participationsContainer, "Cargando tus pujas...");

    try {
        const participations = await api.listMyParticipations();

        if (participations.length === 0) {
            showEmpty(participationsContainer, "Todavía no participaste en ninguna subasta.", "bi-hammer");
            return;
        }

        participationsContainer.innerHTML = renderTable(
            ["Subasta", "Estado", "Mi mayor oferta", "Oferta líder", "Resultado"],
            participations.map(renderParticipationRow));
    } catch (error) {
        showEmpty(participationsContainer, "No se pudieron cargar tus participaciones.", "bi-exclamation-triangle");
        notifyError(error);
    }
}

async function loadPublications() {
    showLoading(publicationsContainer, "Cargando tus publicaciones...");

    try {
        const publications = await api.listMyPublications();

        if (publications.length === 0) {
            showEmpty(publicationsContainer, "Todavía no publicaste ninguna subasta.", "bi-megaphone");
            return;
        }

        publicationsContainer.innerHTML = renderTable(
            ["Subasta", "Estado", "Precio base", "Oferta líder", "Ofertas", "Recaudación", "Ganador"],
            publications.map(renderPublicationRow));
    } catch (error) {
        showEmpty(publicationsContainer, "No se pudieron cargar tus publicaciones.", "bi-exclamation-triangle");
        notifyError(error);
    }
}

renderNavigation();

if (requireSession()) {
    loadParticipations();

    // La segunda solapa se carga bajo demanda, para no pedir datos que quizá nadie mire.
    document.getElementById("publicationsTab")
        .addEventListener("shown.bs.tab", loadPublications, { once: true });
}
