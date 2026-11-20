using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Web.Files;

namespace Moodle.UnitTests;

/// <summary>Yakalanan istek. Çok parçalı yüklemede dosya adı, türü ve içeriği ayrıca saklanır.</summary>
internal sealed record FileWebRequest(string Method, string PathAndQuery, string? Body,
    string? UploadName = null, string? UploadType = null, string? UploadText = null);

internal sealed class FileWebHandler(Func<FileWebRequest, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public ConcurrentQueue<FileWebRequest> Requests { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        FileWebRequest captured;
        if (request.Content is MultipartFormDataContent form)
        {
            var file = form.Single(p => p.Headers.ContentDisposition?.FileNameStar is not null || p.Headers.ContentDisposition?.FileName is not null);
            var disposition = file.Headers.ContentDisposition!;
            captured = new FileWebRequest(request.Method.Method, request.RequestUri!.PathAndQuery, null,
                (disposition.FileNameStar ?? disposition.FileName)!.Trim('"'), file.Headers.ContentType?.MediaType,
                await file.ReadAsStringAsync(ct));
        }
        else
        {
            captured = new FileWebRequest(request.Method.Method, request.RequestUri!.PathAndQuery,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(ct));
        }
        Requests.Enqueue(captured);
        return await respond(captured, ct);
    }
}

/// <summary>FILE arayüz testleri için yalnızca testte kullanılan HTTP yanıtları (GrdWebTestSupport deseni).</summary>
internal static class FileWebTestSupport
{
    public const string AreaPath = "/api/v1/file-areas/7/course/files/0";
    public const string ListPath = AreaPath + "/files";
    public const string CommitPath = AreaPath + "/commit";
    public const string DraftsPath = "/api/v1/file-drafts";
    public const string DraftFilesPath = "/api/v1/file-drafts/55/files";
    public const string SettingsPath = "/api/v1/admin/file-settings";
    public const string MaintenancePath = "/api/v1/admin/file-maintenance/run";

    public static readonly FileAreaRef Area = FileAreaRef.CourseFiles(7);
    private static readonly DateTimeOffset Added = new(2026, 11, 18, 9, 30, 0, TimeSpan.Zero);

    public static readonly FileItem Notes = new(1, "SENG101 Ders Notları.pdf", "/", 1536, "application/pdf", new string('a', 64), 3, Added);
    public static readonly FileItem Slides = new(2, "hafta2.pptx", "/", 2 * 1024 * 1024, "application/octet-stream", new string('b', 64), 3, Added);
    public static readonly FileItem Uploaded = new(3, "yeni.txt", "/", 5, "text/plain", new string('c', 64), 3, Added);

    public static readonly FileSettingsDto Settings = new(50L * 1024 * 1024, 100L * 1024 * 1024, 96, 24);

    public static HttpResponseMessage Json(object body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = JsonContent.Create(body, body.GetType()) };

    public static HttpResponseMessage Error(HttpStatusCode status, string code, string message = "Password=private detail") =>
        Json(new { error = new { code, message } }, status);

    public static HttpResponseMessage List(params FileItem[] files) => Json(new FileListResponse([.. files]));

    public static HttpResponseMessage Default(FileWebRequest request) => (request.Method, request.PathAndQuery) switch
    {
        ("GET", ListPath) => List(Notes, Slides),
        ("GET", SettingsPath) => Json(Settings),
        _ => Error(HttpStatusCode.NotFound, "not_found", "Not found"),
    };

    public static FileWebHandler Handler(Func<FileWebRequest, HttpResponseMessage>? respond = null) =>
        new((request, _) => Task.FromResult((respond ?? Default)(request)));

    public static BunitContext Context(FileWebHandler handler)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose; // InputFile
        context.Services.AddSingleton(new FileApiClient(new HttpClient(handler) { BaseAddress = new("http://localhost/") }));
        return context;
    }

    public static FileApiClient Client(FileWebHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new("http://localhost/") });
}
