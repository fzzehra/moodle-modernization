using Moodle.Application.Files;

namespace Moodle.Infrastructure.Files;

/// <summary>
/// <c>component="user"</c> için temel politika (WBS-2 §3.6.3): taslak alanı ve özel dosyalar yalnızca
/// sahibine açıktır. Taslak sahipliği <see cref="FileService"/> içinde <c>FileDraft.OwnerUserId</c> ile ayrıca
/// denetlenir (Moodle'daki <c>draftfile.php</c>'nin genel yetki akışını atlaması gibi); bu politika asıl olarak
/// "özel dosyalar" (private) alanını kapsar. Özel dosyalar alanında bağlam, sahibinin kullanıcı kimliğidir
/// (Moodle'daki <c>CONTEXT_USER</c> karşılığı): <c>FileArea.ContextId == userId</c>.
/// </summary>
public sealed class UserFilePolicy : IFilePolicy
{
    public string Component => "user";

    public Task<bool> CanReadAsync(int userId, FileArea area, CancellationToken ct = default) =>
        Task.FromResult(Owns(userId, area));

    public Task<bool> CanWriteAsync(int userId, FileArea area, CancellationToken ct = default) =>
        Task.FromResult(Owns(userId, area));

    private static bool Owns(int userId, FileArea area) =>
        area.Area is "private" or "draft" && area.ContextId == userId;
}
