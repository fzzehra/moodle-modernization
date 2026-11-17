using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Moodle.Web.Crs;

// HTTP contracts owned by the UI; Web has no dependency on Domain or Infrastructure.
public sealed record CrsCategory(int Id, string Name, int? ParentId);
public sealed record CrsCourse(int Id, int CategoryId, string FullName, string ShortName,
    string? IdNumber, bool Visible, DateTimeOffset? StartDate, DateTimeOffset? EndDate);
public sealed record CrsEnrolment(int Id, int CourseId, int UserId, string Role,
    bool Suspended, DateTimeOffset? StartsAt, DateTimeOffset? EndsAt);
public sealed record CourseWriteRequest(int CategoryId, string FullName, string ShortName,
    string? IdNumber, bool Visible, DateTimeOffset? StartDate, DateTimeOffset? EndDate);
public sealed record EnrolmentWriteRequest(int UserId, string Role, bool Suspended,
    DateTimeOffset? StartsAt, DateTimeOffset? EndsAt);
public sealed record EnrolmentUpdateRequest(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Suspended,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Role);

public sealed class CourseEditModel : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "Bir kategori seçin.")]
    public int CategoryId { get; set; }
    [Required(ErrorMessage = "Ders adı zorunludur."), StringLength(255, ErrorMessage = "Ders adı en fazla 255 karakter olabilir.")]
    public string FullName { get; set; } = "";
    [Required(ErrorMessage = "Kısa ad zorunludur."), StringLength(100, ErrorMessage = "Kısa ad en fazla 100 karakter olabilir.")]
    public string ShortName { get; set; } = "";
    [StringLength(100, ErrorMessage = "Kimlik numarası en fazla 100 karakter olabilir.")]
    public string? IdNumber { get; set; }
    public bool Visible { get; set; } = true;
    public DateTime? StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }

    public static CourseEditModel From(CrsCourse course) => new()
    {
        CategoryId = course.CategoryId, FullName = course.FullName, ShortName = course.ShortName,
        IdNumber = course.IdNumber, Visible = course.Visible,
        StartDateUtc = course.StartDate?.UtcDateTime, EndDateUtc = course.EndDate?.UtcDateTime
    };

    public CourseWriteRequest ToRequest() => new(CategoryId, FullName, ShortName,
        string.IsNullOrWhiteSpace(IdNumber) ? null : IdNumber, Visible,
        CrsDisplay.Utc(StartDateUtc), CrsDisplay.Utc(EndDateUtc));

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (new[] { FullName, ShortName, IdNumber }.Any(x => x?.Any(char.IsControl) == true))
            yield return new("Ders alanlarında kontrol karakteri kullanılamaz.");
        if (StartDateUtc.HasValue && EndDateUtc < StartDateUtc)
            yield return new("Bitiş tarihi başlangıç tarihinden önce olamaz.", [nameof(EndDateUtc)]);
    }
}

public sealed class EnrolmentEditModel : IValidatableObject
{
    [Range(1, int.MaxValue, ErrorMessage = "Geçerli bir kullanıcı kimliği girin.")]
    public int UserId { get; set; }
    [Required(ErrorMessage = "Rol zorunludur."), StringLength(50, ErrorMessage = "Rol en fazla 50 karakter olabilir.")]
    public string Role { get; set; } = "Student";
    public bool Suspended { get; set; }
    public DateTime? StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }

    public static EnrolmentEditModel From(CrsEnrolment enrolment) => new()
    {
        UserId = enrolment.UserId, Role = enrolment.Role, Suspended = enrolment.Suspended,
        StartsAtUtc = enrolment.StartsAt?.UtcDateTime, EndsAtUtc = enrolment.EndsAt?.UtcDateTime
    };

    public EnrolmentWriteRequest ToRequest() => new(UserId, Role, Suspended,
        CrsDisplay.Utc(StartsAtUtc), CrsDisplay.Utc(EndsAtUtc));

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Role?.Any(char.IsControl) == true)
            yield return new("Rol adında kontrol karakteri kullanılamaz.", [nameof(Role)]);
        if (StartsAtUtc.HasValue && EndsAtUtc.HasValue && EndsAtUtc <= StartsAtUtc)
            yield return new("Kayıt bitişi başlangıçtan sonra olmalıdır.", [nameof(EndsAtUtc)]);
    }
}

public static class CrsDisplay
{
    public static string Date(DateTimeOffset? value) => value?.ToUniversalTime().ToString("dd.MM.yyyy HH:mm 'UTC'") ?? "Belirtilmedi";
    internal static DateTimeOffset? Utc(DateTime? value) => value.HasValue
        ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : null;
    public static string Status(CrsEnrolment enrolment) => enrolment.Suspended ? "Askıda"
        : enrolment.StartsAt > DateTimeOffset.UtcNow ? "Henüz başlamadı"
        : enrolment.EndsAt <= DateTimeOffset.UtcNow ? "Süresi doldu" : "Aktif";
}
