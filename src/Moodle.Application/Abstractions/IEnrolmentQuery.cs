namespace Moodle.Application.Abstractions;

/// <summary>Ders kaydı sorguları. Gerçek gerçekleme CRS modülündedir (Sinem).</summary>
public interface IEnrolmentQuery
{
    /// <summary>Kullanıcının derse etkin (askıda olmayan, süresi geçmemiş) kaydı var mı?</summary>
    Task<bool> IsActivelyEnrolledAsync(int userId, int courseId, CancellationToken ct = default);

    /// <summary>Derse etkin kayıtlı kullanıcıların kimlikleri.</summary>
    Task<IReadOnlyList<int>> ListEnrolledUserIdsAsync(int courseId, CancellationToken ct = default);
}
