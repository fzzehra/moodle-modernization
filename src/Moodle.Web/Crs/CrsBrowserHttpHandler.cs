using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Moodle.Web.Crs;

// Send HTTP in the browser so the API receives the real same-origin session.
// No server-side loopback, cookie copying, or synthetic identity is used.
public sealed class CrsBrowserHttpHandler(IJSRuntime js) : HttpMessageHandler, IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _module = new(() =>
        js.InvokeAsync<IJSObjectReference>("import", "./_content/Moodle.Web/crs-api.js").AsTask());

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

public static class CrsWebRegistration
{
    public static IServiceCollection AddCrsWeb(this IServiceCollection services)
    {
        services.AddScoped<CrsBrowserHttpHandler>();
        services.AddScoped(sp => new CrsApiClient(new HttpClient(sp.GetRequiredService<CrsBrowserHttpHandler>(), disposeHandler: false)
        { BaseAddress = new Uri(sp.GetRequiredService<NavigationManager>().BaseUri) }));
        return services;
    }
}
