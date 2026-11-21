using System.Buffers.Binary;
using Microsoft.EntityFrameworkCore;
using Moodle.Application.Courses;
using Moodle.Domain.Files;
using Moodle.Infrastructure.Files;
using Npgsql;

namespace Moodle.IntegrationTests;

public sealed class FileCourseCleanupTransactionTests(FilesPostgresFixture fixture) : IClassFixture<FilesPostgresFixture>
{
    private async Task<(long Id, string Hash)> Seed(int course)
    {
        await using var db = fixture.CreateContext();
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray())).ToLowerInvariant();
        db.Blobs.Add(new FileBlob { ContentHash = hash, StorageKey = hash, SizeBytes = 3, CreatedAt = DateTimeOffset.UtcNow });
        var entry = new FileEntry { ContextId = course, Component = "course", Area = "files", Name = "course.txt",
            ContentHash = hash, SizeBytes = 3, OwnerUserId = 1, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.Entries.Add(entry); await db.SaveChangesAsync(); return (entry.Id, hash);
    }
    private static CrsChangeEvent Event(int course) => new(1, "course.deleted", course, null, null, DateTimeOffset.UtcNow, 1, []);

    [Fact]
    public async Task File_cleanup_failure_rolls_back_all_changes_and_same_instance_retries()
    {
        var data = await Seed(81101);
        await using var db = fixture.CreateContext();
        var extension = new FileCourseDeletedExtension(db, TimeProvider.System);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION file_test_fail() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'injected FILE failure'; END; $$;
            CREATE TRIGGER file_test_fail BEFORE UPDATE ON file_blob FOR EACH ROW EXECUTE FUNCTION file_test_fail();
            """);
        try
        {
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => extension.HandleAsync(Event(81101)));
            Assert.Equal("P0001", Assert.IsType<PostgresException>(error.InnerException).SqlState);
            Assert.Null((await db.Entries.SingleAsync(x => x.Id == data.Id)).DeletedAt);
            Assert.Null((await db.Blobs.SingleAsync(x => x.ContentHash == data.Hash)).OrphanedAt);
        }
        finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER file_test_fail ON file_blob; DROP FUNCTION file_test_fail();"); }
        await extension.HandleAsync(Event(81101)); await extension.HandleAsync(Event(81101));
        db.ChangeTracker.Clear();
        Assert.NotNull((await db.Entries.SingleAsync(x => x.Id == data.Id)).DeletedAt);
        Assert.NotNull((await db.Blobs.SingleAsync(x => x.ContentHash == data.Hash)).OrphanedAt);
    }

    [Fact]
    public async Task Cleanup_waits_for_upload_content_lock_and_preserves_new_shared_reference()
    {
        var data = await Seed(81102);
        await using var upload = fixture.CreateContext();
        await using var tx = await upload.Database.BeginTransactionAsync();
        var key = BinaryPrimitives.ReadInt64BigEndian(Convert.FromHexString(data.Hash));
        await upload.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({key})");
        await using var configured = fixture.CreateContext();
        var cleaning = new FileCourseDeletedExtension(configured, TimeProvider.System).HandleAsync(Event(81102));
        try
        {
            await using var observer = fixture.CreateContext();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (await observer.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock' AND query LIKE 'SELECT pg_advisory_xact_lock%'").SingleAsync(timeout.Token) == 0)
                await Task.Delay(25, timeout.Token);
            upload.Entries.Add(new FileEntry { ContextId = 2, Component = "user", Area = "draft", ItemId = 123,
                Name = "shared.txt", ContentHash = data.Hash, SizeBytes = 3, OwnerUserId = 2, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            await upload.SaveChangesAsync(); await tx.CommitAsync();
        }
        finally { if (upload.Database.CurrentTransaction is not null) await tx.RollbackAsync(); }
        await cleaning;
        await using var check = fixture.CreateContext();
        Assert.NotNull((await check.Entries.SingleAsync(x => x.Id == data.Id)).DeletedAt);
        Assert.Null((await check.Blobs.SingleAsync(x => x.ContentHash == data.Hash)).OrphanedAt);
        Assert.Single(await check.Entries.Where(x => x.ContentHash == data.Hash && x.DeletedAt == null).ToArrayAsync());
    }
}
