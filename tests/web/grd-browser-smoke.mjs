// İsteğe bağlı gerçek tarayıcı kontrolü (crs-browser-smoke.mjs deseni): depodaki varsayılan "AUTH hazır değil" durumunda
// GRD sayfaları açılır, etkileşimli render tamamlanır, AUTH uyarısı görünür, form/tablo açılmaz ve konsolda hata olmaz.
// API'yi localhost:5087'de, izole profille başsız Chrome'u 9228 hata ayıklama portunda başlatın.
import assert from "node:assert/strict";
import { mkdir, writeFile } from "node:fs/promises";

const base = process.env.GRD_PREVIEW_URL ?? "http://127.0.0.1:5087";
const debug = process.env.GRD_CHROME_DEBUG_URL ?? "http://127.0.0.1:9228";
assert.ok(["127.0.0.1", "localhost"].includes(new URL(base).hostname));
assert.ok(["127.0.0.1", "localhost"].includes(new URL(debug).hostname));
const target = await (await fetch(debug + "/json/new?about:blank", { method: "PUT" })).json();
const socket = new WebSocket(target.webSocketDebuggerUrl);
await new Promise((resolve, reject) => { socket.addEventListener("open", resolve, { once: true }); socket.addEventListener("error", reject, { once: true }); });
let sequence = 0;
const pending = new Map();
const apiResponses = [];
const consoleErrors = [];
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
    if (message.method === "Runtime.exceptionThrown")
        consoleErrors.push(message.params.exceptionDetails.text);
    if (message.method === "Runtime.consoleAPICalled" && message.params.type === "error")
        consoleErrors.push(message.params.args.map(a => a.value ?? a.description).join(" "));
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
const output = new URL("../../TestResults/grd-preview/", import.meta.url);
await mkdir(output, { recursive: true });
const pages = [
    ["editor", "/assignments/1/learners/3/grade", "Ödev puanı"],
    ["my-grades", "/courses/1/my-grades", "Notlarım"],
    ["gradebook", "/courses/1/gradebook", "Not defteri"],
];
try {
    await command("Page.enable");
    await command("Runtime.enable");
    await command("Network.enable");
    for (const [size, width, height] of [["desktop", 1440, 1000], ["mobile", 390, 844]]) {
        await command("Emulation.setDeviceMetricsOverride", { width, height, deviceScaleFactor: 1, mobile: size === "mobile" });
        for (const [slug, path, title] of pages) {
            await command("Page.navigate", { url: base + path });
            let state;
            const deadline = Date.now() + 30000;
            while (Date.now() < deadline) {
                const result = await command("Runtime.evaluate", { returnByValue: true, expression: `JSON.stringify({
                    path: location.pathname, title: document.querySelector('h1')?.textContent,
                    alert: document.querySelector('[role=alert]')?.textContent,
                    code: document.querySelector('[role=alert]')?.dataset.errorCode,
                    loading: document.body.textContent.includes('yükleniyor'),
                    forms: document.querySelectorAll('form').length,
                    tables: document.querySelectorAll('table').length,
                    nav: [...document.querySelectorAll('.app-nav a')].map(a => a.textContent),
                    grdCss: [...document.styleSheets].some(s => (s.href ?? '').includes('grd.css')),
                    fits: document.documentElement.scrollWidth <= innerWidth
                })` });
                state = JSON.parse(result.result.value ?? "null");
                if (state?.path === path && state.title === title && state.alert?.includes("AUTH")) break;
                await new Promise(resolve => setTimeout(resolve, 200));
            }
            const screenshot = await command("Page.captureScreenshot", { format: "png" });
            await writeFile(new URL(`${slug}-${size}.png`, output), Buffer.from(screenshot.data, "base64"));
            assert.ok(state?.alert?.includes("AUTH"), JSON.stringify(state));
            assert.equal(state.code, "auth_not_ready");
            assert.equal(state.loading, false);
            assert.equal(state.forms, 0);
            assert.equal(state.tables, 0);
            assert.ok(state.nav.includes("Notlar"), JSON.stringify(state.nav));
            assert.equal(state.grdCss, true);
            assert.equal(state.fits, true);
            console.log(`PASS ${size} ${path}: AUTH notice visible; form and table closed`);
        }
        await command("Page.navigate", { url: base + "/grades" });
        await new Promise(resolve => setTimeout(resolve, 1500));
        const landing = await command("Runtime.evaluate", { returnByValue: true,
            expression: "JSON.stringify({ title: document.querySelector('h1')?.textContent, input: !!document.querySelector('#grades-course'), fits: document.documentElement.scrollWidth <= innerWidth })" });
        const landingState = JSON.parse(landing.result.value);
        await writeFile(new URL(`grades-${size}.png`, output),
            Buffer.from((await command("Page.captureScreenshot", { format: "png" })).data, "base64"));
        assert.deepEqual(landingState, { title: "Notlar", input: true, fits: true });
        console.log(`PASS ${size} /grades: course picker visible`);
    }
    assert.ok(apiResponses.length >= 6);
    assert.ok(apiResponses.every(status => status === 503), JSON.stringify(apiResponses));
    console.log(`PASS ${apiResponses.length} real API responses preserve HTTP 503`);
    assert.deepEqual(consoleErrors.filter(e => !/503/.test(e)), []);
    console.log("PASS no console errors");
} finally {
    await command("Browser.close").catch(() => {});
    socket.close();
}
