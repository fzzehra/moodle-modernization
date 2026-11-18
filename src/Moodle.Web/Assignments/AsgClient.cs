using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
namespace Moodle.Web.Assignments;

/// <summary>ASG HTTP-only UI boundary. Identity cookies go only to the configured API origin.</summary>
public sealed class AsgClient(HttpClient client, IHttpContextAccessor context)
{
    public async Task<T> SendAsync<T>(HttpMethod method, string path, object? body = null)
    {
        var incoming = context.HttpContext?.Request;
        var target = client.BaseAddress;
        if (incoming is null || target is null || !target.IsAbsoluteUri
            || target.Scheme != incoming.Scheme
            || !string.Equals(target.Authority, incoming.Host.Value, StringComparison.OrdinalIgnoreCase))
            throw new AsgClientException("API adresi bu uygulamanın adresiyle eşleşmiyor; istek gönderilmedi.", 503);
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        var cookie = context.HttpContext?.Request.Headers.Cookie.ToString();
        if (!string.IsNullOrEmpty(cookie)) request.Headers.TryAddWithoutValidation("Cookie", cookie);
        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorBody>();
            throw new AsgClientException(error?.Error.Message ?? "İşlem tamamlanamadı.", (int)response.StatusCode);
        }
        return await response.Content.ReadFromJsonAsync<T>() ?? throw new AsgClientException("Yanıt alınamadı.", 500);
    }
    private sealed record ErrorBody(ErrorValue Error);
    private sealed record ErrorValue(string Code, string Message);
}
public sealed class AsgClientException(string message, int status) : Exception(message)
{
    public int Status { get; } = status;
}
public sealed record AssignmentData(int Id, int CourseId, int SectionId, string Title, string Instructions,
    DateTimeOffset? OpensAt, DateTimeOffset? DueAt, DateTimeOffset? CutoffAt, bool RequiresConfirmation);
public sealed record Permissions(bool CanSubmit, bool CanGrade, bool CanSave, bool CanSend, string? UnavailableReason);
public sealed record AssignmentView(AssignmentData Assignment, Permissions Permissions);
public sealed record SubmissionData(int Id, int LearnerId, string Status, string Text, long Version);
public sealed record MySubmission(string Status, SubmissionData? Submission);
public sealed record SubmissionRow(int LearnerId, string Status, int? SubmissionId,
    DateTimeOffset? SubmittedAt, DateTimeOffset? UpdatedAt, long Version, bool IsLate);
public sealed record SubmissionPage(SubmissionRow[] Items, int? NextCursor);
public static class Labels
{
    public static string Status(string status) => status switch
    {
        "submitted" => "Gönderildi", "draft" => "Taslak", _ => "Teslim edilmedi"
    };
}
