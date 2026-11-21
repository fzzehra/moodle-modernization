using Microsoft.EntityFrameworkCore;
using Moodle.Application.Courses;
using Moodle.Infrastructure.Courses;

namespace Moodle.UnitTests;

public sealed class CrsNfr03DeleteGuardTests
{
    [Fact]
    public void CRS_NFR_03_Registered_operation_rejects_deletion_with_stable_conflict_code()
    {
        var exception = Assert.Throws<CrsConflictException>(() => CrsCourseOperationRules.EnsureDeletionAllowed(true));
        Assert.Equal("course_operation_in_progress", exception.Code);
    }

    [Fact]
    public void CRS_NFR_03_No_registered_operation_allows_deletion_check() =>
        CrsCourseOperationRules.EnsureDeletionAllowed(false);

    [Theory]
    [InlineData(CrsCourseOperationKind.Backup)]
    [InlineData(CrsCourseOperationKind.Restore)]
    public void CRS_NFR_03_Only_documented_backup_restore_kinds_are_supported(CrsCourseOperationKind kind) =>
        CrsCourseOperationRules.ValidateKind(kind);

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public async Task CRS_NFR_03_Unknown_kind_is_rejected_before_database_access(int kind)
    {
        await using var db = ContextWithoutDatabase();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new CrsCourseOperationRegistry(db).RegisterPendingAsync(1, (CrsCourseOperationKind)kind));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CRS_NFR_03_Invalid_course_id_is_rejected_before_database_access(int id)
    {
        await using var db = ContextWithoutDatabase();
        var registry = new CrsCourseOperationRegistry(db);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => registry.RegisterPendingAsync(id, CrsCourseOperationKind.Backup));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => registry.MarkRunningAsync(id, Guid.NewGuid()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => registry.ReleaseAsync(id, Guid.NewGuid()));
    }

    [Fact]
    public async Task CRS_NFR_03_Empty_operation_token_is_rejected_before_database_access()
    {
        await using var db = ContextWithoutDatabase();
        var registry = new CrsCourseOperationRegistry(db);
        await Assert.ThrowsAsync<ArgumentException>(() => registry.MarkRunningAsync(1, Guid.Empty));
        await Assert.ThrowsAsync<ArgumentException>(() => registry.ReleaseAsync(1, Guid.Empty));
    }

    private static CrsDbContext ContextWithoutDatabase() => new(new DbContextOptionsBuilder<CrsDbContext>()
        .UseNpgsql("Host=127.0.0.1;Port=1;Database=not_used;Username=not_used;Timeout=1").Options);
}
