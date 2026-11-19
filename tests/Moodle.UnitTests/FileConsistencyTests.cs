using Moodle.Infrastructure.Files;

namespace Moodle.UnitTests;

/// <summary>FILE-NFR-02: veritabanı kaydı ile fiziksel dosyanın tutarlılığı.</summary>
public class FileConsistencyTests
{
    private static string H(char c) => new(c, 64);

    [Fact]
    public void Ikisi_ayniysa_tutarlidir()
        => Assert.True(FileConsistencyChecker.Compare([H('a'), H('b')], [H('b'), H('a')]).IsConsistent);

    [Fact]
    public void Kayit_var_dosya_yoksa_eksik_icerik_olarak_raporlanir()
    {
        var r = FileConsistencyChecker.Compare([H('a'), H('b')], [H('a')]);
        Assert.False(r.IsConsistent);
        Assert.Equal([H('b')], r.BlobsMissingOnDisk);
        Assert.Empty(r.FilesWithoutBlob);
    }

    [Fact]
    public void Dosya_var_kayit_yoksa_sahipsiz_dosya_olarak_raporlanir()
    {
        var r = FileConsistencyChecker.Compare([H('a')], [H('a'), H('c')]);
        Assert.Equal([H('c')], r.FilesWithoutBlob);
        Assert.Empty(r.BlobsMissingOnDisk);
    }

    [Fact]
    public void Her_ikisi_bos_tutarlidir()
        => Assert.True(FileConsistencyChecker.Compare([], []).IsConsistent);
}
