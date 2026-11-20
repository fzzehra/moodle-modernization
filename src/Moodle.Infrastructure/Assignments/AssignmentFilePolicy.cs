using Microsoft.Extensions.DependencyInjection;
using Moodle.Application.Abstractions;
using Moodle.Application.Assignments;
using Moodle.Application.Files;
namespace Moodle.Infrastructure.Assignments;

public sealed class AssignmentFilePolicy(IServiceProvider services, ICurrentUser current, AssignmentFileWriteScope write) : IFilePolicy
{
    public string Component => "mod_assign";
    public async Task<bool> CanReadAsync(int userId, FileArea area, CancellationToken ct = default)
        => Valid(userId,area) && await services.GetRequiredService<AssignmentService>()
            .CanReadFilesAsync(area.ContextId,(int)area.ItemId,ct);
    public Task<bool> CanWriteAsync(int userId, FileArea area, CancellationToken ct = default)
        => Task.FromResult(Valid(userId,area) && write.SubmissionId==area.ItemId
            && write.AssignmentId==area.ContextId && write.UserId==userId);
    private bool Valid(int userId,FileArea area)
        => current.IsAuthenticated && current.UserId==userId && area.Component==Component
            && area.Area=="submission" && area.ContextId>0 && area.ItemId is >0 and <=int.MaxValue;
}
