using Microsoft.EntityFrameworkCore;
using Moodle.Domain.Grading;
using Moodle.Infrastructure.Grading;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Moodle.IntegrationTests;

public sealed class GrdPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    public GrdDbContext CreateContext() => new(
        new DbContextOptionsBuilder<GrdDbContext>()
            .UseNpgsql(_container.GetConnectionString()).Options);

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

/// <summary>GRD şemasının veritabanı düzeyindeki kısıtları (WBS-2 §3.5.1).</summary>
public sealed class GrdPostgresTests(GrdPostgresFixture fixture) : IClassFixture<GrdPostgresFixture>
{
    private static int _nextId = 1000;
    private static int NextId() => Interlocked.Increment(ref _nextId);

    private static GradeItem Item(int courseId, int? assignmentId = null, decimal min = 0m, decimal max = 100m,
        int? categoryId = null) => new()
    {
        CourseId = courseId, CategoryId = categoryId, AssignmentId = assignmentId,
        Type = assignmentId.HasValue ? GradeItemType.Assignment : GradeItemType.Manual,
        Name = "Kalem " + Guid.NewGuid(), MinScore = min, MaxScore = max
    };

    private static AssignmentGrade Assessment(int assignmentId, int learnerId, int attempt, decimal? score) => new()
    {
        AssignmentId = assignmentId, LearnerId = learnerId, Attempt = attempt, GraderId = 2,
        Score = score, GradedAt = DateTimeOffset.UtcNow
    };

    private static async Task AssertSqlStateAsync(DbContext db, string sqlState)
    {
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(sqlState, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task Migration_is_applied_and_creates_grd_tables()
    {
        await using var db = fixture.CreateContext();
        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(), m => m.EndsWith("_InitialGrd"));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());

        var tables = await db.Database.SqlQuery<string>(
                $"SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_name LIKE 'grd\\_%'")
            .ToListAsync();
        Assert.Equal(
            ["grd_assignment_grades", "grd_categories", "grd_grades", "grd_items"],
            tables.Order());
    }

    [Theory]
    [InlineData("assignment-grade")]
    [InlineData("learner-grade")]
    [InlineData("assignment-item")]
    public async Task Unique_constraints_reject_duplicates(string constraint)
    {
        await using var db = fixture.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var assignmentId = NextId();

        switch (constraint)
        {
            case "assignment-grade":
                db.AssignmentGrades.Add(Assessment(assignmentId, 3, 0, 85m));
                await db.SaveChangesAsync();
                db.AssignmentGrades.Add(Assessment(assignmentId, 3, 0, 90m));
                break;
            case "learner-grade":
                var item = Item(1);
                db.Items.Add(item);
                await db.SaveChangesAsync();
                db.LearnerGrades.Add(new() { ItemId = item.Id, LearnerId = 3, UpdatedAt = DateTimeOffset.UtcNow });
                await db.SaveChangesAsync();
                db.LearnerGrades.Add(new() { ItemId = item.Id, LearnerId = 3, UpdatedAt = DateTimeOffset.UtcNow });
                break;
            default:
                db.Items.Add(Item(1, assignmentId));
                await db.SaveChangesAsync();
                db.Items.Add(Item(1, assignmentId));
                break;
        }

        await AssertSqlStateAsync(db, PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Unique_constraints_allow_new_attempts_other_learners_and_unlinked_items()
    {
        await using var db = fixture.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var assignmentId = NextId();

        db.AssignmentGrades.AddRange(
            Assessment(assignmentId, 3, 0, 70m),
            Assessment(assignmentId, 3, 1, 80m),
            Assessment(assignmentId, 4, 0, 90m));
        // AssignmentId boş olan (manuel) kalemler benzersizlik filtresine girmez.
        db.Items.AddRange(Item(1), Item(1), Item(1, assignmentId));
        await db.SaveChangesAsync();

        Assert.Equal(3, await db.AssignmentGrades.CountAsync(x => x.AssignmentId == assignmentId));
    }

    [Theory]
    [InlineData(0, 100, true)]
    [InlineData(50, 50, true)]
    [InlineData(-10, 10, true)]
    [InlineData(100, 0, false)]
    [InlineData(10.00001, 10, false)]
    public async Task Check_constraint_requires_min_score_not_above_max_score(double min, double max, bool accepted)
    {
        await using var db = fixture.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Items.Add(Item(1, min: (decimal)min, max: (decimal)max));

        if (accepted)
            Assert.Equal(1, await db.SaveChangesAsync());
        else
            await AssertSqlStateAsync(db, PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task Empty_score_is_stored_separately_from_zero_with_fixed_precision()
    {
        var assignmentId = NextId();
        int itemId;
        await using (var db = fixture.CreateContext())
        {
            db.AssignmentGrades.AddRange(
                Assessment(assignmentId, 3, 0, null),
                Assessment(assignmentId, 4, 0, 0m),
                Assessment(assignmentId, 5, 0, 85.12345m));
            var item = Item(1, assignmentId);
            db.Items.Add(item);
            await db.SaveChangesAsync();
            itemId = item.Id;
            db.LearnerGrades.AddRange(
                new LearnerGrade { ItemId = itemId, LearnerId = 3, UpdatedAt = DateTimeOffset.UtcNow },
                new LearnerGrade { ItemId = itemId, LearnerId = 4, RawScore = 0m, FinalScore = 0m, UpdatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        await using var read = fixture.CreateContext();
        var scores = await read.AssignmentGrades.Where(x => x.AssignmentId == assignmentId)
            .ToDictionaryAsync(x => x.LearnerId, x => x.Score);
        Assert.Null(scores[3]);
        Assert.Equal(0m, scores[4]);
        Assert.Equal(85.12345m, scores[5]);

        var grades = await read.LearnerGrades.Where(x => x.ItemId == itemId).ToDictionaryAsync(x => x.LearnerId);
        Assert.Null(grades[3].FinalScore);
        Assert.Equal(0m, grades[4].FinalScore);
    }

    [Fact]
    public async Task Version_is_a_concurrency_token()
    {
        var assignmentId = NextId();
        await using var first = fixture.CreateContext();
        var assessment = Assessment(assignmentId, 3, 0, 50m);
        first.AssignmentGrades.Add(assessment);
        await first.SaveChangesAsync();

        await using (var second = fixture.CreateContext())
        {
            var concurrent = await second.AssignmentGrades.SingleAsync(x => x.Id == assessment.Id);
            concurrent.Score = 60m;
            concurrent.Version++;
            await second.SaveChangesAsync();
        }

        assessment.Score = 70m;
        assessment.Version++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => first.SaveChangesAsync());

        await using var read = fixture.CreateContext();
        Assert.Equal(60m, (await read.AssignmentGrades.SingleAsync(x => x.Id == assessment.Id)).Score);
    }

    [Theory]
    [InlineData("item-category")]
    [InlineData("parent-category")]
    public async Task Category_relations_stay_within_the_same_course(string relation)
    {
        await using var db = fixture.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var category = new GradeCategory { CourseId = 1, Name = "Ödevler" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        // Aynı ders kabul edilir.
        db.Items.Add(Item(1, categoryId: category.Id));
        db.Categories.Add(new() { CourseId = 1, ParentId = category.Id, Name = "Alt" });
        await db.SaveChangesAsync();

        if (relation == "item-category")
            db.Items.Add(Item(2, categoryId: category.Id));
        else
            db.Categories.Add(new() { CourseId = 2, ParentId = category.Id, Name = "Başka ders" });

        await AssertSqlStateAsync(db, PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task Learner_grade_requires_existing_item()
    {
        await using var db = fixture.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.LearnerGrades.Add(new() { ItemId = int.MaxValue, LearnerId = 3, UpdatedAt = DateTimeOffset.UtcNow });
        await AssertSqlStateAsync(db, PostgresErrorCodes.ForeignKeyViolation);
    }
}
