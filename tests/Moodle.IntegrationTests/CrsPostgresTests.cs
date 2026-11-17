using Microsoft.EntityFrameworkCore;
using Moodle.Domain.Courses;
using Moodle.Infrastructure.Courses;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Moodle.IntegrationTests;

public sealed class CrsPostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    public CrsDbContext CreateContext() => new(
        new DbContextOptionsBuilder<CrsDbContext>()
            .UseNpgsql(_container.GetConnectionString()).Options);

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

public sealed class CrsPostgresTests(CrsPostgresFixture fixture) : IClassFixture<CrsPostgresFixture>
{
    [Fact]
    public async Task Migration_and_course_category_enrolment_CRUD_work_on_PostgreSQL()
    {
        await using var db = fixture.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var service = new CrsService(db);

        var category = await service.SaveCategoryAsync(new() { Name = "CRS" });
        await service.SaveCategoryAsync(new() { Id = category.Id, Name = "Courses" });
        Assert.Contains(await service.ListCategoriesAsync(), x => x.Id == category.Id && x.Name == "Courses");

        var course = await service.SaveCourseAsync(new()
        {
            CategoryId = category.Id, FullName = "Database course", ShortName = "DB101"
        });
        await service.SaveCourseAsync(new()
        {
            Id = course.Id, CategoryId = category.Id, FullName = "Updated course", ShortName = "DB101"
        });
        Assert.Equal("Updated course", (await service.FindAsync(course.Id))!.FullName);
        Assert.Contains(await service.ListCoursesAsync(), x => x.Id == course.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteCategoryAsync(category.Id));

        var enrolment = await service.EnrolAsync(new() { CourseId = course.Id, UserId = 42 });
        Assert.True(await service.IsActivelyEnrolledAsync(42, course.Id));
        Assert.Contains(42, await service.ListEnrolledUserIdsAsync(course.Id));
        await service.UpdateEnrolmentAsync(enrolment.Id, true, "Teacher");
        Assert.False(await service.IsActivelyEnrolledAsync(42, course.Id));
        Assert.Empty(await service.ListEnrolledUserIdsAsync(course.Id));
        Assert.Equal("Teacher", Assert.Single(await service.ListEnrolmentsAsync(course.Id)).Role);
        await service.UnenrolAsync(enrolment.Id);
        Assert.Empty(await service.ListEnrolmentsAsync(course.Id));

        await service.EnrolAsync(new() { CourseId = course.Id, UserId = 43 });
        await service.DeleteCourseAsync(course.Id);
        Assert.Null(await service.FindAsync(course.Id));
        Assert.Empty(await service.ListEnrolmentsAsync(course.Id));
        await service.DeleteCategoryAsync(category.Id);
        Assert.DoesNotContain(await service.ListCategoriesAsync(), x => x.Id == category.Id);
    }

    [Theory]
    [InlineData("short-name")]
    [InlineData("id-number")]
    [InlineData("enrolment")]
    public async Task Unique_constraints_reject_duplicates(string constraint)
    {
        await using var db = fixture.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var category = new CourseCategory { Name = "Constraints" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        var course = new Course
        {
            CategoryId = category.Id, FullName = "Original", ShortName = "UNIQUE", IdNumber = "ID1"
        };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        if (constraint == "enrolment")
        {
            db.Enrolments.Add(new() { CourseId = course.Id, UserId = 42 });
            await db.SaveChangesAsync();
            db.Enrolments.Add(new() { CourseId = course.Id, UserId = 42 });
        }
        else
        {
            db.Courses.Add(new()
            {
                CategoryId = category.Id, FullName = "Duplicate",
                ShortName = constraint == "short-name" ? "UNIQUE" : "OTHER",
                IdNumber = constraint == "id-number" ? "ID1" : null
            });
        }

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    [Fact]
    public async Task Category_foreign_key_rejects_missing_parent()
    {
        await using var db = fixture.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Categories.Add(new() { Name = "Orphan", ParentId = int.MaxValue });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }
}
