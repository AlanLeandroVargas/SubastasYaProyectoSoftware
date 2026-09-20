/**
 * Panel de la billetera virtual: métricas de saldo, acreditación de fondos simulados e historial
 * contable tomado del libro mayor.
 */
import { api } from "./api.js";
import { requireSession } from "./session.js";
import {
    renderNavigation, showLoading, showEmpty, notify, notifyError,
    formatCurrency, formatDateTime, escapeHtml, runWithButton, LEDGER_TYPE_LABELS
} from "./ui.js";

/**
 * Cada tipo de asiento se dibuja con su propio color, ícono y signo.
 * El signo refleja el efecto sobre el saldo total: una retención inmoviliza dinero pero no lo
 * gasta, así que no lleva ninguno.
 */
const ENTRY_PRESENTATION = {
    DEPOSIT: { icon: "bi-arrow-down-circle", color: "text-success", sign: "+" },
    HOLD: { icon: "bi-lock", color: "text-warning", sign: "" },
    RELEASE: { icon: "bi-unlock", color: "text-info", sign: "" },
    PAYMENT: { icon: "bi-arrow-up-circle", color: "text-danger", sign: "-" },
    PAYOUT: { icon: "bi-arrow-down-circle", color: "text-success", sign: "+" }
};

const depositForm = document.getElementById("depositForm");
const amountField = document.getElementById("depositAmount");
const depositButton = document.getElementById("depositButton");
const historyContainer = document.getElementById("ledgerHistory");

function renderBalance(balance) {
    document.getElementById("totalBalance").textContent = formatCurrency(balance.total);
    document.getElementById("heldBalance").textContent = formatCurrency(balance.held);
    document.getElementById("availableBalance").textContent = formatCurrency(balance.available);
}

function renderEntryRow(entry) {
    const presentation = ENTRY_PRESENTATION[entry.type] ?? { icon: "bi-dot", color: "", sign: "" };
    const label = LEDGER_TYPE_LABELS[entry.type] ?? entry.type;

    // Los movimientos originados en una subasta enlazan a su sala; los depósitos manuales no
    // tienen origen que mostrar.
    const reference = entry.auctionId
        ? `<a href="/auction.html?id=${entry.auctionId}">Subasta #${entry.auctionId}</a>`
        : '<span class="sy-muted">-</span>';

    return `
        <tr>
            <td class="text-nowrap">
                <i class="bi ${presentation.icon} ${presentation.color} me-1"></i>${escapeHtml(label)}
            </td>
            <td class="text-nowrap fw-semibold ${presentation.color}">
                ${presentation.sign}${formatCurrency(entry.amount)}
            </td>
            <td class="text-nowrap sy-muted small">${formatDateTime(entry.occurredAtUtc)}</td>
            <td class="small">${reference}</td>
            <td class="small sy-muted">${escapeHtml(entry.description)}</td>
        </tr>`;
}

function renderHistory(entries) {
    if (entries.length === 0) {
        showEmpty(historyContainer, "Todavía no registraste movimientos.", "bi-journal");
        return;
    }

    historyContainer.innerHTML = `
        <div class="table-responsive">
            <table class="table table-sm align-middle mb-0">
                <thead>
                    <tr class="sy-muted small">
                        <th>Tipo</th><th>Monto</th><th>Fecha</th><th>Origen</th><th>Detalle</th>
                    </tr>
                </thead>
                <tbody>${entries.map(renderEntryRow).join("")}</tbody>
            </table>
        </div>`;
}

async function loadWallet() {
    showLoading(historyContainer, "Cargando movimientos...");

    try {
        // Las dos consultas son independientes: se lanzan juntas en lugar de encadenarlas.
        const [balance, entries] = await Promise.all([api.getBalance(), api.listLedgerEntries()]);

        renderBalance(balance);
        renderHistory(entries);
    } catch (error) {
        showEmpty(historyContainer, "No se pudo cargar la billetera.", "bi-exclamation-triangle");
        notifyError(error);
    }
}

async function deposit(event) {
    event.preventDefault();

    const amount = Number(amountField.value);

    if (!Number.isFinite(amount) || amount <= 0) {
        notify("warning", "Ingresá un monto mayor a cero.");
        amountField.focus();
        return;
    }

    try {
        const balance = await runWithButton(depositButton, "Acreditando", () => api.deposit(amount));

        renderBalance(balance);
        notify("success", `Se acreditaron ${formatCurrency(amount)} en tu billetera.`);
        amountField.value = "";

        // El asiento recién creado se trae aparte: el depósito sólo devuelve el saldo nuevo.
        renderHistory(await api.listLedgerEntries());
    } catch (error) {
        notifyError(error);
    }
}

/** Botones de monto rápido: suman al valor ya cargado en lugar de reemplazarlo. */
function registerAmountShortcuts() {
    document.querySelectorAll("button[data-amount]").forEach((button) => {
        button.addEventListener("click", () => {
            amountField.value = Number(amountField.value || 0) + Number(button.dataset.amount);
        });
    });
}

renderNavigation();

if (requireSession()) {
    registerAmountShortcuts();
    depositForm.addEventListener("submit", deposit);
    loadWallet();
}
