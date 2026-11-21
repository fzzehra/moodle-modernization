using Moodle.Application.Files;

namespace Moodle.UnitTests;

public sealed class FileRetentionRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
    private const int DraftTtl = 96;   // FileSetting varsayılanı
    private const int Retention = 24;  // FileSetting varsayılanı

    [Fact]
    public void FILE_BR_05_son_etkinligi_sure_kadar_eski_taslak_silinir()
    {
        Assert.True(FileRetentionRules.IsDraftExpired(Now.AddHours(-DraftTtl - 1), DraftTtl, Now));
    }

    [Fact]
    public void FILE_BUG_02_olusturulmasi_eski_ama_son_etkinligi_yeni_taslak_silinmez()
    {
        // Oluşturulma 5 gün önce olsa da kural yalnızca son etkinliğe bakar; son etkinlik 1 saat önce.
        var lastActivity = Now.AddHours(-1);
        Assert.False(FileRetentionRules.IsDraftExpired(lastActivity, DraftTtl, Now));
    }

    [Fact]
    public void FILE_BUG_02_taslak_suresi_sinir_aninda_dolar_bir_an_once_dolmaz()
    {
        var boundary = Now.AddHours(-DraftTtl);
        Assert.True(FileRetentionRules.IsDraftExpired(boundary, DraftTtl, Now));
        Assert.False(FileRetentionRules.IsDraftExpired(boundary.AddTicks(1), DraftTtl, Now));
    }

    [Fact]
    public void FILE_BR_04_cope_atilmasi_sure_kadar_eski_kayit_silinir()
    {
        Assert.True(FileRetentionRules.IsTrashExpired(Now.AddHours(-Retention - 1), Retention, Now));
    }

    [Fact]
    public void FILE_BUG_03_dosya_eski_ama_yeni_cope_atildiysa_silinmez()
    {
        // Kural dosyanın oluşturulma anını hiç almaz: yalnızca çöpe atılma anı (DeletedAt) belirleyicidir.
        var deletedAt = Now.AddMinutes(-5);
        Assert.False(FileRetentionRules.IsTrashExpired(deletedAt, Retention, Now));
    }

    [Fact]
    public void FILE_BUG_03_cop_suresi_sinir_aninda_dolar_bir_an_once_dolmaz()
    {
        var boundary = Now.AddHours(-Retention);
        Assert.True(FileRetentionRules.IsTrashExpired(boundary, Retention, Now));
        Assert.False(FileRetentionRules.IsTrashExpired(boundary.AddTicks(1), Retention, Now));
    }

    [Fact]
    public void FILE_BUG_04_kullanimdaki_icerik_hicbir_zaman_silinmeye_aday_degildir()
    {
        Assert.False(FileRetentionRules.IsBlobPurgeable(null, Retention, Now));
        Assert.False(FileRetentionRules.IsBlobPurgeable(null, 0, Now));
    }

    [Fact]
    public void FILE_BUG_04_bosa_dusen_icerik_bekleme_suresi_sinirinda_aday_olur()
    {
        var boundary = Now.AddHours(-Retention);
        Assert.True(FileRetentionRules.IsBlobPurgeable(boundary, Retention, Now));
        Assert.False(FileRetentionRules.IsBlobPurgeable(boundary.AddTicks(1), Retention, Now));
    }

    [Fact]
    public void Kesim_ani_kurallarla_tutarlidir()
    {
        var cutoff = FileRetentionRules.Cutoff(Now, Retention);
        Assert.Equal(Now.AddHours(-Retention), cutoff);
        Assert.True(FileRetentionRules.IsTrashExpired(cutoff, Retention, Now));
    }
}
