namespace Moodle.Domain.Auth;

/// <summary>AUTH-BR-04, BR-05: giriş ve oturum olaylarının güvenli biçimde izlenmesi (WBS-2 §3.1.2, <c>SecurityAuditLogs</c>).</summary>
public enum SecurityEventType
{
    LoginSucceeded,
    LoginFailed,
    LoginRejectedSuspended,
    LoginRejectedLockedOut,
    LoggedOut,
    PasswordChanged,
}

/// <summary>
/// Güvenlik olayı kaydı. Parola veya oturum çerezi gibi hassas bilgi taşımaz (WBS-2 §3.1.2 kural 8).
/// <see cref="UserId"/>, bilinmeyen kullanıcı adıyla yapılan başarısız denemelerde <c>null</c>'dur.
/// </summary>
public sealed class SecurityEvent
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string AttemptedUsername { get; set; } = "";
    public SecurityEventType EventType { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
