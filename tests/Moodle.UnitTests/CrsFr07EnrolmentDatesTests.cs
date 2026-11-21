using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moodle.Application.Courses;
using Moodle.Domain.Courses;
using Moodle.Infrastructure.Courses;

namespace Moodle.UnitTests;

// YZ destekli (Claude Code): CRS-FR-07 kayıt başlangıç/bitiş zamanı güncelleme birim testleri.
public sealed class CrsFr07EnrolmentDatesTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2027, 1, 31, 17, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CRS_FR_07_End_not_after_start_is_rejected(int endOffsetHours) =>
        Assert.Throws<ArgumentException>(() =>
            CourseValidation.ValidateEnrolmentDates(Start, Start.AddHours(endOffsetHours)));

    [Fact]
    public void CRS_FR_07_Open_or_valid_ranges_are_accepted()
    {
        CourseValidation.ValidateEnrolmentDates(null, null);
        CourseValidation.ValidateEnrolmentDates(Start, null);
        CourseValidation.ValidateEnrolmentDates(null, End);
        CourseValidation.ValidateEnrolmentDates(Start, End);
    }

    [Fact]
    public async Task CRS_FR_07_Date_update_changes_only_dates_and_keeps_status_role_course_and_user()
    {
        var save = new SaveProbe { SuppressSave = true };
        await using var db = CreateContext(save);
        var enrolment = new CourseEnrolment { Id = 7, CourseId = 3, UserId = 42, Suspended = true, Role = "Teacher" };
        db.Attach(enrolment);

        await new CrsService(db).UpdateEnrolmentAsync(7, true, "Teacher", Start.ToOffset(TimeSpan.FromHours(3)), End);

        Assert.Equal(new[] { "EndsAt", "StartsAt" }, save.ModifiedProperties.OrderBy(x => x));
        Assert.Equal(Start, enrolment.StartsAt);
        Assert.Equal(TimeSpan.Zero, enrolment.StartsAt!.Value.Offset); // Normalized to UTC as on creation.
        Assert.Equal(End, enrolment.EndsAt);
        Assert.True(enrolment.Suspended);
        Assert.Equal("Teacher", enrolment.Role);
        Assert.Equal(3, enrolment.CourseId);
        Assert.Equal(42, enrolment.UserId);
    }

    [Fact]
    public async Task CRS_FR_07_Invalid_range_is_rejected_without_changing_or_saving_the_enrolment()
    {
        var save = new SaveProbe();
        await using var db = CreateContext(save);
        var enrolment = new CourseEnrolment { Id = 7, CourseId = 3, UserId = 42, Role = "Student", StartsAt = Start, EndsAt = End };
        db.Attach(enrolment);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new CrsService(db).UpdateEnrolmentAsync(7, true, "Teacher", End, Start));

        Assert.Equal(0, save.Calls);
        Assert.Equal(EntityState.Unchanged, db.Entry(enrolment).State);
        Assert.Equal(Start, enrolment.StartsAt);
        Assert.Equal(End, enrolment.EndsAt);
        Assert.False(enrolment.Suspended);
        Assert.Equal("Student", enrolment.Role);
    }

    [Fact]
    public async Task CRS_FR_07_Status_role_update_without_dates_keeps_existing_dates()
    {
        var save = new SaveProbe { SuppressSave = true };
        await using var db = CreateContext(save);
        var enrolment = new CourseEnrolment { Id = 7, CourseId = 3, UserId = 42, Role = "Student", StartsAt = Start, EndsAt = End };
        db.Attach(enrolment);

        await new CrsService(db).UpdateEnrolmentAsync(7, true, "Student");

        Assert.Equal(new[] { "Suspended" }, save.ModifiedProperties);
        Assert.Equal(Start, enrolment.StartsAt);
        Assert.Equal(End, enrolment.EndsAt);
    }

    [Fact]
    public async Task CRS_FR_07_Same_instant_in_another_offset_is_not_a_change()
    {
        var save = new SaveProbe();
        await using var db = CreateContext(save);
        var enrolment = new CourseEnrolment { Id = 7, CourseId = 3, UserId = 42, Role = "Student", StartsAt = Start, EndsAt = End };
        db.Attach(enrolment);

        await new CrsService(db).UpdateEnrolmentAsync(7, false, "Student",
            Start.ToOffset(TimeSpan.FromHours(3)), End.ToOffset(TimeSpan.FromHours(-5)));

        Assert.Equal(EntityState.Unchanged, db.Entry(enrolment).State);
        Assert.Equal(0, save.PendingWrites); // CRS-NFR-05 is preserved for date updates.
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
            return ValueTask.FromResult(SuppressSave ? InterceptionResult<int>.SuppressWithResult(1) : result);
        }
    }
}
