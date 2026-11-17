import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const source = await readFile(new URL("../../src/Moodle.Web/wwwroot/crs-api.js", import.meta.url), "utf8");
const { send } = await import("data:text/javascript;base64," + Buffer.from(source).toString("base64"));
globalThis.document = { baseURI: "http://localhost/app/" };
globalThis.location = { origin: "http://localhost" };

test("same-origin browser session is used and auth_not_ready is returned unchanged", async () => {
    let options;
    globalThis.fetch = async (url, init) => {
        assert.equal(url.href, "http://localhost/app/api/v1/courses");
        options = init;
        return { status: 503, text: async () => '{"error":{"code":"auth_not_ready"}}' };
    };
    const response = await send("/app/api/v1/courses", "GET", null);
    assert.equal(response.status, 503);
    assert.equal(JSON.parse(response.body).error.code, "auth_not_ready");
    assert.equal(options.credentials, "same-origin");
    assert.equal(options.redirect, "error");
    assert.equal(options.cache, "no-store");
    assert.deepEqual(options.headers, { Accept: "application/json" });
    assert.equal(options.body, undefined);
});

test("mutations preserve the supplied JSON and never synthesize credentials", async () => {
    globalThis.fetch = async (_, options) => {
        assert.equal(options.credentials, "same-origin");
        assert.equal(options.headers["Content-Type"], "application/json");
        assert.equal(options.headers.Authorization, undefined);
        assert.equal(options.headers.Cookie, undefined);
        assert.equal(options.body, '{"suspended":true}');
        return { status: 200, text: async () => "{}" };
    };
    await send("/app/api/v1/courses/1/enrolments/2", "PATCH", '{"suspended":true}');
});

for (const [path, method] of [
    ["https://other.example/app/api/v1/courses", "GET"],
    ["/app/api/v1/assignments", "GET"],
    ["/api/v1/courses", "GET"],
    ["/app/api/v1/courses", "PUT"]
]) {
    test(`unsupported request is rejected before fetch: ${method} ${path}`, async () => {
        globalThis.fetch = () => { throw new Error("fetch must not be called"); };
        await assert.rejects(send(path, method, null), /Unsupported CRS request/);
    });
}

test("network failures are not replaced by a fake success response", async () => {
    globalThis.fetch = async () => { throw new Error("network down"); };
    await assert.rejects(send("/app/api/v1/courses", "GET", null), /network down/);
});
