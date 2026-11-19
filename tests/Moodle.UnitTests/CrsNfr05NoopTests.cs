using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moodle.Domain.Courses;
using Moodle.Infrastructure.Courses;

namespace Moodle.UnitTests;

public sealed class CrsNfr05NoopTests
{
    [Theory]
    [InlineData(false, "Student")]
    [InlineData(true, "Teacher")]
    public async Task CRS_NFR_05_Unchanged_enrolment_stays_unchanged_without_opening_a_connection(bool suspended, string role)
    {
        var connection = new RejectConnection();
        var save = new SaveProbe();
        await using var db = CreateContext(connection, save);
        var enrolment = new CourseEnrolment { Id = 7, CourseId = 3, UserId = 42, Suspended = suspended, Role = role };
        db.Attach(enrolment);

        await new CrsService(db).UpdateEnrolmentAsync(7, suspended, new string(role.ToCharArray()));

        Assert.Equal(EntityState.Unchanged, db.Entry(enrolment).State);
        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Empty(save.ModifiedProperties);
        Assert.Equal(0, save.PendingWrites);
        Assert.Equal(0, connection.Attempts);
    }

    [Theory]
    [InlineData(true, "Student", "Suspended")]
    [InlineData(false, "Teacher", "Role")]
    [InlineData(true, "Teacher", "Role,Suspended")]
    public async Task CRS_NFR_05_Real_enrolment_change_marks_only_requested_properties(bool suspended, string role, string properties)
    {
        var connection = new RejectConnection();
        var save = new SaveProbe { SuppressSave = true };
        await using var db = CreateContext(connection, save);
        var enrolment = new CourseEnrolment { Id = 7, CourseId = 3, UserId = 42, Role = "Student" };
        db.Attach(enrolment);

        await new CrsService(db).UpdateEnrolmentAsync(7, suspended, role);

        Assert.Equal(1, save.PendingWrites);
        Assert.Equal(properties.Split(',').OrderBy(x => x), save.ModifiedProperties.OrderBy(x => x));
        Assert.Equal(suspended, enrolment.Suspended);
        Assert.Equal(role, enrolment.Role);
        Assert.Equal(3, enrolment.CourseId);
        Assert.Equal(42, enrolment.UserId);
        Assert.Equal(0, connection.Attempts);
    }

    [Fact]
    public async Task CRS_NFR_05_Unchanged_category_stays_unchanged_without_opening_a_connection()
    {
        var connection = new RejectConnection();
        var save = new SaveProbe();
        await using var db = CreateContext(connection, save);
        var category = new CourseCategory { Id = 3, Name = "Category" };
        db.Attach(category);

        await new CrsService(db).SaveCategoryAsync(new() { Id = 3, Name = "Category" });

        Assert.Equal(EntityState.Unchanged, db.Entry(category).State);
        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Equal(0, save.PendingWrites);
        Assert.Equal(0, connection.Attempts);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\t")]
    public async Task CRS_NFR_05_Unchanged_values_do_not_bypass_role_validation(string invalidRole)
    {
        var connection = new RejectConnection();
        var save = new SaveProbe();
        await using var db = CreateContext(connection, save);
        // A tracked legacy/invalid value must still be rejected even when the input is identical.
        db.Attach(new CourseEnrolment { Id = 7, CourseId = 3, UserId = 42, Role = invalidRole });

        await Assert.ThrowsAsync<ArgumentException>(() => new CrsService(db).UpdateEnrolmentAsync(7, false, invalidRole));

        Assert.Equal(0, save.Calls);
        Assert.Equal(0, connection.Attempts);
    }

    private static CrsDbContext CreateContext(RejectConnection connection, SaveProbe save) => new(
        new DbContextOptionsBuilder<CrsDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=unit_test_never_connects;Username=unused;Password=unused")
            .AddInterceptors(connection, save).Options);

    private sealed class RejectConnection : DbConnectionInterceptor
    {
        public int Attempts { get; private set; }
        private InvalidOperationException UnexpectedConnection()
        {
            Attempts++;
            return new("This unit test must not open a database connection.");
        }
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
            => throw UnexpectedConnection();
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) => throw UnexpectedConnection();
    }

    private sealed class SaveProbe : SaveChangesInterceptor
    {
        public bool SuppressSave { get; init; }
        public int Calls { get; private set; }
        public int PendingWrites { get; private set; }
        public string[] ModifiedProperties { get; private set; } = [];
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Calls++;
            var db = eventData.Context!;
            db.ChangeTracker.DetectChanges();
            var changed = db.ChangeTracker.Entries()
                .Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
            PendingWrites = changed.Length;
            ModifiedProperties = changed.SelectMany(x => x.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name)).ToArray();
            // Only positive unit cases suppress persistence; real SQL is verified separately against PostgreSQL.
            return ValueTask.FromResult(SuppressSave ? InterceptionResult<int>.SuppressWithResult(1) : result);
        }
    }
}
