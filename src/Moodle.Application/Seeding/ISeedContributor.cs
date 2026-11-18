namespace Moodle.Application.Seeding;

/// <summary>
/// Bir modülün örnek veriyi kendi tablolarına yükleme adımı. Her modül sahibi kendi gerçeklemesini yazar ve
/// <c>Order</c> ile bağımlılık sırasını belirtir: kimlik (10) → kategori ve ders (20) → kayıt (30) → ödev (40) →
/// teslim (50) → not (60). Yükleme <b>tekrar çalıştırılabilir</b> olmalıdır (aynı kayıt varsa atlanır).
/// </summary>
public interface ISeedContributor
{
    /// <summary>Küçük sayı önce çalışır.</summary>
    int Order { get; }

    string Name { get; }

    Task SeedAsync(SeedDataset data, CancellationToken ct = default);
}
