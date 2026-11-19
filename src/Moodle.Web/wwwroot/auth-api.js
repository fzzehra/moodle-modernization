export async function send(path, method, body) {
    const url = new URL(path, document.baseURI);
    const api = new URL("api/v1/", document.baseURI);
    if (url.origin !== location.origin || !url.pathname.startsWith(api.pathname)
        || !/^auth(\/|$)/.test(url.pathname.slice(api.pathname.length))
        || !["GET", "POST"].includes(method)) {
        throw new Error("Unsupported AUTH request");
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
