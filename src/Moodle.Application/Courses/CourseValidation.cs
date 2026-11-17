
namespace Moodle.Application.Courses;

public static class CourseValidation
{
    public static void Validate(
        string fullName,
        string shortName,
        string? idNumber,
        DateTimeOffset? start,
        DateTimeOffset? end)
    {
        if (string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(shortName))
            throw new ArgumentException(
                "Ders adı ve kısa adı zorunludur.");

        if (new[] { fullName, shortName, idNumber ?? "" }
            .Any(s => s.Any(char.IsControl)))
            throw new ArgumentException(
                "Ders alanlarında TAB veya kontrol karakteri bulunamaz.");

        if (fullName.Length > 255 || shortName.Length > 100 || idNumber?.Length > 100)
            throw new ArgumentException("Ders alanları izin verilen uzunluğu aşıyor.");

        if (start.HasValue && end.HasValue &&
            end.Value < start.Value)
            throw new ArgumentException(
                "Bitiş tarihi başlangıç tarihinden önce olamaz.");
    }

    public static void ValidateRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role) || role.Length > 50 || role.Any(char.IsControl))
            throw new ArgumentException("Geçersiz rol.");
    }
}
