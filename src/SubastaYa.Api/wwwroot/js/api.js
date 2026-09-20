/**
 * Cliente HTTP de la API REST de SubastaYa.
 *
 * Centraliza la ruta base, el token de sesión y la traducción de errores, de modo que las vistas
 * nunca tengan que tratar con fetch ni con códigos de estado directamente.
 */
import { getToken, signOut } from "./session.js";

const BASE_PATH = "/api/v1";

/** Error de negocio devuelto por la API, con el estado HTTP y el detalle ya interpretados. */
export class ApiError extends Error {
    constructor(status, title, detail) {
        super(detail || title || "Error inesperado");
        this.name = "ApiError";
        this.status = status;
        this.title = title;
    }

    /** Los conflictos de estado y de concurrencia se pueden recuperar reintentando. */
    get isConflict() {
        return this.status === 409;
    }

    get isInsufficientFunds() {
        return this.status === 422;
    }
}

async function interpretError(response) {
    let title = response.statusText;
    let detail = "";

    try {
        const body = await response.json();
        title = body.title || title;
        detail = body.detail || "";
    } catch {
        // La respuesta no traía ProblemDetails (por ejemplo, un 401 con cuerpo vacío).
    }

    if (response.status === 401) {
        // El token venció o es inválido: se descarta para que la interfaz deje de mostrarse
        // como autenticada en lugar de seguir fallando en cada pedido.
        signOut();
        return new ApiError(401, "Sesión expirada", detail || "Volvé a iniciar sesión para continuar.");
    }

    return new ApiError(response.status, title, detail);
}

async function request(method, path, body) {
    const token = getToken();
    const headers = {};

    if (body !== undefined) {
        headers["Content-Type"] = "application/json";
    }

    if (token) {
        headers.Authorization = `Bearer ${token}`;
    }

    let response;

    try {
        response = await fetch(`${BASE_PATH}${path}`, {
            method,
            headers,
            body: body === undefined ? undefined : JSON.stringify(body)
        });
    } catch {
        // fetch sólo rechaza por fallo de red: se distingue del error de negocio.
        throw new ApiError(0, "Sin conexión", "No se pudo contactar al servidor. Verificá que la API esté en ejecución.");
    }

    if (!response.ok) {
        throw await interpretError(response);
    }

    return response.status === 204 ? null : response.json();
}

/** Arma la cadena de consulta descartando los filtros vacíos. */
function buildQuery(parameters) {
    const query = new URLSearchParams();

    Object.entries(parameters).forEach(([key, value]) => {
        if (value !== undefined && value !== null && value !== "") {
            query.append(key, value);
        }
    });

    return query.toString();
}

export const api = {
    // --- Sesiones ---
    signIn: (email, password) => request("POST", "/sessions", { email, password }),
    getProfile: () => request("GET", "/sessions/current"),

    // --- Catálogo ---
    listCategories: () => request("GET", "/categories"),
    listAuctions: (filters) => request("GET", `/auctions?${buildQuery(filters)}`),
    getAuction: (id) => request("GET", `/auctions/${id}`),
    createAuction: (auction) => request("POST", "/auctions", auction),

    // --- Pujas ---
    placeBid: (id, amount) => request("POST", `/auctions/${id}/bids`, { amount }),

    // --- Billetera ---
    getBalance: () => request("GET", "/wallets/me"),
    deposit: (amount) => request("POST", "/wallets/me/deposits", { amount }),
    listLedgerEntries: (count = 50) => request("GET", `/wallets/me/transactions?count=${count}`),

    // --- Panel del usuario ---
    listMyParticipations: () => request("GET", "/users/me/bids"),
    listMyPublications: () => request("GET", "/users/me/auctions")
};
