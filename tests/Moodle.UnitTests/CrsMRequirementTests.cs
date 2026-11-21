using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moodle.Domain.Courses;
using Moodle.Infrastructure.Courses;

namespace Moodle.UnitTests;

/// <summary>
/// YZ destekli (Claude Code): CRS M gereksinimlerinin servis ve veri modeli düzeyindeki birim testleri.
/// Veritabanı bağlantısı açılmaz; aynı davranışlar PostgreSQL'de CrsPostgresTests/CrsEndpointTests ile sınanır.
/// </summary>
public sealed class CrsMRequirementTests
{
    [Fact]
    public void CRS_FR_02_Short_name_and_id_number_are_unique_in_the_data_model()
    {
        using var db = CreateContext(new SaveProbe());
        var course = db.Model.FindEntityType(typeof(Course))!;
        Assert.True(course.GetIndexes().Single(x => x.Properties.Single().Name == nameof(Course.ShortName)).IsUnique);
        Assert.True(course.GetIndexes().Single(x => x.Properties.Single().Name == nameof(Course.IdNumber)).IsUnique);
    }

    [Fact]
    public void CRS_NFR_01_Same_user_cannot_be_enrolled_twice_in_the_same_course()
    {
        using var db = CreateContext(new SaveProbe());
        var index = db.Model.FindEntityType(typeof(CourseEnrolment))!.GetIndexes()
            .Single(x => x.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(CourseEnrolment.CourseId), nameof(CourseEnrolment.UserId) }));
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void CRS_FR_05_CRS_NFR_02_Course_deletion_removes_enrolments_but_categories_are_protected()
    {
        using var db = CreateContext(new SaveProbe());
        var enrolmentFk = db.Model.FindEntityType(typeof(CourseEnrolment))!.GetForeignKeys()
            .Single(x => x.PrincipalEntityType.ClrType == typeof(Course));
        Assert.Equal(DeleteBehavior.Cascade, enrolmentFk.DeleteBehavior);
        var courseCategoryFk = db.Model.FindEntityType(typeof(Course))!.GetForeignKeys()
            .Single(x => x.PrincipalEntityType.ClrType == typeof(CourseCategory));
        Assert.Equal(DeleteBehavior.Restrict, courseCategoryFk.DeleteBehavior); // A non-empty category is never cascaded.
    }

    [Theory]
    [InlineData(0, "Student")]
    [InlineData(-5, "Student")]
    [InlineData(42, "")]
    [InlineData(42, "Bad\tRole")]
    public async Task CRS_FR_06_Invalid_user_or_role_is_rejected_before_database_access(int userId, string role)
    {
        var save = new SaveProbe();
        await using var db = CreateContext(save);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new CrsService(db).EnrolAsync(new() { CourseId = 1, UserId = userId, Role = role }));

        Assert.Equal(0, save.Calls);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task CRS_FR_08_Unenrol_removes_only_the_requested_enrolment()
    {
        var save = new SaveProbe { SuppressSave = true };
        await using var db = CreateContext(save);
        var target = new CourseEnrolment { Id = 7, CourseId = 3, UserId = 42 };
        var other = new CourseEnrolment { Id = 8, CourseId = 3, UserId = 43 };
        db.AttachRange(target, other);

        await new CrsService(db).UnenrolAsync(7);

        Assert.Equal(1, save.PendingWrites);
        Assert.Equal(EntityState.Deleted, save.States[target]);
        Assert.False(save.States.ContainsKey(other));
    }

    [Theory]
    [InlineData("", "C1")]
    [InlineData("Course", " ")]
    [InlineData("Course\tName", "C1")]
    public async Task CRS_FR_01_CRS_FR_04_Invalid_course_is_rejected_before_database_access(string fullName, string shortName)
    {
        var save = new SaveProbe();
        await using var db = CreateContext(save);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new CrsService(db).SaveCourseAsync(new() { CategoryId = 1, FullName = fullName, ShortName = shortName }));

        Assert.Equal(0, save.Calls);
    }

    private static CrsDbContext CreateContext(SaveProbe save) => new(
        new DbContextOptionsBuilder<CrsDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=unit_test_never_connects;Username=unused;Password=unused")
            .AddInterceptors(new RejectConnection(), save).Options);

    private sealed class RejectConnection : DbConnectionInterceptor
    {
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
            => throw new InvalidOperationException("This unit test must not open a database connection.");
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("This unit test must not open a database connection.");
    }

    private sealed class SaveProbe : SaveChangesInterceptor
    {
        public bool SuppressSave { get; init; }
        public int Calls { get; private set; }
        public int PendingWrites { get; private set; }
        public Dictionary<object, EntityState> States { get; } = new(ReferenceEqualityComparer.Instance);
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Calls++;
            var db = eventData.Context!;
            db.ChangeTracker.DetectChanges();
            foreach (var entry in db.ChangeTracker.Entries()
                         .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
                States[entry.Entity] = entry.State;
            PendingWrites = States.Count;
            return ValueTask.FromResult(SuppressSave ? InterceptionResult<int>.SuppressWithResult(1) : result);
        }
    }
}
