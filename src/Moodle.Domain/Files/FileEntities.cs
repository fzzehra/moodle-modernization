namespace Moodle.Domain.Files;

/// <summary>Fiziksel içerik. Aynı içerik bir kez saklanır; anahtar SHA-256 özetidir (FILE-BR-01, 03).</summary>
public sealed class FileBlob
{
    /// <summary>SHA-256, 64 küçük harf onaltılık karakter.</summary>
    public string ContentHash { get; set; } = "";
    public long SizeBytes { get; set; }
    /// <summary>Depodaki yol: <c>ab/cd/&lt;özet&gt;</c>.</summary>
    public string StorageKey { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>Hiçbir kaydın kullanmadığı an; kalıcı silme bu andan bekleme süresi sonra yapılır. Kullanımdaysa <c>null</c>.</summary>
    public DateTimeOffset? OrphanedAt { get; set; }
}

/// <summary>Bir dosya (veya klasör) kaydı. Klasörler <c>Name = "."</c> kaydıdır (Moodle ile aynı).</summary>
public sealed class FileEntry
{
    public long Id { get; set; }
    public string ContentHash { get; set; } = "";
    public int ContextId { get; set; }
    public string Component { get; set; } = "";
    public string Area { get; set; } = "";
    public long ItemId { get; set; }
    public string Path { get; set; } = "/";
    public string Name { get; set; } = "";
    public int? OwnerUserId { get; set; }
    public long SizeBytes { get; set; }
    public string MimeType { get; set; } = "application/octet-stream";
    public string Status { get; set; } = "active";
    public string? Source { get; set; }
    public string? Author { get; set; }
    public string? License { get; set; }
    public int SortOrder { get; set; }
    /// <summary>Dış depo referansı için yer tutucu; ilk sürümde kullanılmaz.</summary>
    public long? ReferenceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    /// <summary>Dolu ise kayıt çöptedir.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    public bool IsDirectory => Name == ".";
}

/// <summary>Taslak alanı. Süre <b>son etkinliğe</b> göre dolar (FILE-BUG-02 düzeltmesi).</summary>
public sealed class FileDraft
{
    public long DraftId { get; set; }
    public int OwnerUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }
}

/// <summary>Tek satırlık yönetici ayarı.</summary>
public sealed class FileSetting
{
    public int Id { get; set; } = 1;
    public long MaxFileBytes { get; set; } = 50L * 1024 * 1024;
    /// <summary>0 = sınırsız.</summary>
    public long UserQuotaBytes { get; set; } = 100L * 1024 * 1024;
    public int DraftTtlHours { get; set; } = 96;
    public int TrashRetentionHours { get; set; } = 24;
}
