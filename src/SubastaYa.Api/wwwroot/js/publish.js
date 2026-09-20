/**
 * Formulario de publicación de subastas.
 *
 * Replica en pantalla las validaciones del dominio para dar respuesta inmediata; el backend las
 * vuelve a aplicar porque es la única fuente de verdad. La duplicación es deliberada y acotada:
 * acá sólo se anticipa el error, no se decide nada.
 */
import { api } from "./api.js";
import { requireSession } from "./session.js";
import { renderNavigation, notify, notifyError, escapeHtml, runWithButton } from "./ui.js";

const MINIMUM_DURATION_MINUTES = 1;

const form = document.getElementById("publishForm");
const fields = {
    title: document.getElementById("titleField"),
    description: document.getElementById("descriptionField"),
    imageUrl: document.getElementById("imageUrlField"),
    category: document.getElementById("categoryField"),
    startingPrice: document.getElementById("startingPriceField"),
    minimumIncrement: document.getElementById("minimumIncrementField"),
    startsAt: document.getElementById("startsAtField"),
    endsAt: document.getElementById("endsAtField")
};
const warningBox = document.getElementById("validationWarning");
const publishButton = document.getElementById("publishButton");

/** Da formato a una fecha para un input datetime-local, que siempre trabaja en hora local. */
function toLocalInputValue(date) {
    const shifted = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
    return shifted.toISOString().slice(0, 16);
}

function proposeInitialValues() {
    const now = new Date();
    const inOneHour = new Date(now.getTime() + 60 * 60 * 1000);

    fields.startsAt.value = toLocalInputValue(now);
    fields.endsAt.value = toLocalInputValue(inOneHour);
    fields.imageUrl.value = "https://picsum.photos/seed/subastaya/800/600";
}

async function loadCategories() {
    try {
        const categories = await api.listCategories();
        fields.category.insertAdjacentHTML(
            "beforeend",
            categories
                .map((category) => `<option value="${category.id}">${escapeHtml(category.name)}</option>`)
                .join(""));
    } catch (error) {
        notifyError(error);
    }
}

/** Reglas de coherencia que la validación nativa de HTML no cubre. */
function detectInconsistencies() {
    const startingPrice = Number(fields.startingPrice.value);
    const minimumIncrement = Number(fields.minimumIncrement.value);
    const startsAt = new Date(fields.startsAt.value);
    const endsAt = new Date(fields.endsAt.value);
    const problems = [];

    if (endsAt <= startsAt) {
        problems.push("La fecha de finalización debe ser posterior a la de inicio.");
    } else if ((endsAt - startsAt) / 60000 < MINIMUM_DURATION_MINUTES) {
        problems.push(`La subasta debe durar al menos ${MINIMUM_DURATION_MINUTES} minuto.`);
    }

    if (endsAt <= new Date()) {
        problems.push("No es posible publicar una subasta cuyo cierre ya ocurrió.");
    }

    if (minimumIncrement > startingPrice) {
        problems.push("El incremento mínimo no puede superar al precio base.");
    }

    return problems;
}

function showInconsistencies(problems) {
    if (problems.length === 0) {
        warningBox.classList.add("d-none");
        return;
    }

    warningBox.classList.remove("d-none");
    warningBox.innerHTML = problems.map((problem) => `<div>${escapeHtml(problem)}</div>`).join("");
}

async function publish(event) {
    event.preventDefault();
    form.classList.add("was-validated");

    const problems = detectInconsistencies();
    showInconsistencies(problems);

    if (!form.checkValidity() || problems.length > 0) {
        return;
    }

    const request = {
        title: fields.title.value.trim(),
        description: fields.description.value.trim(),
        imageUrl: fields.imageUrl.value.trim(),
        categoryId: Number(fields.category.value),
        startingPrice: Number(fields.startingPrice.value),
        minimumIncrement: Number(fields.minimumIncrement.value),

        // El input devuelve hora local; se envía en UTC para que el servidor no tenga que adivinar.
        startsAtUtc: new Date(fields.startsAt.value).toISOString(),
        endsAtUtc: new Date(fields.endsAt.value).toISOString()
    };

    try {
        const published = await runWithButton(publishButton, "Publicando", () => api.createAuction(request));

        notify("success", `Subasta "${published.title}" publicada correctamente.`);
        window.location.href = `/auction.html?id=${published.id}`;
    } catch (error) {
        notifyError(error);
    }
}

renderNavigation();

if (requireSession()) {
    proposeInitialValues();
    loadCategories();
    form.addEventListener("submit", publish);

    // Respuesta temprana: la coherencia se revisa a medida que se completan fechas e importes,
    // en lugar de esperar al envío.
    [fields.startsAt, fields.endsAt, fields.startingPrice, fields.minimumIncrement].forEach((field) => {
        field.addEventListener("change", () => showInconsistencies(detectInconsistencies()));
    });
}
