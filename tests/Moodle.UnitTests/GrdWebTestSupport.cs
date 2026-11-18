using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Web.Grd;

namespace Moodle.UnitTests;

/// <summary>Yakalanan istek; sorgu dizesi de saklanır (sayfalama imleci için).</summary>
internal sealed record GrdWebRequest(string Method, string PathAndQuery, string? Body);

internal sealed class GrdWebHandler(Func<GrdWebRequest, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public ConcurrentQueue<GrdWebRequest> Requests { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var captured = new GrdWebRequest(request.Method.Method, request.RequestUri!.PathAndQuery,
            request.Content is null ? null : await request.Content.ReadAsStringAsync(ct));
        Requests.Enqueue(captured);
        return await respond(captured, ct);
    }
}

/// <summary>GRD arayüz testleri için yalnızca testte kullanılan HTTP yanıtları (CrsWebTestSupport deseni).</summary>
internal static class GrdWebTestSupport
{
    public const string GradePath = "/api/v1/assignments/10/learners/3/attempts/0/grade";
    public const string MyGradesPath = "/api/v1/courses/7/my-grades";
    public const string GradebookPath = "/api/v1/courses/7/gradebook";

    public static readonly GrdAssessment Assessment =
        new(5, 10, 3, 0, 42, 85m, "İyi çalışma", new DateTimeOffset(2026, 11, 18, 9, 30, 0, TimeSpan.Zero), 1);

    public static readonly GrdGradeView GradedView = new(Assessment, 0m, 100m, 1);
    public static readonly GrdGradeView EmptyView = new(null, 0m, 100m, 0);

    public static readonly GrdMyGrades MyGrades = new(7,
    [
        new(1, "Ödev 1", 0m, 100m, 85m),
        new(2, "Kısa sınav", 0m, 20m, null),
    ]);

    public static readonly GrdGradebook FirstPage = new(7,
        [
            new(1, 10, "Ödev 1", 0m, 100m, false, false),
            new(2, null, "Katılım", 0m, 10m, true, false),
        ],
        [
            new(3, [new(1, 85m, 1, false, true, false), new(2, null, null, true, false, false)]),
            new(4, [new(1, 72m, 0, false, false, true), new(2, 5m, null, true, false, false)]),
        ],
        "4");

    public static readonly GrdGradebook LastPage = FirstPage with
    {
        Learners = [new(5, [new(1, null, null, false, false, false), new(2, null, null, true, false, false)])],
        NextCursor = null,
    };

    public static HttpResponseMessage Json(object body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = JsonContent.Create(body, body.GetType()) };

    public static HttpResponseMessage Error(HttpStatusCode status, string code, string message = "Password=private detail") =>
        Json(new { error = new { code, message } }, status);

    public static HttpResponseMessage Default(GrdWebRequest request) => request.PathAndQuery switch
    {
        GradePath when request.Method == "GET" => Json(GradedView),
        MyGradesPath => Json(MyGrades),
        GradebookPath => Json(FirstPage),
        GradebookPath + "?cursor=4" => Json(LastPage),
        _ => Error(HttpStatusCode.NotFound, "not_found", "Not found"),
    };

    public static GrdWebHandler Handler(Func<GrdWebRequest, HttpResponseMessage>? respond = null) =>
        new((request, _) => Task.FromResult((respond ?? Default)(request)));

    public static BunitContext Context(GrdWebHandler handler)
    {
        var context = new BunitContext();
        context.Services.AddSingleton(new GrdApiClient(new HttpClient(handler) { BaseAddress = new("http://localhost/") }));
        return context;
    }

    public static GrdApiClient Client(GrdWebHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new("http://localhost/") });
}
