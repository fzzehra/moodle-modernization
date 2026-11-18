namespace Moodle.Application.Files;

/// <summary>Bir dosya alanı: hangi bağlam, bileşen, alan ve öğe (Moodle'daki file area).</summary>
public readonly record struct FileArea(int ContextId, string Component, string Area, long ItemId);

public sealed record FileMeta(
    long FileId,
    string Name,
    string Path,
    long SizeBytes,
    string MimeType,
    string ContentHash,
    int? OwnerUserId,
    DateTimeOffset CreatedAt);

public sealed record DraftInfo(long DraftId, DateTimeOffset ExpiresAt);

/// <summary>Açılmış dosya içeriği. Çağıran akışı kapatmakla yükümlüdür.</summary>
public sealed record FileContent(FileMeta Meta, Stream Content, bool ForceDownload);

/// <summary>İçerik deposuna yazma sonucu.</summary>
public sealed record StoredBlob(string ContentHash, long SizeBytes, string StorageKey, bool AlreadyExisted);
