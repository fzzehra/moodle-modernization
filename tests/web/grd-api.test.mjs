import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const source = await readFile(new URL("../../src/Moodle.Web/wwwroot/grd-api.js", import.meta.url), "utf8");
const { send } = await import("data:text/javascript;base64," + Buffer.from(source).toString("base64"));
globalThis.document = { baseURI: "http://localhost/app/" };
globalThis.location = { origin: "http://localhost" };

test("GRD reads use the same-origin browser session and keep auth_not_ready unchanged", async () => {
    let options;
    globalThis.fetch = async (url, init) => {
        assert.equal(url.href, "http://localhost/app/api/v1/courses/7/gradebook?cursor=4");
        options = init;
        return { status: 503, text: async () => '{"error":{"code":"auth_not_ready"}}' };
    };
    const response = await send("/app/api/v1/courses/7/gradebook?cursor=4", "GET", null);
    assert.equal(response.status, 503);
    assert.equal(JSON.parse(response.body).error.code, "auth_not_ready");
    assert.equal(options.credentials, "same-origin");
    assert.equal(options.redirect, "error");
    assert.equal(options.cache, "no-store");
    assert.deepEqual(options.headers, { Accept: "application/json" });
    assert.equal(options.body, undefined);
});

test("grade save sends the supplied JSON with PUT and never synthesizes credentials", async () => {
    globalThis.fetch = async (url, options) => {
        assert.equal(url.pathname, "/app/api/v1/assignments/10/learners/3/attempts/0/grade");
        assert.equal(options.method, "PUT");
        assert.equal(options.credentials, "same-origin");
        assert.equal(options.headers["Content-Type"], "application/json");
        assert.equal(options.headers.Authorization, undefined);
        assert.equal(options.headers.Cookie, undefined);
        assert.equal(options.body, '{"score":90,"feedback":null,"version":1}');
        return { status: 200, text: async () => "{}" };
    };
    await send("/app/api/v1/assignments/10/learners/3/attempts/0/grade", "PUT", '{"score":90,"feedback":null,"version":1}');
});

for (const path of [
    "/app/api/v1/assignments/10/learners/3/attempts/0/grade",
    "/app/api/v1/courses/7/my-grades",
    "/app/api/v1/courses/7/gradebook"
]) {
    test(`allowed GRD read: ${path}`, async () => {
        globalThis.fetch = async () => ({ status: 200, text: async () => "{}" });
        assert.equal((await send(path, "GET", null)).status, 200);
    });
}

for (const [path, method] of [
    ["https://other.example/app/api/v1/courses/7/my-grades", "GET"],
    ["/app/api/v1/courses", "GET"],
    ["/app/api/v1/courses/7/enrolments", "GET"],
    ["/app/api/v1/courses/7/my-grades", "PUT"],
    ["/app/api/v1/courses/7/gradebook", "DELETE"],
    ["/app/api/v1/assignments/10/learners/3/attempts/0/grade", "DELETE"],
    ["/app/api/v1/assignments/x/learners/3/attempts/0/grade", "GET"],
    ["/api/v1/courses/7/my-grades", "GET"]
]) {
    test(`unsupported request is rejected before fetch: ${method} ${path}`, async () => {
        globalThis.fetch = () => { throw new Error("fetch must not be called"); };
        await assert.rejects(send(path, method, null), /Unsupported GRD request/);
    });
}

test("network failures are not replaced by a fake success response", async () => {
    globalThis.fetch = async () => { throw new Error("network down"); };
    await assert.rejects(send("/app/api/v1/courses/7/my-grades", "GET", null), /network down/);
});
