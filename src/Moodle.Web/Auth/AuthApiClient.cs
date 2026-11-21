using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;

namespace Moodle.Web.Auth;

public sealed class AuthApiException(HttpStatusCode status, string code, string message) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
    public string Code { get; } = code;
}

public sealed record AuthLoginResult(bool Authenticated, bool RequiresPasswordChange);
public sealed record AuthSession(bool Authenticated, string? Username);

public sealed class AuthApiClient(HttpClient http) : IDisposable
{
    public Task<AuthLoginResult> LoginAsync(string username, string password, CancellationToken ct = default) =>
        WriteAsync<AuthLoginResult>(HttpMethod.Post, "login", new { username, password }, ct);

    public Task LogoutAsync(CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, "logout", null, ct);

    public Task<AuthSession> SessionAsync(CancellationToken ct = default) =>
        WriteAsync<AuthSession>(HttpMethod.Get, "session", null, ct);

    public Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, "change-password", new { currentPassword, newPassword }, ct);

    private async Task<T> WriteAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var response = await SendAsync(method, path, body, ct);
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
                ?? throw InvalidResponse();
        }
        catch (JsonException) { throw InvalidResponse(); }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, "api/v1/auth/" + path);
        if (body is not null) request.Content = JsonContent.Create(body);
        HttpResponseMessage response;
        try { response = await http.SendAsync(request, ct); }
        catch (Exception ex) when (ex is HttpRequestException or JSException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        { throw new AuthApiException(0, "connection_failed", "Sunucuya ulaşılamadı. Bağlantınızı kontrol edip yeniden deneyin."); }
        if (response.IsSuccessStatusCode) return response;

        using (response)
        {
            string? code = null;
            string? message = null;
            try
            {
                using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                if (json.RootElement.ValueKind == JsonValueKind.Object
                    && json.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
                {
                    if (error.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String) code = c.GetString();
                    if (error.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String) message = m.GetString();
                }
            }
            catch (JsonException) { }
            var friendly = code switch
            {
                "invalid_credentials" => "Kullanıcı adı veya parola hatalı.",
                "account_locked" => "Hesabınız çok sayıda başarısız denemeden sonra kilitlendi. Lütfen daha sonra tekrar deneyin.",
                "current_password_invalid" => "Mevcut parola hatalı.",
                "not_authenticated" => "Oturum açmanız gerekiyor.",
                "password_change_required" => "Devam etmek için parolanızı değiştirmeniz gerekiyor.",
                _ when !string.IsNullOrWhiteSpace(message) => message,
                _ => "İşlem tamamlanamadı. Lütfen daha sonra yeniden deneyin.",
            };
            throw new AuthApiException(response.StatusCode, code ?? "request_failed", friendly);
        }
    }

    private static AuthApiException InvalidResponse() => new(0, "invalid_response", "Sunucudan beklenen yanıt alınamadı. Lütfen yeniden deneyin.");
    public void Dispose() => http.Dispose();
}
