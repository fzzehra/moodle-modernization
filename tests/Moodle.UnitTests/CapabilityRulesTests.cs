using Moodle.Application.Auth;

namespace Moodle.UnitTests;

/// <summary>
/// AUTH-FR-02, 03, 07 (WBS-1 §4.1.9, WBS-2 §3.1.4): rol + bağlam değerlendirmesi. Saf mantık,
/// veritabanı veya HTTP olmadan test edilir — gerçek <c>AuthAccessPolicy</c> yalnızca bu kuralların
/// girdilerini (sistem rolleri, ders rolü) toplar.
/// </summary>
public class CapabilityRulesTests
{
    private static readonly HashSet<string> NoSystemRoles = [];
    private static readonly HashSet<string> ManagerRole = [CapabilityRules.Manager];
    private static readonly HashSet<string> SiteAdminRole = [CapabilityRules.SiteAdmin];

    // AUTH-FR-03: düzenleme yetkili öğretmen kendi dersinin ayarlarını düzenleyebilir; düzenleme
    // yetkisiz öğretmen (Moodle "teacher" archetype) ve öğrenci düzenleyemez.
    [Theory]
    [InlineData(CapabilityRules.EditingTeacher, true)]
    [InlineData(CapabilityRules.Teacher, false)]
    [InlineData(CapabilityRules.Student, false)]
    [InlineData(null, false)]
    public void Course_update_requires_editing_teacher_or_manager(string? courseRole, bool expected)
    {
        Assert.Equal(expected, CapabilityRules.Evaluate("course.update", NoSystemRoles, courseRole));
        Assert.True(CapabilityRules.Evaluate("course.update", ManagerRole, courseRole));
        Assert.True(CapabilityRules.Evaluate("course.update", SiteAdminRole, courseRole));
    }

    // AUTH-FR-02, NFR-02: yönetici dışı kimse site/kategori düzeyi işlem yapamaz.
    [Theory]
    [InlineData("category.manage")]
    [InlineData("course.create")]
    [InlineData("course.delete")]
    public void Site_level_capabilities_are_manager_only(string capability)
    {
        Assert.False(CapabilityRules.Evaluate(capability, NoSystemRoles, CapabilityRules.EditingTeacher));
        Assert.True(CapabilityRules.Evaluate(capability, ManagerRole, null));
        Assert.True(CapabilityRules.Evaluate(capability, SiteAdminRole, null));
    }

    // AUTH-FR-07: kişisel not raporu (grade.view) dersin herhangi bir kayıtlı katılımcısına açık;
    // kayıtlı olmayana kapalı.
    [Theory]
    [InlineData(CapabilityRules.Student, true)]
    [InlineData(CapabilityRules.Teacher, true)]
    [InlineData(CapabilityRules.EditingTeacher, true)]
    [InlineData(null, false)]
    public void Grade_view_requires_any_active_enrolment(string? courseRole, bool expected) =>
        Assert.Equal(expected, CapabilityRules.Evaluate("grade.view", NoSystemRoles, courseRole));

    // GRD-FR-01, FR-03: notlandırma ve not defteri, öğrenciye değil, dersin herhangi bir öğretmenine açık.
    [Theory]
    [InlineData("grade.edit")]
    [InlineData("grade.viewall")]
    public void Grading_capabilities_require_any_teacher_role(string capability)
    {
        Assert.True(CapabilityRules.Evaluate(capability, NoSystemRoles, CapabilityRules.EditingTeacher));
        Assert.True(CapabilityRules.Evaluate(capability, NoSystemRoles, CapabilityRules.Teacher));
        Assert.False(CapabilityRules.Evaluate(capability, NoSystemRoles, CapabilityRules.Student));
        Assert.False(CapabilityRules.Evaluate(capability, NoSystemRoles, null));
    }

    // Öğrenci rolü vermek öğretmenin günlük işidir; öğretmenlik rolü vermek yalnızca yöneticiye özeldir.
    [Fact]
    public void Only_manager_can_grant_teaching_roles()
    {
        Assert.True(CapabilityRules.Evaluate("course.role.assign:" + CapabilityRules.Student, NoSystemRoles, CapabilityRules.EditingTeacher));
        Assert.False(CapabilityRules.Evaluate("course.role.assign:" + CapabilityRules.Teacher, NoSystemRoles, CapabilityRules.EditingTeacher));
        Assert.False(CapabilityRules.Evaluate("course.role.assign:" + CapabilityRules.EditingTeacher, NoSystemRoles, CapabilityRules.EditingTeacher));
        Assert.True(CapabilityRules.Evaluate("course.role.assign:" + CapabilityRules.Teacher, ManagerRole, null));
    }

    // Herkese açık: kategori/ders görünürlüğü yetki gerektirmez (Moodle'da da misafir/öğrenci görebilir).
    [Theory]
    [InlineData("category.view")]
    [InlineData("course.view")]
    public void Public_capabilities_do_not_require_any_role(string capability) =>
        Assert.True(CapabilityRules.Evaluate(capability, NoSystemRoles, null));

    // AUTH-NFR-02: bilinmeyen/tanımsız yetenek güvenli tarafta kalır, yalnızca yönetici geçer.
    [Fact]
    public void Unknown_capability_defaults_to_manager_only()
    {
        Assert.False(CapabilityRules.Evaluate("made.up.capability", NoSystemRoles, CapabilityRules.EditingTeacher));
        Assert.True(CapabilityRules.Evaluate("made.up.capability", ManagerRole, null));
    }
}
