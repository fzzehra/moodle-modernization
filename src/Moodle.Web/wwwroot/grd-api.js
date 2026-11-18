// GRD tarayıcı taşıması (crs-api.js deseni). Yalnızca aynı kaynaktaki dört GRD ucuna,
// yalnızca GET ve PUT ile istek atar; kullanıcının gerçek oturumu (same-origin çerez) kullanılır.
const routes = [
    { method: "GET", pattern: /^assignments\/\d+\/learners\/\d+\/attempts\/\d+\/grade$/ },
    { method: "PUT", pattern: /^assignments\/\d+\/learners\/\d+\/attempts\/\d+\/grade$/ },
    { method: "GET", pattern: /^courses\/\d+\/my-grades$/ },
    { method: "GET", pattern: /^courses\/\d+\/gradebook$/ }
];

export async function send(path, method, body) {
    const url = new URL(path, document.baseURI);
    const api = new URL("api/v1/", document.baseURI);
    const relative = url.pathname.startsWith(api.pathname) ? url.pathname.slice(api.pathname.length) : null;
    if (url.origin !== location.origin || relative === null
        || !routes.some(route => route.method === method && route.pattern.test(relative))) {
        throw new Error("Unsupported GRD request");
    }
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 30000);
    try {
        const response = await fetch(url, {
            method,
            credentials: "same-origin",
            redirect: "error",
            cache: "no-store",
            headers: body === null ? { Accept: "application/json" }
                : { Accept: "application/json", "Content-Type": "application/json" },
            body: body === null ? undefined : body,
            signal: controller.signal
        });
        return { status: response.status, body: await response.text() };
    } finally {
        clearTimeout(timeout);
    }
}
