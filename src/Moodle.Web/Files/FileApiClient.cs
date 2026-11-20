using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;

namespace Moodle.Web.Files;

public sealed class FileApiException(HttpStatusCode status, string code, string message) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
    public string Code { get; } = code;

    /// <summary>Yazma işlemi yetki nedeniyle reddedildi (FILE yetkisizlikte 404 döner, FILE-BR-11).</summary>
    public bool IsDenied => Status is HttpStatusCode.Forbidden or HttpStatusCode.NotFound;
}

/// <summary>
/// FILE HTTP uçlarını çağırır (GrdApiClient deseni; WBS-2 §3.6.4). Hata gövdeleri anlaşılır Türkçe mesaja çevrilir.
/// Yükleme uçları (<c>/file-drafts</c>) yoksa (404/405) <c>upload_unavailable</c> hatası verilir.
/// </summary>
public sealed class FileApiClient(HttpClient http) : IDisposable
{
    public const string UploadUnavailable = "upload_unavailable";

    // ---- Alanlar ve dosyalar (FILE-FR-02, 03, 04, 05) ----

    public async Task<IReadOnlyList<FileItem>> ListAsync(FileAreaRef area, string path = "/", CancellationToken ct = default) =>
        (await SendAsync<FileListResponse>(HttpMethod.Get,
            $"{area.Path}/files" + (path == "/" ? "" : "?path=" + Uri.EscapeDataString(path)), null, ct)).Files;

    public Task<FileItem> MetaAsync(long fileId, CancellationToken ct = default) =>
        SendAsync<FileItem>(HttpMethod.Get, $"files/{fileId}", null, ct);

    /// <summary>
    /// İndirme bağlantısı (göreli). Tarayıcı aynı kaynaktaki oturum çereziyle doğrudan açar; içerik Blazor devresinden geçmez.
    /// </summary>
    public static string DownloadUrl(long fileId) => $"api/v1/files/{fileId}/content?download=true";

    public Task DeleteAsync(long fileId, CancellationToken ct = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"files/{fileId}", ct);

    public Task<FileQuota> QuotaAsync(CancellationToken ct = default) =>
        SendAsync<FileQuota>(HttpMethod.Get, "users/me/quota", null, ct);

    // ---- Yükleme: taslak → kalıcı alan (FILE-FR-01, 02; FILE-BR-05) ----

    public Task<FileDraftInfo> CreateDraftAsync(CancellationToken ct = default) =>
        UploadEndpointAsync(() => SendAsync<FileDraftInfo>(HttpMethod.Post, "file-drafts", null, ct));

    public Task<FileUploadResult> UploadToDraftAsync(long draftId, string fileName, string? contentType, Stream content,
        CancellationToken ct = default) => UploadEndpointAsync(() =>
    {
        var form = new MultipartFormDataContent();
        var file = new StreamContent(content);
        file.Headers.ContentType = MediaTypeHeaderValue.TryParse(contentType, out var type)
            ? type : new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        return SendAsync<FileUploadResult>(HttpMethod.Post, $"file-drafts/{draftId}/files", form, ct);
    });

    /// <summary>Taslaktakileri alana kaydeder; <paramref name="replace"/> varsayılan olarak kapalıdır, alandaki dosyalar korunur.</summary>
    public async Task<IReadOnlyList<FileItem>> CommitAsync(FileAreaRef area, long draftId, bool replace = false,
        CancellationToken ct = default) =>
        (await SendAsync<FileListResponse>(HttpMethod.Post, $"{area.Path}/commit",
            JsonContent.Create(new { draftId, replace }), ct)).Files;

    // ---- Yönetici (FILE-FR-12; FILE-BR-04, 05, 08, 10) ----

    public Task<FileSettingsDto> SettingsAsync(CancellationToken ct = default) =>
        SendAsync<FileSettingsDto>(HttpMethod.Get, "admin/file-settings", null, ct);

    public Task<FileSettingsDto> SaveSettingsAsync(FileSettingsDto settings, CancellationToken ct = default) =>
        SendAsync<FileSettingsDto>(HttpMethod.Put, "admin/file-settings", JsonContent.Create(settings), ct);

    public Task<FileMaintenanceResult> RunMaintenanceAsync(CancellationToken ct = default) =>
        SendAsync<FileMaintenanceResult>(HttpMethod.Post, "admin/file-maintenance/run", null, ct);

    // ---- Taşıma ----

    private static async Task<T> UploadEndpointAsync<T>(Func<Task<T>> call)
    {
        try { return await call(); }
        catch (FileApiException ex) when (ex.Status is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed)
        {
            throw new FileApiException(ex.Status, UploadUnavailable,
                "Yükleme şu an kullanılamıyor. Dosya listesi ve silme çalışmaya devam ediyor.");
        }
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        using var response = await SendRawAsync(method, path, content, ct);
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct) ?? throw InvalidResponse();
        }
        catch (JsonException) { throw InvalidResponse(); }
    }

    private async Task SendNoContentAsync(HttpMethod method, string path, CancellationToken ct)
    {
        using var _ = await SendRawAsync(method, path, null, ct);
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, "api/v1/" + path) { Content = content };
        HttpResponseMessage response;
        try { response = await http.SendAsync(request, ct); }
        catch (Exception ex) when (ex is HttpRequestException or JSException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        { throw new FileApiException(0, "connection_failed", "Sunucuya ulaşılamadı. Bağlantınızı kontrol edip yeniden deneyin."); }

        if (response.IsSuccessStatusCode) return response;
        using (response) throw await ToExceptionAsync(response, ct);
    }

    private static async Task<FileApiException> ToExceptionAsync(HttpResponseMessage response, CancellationToken ct)
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

        return new FileApiException(response.StatusCode, code ?? "request_failed", Friendly(response.StatusCode, code, message));
    }

    /// <summary>
    /// Kullanıcıya gösterilen mesaj. Sunucu mesajı yalnızca 422'de gösterilir (doğrulama, kota, antivirüs mesajları
    /// kullanıcıya yazılmıştır); diğer durumlarda iç ayrıntı sızmasın diye arayüzün kendi metni kullanılır.
    /// </summary>
    internal static string Friendly(HttpStatusCode status, string? code, string? message) => (status, code) switch
    {
        (HttpStatusCode.ServiceUnavailable, "auth_not_ready") =>
            "Dosya işlemleri şu anda kullanılamıyor. Kimlik ve yetkilendirme sistemi (AUTH) henüz hazır değil. Hazır olduğunda bu ekrandan işlem yapabilirsiniz.",
        (HttpStatusCode.Unauthorized, _) =>
            "Dosyalar için oturum açmanız gerekiyor. Oturum açıp yeniden deneyin.",
        (HttpStatusCode.Forbidden, _) =>
            "Bu işlem için yetkiniz yok.",
        (HttpStatusCode.NotFound, _) =>
            "Dosya veya dosya alanı bulunamadı ya da bu işlem için yetkiniz yok.",
        (HttpStatusCode.Conflict, "maintenance_running") =>
            "Temizlik zaten çalışıyor. Bitmesini bekleyip yeniden deneyin.",
        (HttpStatusCode.Conflict, "name_conflict") =>
            "Bu adla bir dosya zaten var. Dosyayı yeniden adlandırıp yeniden deneyin.",
        (HttpStatusCode.Conflict, _) =>
            "İşlem mevcut kayıtlarla çakışıyor. Sayfayı yenileyip yeniden deneyin.",
        (HttpStatusCode.RequestEntityTooLarge, _) =>
            "Dosya izin verilen boyut sınırını aşıyor. Daha küçük bir dosya seçin.",
        (HttpStatusCode.UnsupportedMediaType, _) =>
            "Bu dosya türü bu alanda kabul edilmiyor.",
        (HttpStatusCode.UnprocessableEntity, _) when !string.IsNullOrWhiteSpace(message) => message,
        (HttpStatusCode.UnprocessableEntity, _) =>
            "Girilen değer geçersiz. Alanları kontrol edip yeniden deneyin.",
        (HttpStatusCode.BadRequest, _) =>
            "İstek geçersiz. Sayfayı yenileyip yeniden deneyin.",
        _ => "İşlem tamamlanamadı. Lütfen daha sonra yeniden deneyin."
    };

    private static FileApiException InvalidResponse() =>
        new(0, "invalid_response", "Sunucudan beklenen yanıt alınamadı. Lütfen yeniden deneyin.");

    public void Dispose() => http.Dispose();
}
