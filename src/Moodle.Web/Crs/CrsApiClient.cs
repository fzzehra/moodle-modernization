using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;

namespace Moodle.Web.Crs;

public sealed class CrsApiException(HttpStatusCode status, string code, string message) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
    public string Code { get; } = code;
}

public sealed class CrsApiClient(HttpClient http) : IDisposable
{
    public Task<List<CrsCategory>> CategoriesAsync(CancellationToken ct = default) => GetAsync<List<CrsCategory>>("categories", ct);
    public Task<List<CrsCourse>> CoursesAsync(CancellationToken ct = default) => GetAsync<List<CrsCourse>>("courses", ct);
    public Task<CrsCourse> CourseAsync(int id, CancellationToken ct = default) => GetAsync<CrsCourse>($"courses/{id}", ct);
    public Task<List<CrsEnrolment>> EnrolmentsAsync(int courseId, CancellationToken ct = default) => GetAsync<List<CrsEnrolment>>($"courses/{courseId}/enrolments", ct);
    public Task<CrsCourse> CreateCourseAsync(CourseWriteRequest request, CancellationToken ct = default) =>
        WriteAsync<CrsCourse>(HttpMethod.Post, "courses", request, ct);
    public Task<CrsCourse> UpdateCourseAsync(int id, CourseWriteRequest request, CancellationToken ct = default) =>
        WriteAsync<CrsCourse>(HttpMethod.Patch, $"courses/{id}", request, ct);
    public Task DeleteCourseAsync(int id, CancellationToken ct = default) => DeleteAsync($"courses/{id}", ct);
    public Task<CrsEnrolment> EnrolAsync(int courseId, EnrolmentWriteRequest request, CancellationToken ct = default) =>
        WriteAsync<CrsEnrolment>(HttpMethod.Post, $"courses/{courseId}/enrolments", request, ct);
    public Task<CrsEnrolment> UpdateEnrolmentAsync(int courseId, int id, EnrolmentUpdateRequest request, CancellationToken ct = default) =>
        WriteAsync<CrsEnrolment>(HttpMethod.Patch, $"courses/{courseId}/enrolments/{id}", request, ct);
    public Task UnenrolAsync(int courseId, int id, CancellationToken ct = default) => DeleteAsync($"courses/{courseId}/enrolments/{id}", ct);

    private Task<T> GetAsync<T>(string path, CancellationToken ct) => WriteAsync<T>(HttpMethod.Get, path, null, ct);
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

    private async Task DeleteAsync(string path, CancellationToken ct)
    {
        using var response = await SendAsync(HttpMethod.Delete, path, null, ct);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, "api/v1/" + path);
        if (body is not null) request.Content = JsonContent.Create(body, body.GetType());
        HttpResponseMessage response;
        try { response = await http.SendAsync(request, ct); }
        catch (Exception ex) when (ex is HttpRequestException or JSException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        { throw new CrsApiException(0, "connection_failed", "Sunucuya ulaşılamadı. Bağlantınızı kontrol edip yeniden deneyin."); }
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
            var friendly = (response.StatusCode, code) switch
            {
                (HttpStatusCode.ServiceUnavailable, "auth_not_ready") =>
                    "Ders ve kayıt işlemleri şu anda kullanılamıyor. Kimlik ve yetkilendirme sistemi (AUTH) henüz hazır değil. Hazır olduğunda bu ekrandan işlem yapabilirsiniz.",
                (HttpStatusCode.Unauthorized, _) => "Bu işlem için oturum açmanız gerekiyor. Oturum açıp yeniden deneyin.",
                (HttpStatusCode.Forbidden, _) => "Bu işlem için yetkiniz yok.",
                (HttpStatusCode.NotFound, _) => "İstenen ders veya kayıt bulunamadı. Ders listesine dönebilirsiniz.",
                (HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity, _) when !string.IsNullOrWhiteSpace(message) => message,
                (HttpStatusCode.BadRequest, _) => "Gönderilen bilgiler geçersiz. Alanları kontrol edip yeniden deneyin.",
                _ => "İşlem tamamlanamadı. Lütfen daha sonra yeniden deneyin."
            };
            throw new CrsApiException(response.StatusCode, code ?? "request_failed", friendly);
        }
    }

    private static CrsApiException InvalidResponse() => new(0, "invalid_response", "Sunucudan beklenen yanıt alınamadı. Lütfen yeniden deneyin.");
    public void Dispose() => http.Dispose();
}
