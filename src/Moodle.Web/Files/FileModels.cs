using System.Globalization;

namespace Moodle.Web.Files;

// FILE HTTP sözleşmeleri (WBS-2 §3.6.4). Web projesi Domain'e veya Application'a bağlanmaz;
// yalnızca API'nin JSON biçimini bilir.

/// <summary>Bir dosya alanı: bağlam, bileşen, alan ve öğe (sunucudaki <c>FileArea</c> karşılığı).</summary>
public sealed record FileAreaRef(int ContextId, string Component, string Area, long ItemId)
{
    /// <summary>Ders dosyaları (FILE-FR-13): <c>(courseId, "course", "files", 0)</c>.</summary>
    public static FileAreaRef CourseFiles(int courseId) => new(courseId, "course", "files", 0);

    internal string Path =>
        $"file-areas/{ContextId}/{Uri.EscapeDataString(Component)}/{Uri.EscapeDataString(Area)}/{ItemId}";
}

/// <summary>Dosya meta verisi (<c>GET /files/{id}</c>, alan listeleri ve kaydetme yanıtı).</summary>
public sealed record FileItem(
    long FileId, string Name, string Path, long SizeBytes, string MimeType, string ContentHash,
    int? OwnerUserId, DateTimeOffset CreatedAt);

public sealed record FileListResponse(List<FileItem> Files);

/// <summary><c>POST /file-drafts</c> yanıtı.</summary>
public sealed record FileDraftInfo(long DraftId, DateTimeOffset ExpiresAt);

/// <summary><c>POST /file-drafts/{id}/files</c> yanıtı. <c>Name</c> sunucunun kaydettiği gerçek addır (FILE-FR-07).</summary>
public sealed record FileUploadResult(long FileId, string Name, long Size, string MimeType, string ContentHash);

public sealed record FileQuota(long LimitBytes, long UsedBytes, bool Unlimited);

/// <summary>Yönetici dosya ayarları (<c>/admin/file-settings</c>).</summary>
public sealed record FileSettingsDto(long MaxFileBytes, long UserQuotaBytes, int DraftTtlHours, int TrashRetentionHours);

/// <summary><c>POST /admin/file-maintenance/run</c> yanıtı.</summary>
public sealed record FileMaintenanceResult(int ExpiredDrafts, int PurgedEntries, int PurgedBlobs, List<string> Errors);

/// <summary>Ayar formu. Sınırlar yalnızca sunucuda denetlenir; geçersiz değer 422 ile forma döner.</summary>
public sealed class FileSettingsModel
{
    public long MaxFileBytes { get; set; }
    public long UserQuotaBytes { get; set; }
    public int DraftTtlHours { get; set; }
    public int TrashRetentionHours { get; set; }

    public static FileSettingsModel From(FileSettingsDto dto) => new()
    {
        MaxFileBytes = dto.MaxFileBytes,
        UserQuotaBytes = dto.UserQuotaBytes,
        DraftTtlHours = dto.DraftTtlHours,
        TrashRetentionHours = dto.TrashRetentionHours,
    };

    public FileSettingsDto ToDto() => new(MaxFileBytes, UserQuotaBytes, DraftTtlHours, TrashRetentionHours);
}

public static class FileDisplay
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>İkili önekle okunabilir boyut: 512 B, 1,5 KB, 2 MB.</summary>
    public static string Size(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} B" : $"{value.ToString("0.#", Turkish)} {units[unit]}";
    }

    public static string Date(DateTimeOffset value) => value.ToUniversalTime().ToString("dd.MM.yyyy HH:mm 'UTC'", Turkish);
}
