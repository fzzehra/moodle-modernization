using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;

namespace Moodle.Web.Grd;

public sealed class GrdApiException(HttpStatusCode status, string code, string message) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
    public string Code { get; } = code;

    /// <summary>Not defterinde korunan (kilitli veya elle değiştirilmiş) not.</summary>
    public bool IsProtectedGrade => Code is "grade_locked" or "grade_overridden";
}

/// <summary>GRD'nin dört HTTP ucunu çağırır (CrsApiClient deseni). Hata gövdeleri anlaşılır Türkçe mesaja çevrilir.</summary>
public sealed class GrdApiClient(HttpClient http) : IDisposable
{
    public Task<GrdGradeView> GradeAsync(int assignmentId, int learnerId, int attempt, CancellationToken ct = default) =>
        SendAsync<GrdGradeView>(HttpMethod.Get, GradePath(assignmentId, learnerId, attempt), null, ct);

    public Task<GrdGradeSaveResult> SaveGradeAsync(int assignmentId, int learnerId, int attempt,
        GradeWriteRequest request, CancellationToken ct = default) =>
        SendAsync<GrdGradeSaveResult>(HttpMethod.Put, GradePath(assignmentId, learnerId, attempt), request, ct);

    /// <summary>Kişisel rapor: öğrenci kimliği gönderilmez, sunucu oturumdan alır (GRD-NFR-01).</summary>
    public Task<GrdMyGrades> MyGradesAsync(int courseId, CancellationToken ct = default) =>
        SendAsync<GrdMyGrades>(HttpMethod.Get, $"courses/{courseId}/my-grades", null, ct);

    public Task<GrdGradebook> GradebookAsync(int courseId, string? cursor = null, int? limit = null, CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(cursor)) query.Add("cursor=" + Uri.EscapeDataString(cursor));
        if (limit.HasValue) query.Add("limit=" + limit.Value);
        var path = $"courses/{courseId}/gradebook" + (query.Count == 0 ? "" : "?" + string.Join("&", query));
        return SendAsync<GrdGradebook>(HttpMethod.Get, path, null, ct);
    }

    private static string GradePath(int assignmentId, int learnerId, int attempt) =>
        $"assignments/{assignmentId}/learners/{learnerId}/attempts/{attempt}/grade";

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, "api/v1/" + path);
        if (body is not null) request.Content = JsonContent.Create(body, body.GetType());
        HttpResponseMessage response;
        try { response = await http.SendAsync(request, ct); }
        catch (Exception ex) when (ex is HttpRequestException or JSException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        { throw new GrdApiException(0, "connection_failed", "Sunucuya ulaşılamadı. Bağlantınızı kontrol edip yeniden deneyin."); }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw await ToExceptionAsync(response, ct);
            try
            {
                return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct) ?? throw InvalidResponse();
            }
            catch (JsonException) { throw InvalidResponse(); }
        }
    }

    private static async Task<GrdApiException> ToExceptionAsync(HttpResponseMessage response, CancellationToken ct)
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

        return new GrdApiException(response.StatusCode, code ?? "request_failed", Friendly(response.StatusCode, code, message));
    }

    /// <summary>
    /// Kullanıcıya gösterilen mesaj. Sunucu mesajı yalnızca 422'de gösterilir: GRD doğrulama mesajları
    /// (ör. "Puan 0 ile 100 arasında olmalıdır.") kullanıcıya yazılmıştır. Diğer durumlarda iç ayrıntı sızmasın
    /// diye arayüzün kendi metni kullanılır.
    /// </summary>
    internal static string Friendly(HttpStatusCode status, string? code, string? message) => (status, code) switch
    {
        (HttpStatusCode.ServiceUnavailable, "auth_not_ready") =>
            "Not işlemleri şu anda kullanılamıyor. Kimlik ve yetkilendirme sistemi (AUTH) henüz hazır değil. Hazır olduğunda bu ekrandan işlem yapabilirsiniz.",
        (HttpStatusCode.Unauthorized, _) =>
            "Notları görmek için oturum açmanız gerekiyor. Oturum açıp yeniden deneyin.",
        (HttpStatusCode.Forbidden, _) =>
            "Bu notları görüntüleme veya düzenleme yetkiniz yok.",
        (HttpStatusCode.NotFound, _) =>
            "İstenen ödev, ders veya öğrenci bulunamadı. Öğrencinin derse kayıtlı olduğundan emin olun.",
        (HttpStatusCode.Conflict, "grade_locked") =>
            "Bu not defterde korunuyor: not kilitli olduğu için puan değiştirilemez.",
        (HttpStatusCode.Conflict, "grade_overridden") =>
            "Bu not defterde korunuyor: not defterde elle değiştirildiği için ödev puanı aktarılamaz.",
        (HttpStatusCode.Conflict, "version_conflict") =>
            "Bu değerlendirmeyi siz düzenlerken başka biri değiştirdi. Güncel puanı görmek için yenileyin.",
        (HttpStatusCode.Conflict, _) =>
            "İşlem mevcut kayıtlarla çakışıyor. Sayfayı yenileyip yeniden deneyin.",
        (HttpStatusCode.UnprocessableEntity, _) when !string.IsNullOrWhiteSpace(message) => message,
        (HttpStatusCode.UnprocessableEntity, _) =>
            "Girilen değer geçersiz. Alanları kontrol edip yeniden deneyin.",
        (HttpStatusCode.BadRequest, _) =>
            "İstek geçersiz. Sayfayı yenileyip yeniden deneyin.",
        _ => "İşlem tamamlanamadı. Lütfen daha sonra yeniden deneyin."
    };

    private static GrdApiException InvalidResponse() =>
        new(0, "invalid_response", "Sunucudan beklenen yanıt alınamadı. Lütfen yeniden deneyin.");

    public void Dispose() => http.Dispose();
}
