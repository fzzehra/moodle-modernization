using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Web.Crs;

namespace Moodle.UnitTests;

internal sealed record WebRequest(string Method, string Path, string? Body);
internal sealed class CrsWebHandler(Func<WebRequest, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public ConcurrentQueue<WebRequest> Requests { get; } = new();
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var captured = new WebRequest(request.Method.Method, request.RequestUri!.AbsolutePath,
            request.Content is null ? null : await request.Content.ReadAsStringAsync(ct));
        Requests.Enqueue(captured);
        return await respond(captured, ct);
    }
}

internal static class CrsWebTestSupport
{
    public static readonly CrsCategory[] Categories = [new(1, "Matematik", null), new(2, "Bilim", null)];
    public static readonly CrsCourse First = new(1, 1, "Cebir", "MAT101", "ID-1", true,
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), null);
    public static readonly CrsCourse Second = new(2, 2, "Fizik", "FIZ101", null, false, null, null);
    public static readonly CrsEnrolment Enrolment = new(11, 1, 42, "Student", false, null, null);
    public static HttpResponseMessage Json(object body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = JsonContent.Create(body, body.GetType()) };
    public static HttpResponseMessage Default(WebRequest request) => request.Path switch
    {
        "/api/v1/categories" => Json(Categories),
        "/api/v1/courses" => Json(new[] { First, Second }),
        "/api/v1/courses/1" => Json(First),
        "/api/v1/courses/2" => Json(Second),
        "/api/v1/courses/1/enrolments" => Json(new[] { Enrolment }),
        _ => Json(new { error = new { code = "not_found", message = "Not found" } }, HttpStatusCode.NotFound)
    };
    public static BunitContext Context(CrsWebHandler handler)
    {
        var context = new BunitContext();
        context.Services.AddSingleton(new CrsApiClient(new HttpClient(handler) { BaseAddress = new("http://localhost/") }));
        return context;
    }
    public static CrsWebHandler Handler(Func<WebRequest, HttpResponseMessage>? respond = null) =>
        new((request, _) => Task.FromResult((respond ?? Default)(request)));
}
