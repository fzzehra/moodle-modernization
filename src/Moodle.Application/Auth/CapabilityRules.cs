using Moodle.Application.Abstractions;

namespace Moodle.Application.Auth;

/// <summary>
/// Rol, bağlam ve yetenek modeli (AUTH-FR-02, 03, 07; WBS-1 §4.1, WBS-2 §3.1.4). Saf mantık: veritabanı,
/// HTTP veya EF Core'a bağımlı değildir, bu yüzden <c>Moodle.UnitTests</c>'te doğrudan test edilir.
/// Gerçek <c>IAccessPolicy</c> gerçeklemesi (<c>Moodle.Infrastructure.Auth.AuthAccessPolicy</c>) yalnızca
/// kullanıcının sistem rollerini ve ders bağlamındaki rolünü toplar, kararı burada verdirir.
/// </summary>
public static class CapabilityRules
{
    public const string SiteAdmin = "SiteAdmin";
    public const string Manager = "Manager";
    public const string EditingTeacher = "EditingTeacher";
    public const string Teacher = "Teacher";
    public const string Student = "Student";

    /// <summary>
    /// Bilinen tüm yetenek adları (WBS-2 §3.1.3 <c>GET /auth/permissions</c> için). Tek kaynak burasıdır;
    /// diğer modüller (CRS, GRD) kendi uçlarında bu listedeki adları kullanır.
    /// </summary>
    public static IReadOnlyList<string> KnownCapabilities { get; } =
    [
        "category.view", "category.manage",
        "course.view", "course.viewhidden", "course.create", "course.update", "course.delete",
        "enrolment.view", "enrolment.create", "enrolment.update", "enrolment.delete",
        "course.role.assign",
        "course.role.assign:" + EditingTeacher, "course.role.assign:" + Teacher, "course.role.assign:" + Student,
        "grade.edit", "grade.view", "grade.viewall",
        "mod/assign:view", "mod/assign:submit", "mod/assign:grade", "mod/assign:addinstance", "moodle/course:manageactivities",
    ];

    /// <param name="capability">Yetenek adı (ör. <c>course.update</c>, <c>course.role.assign:Teacher</c>).</param>
    /// <param name="systemRoles">Kullanıcının sistem geneli rolleri (ASP.NET Identity rolleri).</param>
    /// <param name="courseRole">
    /// Bağlam bir ders veya derse bağlı bir kaynaksa kullanıcının o dersteki etkin rolü
    /// (<see cref="EditingTeacher"/>/<see cref="Teacher"/>/<see cref="Student"/>); kayıtlı değilse <c>null</c>.
    /// Bağlam site veya kategori düzeyindeyse çağıran <c>null</c> geçer.
    /// </param>
    public static bool Evaluate(string capability, IReadOnlySet<string> systemRoles, string? courseRole)
    {
        var isSiteManager = systemRoles.Contains(SiteAdmin) || systemRoles.Contains(Manager);
        var isEditingTeacher = courseRole == EditingTeacher;
        var isAnyTeacher = courseRole is EditingTeacher or Teacher;
        var isEnrolled = courseRole is not null;

        return capability switch
        {
            "mod/assign:view" => isSiteManager || isEnrolled,
            "mod/assign:submit" => courseRole == Student,
            "mod/assign:grade" => isSiteManager || isAnyTeacher,
            "mod/assign:addinstance" or "moodle/course:manageactivities" => isSiteManager || isEditingTeacher,
            // Herkese açık: kategori listesi ve görünür ders listesi gizli değildir (WBS-1 §4.1 ile uyumlu;
            // Moodle'da da misafir/öğrenci kategori ve görünür dersleri görebilir).
            "category.view" => true,
            "course.view" => true,

            // Site/kategori düzeyi yönetim: yalnızca yönetici (site yöneticisi veya manager).
            "category.manage" => isSiteManager,
            "course.create" => isSiteManager,
            "course.delete" => isSiteManager,

            // Gizli ders ve katılımcı listesi: yönetici veya dersin herhangi bir öğretmeni (AUTH-FR-03).
            "course.viewhidden" => isSiteManager || isAnyTeacher,
            "enrolment.view" => isSiteManager || isAnyTeacher,

            // Ders ayarları ve kayıt yönetimi: yönetici veya dersin düzenleme yetkili öğretmeni.
            // Moodle'da enrol/manual:enrol ve moodle/course:update yalnızca manager+editingteacher'a
            // açıktır (düzenleme yetkisiz "teacher" archetype'ı hariç) — bkz. WBS-1 §4.1.4, §4.4.7.
            "course.update" => isSiteManager || isEditingTeacher,
            "course.managefiles" => isSiteManager || isEditingTeacher,
            "enrolment.create" => isSiteManager || isEditingTeacher,
            "enrolment.update" => isSiteManager || isEditingTeacher,
            "enrolment.delete" => isSiteManager || isEditingTeacher,
            "course.role.assign" => isSiteManager || isEditingTeacher,

            // Öğrenci rolü atamak öğretmenin günlük işidir; öğretmenlik rolü vermek yöneticiye özeldir.
            "course.role.assign:" + Student => isSiteManager || isEditingTeacher,
            "course.role.assign:" + Teacher => isSiteManager,
            "course.role.assign:" + EditingTeacher => isSiteManager,

            // Notlandırma: yönetici veya dersin herhangi bir öğretmeni (editing veya değil) girebilir/görebilir
            // (GRD-FR-01, FR-03; Moodle'da mod/assign:grade her iki öğretmen archetype'ına da açıktır).
            "grade.edit" => isSiteManager || isAnyTeacher,
            "grade.viewall" => isSiteManager || isAnyTeacher,

            // Kişisel not raporu: dersin kayıtlı herhangi bir katılımcısı kendi notunu görebilir (GRD-FR-02).
            "grade.view" => isSiteManager || isEnrolled,

            // Bilinmeyen yetenek: güvenli taraf, yalnızca yönetici (AUTH-NFR-02: yetkisiz erişim reddedilir).
            _ => isSiteManager,
        };
    }
}
