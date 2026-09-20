/**
 * Vista de inicio de sesión. Valida en pantalla antes de llamar a la API y, cuando las
 * credenciales son correctas, devuelve al usuario a la página desde la que se lo redirigió.
 */
import { api } from "./api.js";
import { saveSession, isAuthenticated } from "./session.js";
import { renderNavigation, notify, notifyError, runWithButton, escapeHtml } from "./ui.js";

const DEMO_PASSWORD = "Password123!";

/**
 * Cuentas de la consigna. Se listan en pantalla porque no hay registro público: el conjunto de
 * usuarios es fijo y quien corrige la práctica necesita poder entrar sin buscar en el código.
 */
const TEST_ACCOUNTS = [
    { email: "vendedor@test.com", description: "Vendedor: publica las subastas del catálogo." },
    { email: "comprador1@test.com", description: "Postor líder, con saldo retenido en garantía." },
    { email: "comprador2@test.com", description: "Postor habilitado, con saldo disponible." },
    { email: "sinfondos@test.com", description: "Sin saldo: sirve para probar el rechazo de pujas." }
];

const form = document.getElementById("loginForm");
const emailField = document.getElementById("emailField");
const passwordField = document.getElementById("passwordField");
const signInButton = document.getElementById("signInButton");
const accountList = document.getElementById("accountList");

function returnTarget() {
    const parameters = new URLSearchParams(window.location.search);
    return parameters.get("returnTo") || "/index.html";
}

function renderTestAccounts() {
    accountList.innerHTML = TEST_ACCOUNTS
        .map((account) => `
            <button type="button" class="list-group-item list-group-item-action bg-transparent text-start px-0"
                    data-email="${escapeHtml(account.email)}">
                <div class="fw-semibold">${escapeHtml(account.email)}</div>
                <small class="sy-muted">${escapeHtml(account.description)}</small>
            </button>`)
        .join("");

    accountList.querySelectorAll("button[data-email]").forEach((button) => {
        button.addEventListener("click", () => {
            emailField.value = button.dataset.email;
            passwordField.value = DEMO_PASSWORD;
            passwordField.focus();
        });
    });
}

async function signIn(event) {
    event.preventDefault();

    // Validación en pantalla: no se molesta al backend con datos incompletos.
    if (!form.checkValidity()) {
        form.classList.add("was-validated");
        return;
    }

    try {
        const session = await runWithButton(signInButton, "Ingresando", () =>
            api.signIn(emailField.value.trim(), passwordField.value));

        saveSession(session);
        notify("success", `Bienvenido, ${session.user.name}.`);
        window.location.href = returnTarget();
    } catch (error) {
        notifyError(error);

        // Se limpia la contraseña y no el email: casi siempre el error está en la primera.
        passwordField.value = "";
        passwordField.focus();
    }
}

renderNavigation();
renderTestAccounts();
form.addEventListener("submit", signIn);

if (isAuthenticated()) {
    window.location.href = returnTarget();
}
