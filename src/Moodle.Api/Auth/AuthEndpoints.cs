using Moodle.Api.Errors;
using Moodle.Application.Abstractions;
using Moodle.Application.Auth;
using Moodle.Application.Errors;
using Moodle.Infrastructure.Auth;

namespace Moodle.Api.Auth;

public sealed record LoginRequest(string Username, string Password);
public sealed record LoginResponse(bool Authenticated, bool RequiresPasswordChange);
public sealed record SessionResponse(bool Authenticated, string? Username);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record PermissionsResponse(int CourseId, IReadOnlyList<string> Permissions);

/// <summary>
/// AUTH-FR-01, 04, 05, 08, 09 (giriş, çıkış, hesap durumu, başarısız giriş kaydı, parola değişikliği)
/// ve AUTH-FR-02, 03, 07 (yetki sorgulama). WBS-2 §3.1.3.
/// </summary>
public static class AuthEndpoints
{
    public static IServiceCollection AddAuthApi(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Sayfa gezintisi için düz uç (JSON değil): NavMenu'deki "Çıkış yap" bağlantısı, Blazor
        // etkileşimliliği gerektirmeden normal bir HTTP isteğiyle oturumu kapatır.
        endpoints.MapGet("/logout", async (ICurrentUser user, AuthService auth, CancellationToken ct) =>
        {
            await auth.LogoutAsync(user.UserId, ct);
            return Results.LocalRedirect("/");
        });

        var api = endpoints.MapGroup("/api/v1/auth").WithTags("AUTH");

        // AUTH-FR-01, 04, 05: geçerli girişte oturum açılır; askıya alınmış/silinmiş hesap ve yanlış
        // parola aynı genel mesajı alır (hangisi olduğu sızdırılmaz), başarısız denemeler kaydedilir.
        api.MapPost("/login", async (LoginRequest request, AuthService auth, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                throw AppException.Validation("validation_failed", "Kullanıcı adı ve parola zorunludur.");

            var result = await auth.LoginAsync(request.Username, request.Password, ct);
            return result.Outcome switch
            {
                LoginOutcome.Success => Results.Ok(new LoginResponse(true, result.RequiresPasswordChange)),
                LoginOutcome.LockedOut => throw new AppException("account_locked",
                    "Hesabınız çok sayıda başarısız denemeden sonra kilitlendi. Lütfen daha sonra tekrar deneyin.", 401),
                _ => throw new AppException("invalid_credentials", "Kullanıcı adı veya parola hatalı.", 401),
            };
        }).Produces<LoginResponse>(200).Errors(401, 422);

        // AUTH-FR-08: oturumu sonlandırır.
        api.MapPost("/logout", async (ICurrentUser user, AuthService auth, CancellationToken ct) =>
        {
            await auth.LogoutAsync(user.UserId, ct);
            return Results.NoContent();
        }).Produces(204);

        // Oturum durumu; hassas bilgi (kimlik, rol) döndürmez.
        api.MapGet("/session", (ICurrentUser user) =>
            Results.Ok(new SessionResponse(user.IsAuthenticated, user.Username)))
            .Produces<SessionResponse>(200);

        // AUTH-FR-09: zorunlu/istemli parola değişikliği.
        api.MapPost("/change-password", async (
            ChangePasswordRequest request, ICurrentUser user, AuthService auth, CancellationToken ct) =>
        {
            RequireAuthenticated(user);
            if (string.IsNullOrWhiteSpace(request.NewPassword))
                throw AppException.Validation("validation_failed", "Yeni parola zorunludur.");

            var changed = await auth.ChangePasswordAsync(user.UserId!.Value, request.CurrentPassword, request.NewPassword, ct);
            if (!changed)
                throw new AppException("current_password_invalid", "Mevcut parola hatalı.", 400);
            return Results.NoContent();
        }).Produces(204).Errors(400, 401, 422);

        // AUTH-FR-02, 03, 07: çağıranın belirtilen ders bağlamındaki yetkileri. Yalnızca çağıranın
        // kendi yetkileri döner (WBS-2 §3.1.3: "yalnızca çağıranın görüntülemeye yetkili olduğu bilgiler").
        api.MapGet("/permissions", async (
            int courseId, string? capabilities, ICurrentUser user, IAccessPolicy policy, CancellationToken ct) =>
        {
            RequireAuthenticated(user);
            var candidates = string.IsNullOrWhiteSpace(capabilities)
                ? CapabilityRules.KnownCapabilities
                : capabilities.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var granted = new List<string>();
            foreach (var capability in candidates)
                if (await policy.HasCapabilityAsync(user.UserId!.Value, capability, ContextRef.ForCourse(courseId), ct))
                    granted.Add(capability);

            return Results.Ok(new PermissionsResponse(courseId, granted));
        }).Produces<PermissionsResponse>(200).Errors(401);

        return endpoints;
    }

    private static void RequireAuthenticated(ICurrentUser user)
    {
        if (!user.IsAuthenticated || user.UserId is not > 0)
            throw new AppException("not_authenticated", "Oturum açmanız gerekiyor.", 401);
    }

    private static RouteHandlerBuilder Errors(this RouteHandlerBuilder endpoint, params int[] statuses)
    {
        foreach (var status in statuses) endpoint.Produces<ApiErrorResponse>(status);
        return endpoint;
    }
}
