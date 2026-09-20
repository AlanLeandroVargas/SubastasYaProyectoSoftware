/**
 * Vista del catálogo: filtros, tarjetas informativas, contador regresivo y paginación.
 *
 * Es la única pantalla pública de la aplicación: funciona con o sin sesión, y la API tampoco
 * exige token para consultarla.
 */
import { api } from "./api.js";
import {
    renderNavigation, showLoading, showEmpty, notifyError, formatCurrency,
    escapeHtml, statusClass, statusLabel, startCountdowns, runWithButton
} from "./ui.js";

const PAGE_SIZE = 12;

/** Marcador de posición para las imágenes que no cargan, sin depender de un servicio externo. */
const FALLBACK_IMAGE =
    "data:image/svg+xml;utf8," +
    encodeURIComponent(
        `<svg xmlns="http://www.w3.org/2000/svg" width="400" height="300">
            <rect width="100%" height="100%" fill="#212a3c"/>
            <text x="50%" y="50%" fill="#6c7891" font-family="sans-serif" font-size="18"
                  text-anchor="middle">Sin imagen</text>
         </svg>`);

const elements = {
    form: document.getElementById("filtersForm"),
    search: document.getElementById("searchFilter"),
    status: document.getElementById("statusFilter"),
    category: document.getElementById("categoryFilter"),
    minPrice: document.getElementById("minPriceFilter"),
    maxPrice: document.getElementById("maxPriceFilter"),
    sort: document.getElementById("sortFilter"),
    searchButton: document.getElementById("searchButton"),
    clearButton: document.getElementById("clearButton"),
    list: document.getElementById("auctionList"),
    summary: document.getElementById("resultsSummary"),
    pagination: document.getElementById("pagination")
};

let currentPage = 1;

function readFilters() {
    return {
        search: elements.search.value.trim(),
        status: elements.status.value,
        categoryId: elements.category.value,
        minPrice: elements.minPrice.value,
        maxPrice: elements.maxPrice.value,
        sort: elements.sort.value,
        page: currentPage,
        pageSize: PAGE_SIZE
    };
}

function renderCard(auction) {
    const status = auction.status;
    const isRunning = status === "ACTIVE";
    const isCountingDown = isRunning || status === "SCHEDULED";
    const referenceDate = status === "SCHEDULED" ? auction.startsAtUtc : auction.endsAtUtc;

    // Una subasta cerrada no tiene cuenta regresiva: muestra su desenlace en lugar de un
    // prefijo temporal, para no terminar diciendo "cierra en finalizada".
    const timeInfo = isCountingDown
        ? `${status === "SCHEDULED" ? "Comienza en" : "Cierra en"}
           <span class="sy-countdown" data-ends-at="${referenceDate}"></span>`
        : `<span class="sy-muted">${escapeHtml(statusLabel(status))}</span>`;

    return `
        <div class="col-12 col-sm-6 col-lg-4 col-xl-3">
            <article class="sy-card">
                <img class="sy-card-image" src="${escapeHtml(auction.imageUrl)}"
                     alt="${escapeHtml(auction.title)}" loading="lazy"
                     onerror="this.onerror=null;this.src='${FALLBACK_IMAGE}'" />
                <div class="sy-card-body">
                    <div class="d-flex justify-content-between align-items-center">
                        <span class="${statusClass(status)}">${escapeHtml(statusLabel(status))}</span>
                        <span class="sy-muted small">
                            <i class="bi bi-tag me-1"></i>${escapeHtml(auction.category)}
                        </span>
                    </div>

                    <h2 class="sy-card-title">${escapeHtml(auction.title)}</h2>

                    <div>
                        <div class="sy-muted small">
                            ${auction.bidCount > 0 ? "Oferta más alta" : "Precio base"}
                        </div>
                        <div class="sy-amount">${formatCurrency(auction.currentAmount)}</div>
                    </div>

                    <div class="d-flex justify-content-between align-items-center sy-muted small">
                        <span><i class="bi bi-hammer me-1"></i>${auction.bidCount} oferta(s)</span>
                        <span><i class="bi bi-clock me-1"></i>${timeInfo}</span>
                    </div>

                    <a class="btn btn-primary btn-sm mt-auto" href="/auction.html?id=${auction.id}">
                        ${isRunning ? "Entrar a la sala" : "Ver detalle"}
                    </a>
                </div>
            </article>
        </div>`;
}

function renderPagination(result) {
    if (result.totalPages <= 1) {
        elements.pagination.innerHTML = "";
        return;
    }

    const items = [];
    const addItem = (label, page, disabled, active) => {
        items.push(`
            <li class="page-item ${disabled ? "disabled" : ""} ${active ? "active" : ""}">
                <button class="page-link" data-page="${page}" ${disabled ? "disabled" : ""}>
                    ${label}
                </button>
            </li>`);
    };

    addItem("&laquo;", result.page - 1, result.page === 1, false);

    for (let page = 1; page <= result.totalPages; page++) {
        addItem(page, page, false, page === result.page);
    }

    addItem("&raquo;", result.page + 1, !result.hasNextPage, false);

    elements.pagination.innerHTML = items.join("");
    elements.pagination.querySelectorAll("button[data-page]").forEach((button) => {
        button.addEventListener("click", () => {
            currentPage = Number(button.dataset.page);
            loadAuctions();
            window.scrollTo({ top: 0, behavior: "smooth" });
        });
    });
}

async function loadAuctions() {
    showLoading(elements.list, "Buscando subastas...");
    elements.summary.textContent = "";

    try {
        const result = await api.listAuctions(readFilters());

        if (result.items.length === 0) {
            showEmpty(elements.list, "No hay subastas que coincidan con los filtros aplicados.", "bi-search");
            elements.pagination.innerHTML = "";
            return;
        }

        elements.summary.textContent =
            `${result.totalItems} subasta(s) encontradas · página ${result.page} de ${result.totalPages}`;
        elements.list.innerHTML = result.items.map(renderCard).join("");
        renderPagination(result);
    } catch (error) {
        showEmpty(elements.list, "No se pudo cargar el catálogo.", "bi-exclamation-triangle");
        notifyError(error);
    }
}

async function loadCategories() {
    try {
        const categories = await api.listCategories();
        elements.category.insertAdjacentHTML(
            "beforeend",
            categories
                .map((category) => `<option value="${category.id}">${escapeHtml(category.name)}</option>`)
                .join(""));
    } catch (error) {
        notifyError(error);
    }
}

function registerEvents() {
    elements.form.addEventListener("submit", async (event) => {
        event.preventDefault();
        currentPage = 1;
        await runWithButton(elements.searchButton, "Buscando", loadAuctions);
    });

    elements.clearButton.addEventListener("click", () => {
        elements.form.reset();
        currentPage = 1;
        loadAuctions();
    });
}

renderNavigation();
registerEvents();
loadCategories();
loadAuctions();

// Cuando una subasta llega a cero se recarga el listado: para entonces el proceso en segundo
// plano ya resolvió su estado final, así que la tarjeta pasa a mostrar el desenlace real.
startCountdowns(() => setTimeout(loadAuctions, 3000));
