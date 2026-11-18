using Moodle.Application.Seeding;

namespace Moodle.UnitTests;

/// <summary>
/// Örnek veri kümesi, <c>ortam/ornek-veri.feature</c> dosyasıyla aynı kalmalıdır. Biri değişirse bu testler kırılır
/// ve eski/yeni sistemin farklı verilerle karşılaştırılması önlenir.
/// </summary>
public class SampleDatasetTests
{
    private static readonly SeedDataset Data = SampleDataset.Create();

    private static string FeatureText()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ortam", "ornek-veri.feature")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, "ortam", "ornek-veri.feature"));
    }

    /// <summary>Verilen başlıktaki tablonun veri satır sayısı (başlık satırı hariç).</summary>
    private static int Rows(string feature, string header)
    {
        var lines = feature.Split('\n').Select(l => l.Trim()).ToList();
        var start = lines.FindIndex(l => l.Contains(header, StringComparison.Ordinal));
        Assert.True(start >= 0, $"'{header}' bulunamadı");
        return lines.Skip(lines[start].StartsWith('|') ? start : start + 1).SkipWhile(l => !l.StartsWith('|')).TakeWhile(l => l.StartsWith('|')).Count() - 1;
    }

    [Fact]
    public void Kategori_sayisi_ayni() =>
        Assert.Equal(Rows(FeatureText(), "\"categories\" exist"), Data.Categories.Count);

    [Fact]
    public void Ders_sayisi_ayni() =>
        Assert.Equal(Rows(FeatureText(), "\"courses\" exist"), Data.Courses.Count);

    [Fact]
    public void Kullanici_sayisi_ayni_ogrenciler_dahil() =>
        Assert.Equal(Rows(FeatureText(), "\"users\" exist:") + 10, Data.Users.Count);

    [Fact]
    public void Odev_ve_kaynak_sayisi_ayni()
    {
        var f = FeatureText();
        Assert.Equal(Rows(f, "| activity | name           | intro                      |"), Data.Assignments.Count);
        Assert.Single(Data.Resources);
    }

    [Fact]
    public void Teslim_ve_not_sayisi_ayni()
    {
        var f = FeatureText();
        Assert.Equal(Rows(f, "\"mod_assign > submissions\" exist"), Data.Submissions.Count);
        Assert.Equal(Rows(f, "\"grade grades\" exist"), Data.Grades.Count);
    }

    [Fact]
    public void Kayit_sayisi_ayni_ogrenci_kayitlari_dahil()
    {
        var f = FeatureText();
        Assert.Equal(Rows(f, "the following \"course enrolments\" exist") + 10 + 5 + 5, Data.Enrolments.Count);
    }

    [Fact]
    public void Her_baglanti_var_olan_bir_kayda_isaret_eder()
    {
        var users = Data.Users.Select(u => u.Username).ToHashSet();
        var courses = Data.Courses.Select(c => c.ShortName).ToHashSet();
        var cats = Data.Categories.Select(c => c.IdNumber).ToHashSet();
        var assigns = Data.Assignments.Select(a => a.Name).ToHashSet();

        Assert.All(Data.Courses, c => Assert.Contains(c.CategoryIdNumber, cats));
        Assert.All(Data.Enrolments, e => { Assert.Contains(e.Username, users); Assert.Contains(e.CourseShortName, courses); });
        Assert.All(Data.Assignments, a => Assert.Contains(a.CourseShortName, courses));
        Assert.All(Data.Submissions, s => { Assert.Contains(s.Username, users); Assert.Contains(s.AssignmentName, assigns); });
        Assert.All(Data.Grades, g => { Assert.Contains(g.Username, users); Assert.Contains(g.AssignmentName, assigns); });
    }

    [Fact]
    public void Her_not_bir_teslime_aittir() =>
        Assert.All(Data.Grades, g =>
            Assert.Contains(Data.Submissions, s => s.AssignmentName == g.AssignmentName && s.Username == g.Username));

    [Fact]
    public void UserId_kararlidir_ve_birdir_birinden_baslar()
    {
        Assert.Equal(1, Data.UserId("yonetici1"));
        Assert.Equal(15, Data.UserId("ogrenci10"));
        Assert.Equal(Data.Users.Count, Data.Users.Select(u => Data.UserId(u.Username)).Distinct().Count());
        Assert.Throws<ArgumentException>(() => Data.UserId("yok"));
    }

    [Fact]
    public async Task SeedRunner_katki_saglayicilari_Order_sirasiyla_calistirir()
    {
        var log = new List<string>();
        var runner = new SeedRunner(
            [new Fake("ogrenci", 50, log), new Fake("kimlik", 10, log), new Fake("ders", 20, log)],
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SeedRunner>.Instance);
        await runner.RunAsync(Data);
        Assert.Equal(["kimlik", "ders", "ogrenci"], log);
    }

    private sealed class Fake(string name, int order, List<string> log) : ISeedContributor
    {
        public int Order => order;
        public string Name => name;
        public Task SeedAsync(SeedDataset data, CancellationToken ct = default) { log.Add(name); return Task.CompletedTask; }
    }
}
