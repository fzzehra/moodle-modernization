namespace Moodle.Application.Files;

/// <summary>
/// Bir sahip bileşenin (ödev, kaynak, profil vb.) dosya yetkisi (WBS-2 §3.6.3). Moodle'daki <c>*_pluginfile</c>
/// işlevlerinin karşılığıdır. Dosya servisi karar için yalnızca bunları çağırır.
/// </summary>
public interface IFilePolicy
{
    /// <summary>Bu politikanın sahibi bileşen, ör. <c>mod_assign</c>, <c>user</c>, <c>course</c>.</summary>
    string Component { get; }

    Task<bool> CanReadAsync(int userId, FileArea area, CancellationToken ct = default);
    Task<bool> CanWriteAsync(int userId, FileArea area, CancellationToken ct = default);
}

/// <summary>Bileşen adına göre politika bulur. Modüller kendi politikasını <see cref="IFilePolicy"/> olarak kaydeder.</summary>
public interface IFilePolicyRegistry
{
    /// <returns>Bileşenin politikası; kayıtlı değilse <c>null</c>. Kayıtsız bileşende erişim reddedilir.</returns>
    IFilePolicy? Find(string component);
}
