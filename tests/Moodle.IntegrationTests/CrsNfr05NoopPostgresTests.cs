using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moodle.Domain.Courses;
using Moodle.Infrastructure.Courses;
using Xunit.Abstractions;

namespace Moodle.IntegrationTests;

public sealed class CrsNfr05NoopPostgresTests(CrsPostgresFixture fixture, ITestOutputHelper output)
    : IClassFixture<CrsPostgresFixture>
{
    [Theory]
    [InlineData("category", false)]
    [InlineData("category", true)]
    [InlineData("course", false)]
    [InlineData("course", true)]
    [InlineData("enrolment", false)]
    [InlineData("enrolment", true)]
    public async Task CRS_NFR_05_Identical_updates_execute_zero_DML(string entity, bool preload)
    {
        var data = await CreateDataAsync();
        var writes = new DmlCounter();
        await using (var db = CreateMeasuredContext(writes))
        {
            if (preload)
            {
                await db.Categories.FindAsync(data.Category.Id);
                await db.Courses.FindAsync(data.Course.Id);
                await db.Enrolments.FindAsync(data.Enrolment.Id);
            }
            await ApplyAsync(new CrsService(db), data, entity);
            AssertDml(writes, 0, 0, 0, $"identical {entity}, preload={preload}");
            Assert.False(db.ChangeTracker.HasChanges());
        }
        await AssertPersistedAsync(data);
    }

    [Theory]
    [InlineData("category-name")]
    [InlineData("category-parent")]
    [InlineData("course-name")]
    [InlineData("course-visible")]
    [InlineData("course-date")]
    [InlineData("course-id-clear")]
    [InlineData("enrolment-status")]
    [InlineData("enrolment-role")]
    [InlineData("enrolment-both")]
    public async Task CRS_NFR_05_Changed_values_write_once_and_repeating_them_writes_nothing(string scenario)
    {
        var original = await CreateDataAsync();
        var expected = Changed(original, scenario);
        var first = new DmlCounter();
        await using (var db = CreateMeasuredContext(first))
        {
            await ApplyAsync(new CrsService(db), expected, scenario);
            AssertDml(first, 0, 1, 0, $"changed {scenario}");
        }
        // Verify committed values through an independent context, not the measured change tracker.
        await AssertPersistedAsync(expected);

        var repeated = new DmlCounter();
        await using (var db = CreateMeasuredContext(repeated))
        {
            await ApplyAsync(new CrsService(db), expected, scenario);
            AssertDml(repeated, 0, 0, 0, $"repeated {scenario}");
        }
        await AssertPersistedAsync(expected);
    }

    [Fact]
    public async Task CRS_NFR_05_Equivalent_dates_and_blank_identifier_do_not_create_a_write()
    {
        var data = await CreateDataAsync(noIdentifier: true);
        var writes = new DmlCounter();
        await using (var db = CreateMeasuredContext(writes))
        {
            var request = Copy(data.Course);
            request.IdNumber = "   ";
            request.StartDate = request.StartDate!.Value.ToOffset(TimeSpan.FromHours(3));
            request.EndDate = request.EndDate!.Value.ToOffset(TimeSpan.FromHours(3));
            await new CrsService(db).SaveCourseAsync(request);
            AssertDml(writes, 0, 0, 0, "equivalent UTC dates and blank identifier");
        }
        await AssertPersistedAsync(data);
    }

    [Fact]
    public async Task CRS_NFR_05_Measurement_control_observes_insert_update_and_delete()
    {
        var writes = new DmlCounter();
        await using var db = CreateMeasuredContext(writes);
        var service = new CrsService(db);
        var category = await service.SaveCategoryAsync(new() { Name = "Counter " + Guid.NewGuid() });
        AssertDml(writes, 1, 0, 0, "counter INSERT control");
        await service.SaveCategoryAsync(new() { Id = category.Id, Name = category.Name + " updated" });
        AssertDml(writes, 1, 1, 0, "counter UPDATE control");
        await service.DeleteCategoryAsync(category.Id);
        AssertDml(writes, 1, 1, 1, "counter DELETE control");
    }

    private CrsDbContext CreateMeasuredContext(DmlCounter counter)
    {
        using var source = fixture.CreateContext();
        return new(new DbContextOptionsBuilder<CrsDbContext>()
            .UseNpgsql(source.Database.GetConnectionString()).AddInterceptors(counter).Options);
    }

    private async Task<Data> CreateDataAsync(bool noIdentifier = false)
    {
        // Isolated test-container setup is outside the measured context.
        await using var db = fixture.CreateContext();
        var parent = new CourseCategory { Name = "Parent " + Guid.NewGuid() };
        var otherParent = new CourseCategory { Name = "Other parent " + Guid.NewGuid() };
        db.Categories.AddRange(parent, otherParent);
        await db.SaveChangesAsync();
        var category = new CourseCategory { Name = "Category " + Guid.NewGuid(), ParentId = parent.Id };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        var course = new Course
        {
            CategoryId = category.Id, FullName = "No-op course", ShortName = Guid.NewGuid().ToString(),
            IdNumber = noIdentifier ? null : Guid.NewGuid().ToString(), Visible = true,
            StartDate = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero),
            EndDate = new(2026, 12, 1, 9, 0, 0, TimeSpan.Zero)
        };
        db.Courses.Add(course);
        await db.SaveChangesAsync();
        var enrolment = new CourseEnrolment
        {
            CourseId = course.Id, UserId = 42, Role = "Student", Suspended = false,
            StartsAt = course.StartDate, EndsAt = course.EndDate
        };
        db.Enrolments.Add(enrolment);
        await db.SaveChangesAsync();
        return new(category, course, enrolment, otherParent.Id);
    }

    private static async Task ApplyAsync(CrsService service, Data data, string scenario)
    {
        if (scenario.StartsWith("category", StringComparison.Ordinal))
            await service.SaveCategoryAsync(new() { Id = data.Category.Id, Name = data.Category.Name, ParentId = data.Category.ParentId });
        else if (scenario.StartsWith("course", StringComparison.Ordinal))
            await service.SaveCourseAsync(Copy(data.Course));
        else if (scenario.StartsWith("enrolment", StringComparison.Ordinal))
            await service.UpdateEnrolmentAsync(data.Enrolment.Id, data.Enrolment.Suspended, data.Enrolment.Role);
        else throw new ArgumentOutOfRangeException(nameof(scenario));
    }

    private static Data Changed(Data source, string scenario)
    {
        var category = new CourseCategory { Id = source.Category.Id, Name = source.Category.Name, ParentId = source.Category.ParentId };
        var course = Copy(source.Course);
        var enrolment = new CourseEnrolment
        {
            Id = source.Enrolment.Id, CourseId = source.Enrolment.CourseId, UserId = source.Enrolment.UserId,
            Role = source.Enrolment.Role, Suspended = source.Enrolment.Suspended,
            StartsAt = source.Enrolment.StartsAt, EndsAt = source.Enrolment.EndsAt
        };
        switch (scenario)
        {
            case "category-name": category.Name += " updated"; break;
            case "category-parent": category.ParentId = source.OtherParentId; break;
            case "course-name": course.FullName += " updated"; break;
            case "course-visible": course.Visible = false; break;
            case "course-date": course.EndDate = course.EndDate!.Value.AddDays(1); break;
            case "course-id-clear": course.IdNumber = null; break;
            case "enrolment-status": enrolment.Suspended = true; break;
            case "enrolment-role": enrolment.Role = "Teacher"; break;
            case "enrolment-both": enrolment.Suspended = true; enrolment.Role = "Teacher"; break;
            default: throw new ArgumentOutOfRangeException(nameof(scenario));
        }
        return new(category, course, enrolment, source.OtherParentId);
    }

    private static Course Copy(Course value) => new()
    {
        Id = value.Id, CategoryId = value.CategoryId, FullName = value.FullName, ShortName = value.ShortName,
        IdNumber = value.IdNumber, Visible = value.Visible, StartDate = value.StartDate, EndDate = value.EndDate
    };

    private async Task AssertPersistedAsync(Data expected)
    {
        await using var db = fixture.CreateContext();
        var category = await db.Categories.AsNoTracking().SingleAsync(x => x.Id == expected.Category.Id);
        Assert.Equal(expected.Category.Name, category.Name);
        Assert.Equal(expected.Category.ParentId, category.ParentId);
        var course = await db.Courses.AsNoTracking().SingleAsync(x => x.Id == expected.Course.Id);
        Assert.Equal(expected.Course.CategoryId, course.CategoryId);
        Assert.Equal(expected.Course.FullName, course.FullName);
        Assert.Equal(expected.Course.ShortName, course.ShortName);
        Assert.Equal(expected.Course.IdNumber, course.IdNumber);
        Assert.Equal(expected.Course.Visible, course.Visible);
        Assert.Equal(expected.Course.StartDate, course.StartDate);
        Assert.Equal(expected.Course.EndDate, course.EndDate);
        var enrolment = await db.Enrolments.AsNoTracking().SingleAsync(x => x.Id == expected.Enrolment.Id);
        Assert.Equal(expected.Enrolment.CourseId, enrolment.CourseId);
        Assert.Equal(expected.Enrolment.UserId, enrolment.UserId);
        Assert.Equal(expected.Enrolment.Role, enrolment.Role);
        Assert.Equal(expected.Enrolment.Suspended, enrolment.Suspended);
        Assert.Equal(expected.Enrolment.StartsAt, enrolment.StartsAt);
        Assert.Equal(expected.Enrolment.EndsAt, enrolment.EndsAt);
    }

    private void AssertDml(DmlCounter counter, int inserts, int updates, int deletes, string scenario)
    {
        output.WriteLine($"{scenario}: INSERT={counter.Inserts}, UPDATE={counter.Updates}, DELETE={counter.Deletes}");
        Assert.Equal(inserts, counter.Inserts);
        Assert.Equal(updates, counter.Updates);
        Assert.Equal(deletes, counter.Deletes);
    }

    private sealed record Data(CourseCategory Category, Course Course, CourseEnrolment Enrolment, int OtherParentId);

    private sealed class DmlCounter : DbCommandInterceptor
    {
        private static readonly Regex Statements = new(@"^\s*(?<verb>INSERT|UPDATE|DELETE)\b",
            RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        public int Inserts { get; private set; }
        public int Updates { get; private set; }
        public int Deletes { get; private set; }

        private void Record(DbCommand command)
        {
            // Count statements, not affected-row counts; SELECTs and transaction commands are not writes.
            // Npgsql also executes INSERT/UPDATE/DELETE with RETURNING through ExecuteReader.
            foreach (Match match in Statements.Matches(command.CommandText))
                switch (match.Groups["verb"].Value.ToUpperInvariant())
                {
                    case "INSERT": Inserts++; break;
                    case "UPDATE": Updates++; break;
                    case "DELETE": Deletes++; break;
                }
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        { Record(command); return result; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Record(command); return ValueTask.FromResult(result); }
        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        { Record(command); return result; }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { Record(command); return ValueTask.FromResult(result); }
        public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        { Record(command); return result; }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<object> result, CancellationToken cancellationToken = default)
        { Record(command); return ValueTask.FromResult(result); }
    }
}
