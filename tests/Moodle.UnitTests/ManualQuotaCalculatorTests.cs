using Moodle.Domain.Files;
using Moodle.Infrastructure.Files.Comparisons;

namespace Moodle.UnitTests;

/// <summary>
/// FILE-FR-09, BUG-01 karşılaştırma uygulamasının (<see cref="ManualQuotaCalculator"/>, WBS-2 §4.4) saf
/// mantığını sınar. Bu dosya da YZ tarafından yazılmıştır; "elle" karşılaştırma çiftinin kod tarafını
/// doğrular, "elle yazıldı" iddiası taşımaz.
/// </summary>
public class ManualQuotaCalculatorTests
{
    private static FileEntry Entry(int owner, string component, string area, long size, DateTimeOffset? deletedAt = null) => new()
    {
        OwnerUserId = owner, Component = component, Area = area, SizeBytes = size, DeletedAt = deletedAt,
        ContentHash = new string('a', 64), ContextId = owner, ItemId = 0, Path = "/", Name = Guid.NewGuid().ToString(),
    };

    [Fact]
    public void Counts_each_active_private_file_once_even_with_identical_content()
    {
        // FILE-BUG-01: aynı içerik (gerçek şemada aynı ContentHash) iki farklı kayıtta olsa bile
        // her kayıt kendi SizeBytes'ıyla ayrı sayılmalıdır.
        var entries = new[]
        {
            Entry(42, "user", "private", 1000),
            Entry(42, "user", "private", 1000),
        };
        Assert.Equal(2000, ManualQuotaCalculator.CalculateUsedBytes(entries, 42));
    }

    [Fact]
    public void Excludes_drafts_other_users_other_components_and_trashed_entries()
    {
        var entries = new[]
        {
            Entry(42, "user", "private", 500),
            Entry(42, "user", "draft", 999),             // taslak -> hariç
            Entry(43, "user", "private", 999),           // başka kullanıcı -> hariç
            Entry(42, "course", "files", 999),            // başka bileşen -> hariç
            Entry(42, "user", "private", 999, DateTimeOffset.UtcNow), // çöpte -> hariç
        };
        Assert.Equal(500, ManualQuotaCalculator.CalculateUsedBytes(entries, 42));
    }

    [Fact]
    public void Calculate_returns_the_given_limit_unchanged()
    {
        var (used, limit) = ManualQuotaCalculator.Calculate([Entry(1, "user", "private", 100)], 1, 100L * 1024 * 1024);
        Assert.Equal(100, used);
        Assert.Equal(100L * 1024 * 1024, limit);
    }
}
