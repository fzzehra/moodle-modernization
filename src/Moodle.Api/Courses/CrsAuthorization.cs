using Moodle.Application.Abstractions;
using Moodle.Application.Errors;
using Moodle.Infrastructure.Fakes;

namespace Moodle.Api.Courses;

public sealed class CrsAuthorization(ICurrentUser user, IAccessPolicy policy)
{
    public void RequireIdentity()
    {
        // The repository's AUTH placeholders impersonate an administrator.
        // Never expose CRS through these placeholders, including in Production.
        if (user is FakeCurrentUser or UnavailableIdentity || policy is FakeAccessPolicy or UnavailableIdentity)
            throw new AppException("auth_not_ready",
                "CRS API için gerçek AUTH kimlik ve yetki servisleri henüz bağlı değil.", 503);

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

    public async Task RequireRoleAssignmentAsync(string role, int courseId, CancellationToken ct)
    {
        var context = ContextRef.ForCourse(courseId);
        await RequireAsync("course.role.assign", context, ct);
        // IAccessPolicy has no target-role parameter. Require an explicit grant
        // for the requested role as well as the general assignment capability.
        await RequireAsync($"course.role.assign:{role}", context, ct);
    }
}
