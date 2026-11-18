using Microsoft.EntityFrameworkCore;
using Moodle.Domain.Files;

namespace Moodle.Infrastructure.Files;

/// <summary>FILE modülünün tabloları (WBS-2 §3.6.2). Moodle'ın tek <c>files</c> tablosu "içerik" ve "kayıt" olarak ikiye ayrılmıştır.</summary>
public sealed class FilesDbContext(DbContextOptions<FilesDbContext> options) : DbContext(options)
{
    public DbSet<FileBlob> Blobs => Set<FileBlob>();
    public DbSet<FileEntry> Entries => Set<FileEntry>();
    public DbSet<FileDraft> Drafts => Set<FileDraft>();
    public DbSet<FileSetting> Settings => Set<FileSetting>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<FileBlob>(e =>
        {
            e.ToTable("file_blob");
            e.HasKey(x => x.ContentHash);
            e.Property(x => x.ContentHash).HasMaxLength(64).IsFixedLength();
            e.Property(x => x.StorageKey).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.OrphanedAt); // temizlik görevi boşa düşenleri bu dizinle bulur
        });

        model.Entity<FileEntry>(e =>
        {
            e.ToTable("file_entry");
            e.HasKey(x => x.Id);
            e.Property(x => x.ContentHash).HasMaxLength(64).IsFixedLength().IsRequired();
            e.Property(x => x.Component).HasMaxLength(100).IsRequired();
            e.Property(x => x.Area).HasMaxLength(100).IsRequired();
            e.Property(x => x.Path).HasMaxLength(1024).IsRequired();
            e.Property(x => x.Name).HasMaxLength(255).IsRequired(); // FILE-BR-06
            e.Property(x => x.MimeType).HasMaxLength(255).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Source).HasMaxLength(255);
            e.Property(x => x.Author).HasMaxLength(255);
            e.Property(x => x.License).HasMaxLength(255);

            e.HasOne<FileBlob>().WithMany().HasForeignKey(x => x.ContentHash).OnDelete(DeleteBehavior.Restrict);

            // FILE-BR-02: aynı alanda, aynı yolda aynı ad iki kez bulunamaz.
            e.HasIndex(x => new { x.ContextId, x.Component, x.Area, x.ItemId, x.Path, x.Name }).IsUnique();
            e.HasIndex(x => new { x.Component, x.Area, x.ContextId, x.ItemId });
            e.HasIndex(x => x.ContentHash);          // içerik kullanımda mı sorgusu
            e.HasIndex(x => new { x.OwnerUserId, x.Component }); // kota hesabı
        });

        model.Entity<FileDraft>(e =>
        {
            e.ToTable("file_draft");
            e.HasKey(x => x.DraftId);
            e.Property(x => x.DraftId).ValueGeneratedOnAdd();
            e.HasIndex(x => x.LastActivityAt); // süresi dolan taslakları bulmak için
        });

        model.Entity<FileSetting>(e =>
        {
            e.ToTable("file_setting");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.ToTable(t => t.HasCheckConstraint("ck_file_setting_single_row", "\"Id\" = 1"));
            e.HasData(new FileSetting());
        });
    }
}
