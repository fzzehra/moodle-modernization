using Moodle.Application.Errors;
namespace Moodle.Application.Files;

/// <summary>User-approved configurable defaults; not numeric constants asserted by WBS.</summary>
public sealed class UploadLimits
{
    public long MaxFileBytes { get; set; } = 50L * 1024 * 1024;
    public long MaxAreaBytes { get; set; } = 50L * 1024 * 1024;
    public int MaxFiles { get; set; } = 10;
    public string[] Extensions { get; set; } = [".pdf", ".png", ".jpg", ".jpeg", ".txt"];
}
public static class UploadValidation
{
    public static void Name(string name, string path)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name is "." or ".."
            || name.EndsWith('.') || name.EndsWith(' ') || name.Any(c => char.IsControl(c) || "/\\:*?<>|".Contains(c))
            || path != "/")
            throw new AppException("invalid_name", "Dosya adı geçersiz; en fazla 255 karakter ve düz dosya adı kullanın.", 400);
    }
    public static string UniqueName(string name, ISet<string> existing)
    {
        if (!existing.Contains(name)) return name;
        var extension = Path.GetExtension(name);
        var stem = name[..^extension.Length];
        for (var i=1;;i++) {
            var suffix = $" ({i})";
            var candidate = stem[..Math.Min(stem.Length, 255-extension.Length-suffix.Length)] + suffix + extension;
            if (!existing.Contains(candidate)) return candidate;
        }
    }
    public static string Type(string name, ReadOnlySpan<byte> prefix, UploadLimits limits)
    {
        var ext = Path.GetExtension(name).ToLowerInvariant();
        if (!limits.Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) throw TypeError();
        return ext switch {
            ".pdf" when prefix.StartsWith("%PDF-"u8) => "application/pdf",
            ".png" when prefix.StartsWith(new byte[]{137,80,78,71,13,10,26,10}) => "image/png",
            ".jpg" or ".jpeg" when prefix.StartsWith(new byte[]{255,216,255}) => "image/jpeg",
            ".txt" when !prefix.Contains((byte)0) && !prefix.StartsWith("MZ"u8) => "text/plain",
            _ => throw TypeError()
        };
    }
    public static AppException TypeError() => new("type_not_allowed", "Dosya uzantısı veya içeriği izin verilen türlerle eşleşmiyor.", 415);
}
