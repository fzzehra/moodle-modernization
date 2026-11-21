using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Application.Files;
using Moodle.Application.Errors;
using Moodle.Infrastructure.Courses;
using Moodle.Infrastructure.Files;
using System.Text;

namespace Moodle.IntegrationTests;

public sealed class CourseFileDeletionRaceTests(V2Fixture fixture) : IClassFixture<V2Fixture>
{
    [Fact]
    public async Task Real_course_delete_wins_against_waiting_commit_without_losing_the_draft()
    {
        using var scope = fixture.Services.CreateScope();
        var files = scope.ServiceProvider.GetRequiredService<IFileService>();
        var draft = await files.CreateDraftAsync(fixture.TeacherId);
        var uploaded = await files.AddToDraftAsync(fixture.TeacherId, draft.DraftId, "race.txt", new MemoryStream(Encoding.UTF8.GetBytes("race")));
        using var deleting = fixture.Services.CreateScope();
        var crs = deleting.ServiceProvider.GetRequiredService<CrsDbContext>();
        await using var tx = await crs.Database.BeginTransactionAsync();
        await crs.Database.ExecuteSqlAsync($"SELECT \"Id\" FROM crs_courses WHERE \"Id\"={fixture.CourseId} FOR UPDATE");
        var committing = files.CommitAsync(fixture.TeacherId, draft.DraftId, CourseFilePolicy.AreaFor(fixture.CourseId));
        try
        {
            using var observing = fixture.Services.CreateScope();
            var observer = observing.ServiceProvider.GetRequiredService<CrsDbContext>();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (await observer.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock' AND query LIKE '%FOR KEY SHARE%'").SingleAsync(timeout.Token) == 0)
                await Task.Delay(25, timeout.Token);
            await crs.Courses.Where(x => x.Id == fixture.CourseId).ExecuteDeleteAsync();
            await tx.CommitAsync();
        }
        finally { if (crs.Database.CurrentTransaction is not null) await tx.RollbackAsync(); }
        var error = await Assert.ThrowsAsync<AppException>(() => committing);
        Assert.Equal(404, error.StatusCode);
        var draftEntries = await files.ListDraftAsync(fixture.TeacherId, draft.DraftId);
        Assert.Single(draftEntries);
        Assert.Equal(uploaded.ContentHash, draftEntries[0].ContentHash);
    }
}
