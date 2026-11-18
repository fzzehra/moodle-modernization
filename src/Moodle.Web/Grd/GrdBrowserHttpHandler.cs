using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Moodle.Web.Grd;

/// <summary>
/// İstekleri tarayıcıda gönderir; API kullanıcının gerçek aynı-kaynak oturumunu alır
/// (CrsBrowserHttpHandler deseni). Sunucu tarafında döngü isteği, çerez kopyalama veya sahte kimlik yoktur.
/// </summary>
public sealed class GrdBrowserHttpHandler(IJSRuntime js) : HttpMessageHandler, IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _module = new(() =>
        js.InvokeAsync<IJSObjectReference>("import", "./_content/Moodle.Web/grd-api.js").AsTask());

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var module = await _module.Value;
        var result = await module.InvokeAsync<BrowserResponse>("send", ct,
            request.RequestUri!.PathAndQuery, request.Method.Method,
            request.Content is null ? null : await request.Content.ReadAsStringAsync(ct));
        return new((System.Net.HttpStatusCode)result.Status)
        { Content = new StringContent(result.Body, Encoding.UTF8, "application/json") };
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

public static class GrdWebRegistration
{
    public static IServiceCollection AddGrdWeb(this IServiceCollection services)
    {
        services.AddScoped<GrdBrowserHttpHandler>();
        services.AddScoped(sp => new GrdApiClient(new HttpClient(sp.GetRequiredService<GrdBrowserHttpHandler>(), disposeHandler: false)
        { BaseAddress = new Uri(sp.GetRequiredService<NavigationManager>().BaseUri) }));
        return services;
    }
}
