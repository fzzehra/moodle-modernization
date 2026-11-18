using Moodle.Application.Files;

namespace Moodle.Infrastructure.Fakes;

/// <summary>Kayıtlı <see cref="IFilePolicy"/>'leri bileşen adına göre bulur. Kayıtsız bileşende <c>null</c> döner, erişim reddedilir.</summary>
public sealed class FilePolicyRegistry(IEnumerable<IFilePolicy> policies) : IFilePolicyRegistry
{
    private readonly Dictionary<string, IFilePolicy> _byComponent =
        policies.ToDictionary(p => p.Component, StringComparer.Ordinal);

    public IFilePolicy? Find(string component) => _byComponent.GetValueOrDefault(component);
}
