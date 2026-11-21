using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Moodle.Web.Files;

/// <summary>
/// İstekleri tarayıcıda gönderir; API kullanıcının gerçek aynı-kaynak oturumunu alır (GrdBrowserHttpHandler deseni).
/// JSON istekleri <c>file-api.js</c>'in <c>send</c> işlevine, dosya yüklemeleri (<see cref="MultipartFormDataContent"/>)
/// <c>upload</c> işlevine gider; dosya içeriği <see cref="DotNetStreamReference"/> ile akıtılır ve tarayıcıda
/// <c>FormData</c> olarak gönderilir. Sunucu tarafında döngü isteği, çerez kopyalama veya sahte kimlik yoktur.
/// </summary>
public sealed class FileBrowserHttpHandler(IJSRuntime js) : HttpMessageHandler, IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _module = new(() =>
        js.InvokeAsync<IJSObjectReference>("import", "./_content/Moodle.Web/file-api.js").AsTask());

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var module = await _module.Value;
        var path = request.RequestUri!.PathAndQuery;
        var result = request.Content is MultipartFormDataContent form
            ? await UploadAsync(module, path, form, ct)
            : await module.InvokeAsync<BrowserResponse>("send", ct, path, request.Method.Method,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(ct));
        return new((System.Net.HttpStatusCode)result.Status)
        { Content = new StringContent(result.Body, Encoding.UTF8, "application/json") };
    }

    private static async Task<BrowserResponse> UploadAsync(IJSObjectReference module, string path,
        MultipartFormDataContent form, CancellationToken ct)
    {
        HttpContent? file = null;
        var fields = new Dictionary<string, string>();
        foreach (var part in form)
        {
            var disposition = part.Headers.ContentDisposition;
            if (disposition?.FileNameStar is not null || disposition?.FileName is not null) file = part;
            else if (disposition?.Name is { } name) fields[name.Trim('"')] = await part.ReadAsStringAsync(ct);
        }
        if (file is null) throw new InvalidOperationException("Yükleme isteğinde dosya yok.");

        var fileName = (file.Headers.ContentDisposition!.FileNameStar ?? file.Headers.ContentDisposition.FileName!).Trim('"');
        var contentType = file.Headers.ContentType?.MediaType ?? "application/octet-stream";
        await using var stream = await file.ReadAsStreamAsync(ct);
        using var reference = new DotNetStreamReference(stream, leaveOpen: true);
        return await module.InvokeAsync<BrowserResponse>("upload", ct, path, fileName, contentType, reference, fields);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module.IsValueCreated)
        {
            try { await (await _module.Value).DisposeAsync(); }
            catch (JSDisconnectedException) { }
            catch (JSException) { }
        }
        Dispose();
    }

    public sealed record BrowserResponse(int Status, string Body);
}

public static class FileWebRegistration
{
    public static IServiceCollection AddFileWeb(this IServiceCollection services)
    {
        services.AddScoped<FileBrowserHttpHandler>();
        services.AddScoped(sp => new FileApiClient(new HttpClient(sp.GetRequiredService<FileBrowserHttpHandler>(), disposeHandler: false)
        { BaseAddress = new Uri(sp.GetRequiredService<NavigationManager>().BaseUri) }));
        return services;
    }
}
