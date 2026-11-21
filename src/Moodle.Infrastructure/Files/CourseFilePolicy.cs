using Moodle.Application.Abstractions;
using Moodle.Application.Auth;
using Moodle.Application.Files;

namespace Moodle.Infrastructure.Files;

/// <summary>
/// Ders dosyaları politikası (FILE-FR-13; WBS-1 §4.6.7, WBS-2 §3.6.3). Alan: <c>FileArea(courseId, "course", "files", 0)</c>.
/// <list type="bullet">
/// <item>Okuma: ders varsa ve kullanıcı yöneticiyse ya da derse etkin kayıtlıysa (düzenleyen öğretmen, öğretmen, öğrenci).
/// Ders gizliyse öğrenci okuyamaz; öğretmenler ve yönetici okumaya devam eder (Moodle davranışı).</item>
/// <item>Yazma (kaydetme, silme): yönetici veya düzenleyen öğretmen. Düzenleme yetkisiz öğretmen ve öğrenci yazamaz
/// (Moodle'da <c>moodle/course:managefiles</c> yalnızca editingteacher ve manager'dadır).</item>
/// </list>
/// Yönetici <see cref="IAccessPolicy"/> ile <see cref="ManageFiles"/> yeteneği sorularak bulunur; bu yetenek
/// <c>CapabilityRules</c> yöneticiyi ve düzenleyen öğretmeni açıkça destekler. Rol ayrıca
/// <see cref="IEnrolmentQuery"/> ile denetlenir; böylece politika AUTH'taki değişikliğe bağımlı değildir.
/// Reddedilen erişim dosya servisinde 404 olur (FILE-BR-11).
/// </summary>
public sealed class CourseFilePolicy(ICourseDirectory courses, IEnrolmentQuery enrolments, IAccessPolicy access) : IFilePolicy
{
    public const string ComponentName = "course";
    public const string AreaName = "files";
    public const string ManageFiles = "course.managefiles";

    public static FileArea AreaFor(int courseId) => new(courseId, ComponentName, AreaName, 0);

    public string Component => ComponentName;

    public async Task<bool> CanReadAsync(int userId, FileArea area, CancellationToken ct = default)
    {
        if (await FindCourseAsync(area, ct) is not { } course) return false;
        if (await IsManagerAsync(userId, area.ContextId, ct)) return true;
        return await enrolments.FindActiveRoleAsync(userId, area.ContextId, ct) switch
        {
            CapabilityRules.EditingTeacher or CapabilityRules.Teacher => true,
            // Gizli dersi öğrenci göremez (Moodle: moodle/course:viewhiddencourses yalnızca öğretmen ve yöneticide).
            CapabilityRules.Student => course.Visible,
            _ => false,
        };
    }

    public async Task<bool> CanWriteAsync(int userId, FileArea area, CancellationToken ct = default)
    {
        if (await FindCourseAsync(area, ct) is null) return false;
        if (await IsManagerAsync(userId, area.ContextId, ct)) return true;
        return await enrolments.FindActiveRoleAsync(userId, area.ContextId, ct) == CapabilityRules.EditingTeacher;
    }

    private async Task<CourseInfo?> FindCourseAsync(FileArea area, CancellationToken ct) =>
        area is { Component: ComponentName, Area: AreaName, ItemId: 0, ContextId: > 0 }
            ? await courses.FindAsync(area.ContextId, ct)
            : null;

    private Task<bool> IsManagerAsync(int userId, int courseId, CancellationToken ct) =>
        access.HasCapabilityAsync(userId, ManageFiles, ContextRef.ForCourse(courseId), ct);
}
