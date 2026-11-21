using Moodle.Application.Errors;

namespace Moodle.Application.Files;

/// <summary>Yönetici dosya ayarları (WBS-2 §3.6.2 <c>file_setting</c>, §3.6.4 <c>/admin/file-settings</c>).</summary>
/// <param name="MaxFileBytes">Dosya başına en büyük boyut (FILE-BR-08).</param>
/// <param name="UserQuotaBytes">Kullanıcı kotası; 0 = sınırsız (FILE-BR-10).</param>
/// <param name="DraftTtlHours">Taslak süresi, son etkinlikten itibaren (FILE-BR-05, FILE-BUG-02).</param>
/// <param name="TrashRetentionHours">Çöp bekleme süresi, çöpe atılma anından itibaren (FILE-BR-04, FILE-BUG-03).</param>
public sealed record FileSettings(long MaxFileBytes, long UserQuotaBytes, int DraftTtlHours, int TrashRetentionHours)
{
    public const int MaxHours = 8760; // bir yıl

    /// <summary>Geçersiz ayarda 422 <c>validation_failed</c>; ayar değişmez.</summary>
    public void Validate()
    {
        if (MaxFileBytes <= 0)
            throw AppException.Validation("validation_failed", "maxFileBytes sıfırdan büyük olmalıdır.");
        if (UserQuotaBytes < 0)
            throw AppException.Validation("validation_failed", "userQuotaBytes negatif olamaz (0 = sınırsız).");
        if (DraftTtlHours is < 1 or > MaxHours)
            throw AppException.Validation("validation_failed", $"draftTtlHours 1 ile {MaxHours} arasında olmalıdır.");
        if (TrashRetentionHours is < 0 or > MaxHours)
            throw AppException.Validation("validation_failed", $"trashRetentionHours 0 ile {MaxHours} arasında olmalıdır.");
    }
}

public interface IFileSettingsService
{
    Task<FileSettings> GetAsync(CancellationToken ct = default);

    /// <summary>Ayarları doğrulayıp kaydeder ve kaydedilmiş hâli döner.</summary>
    Task<FileSettings> UpdateAsync(FileSettings settings, CancellationToken ct = default);
}

/// <summary>
/// Kullanıcı site yöneticisi mi (<c>SiteAdmin</c> sistem rolü)? <c>IAccessPolicy</c> yönetici rollerini
/// (<c>SiteAdmin</c> ve <c>Manager</c>) ayırt etmediği için dosya ayarları bu sorguyla korunur
/// (Moodle'da dosya ayarları yalnızca site yöneticisine açıktır).
/// </summary>
public interface ISiteAdminQuery
{
    Task<bool> IsSiteAdminAsync(int userId, CancellationToken ct = default);
}
