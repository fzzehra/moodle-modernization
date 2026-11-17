namespace Moodle.Application.Abstractions;

/// <summary>
/// Rol, bağlam ve yetenek modeli (Moodle'daki <c>has_capability</c> karşılığı).
/// Gerçek gerçekleme AUTH modülündedir (İrem, AUTH-FR-02, 03, 07).
/// </summary>
public interface IAccessPolicy
{
    /// <param name="capability">Yetenek adı, ör. <c>moodle/course:update</c>.</param>
    Task<bool> HasCapabilityAsync(int userId, string capability, ContextRef context, CancellationToken ct = default);
}
