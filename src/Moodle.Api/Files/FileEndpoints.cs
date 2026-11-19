using Moodle.Api.Errors;
using Moodle.Application.Abstractions;
using Moodle.Application.Errors;
using Moodle.Application.Files;

namespace Moodle.Api.Files;

public sealed record CommitRequest(long DraftId, bool Replace = true);
public sealed record QuotaResponse(long LimitBytes, long UsedBytes, bool Unlimited);

/// <summary>
/// FILE-FR-02, 03, 04, 05, 09 (WBS-2 §3.6.4). Taslak oluşturma/yükleme uçları (FR-01, 07, 08, 10, 11)
/// Aslı Ceren'in kapsamıdır ve ayrı bir PR'da eklenecektir; <see cref="IFileService"/> şimdiden
/// tüm arayüzü uyguladığından (temel bir baz olarak) bu uçlar o PR'ı beklemeden çalışır.
/// </summary>
public static class FileEndpoints
{
    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1").WithTags("FILE");

        // FILE-FR-02: taslaktaki dosyalar hedef alana taşınır.
        api.MapPost("/file-areas/{contextId:int}/{component}/{area}/{itemId:long}/commit",
            async (int contextId, string component, string area, long itemId, CommitRequest request,
                ICurrentUser user, IFileService files, CancellationToken ct) =>
            {
                var userId = RequireAuthenticated(user);
                var target = new FileArea(contextId, component, area, itemId);
                var result = await files.CommitAsync(userId, request.DraftId, target, request.Replace, ct);
                return Results.Ok(new { files = result });
            }).Errors(401, 404, 422);

        // Hedef alandaki (ör. ders dosyaları, ödev eki) dosya listesi; politika reddederse 404 (varlık sızdırılmaz).
        api.MapGet("/file-areas/{contextId:int}/{component}/{area}/{itemId:long}/files",
            async (int contextId, string component, string area, long itemId, string? path,
                ICurrentUser user, IFileService files, CancellationToken ct) =>
            {
                var userId = RequireAuthenticated(user);
                var result = await files.ListAsync(userId, new FileArea(contextId, component, area, itemId), path ?? "/", ct);
                return Results.Ok(new { files = result });
            }).Errors(401, 404);

        // FILE-FR-03: meta veri (ad, boyut, tür vb.), yetkisizse 404.
        api.MapGet("/files/{fileId:long}", async (long fileId, ICurrentUser user, IFileService files, CancellationToken ct) =>
        {
            var userId = RequireAuthenticated(user);
            return Results.Ok(await files.GetMetaAsync(userId, fileId, ct));
        }).Errors(401, 404);

        // FILE-FR-03, BR-11, 12: dosya içeriği akış hâlinde döner (NFR-01); taslak dosyalar her zaman indirmeye
        // zorlanır. Yetkisiz veya olmayan dosyada 404 (varlık sızdırılmaz).
        api.MapGet("/files/{fileId:long}/content", async (long fileId, bool? download,
            ICurrentUser user, IFileService files, CancellationToken ct) =>
        {
            var userId = RequireAuthenticated(user);
            var content = await files.OpenAsync(userId, fileId, download ?? false, ct);
            var forceDownload = content.ForceDownload;
            return Results.File(content.Content, content.Meta.MimeType,
                fileDownloadName: forceDownload ? content.Meta.Name : null, enableRangeProcessing: true);
        }).Errors(401, 404);

        // FILE-FR-05, BR-13, 14: silme (çöpe taşıma); yetkisizse 404.
        api.MapDelete("/files/{fileId:long}", async (long fileId, ICurrentUser user, IFileService files, CancellationToken ct) =>
        {
            var userId = RequireAuthenticated(user);
            await files.DeleteAsync(userId, fileId, ct);
            return Results.NoContent();
        }).Errors(401, 404);

        // FILE-FR-09, BR-10, BUG-01 düzeltmesi.
        api.MapGet("/users/me/quota", async (ICurrentUser user, IFileService files, CancellationToken ct) =>
        {
            var userId = RequireAuthenticated(user);
            var (used, limit) = await files.GetQuotaAsync(userId, ct);
            return Results.Ok(new QuotaResponse(limit, used, limit <= 0));
        }).Produces<QuotaResponse>(200).Errors(401);

        return endpoints;
    }

    private static int RequireAuthenticated(ICurrentUser user) =>
        user.IsAuthenticated && user.UserId is > 0
            ? user.UserId!.Value
            : throw new AppException("not_authenticated", "Oturum açmanız gerekiyor.", 401);

    private static RouteHandlerBuilder Errors(this RouteHandlerBuilder endpoint, params int[] statuses)
    {
        foreach (var status in statuses) endpoint.Produces<ApiErrorResponse>(status);
        return endpoint;
    }
}
