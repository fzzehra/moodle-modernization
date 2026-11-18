using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Application.Files;
using Moodle.Domain.Files;
using Moodle.Infrastructure.Fakes;
using Moodle.Infrastructure.Files;

namespace Moodle.UnitTests;

/// <summary>FILE veri modeli (WBS-2 §3.6.2) ve sahte gerçeklemeler. Veritabanı gerektirmez.</summary>
public class FileModelTests
{
    private static FilesDbContext Context() =>
        new(new DbContextOptionsBuilder<FilesDbContext>().UseNpgsql("Host=x").Options);

    [Fact]
    public void FILE_BR_02_ayni_alanda_ayni_ad_benzersiz_dizinle_korunur()
    {
        using var db = Context();
        var entity = db.Model.FindEntityType(typeof(FileEntry))!;
        var unique = entity.GetIndexes().Single(i => i.IsUnique);
        Assert.Equal(["ContextId", "Component", "Area", "ItemId", "Path", "Name"],
            unique.Properties.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void FILE_BR_01_icerik_ozet_anahtarlidir_ve_kayit_icerige_baglidir()
    {
        using var db = Context();
        Assert.Equal(["ContentHash"], db.Model.FindEntityType(typeof(FileBlob))!.FindPrimaryKey()!.Properties.Select(p => p.Name));
        var fk = db.Model.FindEntityType(typeof(FileEntry))!.GetForeignKeys().Single();
        Assert.Equal(typeof(FileBlob), fk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior); // kayıtlı içerik silinemez
    }

    [Fact]
    public void FILE_BR_06_dosya_adi_en_cok_255_karakter()
    {
        using var db = Context();
        Assert.Equal(255, db.Model.FindEntityType(typeof(FileEntry))!.FindProperty(nameof(FileEntry.Name))!.GetMaxLength());
    }

    [Fact]
    public void Ayar_tek_satirdir_ve_varsayilanlari_vardir()
    {
        using var db = Context();
        var model = ((IInfrastructure<IServiceProvider>)db).Instance.GetRequiredService<IDesignTimeModel>().Model;
        var seed = model.FindEntityType(typeof(FileSetting))!.GetSeedData().Single();
        Assert.Equal(1, seed["Id"]);
        Assert.Equal(100L * 1024 * 1024, seed["UserQuotaBytes"]);
        Assert.Equal(96, seed["DraftTtlHours"]);
    }

    [Fact]
    public void Klasor_kaydi_adi_nokta_olandir() =>
        Assert.True(new FileEntry { Name = "." }.IsDirectory);

    [Fact]
    public async Task Sahte_depo_ayni_icerigi_tek_kopya_olarak_tutar_ve_SHA256_uretir()
    {
        var store = new FakeFileStore();
        var first = await store.WriteAsync(new MemoryStream("merhaba"u8.ToArray()));
        var second = await store.WriteAsync(new MemoryStream("merhaba"u8.ToArray()));
        Assert.Equal(first.ContentHash, second.ContentHash);
        Assert.Equal(64, first.ContentHash.Length);
        Assert.Equal($"{first.ContentHash[..2]}/{first.ContentHash[2..4]}/{first.ContentHash}", first.StorageKey);
        Assert.False(first.AlreadyExisted);
        Assert.True(second.AlreadyExisted);
    }

    [Fact]
    public async Task Sahte_servis_taslaktan_kalici_alana_kaydeder_ve_okur()
    {
        var store = new FakeFileStore();
        var svc = new FakeFileService(store);
        var draft = await svc.CreateDraftAsync(userId: 3);
        var meta = await svc.AddToDraftAsync(3, draft.DraftId, "ozet.txt", new MemoryStream("abc"u8.ToArray()));
        var area = new FileArea(ContextId: 77, Component: "mod_assign", Area: "submission_files", ItemId: 5);

        var committed = await svc.CommitAsync(3, draft.DraftId, area);
        Assert.Single(committed);
        Assert.Single(await svc.ListAsync(3, area));
        Assert.Empty(await svc.ListDraftAsync(3, draft.DraftId));

        await using var content = (await svc.OpenAsync(3, meta.FileId)).Content;
        using var reader = new StreamReader(content);
        Assert.Equal("abc", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Politika_kayitsiz_bilesen_icin_null_doner()
    {
        var registry = new FilePolicyRegistry([]);
        Assert.Null(registry.Find("mod_assign"));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Kapali_servis_sahte_veriyle_calismaz() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() => new UnavailableFiles().CreateDraftAsync(1));
}
