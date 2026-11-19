namespace Moodle.Application.Abstractions;

/// <summary>Ders kaydı sorguları. Gerçek gerçekleme CRS modülündedir (Sinem).</summary>
public interface IEnrolmentQuery
{
    /// <summary>Kullanıcının derse etkin (askıda olmayan, süresi geçmemiş) kaydı var mı?</summary>
    Task<bool> IsActivelyEnrolledAsync(int userId, int courseId, CancellationToken ct = default);

    /// <summary>Derse etkin kayıtlı kullanıcıların kimlikleri.</summary>
    Task<IReadOnlyList<int>> ListEnrolledUserIdsAsync(int courseId, CancellationToken ct = default);

    /// <summary>
    /// Kullanıcının dersteki etkin kaydındaki rolü (ör. <c>"EditingTeacher"</c>, <c>"Teacher"</c>, <c>"Student"</c>).
    /// Etkin kaydı yoksa <c>null</c>. AUTH'un gerçek <c>IAccessPolicy</c>'si (İrem), rol+bağlam değerlendirmesi
    /// için (AUTH-FR-03, 07) bu sorguyu kullanır; CRS kendi <c>CourseEnrolment.Role</c> alanını döndürür.
    /// </summary>
    Task<string?> FindActiveRoleAsync(int userId, int courseId, CancellationToken ct = default);
}
