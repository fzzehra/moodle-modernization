using Moodle.Domain.Auth;

namespace Moodle.Infrastructure.Auth.Comparisons;

/// <summary>
/// AUTH-FR-04, 05; BR-02, 03 için <b>ikinci, bağımsız</b> bir uygulama: hesap durumu kontrolü ve
/// başarısız giriş kaydı. WBS-2 §4.4'teki YZ/elle karşılaştırması (K17) için yazılmıştır.
/// <b>Üretimde kullanılmaz</b> — gerçek, çalışan mantık <see cref="AuthService.LoginAsync"/> içindedir;
/// bu sınıf yalnızca karşılaştırma amaçlıdır.
///
/// <para>
/// <b>Yazarlık notu (önemli):</b> Bu dosya da Claude (YZ) tarafından, kullanıcının açık isteği üzerine
/// yazılmıştır. "Elle" (insan tarafından YZ yardımı almadan) yazılmamıştır. WBS-2 §4.4'teki "adil
/// karşılaştırma" ilkesi bağımsız bir insan geliştiricisi gerektirdiğinden, bu dosya karşılaştırma
/// tablosunda "Elle" olarak değil, "YZ (ikinci/karşılaştırma uygulaması)" olarak raporlanmalıdır.
/// Efor (adam-saat) tahmini bu PR'da hesaplanmamıştır; kullanıcı kendi COCOMO II hesaplamasını ayrıca
/// yapacaktır.
/// </para>
/// <para>
/// Kasıtlı olarak <see cref="AuthService"/>'ten farklı, daha ayrıntılı/"elle" bir üslupla yazılmıştır:
/// desen eşleştirme veya üç terimli ifadeler yerine açık <c>if</c>/<c>else</c> zincirleri, küçük ve
/// tek işli yardımcı yöntemler, her dalın ne yaptığını anlatan yorumlar.
/// </para>
/// </summary>
public static class ManualAccountStatusCheck
{
    /// <summary>Girişin hangi aşamada ve neden durduğunu (veya devam ettiğini) belirtir.</summary>
    public enum Decision
    {
        Continue,
        RejectGeneric,
        RejectLockedOut,
    }

    /// <summary>
    /// AUTH-BR-02: kullanıcı yoksa, silinmişse veya askıya alınmışsa aynı genel ret kararı verilir.
    /// Hangisinin doğru olduğu bu adımda belli edilmez (AUTH-NFR-01/02).
    /// </summary>
    public static Decision EvaluateAccountStatus(bool userExists, bool isDeleted, bool isSuspended)
    {
        if (userExists == false)
        {
            // Kullanıcı adı veritabanında yok. Parola kontrolüne hiç geçilmeden reddedilir.
            return Decision.RejectGeneric;
        }

        if (isDeleted == true)
        {
            // Hesap yumuşak silinmiş (Moodle'daki user.deleted karşılığı).
            return Decision.RejectGeneric;
        }

        if (isSuspended == true)
        {
            // Hesap askıya alınmış (Moodle'daki user.suspended karşılığı).
            return Decision.RejectGeneric;
        }

        // Hesap durumu sorunsuz; parola kontrolüne geçilebilir.
        return Decision.Continue;
    }

    /// <summary>AUTH-BR-03: kilitlenme durumu, parola doğru olsa bile girişi engeller.</summary>
    public static Decision EvaluateLockout(bool isLockedOut)
    {
        if (isLockedOut == true)
        {
            return Decision.RejectLockedOut;
        }

        return Decision.Continue;
    }

    /// <summary>
    /// AUTH-FR-05: başarısız giriş denemesini (veya başarılı girişi) güvenlik günlüğüne kaydeder.
    /// Parola veya çerez gibi hassas bilgi taşınmaz.
    /// </summary>
    public static SecurityEvent BuildAuditEntry(
        int? userId, string attemptedUsername, Decision decision, bool succeeded, DateTimeOffset occurredAt)
    {
        SecurityEventType eventType;

        if (succeeded == true)
        {
            eventType = SecurityEventType.LoginSucceeded;
        }
        else if (decision == Decision.RejectLockedOut)
        {
            eventType = SecurityEventType.LoginRejectedLockedOut;
        }
        else
        {
            // decision == Decision.RejectGeneric (ya da parola yanlış) -> genel başarısız giriş.
            eventType = SecurityEventType.LoginFailed;
        }

        var entry = new SecurityEvent();
        entry.UserId = userId;
        entry.AttemptedUsername = attemptedUsername;
        entry.EventType = eventType;
        entry.OccurredAt = occurredAt;
        return entry;
    }
}
