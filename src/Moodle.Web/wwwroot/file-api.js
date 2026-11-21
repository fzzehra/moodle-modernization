// FILE tarayıcı taşıması (grd-api.js deseni). Yalnızca aynı kaynaktaki FILE uçlarına istek atar;
// kullanıcının gerçek oturumu (same-origin çerez) kullanılır. Dosya içeriği FormData ile gönderilir.
const area = String.raw`file-areas\/\d+\/[A-Za-z0-9_]+\/[A-Za-z0-9_]+\/\d+`;
const routes = [
    { method: "GET", pattern: new RegExp(`^${area}\\/files$`) },
    { method: "POST", pattern: new RegExp(`^${area}\\/commit$`) },
    { method: "GET", pattern: /^files\/\d+$/ },
    { method: "DELETE", pattern: /^files\/\d+$/ },
    { method: "GET", pattern: /^users\/me\/quota$/ },
    { method: "POST", pattern: /^file-drafts$/ },
    { method: "GET", pattern: /^file-drafts\/\d+\/files$/ },
    { method: "DELETE", pattern: /^file-drafts\/\d+\/files\/\d+$/ },
    { method: "GET", pattern: /^admin\/file-settings$/ },
    { method: "PUT", pattern: /^admin\/file-settings$/ },
    { method: "POST", pattern: /^admin\/file-maintenance\/run$/ }
];
const uploadRoute = /^file-drafts\/\d+\/files$/;

function resolve(path, method, allowed) {
    const url = new URL(path, document.baseURI);
    const api = new URL("api/v1/", document.baseURI);
    const relative = url.pathname.startsWith(api.pathname) ? url.pathname.slice(api.pathname.length) : null;
    if (url.origin !== location.origin || relative === null || !allowed(method, relative)) {
        throw new Error("Unsupported FILE request");
    }
    return url;
}

async function request(url, init, timeoutMs) {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), timeoutMs);
    try {
        // Draft writes share cookie-based antiforgery protection.
        const draftRoot = new URL("api/v1/file-drafts", document.baseURI);
        if (init.method !== "GET" && (url.pathname === draftRoot.pathname || url.pathname.startsWith(draftRoot.pathname + "/"))) {
            const csrf = await fetch(new URL("api/v1/antiforgery", document.baseURI), {
                credentials: "same-origin", redirect: "error", cache: "no-store", signal: controller.signal
            });
            if (csrf.status !== 200) throw new Error("Antiforgery token unavailable");
            const { token } = JSON.parse(await csrf.text());
            if (typeof token !== "string" || !token) throw new Error("Antiforgery token unavailable");
            init = { ...init, headers: { ...init.headers, "X-CSRF-TOKEN": token } };
        }
        const response = await fetch(url, {
            ...init,
            credentials: "same-origin",
            redirect: "error",
            cache: "no-store",
            signal: controller.signal
        });
        return { status: response.status, body: await response.text() };
    } finally {
        clearTimeout(timeout);
    }
}

export async function send(path, method, body) {
    const url = resolve(path, method, (m, relative) =>
        routes.some(route => route.method === m && route.pattern.test(relative)));
    return request(url, {
        method,
        headers: body === null ? { Accept: "application/json" }
            : { Accept: "application/json", "Content-Type": "application/json" },
        body: body === null ? undefined : body
    }, 30000);
}

// streamRef: Blazor DotNetStreamReference (arrayBuffer() ile okunur). fields: ek form alanları (ör. path, overwrite).
export async function upload(path, fileName, contentType, streamRef, fields) {
    const url = resolve(path, "POST", (m, relative) => m === "POST" && uploadRoute.test(relative));
    const form = new FormData();
    const bytes = await streamRef.arrayBuffer();
    form.append("file", new Blob([bytes], { type: contentType || "application/octet-stream" }), fileName);
    for (const [name, value] of Object.entries(fields ?? {})) form.append(name, value);
    // Content-Type başlığı bilerek verilmez: tarayıcı multipart sınırını kendisi ekler.
    return request(url, { method: "POST", headers: { Accept: "application/json" }, body: form }, 120000);
}
