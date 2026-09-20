/**
 * Manejo de la sesión del lado del navegador.
 *
 * El token y el perfil se guardan en localStorage para que sobrevivan a una recarga. No se
 * guarda nada más: el backend es stateless y el token ya lleva la identidad firmada.
 */

const SESSION_KEY = "subastaya.session";

export function saveSession(session) {
    localStorage.setItem(SESSION_KEY, JSON.stringify(session));
}

export function getSession() {
    const stored = localStorage.getItem(SESSION_KEY);

    if (!stored) {
        return null;
    }

    try {
        const session = JSON.parse(stored);

        // Una sesión vencida equivale a no tener sesión: se descarta antes de usarla, para no
        // mostrar la interfaz de usuario autenticado y que después la API responda 401.
        if (new Date(session.expiresAtUtc) <= new Date()) {
            localStorage.removeItem(SESSION_KEY);
            return null;
        }

        return session;
    } catch {
        // Contenido corrupto: se descarta en lugar de arrastrar el problema a toda la aplicación.
        localStorage.removeItem(SESSION_KEY);
        return null;
    }
}

export function getToken() {
    return getSession()?.token ?? null;
}

export function getUser() {
    return getSession()?.user ?? null;
}

export function isAuthenticated() {
    return getSession() !== null;
}

export function signOut() {
    localStorage.removeItem(SESSION_KEY);
}

/**
 * Redirige al inicio de sesión conservando el origen, para devolver al usuario a la misma
 * pantalla una vez que ingresa en lugar de dejarlo en el catálogo.
 */
export function requireSession() {
    if (isAuthenticated()) {
        return true;
    }

    const target = encodeURIComponent(window.location.pathname + window.location.search);
    window.location.href = `/login.html?returnTo=${target}`;

    return false;
}
