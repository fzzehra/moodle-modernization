using System.Text.Json.Serialization;
using Moodle.Api.Errors;
using Moodle.Application.Abstractions;
using Moodle.Application.Errors;
using Moodle.Application.Files;
using Moodle.Infrastructure.Fakes;

namespace Moodle.Api.Files;

/// <summary>PUT gövdesi. Bilinmeyen alan 400; eksik alan 422 (ayar değişmez).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FileSettingsRequest(
    long? MaxFileBytes = null, long? UserQuotaBytes = null, int? DraftTtlHours = null, int? TrashRetentionHours = null)
{
    public FileSettings ToSettings() =>
        MaxFileBytes is { } max && UserQuotaBytes is { } quota && DraftTtlHours is { } ttl && TrashRetentionHours is { } trash
            ? new FileSettings(max, quota, ttl, trash)
            : throw AppException.Validation("validation_failed",
                "maxFileBytes, userQuotaBytes, draftTtlHours ve trashRetentionHours alanlarının hepsi gönderilmelidir.");
}

public sealed record FileMaintenanceResponse(int ExpiredDrafts, int PurgedEntries, int PurgedBlobs, IReadOnlyList<string> Errors);

/// <summary>
/// Dosya yönetimi yetki girişi (CRS/GRD deseni). Yalnızca site yöneticisi (<c>SiteAdmin</c>) geçer; <c>Manager</c> geçmez.
/// AUTH sahteyse 503 <c>auth_not_ready</c>, oturum yoksa 401, site yöneticisi değilse 403.
/// </summary>
public sealed class FileAdminAuthorization(ICurrentUser user, IAccessPolicy policy, ISiteAdminQuery siteAdmins)
{
    public async Task RequireSiteAdminAsync(CancellationToken ct)
    {
        if (user is FakeCurrentUser or UnavailableIdentity || policy is FakeAccessPolicy or UnavailableIdentity)
            throw new AppException("auth_not_ready",
                "Dosya yönetimi API'si için gerçek AUTH kimlik ve yetki servisleri henüz bağlı değil.", 503);

        if (!user.IsAuthenticated || user.UserId is not > 0)
            throw new AppException("not_authenticated", "Oturum açmanız gerekiyor.", 401);

        if (!await siteAdmins.IsSiteAdminAsync(user.UserId.Value, ct))
            throw new AppException("forbidden", "Bu işlem için yetkiniz yok.", 403);
    }
}

/// <summary>Yönetici dosya ayarları ve bakım ucu (WBS-2 §3.6.4; FILE-FR-12; FILE-BR-04, 05, 08, 10).</summary>
public static class FileAdminEndpoints
{
    public static IServiceCollection AddFileAdminApi(this IServiceCollection services) =>
        services.AddScoped<FileAdminAuthorization>();

    public static IEndpointRouteBuilder MapFileAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/v1/admin").WithTags("FILE");

        admin.MapGet("/file-settings", async (FileAdminAuthorization auth, IFileSettingsService settings, CancellationToken ct) =>
        {
            await auth.RequireSiteAdminAsync(ct);
            return Results.Ok(await settings.GetAsync(ct));
        }).Produces<FileSettings>(200).Errors(401, 403, 503);

        admin.MapPut("/file-settings", async (FileSettingsRequest request, FileAdminAuthorization auth,
            IFileSettingsService settings, CancellationToken ct) =>
        {
            await auth.RequireSiteAdminAsync(ct);
            return Results.Ok(await settings.UpdateAsync(request.ToSettings(), ct));
        }).Produces<FileSettings>(200).Errors(400, 401, 403, 422, 503);

        // Temizlik görevini hemen çalıştırır (test ve bakım için). Başka bir örnek temizliği yürütüyorsa 409.
        admin.MapPost("/file-maintenance/run", async (FileAdminAuthorization auth, IFileMaintenance maintenance,
            CancellationToken ct) =>
        {
            await auth.RequireSiteAdminAsync(ct);
            var result = await maintenance.RunAsync(ct);
            if (result.Skipped)
                throw AppException.Conflict("maintenance_running", "Dosya temizliği şu anda başka bir işlemde yürütülüyor.");
            return Results.Json(
                new FileMaintenanceResponse(result.ExpiredDrafts, result.PurgedEntries, result.PurgedBlobs, result.Errors),
                statusCode: StatusCodes.Status202Accepted);
        }).Produces<FileMaintenanceResponse>(202).Errors(401, 403, 409, 503);

        return endpoints;
    }

    private static RouteHandlerBuilder Errors(this RouteHandlerBuilder endpoint, params int[] statuses)
    {
        foreach (var status in statuses) endpoint.Produces<ApiErrorResponse>(status);
        return endpoint;
    }
}
