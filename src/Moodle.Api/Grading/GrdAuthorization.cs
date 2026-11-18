using Moodle.Application.Abstractions;
using Moodle.Application.Errors;
using Moodle.Infrastructure.Fakes;

namespace Moodle.Api.Grading;

/// <summary>GRD yetki girişi (CrsAuthorization deseni). Kimlik her zaman oturumdan okunur.</summary>
public sealed class GrdAuthorization(ICurrentUser user, IAccessPolicy policy)
{
    /// <summary>Ödev puanını okuma ve yazma yeteneği (GRD-FR-01), ödevin ders bağlamında sorulur.</summary>
    public const string GradeEdit = "grade.edit";

    /// <summary>Kişisel not raporunu görme yeteneği (GRD-FR-02), ders bağlamında sorulur.</summary>
    public const string GradeView = "grade.view";

    /// <summary>Dersin not defterini görme yeteneği (GRD-FR-03), ders bağlamında sorulur.</summary>
    public const string GradeViewAll = "grade.viewall";

    public void RequireIdentity()
    {
        // Depodaki AUTH sahteleri yönetici gibi davranır; GRD bu sahtelerle hiçbir ortamda açılmaz.
        // UnavailableIdentity, AUTH bağlanmadığında kullanılan kapalı varsayılandır; o da "AUTH hazır değil" sayılır.
        if (user is FakeCurrentUser or UnavailableIdentity || policy is FakeAccessPolicy or UnavailableIdentity)
            throw new AppException("auth_not_ready",
                "GRD API için gerçek AUTH kimlik ve yetki servisleri henüz bağlı değil.", 503);

        if (!user.IsAuthenticated || user.UserId is not > 0)
            throw new AppException("not_authenticated", "Oturum açmanız gerekiyor.", 401);
    }

    public Task<bool> CanAsync(string capability, ContextRef context, CancellationToken ct)
    {
        RequireIdentity();
        return policy.HasCapabilityAsync(user.UserId!.Value, capability, context, ct);
    }

    public async Task RequireAsync(string capability, ContextRef context, CancellationToken ct)
    {
        if (!await CanAsync(capability, context, ct))
            throw new AppException("forbidden", "Bu işlem için yetkiniz yok.", 403);
    }
}
