// Optional real-browser check for the repository's default AUTH-unavailable state.
// Start the API on localhost:5087 and an isolated headless Chrome on debug port 9227.
import assert from "node:assert/strict";
import { mkdir, writeFile } from "node:fs/promises";

const base = process.env.CRS_PREVIEW_URL ?? "http://127.0.0.1:5087";
const debug = process.env.CRS_CHROME_DEBUG_URL ?? "http://127.0.0.1:9227";
assert.ok(["127.0.0.1", "localhost"].includes(new URL(base).hostname));
assert.ok(["127.0.0.1", "localhost"].includes(new URL(debug).hostname));
const target = await (await fetch(debug + "/json/new?about:blank", { method: "PUT" })).json();
const socket = new WebSocket(target.webSocketDebuggerUrl);
await new Promise((resolve, reject) => { socket.addEventListener("open", resolve, { once: true }); socket.addEventListener("error", reject, { once: true }); });
let sequence = 0;
const pending = new Map();
const apiResponses = [];
socket.addEventListener("message", ({ data }) => {
    const message = JSON.parse(data);
    if (message.id && pending.has(message.id)) {
        const { resolve, reject, timer } = pending.get(message.id);
        pending.delete(message.id);
        clearTimeout(timer);
        message.error ? reject(new Error(JSON.stringify(message.error))) : resolve(message.result);
    }
    if (message.method === "Network.responseReceived" && message.params.response.url.includes("/api/v1/"))
        apiResponses.push(message.params.response.status);
});
socket.addEventListener("close", () => {
    for (const entry of pending.values()) { clearTimeout(entry.timer); entry.reject(new Error("Browser closed")); }
    pending.clear();
});
function command(method, params = {}) {
    const id = ++sequence;
    return new Promise((resolve, reject) => {
        const timer = setTimeout(() => { pending.delete(id); reject(new Error("CDP timeout: " + method)); }, 10000);
        pending.set(id, { resolve, reject, timer });
        socket.send(JSON.stringify({ id, method, params }));
    });
}
const output = new URL("../../TestResults/crs-preview/", import.meta.url);
await mkdir(output, { recursive: true });
try {
    await command("Page.enable");
    await command("Runtime.enable");
    await command("Network.enable");
    for (const [size, width, height] of [["desktop", 1440, 1000], ["mobile", 390, 844]]) {
        await command("Emulation.setDeviceMetricsOverride", { width, height, deviceScaleFactor: 1, mobile: size === "mobile" });
        for (const [slug, path, title] of [["courses", "/courses", "Dersler"], ["details", "/courses/1", "Ders detayı"], ["participants", "/courses/1/participants", "Katılımcılar"]]) {
            await command("Page.navigate", { url: base + path });
            let state;
            const deadline = Date.now() + 30000;
            while (Date.now() < deadline) {
                const result = await command("Runtime.evaluate", { returnByValue: true, expression: `JSON.stringify({
                    path: location.pathname, title: document.querySelector('h1')?.textContent,
                    alert: document.querySelector('[role=alert]')?.textContent,
                    loading: document.body.textContent.includes('yükleniyor'),
                    disabled: [...document.querySelectorAll('.crs-heading button')].every(b => b.disabled),
                    forms: document.querySelectorAll('form').length,
                    font: getComputedStyle(document.body).fontFamily,
                    fits: document.documentElement.scrollWidth <= innerWidth
                })` });
                state = JSON.parse(result.result.value ?? "null");
                if (state?.path === path && state.title === title && state.alert?.includes("AUTH")) break;
                await new Promise(resolve => setTimeout(resolve, 200));
            }
            const screenshot = await command("Page.captureScreenshot", { format: "png" });
            await writeFile(new URL(`${slug}-${size}.png`, output), Buffer.from(screenshot.data, "base64"));
            assert.ok(state?.alert?.includes("AUTH"), JSON.stringify(state));
            assert.equal(state.loading, false);
            assert.equal(state.disabled, true);
            assert.equal(state.forms, 0);
            assert.ok(state.font.includes("system-ui"));
            assert.equal(state.fits, true);
            console.log(`PASS ${size} ${path}: AUTH notice visible; protected operations disabled`);
        }
    }
    assert.ok(apiResponses.length >= 6);
    assert.ok(apiResponses.every(status => status === 503), JSON.stringify(apiResponses));
    console.log(`PASS ${apiResponses.length} real API responses preserve HTTP 503`);
} finally {
    await command("Browser.close").catch(() => {});
    socket.close();
}
