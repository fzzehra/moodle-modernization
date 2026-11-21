using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Application.Courses;
using Moodle.Domain.Courses;
using static Moodle.IntegrationTests.CrsApiTestSupport;

namespace Moodle.IntegrationTests;

/// <summary>
/// YZ destekli (Claude Code), CRS-FR-05 / CRS-NFR-02: ders silindiğinde CRS kendi kayıtlarını aynı transaction'da
/// siler ve diğer modüllerin (ASG/GRD/FILE) kendi verilerini temizleyebilmesi için <c>course.deleted</c> olayını
/// kayıtlı <see cref="ICrsEventExtension"/> uzantılarına iletir. Uzantı burada test kaydedicisidir.
/// </summary>
public sealed class CrsCourseDeletionCleanupTests(CrsApiFixture fixture) : IClassFixture<CrsApiFixture>
{
    private sealed class Recorder : ICrsEventExtension
    {
        public string Id { get; } = "test-cleanup-" + Guid.NewGuid().ToString("N");
        public int Order => 0;
        public ConcurrentQueue<CrsChangeEvent> Events { get; } = new();
        public Task HandleAsync(CrsChangeEvent notification, CancellationToken ct = default)
        {
            Events.Enqueue(notification);
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CRS_FR_05_CRS_NFR_02_Deleted_course_removes_enrolments_and_notifies_other_modules(bool background)
    {
        var recorder = new Recorder();
        var settings = new Dictionary<string, string?>
        {
            ["Crs:EventDispatch:IntervalMilliseconds"] = "50",
            ["Crs:BackgroundDeletion:Enabled"] = background ? "true" : "false",
            ["Crs:BackgroundDeletion:PollIntervalMilliseconds"] = "50",
        };
        await using var factory = fixture.CreateFactory(settings: settings)
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddSingleton<ICrsEventExtension>(recorder)));
        var client = factory.CreateClient();
        var category = await CreateCategoryAsync(client);
        var course = await CreateCourseAsync(client, category.Id);
        var enrolment = await (await client.PostAsJsonAsync($"/api/v1/courses/{course.Id}/enrolments", new { userId = 77 }))
            .Content.ReadFromJsonAsync<CourseEnrolment>();

        var response = await client.DeleteAsync($"/api/v1/courses/{course.Id}");
        Assert.Equal(background ? HttpStatusCode.Accepted : HttpStatusCode.NoContent, response.StatusCode);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var poll = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        // Both events commit in the deletion transaction, but the dispatcher hands them to the extension one by one
        // (ordered by event Id). Wait for both deliveries instead of asserting between two HandleAsync calls.
        bool Delivered() =>
            recorder.Events.Any(x => x.Kind == "course.deleted" && x.CourseId == course.Id) &&
            recorder.Events.Any(x => x.Kind == "enrolment.deleted" && x.EnrolmentId == enrolment!.Id);
        while (!Delivered())
        {
            try { await poll.WaitForNextTickAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                Assert.Fail("course.deleted and enrolment.deleted were not both delivered to the extension. Received: "
                    + string.Join(", ", recorder.Events.Select(x => $"{x.Kind}#{x.Id}")));
            }
        }

        var deleted = recorder.Events.Single(x => x.Kind == "course.deleted" && x.CourseId == course.Id);
        Assert.Null(deleted.EnrolmentId);
        Assert.Contains(recorder.Events, x => x.Kind == "enrolment.deleted" && x.EnrolmentId == enrolment!.Id);
        await AssertErrorAsync(await client.GetAsync($"/api/v1/courses/{course.Id}"), HttpStatusCode.NotFound);
    }
}
