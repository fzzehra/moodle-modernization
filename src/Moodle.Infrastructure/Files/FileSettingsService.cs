using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moodle.Application.Auth;
using Moodle.Application.Files;
using Moodle.Infrastructure.Auth;

namespace Moodle.Infrastructure.Files;

/// <summary>Tek satırlık <c>file_setting</c> tablosunu okur ve yazar (WBS-2 §3.6.2).</summary>
public sealed class FileSettingsService(FilesDbContext db) : IFileSettingsService
{
    public async Task<FileSettings> GetAsync(CancellationToken ct = default) =>
        ToDto(await db.Settings.AsNoTracking().SingleAsync(ct));

    public async Task<FileSettings> UpdateAsync(FileSettings settings, CancellationToken ct = default)
    {
        settings.Validate(); // geçersizse hiçbir şey yazılmadan 422
        var row = await db.Settings.SingleAsync(ct);
        row.MaxFileBytes = settings.MaxFileBytes;
        row.UserQuotaBytes = settings.UserQuotaBytes;
        row.DraftTtlHours = settings.DraftTtlHours;
        row.TrashRetentionHours = settings.TrashRetentionHours;
        await db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    private static FileSettings ToDto(Domain.Files.FileSetting s) =>
        new(s.MaxFileBytes, s.UserQuotaBytes, s.DraftTtlHours, s.TrashRetentionHours);
}

/// <summary>
/// <see cref="ISiteAdminQuery"/>'nin ASP.NET Identity gerçeklemesi. AUTH dosyalarını değiştirmeden, AUTH'un kullandığı
/// aynı <see cref="UserManager{TUser}"/> ve rol adını (<see cref="CapabilityRules.SiteAdmin"/>) okur.
/// Askıya alınmış veya silinmiş kullanıcı yönetici sayılmaz (AUTH-BR-02 ile aynı).
/// </summary>
public sealed class IdentitySiteAdminQuery(UserManager<AppUser> users) : ISiteAdminQuery
{
    public async Task<bool> IsSiteAdminAsync(int userId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || user.Suspended || user.Deleted) return false;
        return await users.IsInRoleAsync(user, CapabilityRules.SiteAdmin);
    }
}
