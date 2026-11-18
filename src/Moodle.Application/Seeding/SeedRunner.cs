using Microsoft.Extensions.Logging;

namespace Moodle.Application.Seeding;

/// <summary>Kayıtlı tüm <see cref="ISeedContributor"/>'ları <c>Order</c> sırasıyla çalıştırır.</summary>
public sealed class SeedRunner(IEnumerable<ISeedContributor> contributors, ILogger<SeedRunner> logger)
{
    public async Task RunAsync(SeedDataset data, CancellationToken ct = default)
    {
        foreach (var c in contributors.OrderBy(c => c.Order).ThenBy(c => c.Name, StringComparer.Ordinal))
        {
            logger.LogInformation("Tohumlama: {Name}", c.Name);
            await c.SeedAsync(data, ct);
        }
    }
}
