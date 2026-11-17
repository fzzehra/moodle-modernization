namespace Moodle.Application.Abstractions;

/// <summary>
/// Oturumdaki kullanıcı. Kimlik her zaman oturumdan okunur, istekten alınmaz.
/// Gerçek gerçekleme AUTH modülündedir (İrem).
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Oturum yoksa <c>null</c>.</summary>
    int? UserId { get; }

    string? Username { get; }
}
