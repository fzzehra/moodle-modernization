using Moodle.Application.Abstractions;
using Moodle.Application.Errors;
using Moodle.Infrastructure.Auth;

namespace Moodle.Api.Auth;

/// <summary>
/// AUTH-FR-09 / AUTH-BR-06 (YZ destekli, Claude Code): zorunlu parola değişikliği bekleyen kullanıcı, değişikliği
/// tamamlayana kadar AUTH uçları dışındaki API işlemlerini kullanamaz (<c>403 password_change_required</c>).
/// Bazı uçlar (ör. kullanıcının kendi dosyaları) <see cref="IAccessPolicy"/> yerine yalnızca oturum kimliğine
/// baktığı için yetki politikasındaki ret tek başına yetmez; bu kontrol tüm <c>/api/v1</c> uçlarını kapsar.
/// Yalnızca gerçek AUTH oturumu (<see cref="AuthCurrentUser"/>) denetlenir; geliştirme sahteleri ve test
/// yerine geçenler kendi kurallarıyla ele alınır.
/// </summary>
public sealed class PasswordChangeRequiredMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUser user)
    {
        if (!IsGuarded(context.Request.Path) || user is not AuthCurrentUser || !user.IsAuthenticated || user.UserId is not > 0)
        {
            await next(context);
            return;
        }

        var auth = context.RequestServices.GetRequiredService<AuthService>();
        if (await auth.RequiresPasswordChangeAsync(user.UserId.Value))
            throw new AppException("password_change_required",
                "Devam etmek için parolanızı değiştirmeniz gerekiyor.", StatusCodes.Status403Forbidden);

        await next(context);
    }

    /// <summary>
    /// Giriş, çıkış, oturum durumu ve parola değiştirme (<c>/api/v1/auth/*</c>), sağlık kontrolü ve OpenAPI
    /// belgesi serbesttir; diğer tüm <c>/api/v1</c> uçları korunur. Blazor sayfaları ve <c>/logout</c> API değildir.
    /// </summary>
    public static bool IsGuarded(PathString path) =>
        path.StartsWithSegments("/api/v1")
        && !path.StartsWithSegments("/api/v1/auth")
        && !path.StartsWithSegments("/api/v1/health")
        && !path.StartsWithSegments("/api/v1/openapi.json");
}
