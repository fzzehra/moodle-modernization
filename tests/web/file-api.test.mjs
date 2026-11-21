import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const source = await readFile(new URL("../../src/Moodle.Web/wwwroot/file-api.js", import.meta.url), "utf8");
const { send, upload } = await import("data:text/javascript;base64," + Buffer.from(source).toString("base64"));
globalThis.document = { baseURI: "http://localhost/app/" };
globalThis.location = { origin: "http://localhost" };

const streamRef = text => ({ arrayBuffer: async () => new TextEncoder().encode(text).buffer });

test("FILE reads use the same-origin browser session and keep auth_not_ready unchanged", async () => {
    let options;
    globalThis.fetch = async (url, init) => {
        assert.equal(url.href, "http://localhost/app/api/v1/file-areas/7/course/files/0/files?path=%2F");
        options = init;
        return { status: 503, text: async () => '{"error":{"code":"auth_not_ready"}}' };
    };
    const response = await send("/app/api/v1/file-areas/7/course/files/0/files?path=%2F", "GET", null);
    assert.equal(response.status, 503);
    assert.equal(JSON.parse(response.body).error.code, "auth_not_ready");
    assert.equal(options.credentials, "same-origin");
    assert.equal(options.redirect, "error");
    assert.equal(options.cache, "no-store");
    assert.deepEqual(options.headers, { Accept: "application/json" });
    assert.equal(options.body, undefined);
});

test("settings save sends the supplied JSON with PUT and never synthesizes credentials", async () => {
    globalThis.fetch = async (url, options) => {
        assert.equal(url.pathname, "/app/api/v1/admin/file-settings");
        assert.equal(options.method, "PUT");
        assert.equal(options.headers["Content-Type"], "application/json");
        assert.equal(options.headers.Authorization, undefined);
        assert.equal(options.headers.Cookie, undefined);
        assert.equal(options.body, '{"maxFileBytes":1,"userQuotaBytes":0,"draftTtlHours":1,"trashRetentionHours":0}');
        return { status: 200, text: async () => "{}" };
    };
    await send("/app/api/v1/admin/file-settings", "PUT", '{"maxFileBytes":1,"userQuotaBytes":0,"draftTtlHours":1,"trashRetentionHours":0}');
});

test("upload posts FormData with the file and extra fields, without a manual Content-Type", async () => {
    let options;
    globalThis.fetch = async (url, init) => {
        if (url.pathname === "/app/api/v1/antiforgery") {
            assert.equal(init.credentials, "same-origin");
            assert.equal(init.redirect, "error");
            return { status: 200, text: async () => '{"token":"csrf-test"}' };
        }
        assert.equal(url.pathname, "/app/api/v1/file-drafts/55/files");
        options = init;
        return { status: 201, text: async () => '{"fileId":3}' };
    };
    const response = await upload("/app/api/v1/file-drafts/55/files", "Ödev açıklaması.txt", "text/plain",
        streamRef("merhaba"), { overwrite: "false" });

    assert.equal(response.status, 201);
    assert.equal(options.method, "POST");
    assert.equal(options.credentials, "same-origin");
    assert.equal(options.redirect, "error");
    assert.deepEqual(options.headers, { Accept: "application/json", "X-CSRF-TOKEN": "csrf-test" });
    assert.ok(options.body instanceof FormData);
    const file = options.body.get("file");
    assert.equal(file.name, "Ödev açıklaması.txt");
    assert.equal(file.type, "text/plain");
    assert.equal(await file.text(), "merhaba");
    assert.equal(options.body.get("overwrite"), "false");
});

for (const [path, method] of [
    ["/app/api/v1/file-areas/7/course/files/0/files", "GET"],
    ["/app/api/v1/file-areas/7/course/files/0/commit", "POST"],
    ["/app/api/v1/files/9", "GET"],
    ["/app/api/v1/files/9", "DELETE"],
    ["/app/api/v1/users/me/quota", "GET"],
    ["/app/api/v1/file-drafts", "POST"],
    ["/app/api/v1/file-drafts/55/files", "GET"],
    ["/app/api/v1/file-drafts/55/files/3", "DELETE"],
    ["/app/api/v1/admin/file-settings", "GET"],
    ["/app/api/v1/admin/file-settings", "PUT"],
    ["/app/api/v1/admin/file-maintenance/run", "POST"]
]) {
    test(`allowed FILE request: ${method} ${path}`, async () => {
        globalThis.fetch = async (url, options) => {
            if (url.pathname.endsWith("/antiforgery"))
                return { status: 200, text: async () => '{"token":"csrf-test"}' };
            if (method !== "GET" && path.includes("/file-drafts"))
                assert.equal(options.headers["X-CSRF-TOKEN"], "csrf-test");
            return { status: 200, text: async () => "{}" };
        };
        assert.equal((await send(path, method, null)).status, 200);
    });
}

for (const [path, method] of [
    ["https://other.example/app/api/v1/files/9", "GET"],
    ["/app/api/v1/courses/7", "GET"],
    ["/app/api/v1/courses/7/my-grades", "GET"],
    ["/app/api/v1/files/9", "PUT"],
    ["/app/api/v1/files/9/content", "GET"],
    ["/app/api/v1/files/x", "GET"],
    ["/app/api/v1/admin/file-settings", "DELETE"],
    ["/app/api/v1/admin/users", "GET"],
    ["/app/api/v1/file-areas/7/course/files/0/../../../auth/login", "POST"],
    ["/api/v1/files/9", "GET"]
]) {
    test(`unsupported request is rejected before fetch: ${method} ${path}`, async () => {
        globalThis.fetch = () => { throw new Error("fetch must not be called"); };
        await assert.rejects(send(path, method, null), /Unsupported FILE request/);
    });
}

for (const path of ["/app/api/v1/file-drafts", "/app/api/v1/files/9", "https://other.example/app/api/v1/file-drafts/55/files"]) {
    test(`upload only goes to the draft files endpoint: ${path}`, async () => {
        globalThis.fetch = () => { throw new Error("fetch must not be called"); };
        await assert.rejects(upload(path, "a.txt", "text/plain", streamRef("x"), {}), /Unsupported FILE request/);
    });
}

test("network failures are not replaced by a fake success response", async () => {
    globalThis.fetch = async () => { throw new Error("network down"); };
    await assert.rejects(send("/app/api/v1/users/me/quota", "GET", null), /network down/);
});

for (const body of ['{}', '{"token":""}', '{"token":42}']) {
    test('draft writes stop when antiforgery token is invalid: ' + body, async () => {
        let calls = 0;
        globalThis.fetch = async url => {
            calls++;
            assert.equal(url.pathname, "/app/api/v1/antiforgery");
            return { status: 200, text: async () => body };
        };
        await assert.rejects(send("/app/api/v1/file-drafts", "POST", null), /Antiforgery token unavailable/);
        assert.equal(calls, 1);
    });
}
