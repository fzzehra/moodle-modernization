using Microsoft.Extensions.Configuration;
using Moodle.Application.Courses;
using Moodle.Infrastructure.Courses;
using Npgsql;

namespace Moodle.UnitTests;

// YZ destekli (Claude Code): CRS-FR-11 / CRS-NFR-06 birim testleri.
public sealed class CrsBackgroundDeletionTests
{
    private static CrsBackgroundDeletionOptions Options(params (string Key, string Value)[] values) =>
        CrsBackgroundDeletionOptions.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value))).Build());

    [Fact]
    public void CRS_FR_11_Background_deletion_is_disabled_by_default()
    {
        var options = Options();
        Assert.False(options.Enabled);
        Assert.Equal(TimeSpan.FromSeconds(5), options.PollInterval);
        Assert.Equal(3, options.MaxAttempts);
    }

    [Fact]
    public void CRS_NFR_06_Configuration_enables_deletion_and_sets_poll_interval_and_attempts()
    {
        var options = Options(("Crs:BackgroundDeletion:Enabled", "true"),
            ("Crs:BackgroundDeletion:PollIntervalMilliseconds", "50"), ("Crs:BackgroundDeletion:MaxAttempts", "5"));
        Assert.True(options.Enabled);
        Assert.Equal(TimeSpan.FromMilliseconds(50), options.PollInterval);
        Assert.Equal(5, options.MaxAttempts);
    }

    [Theory]
    [InlineData("Crs:BackgroundDeletion:PollIntervalMilliseconds", "9")]
    [InlineData("Crs:BackgroundDeletion:PollIntervalMilliseconds", "0")]
    [InlineData("Crs:BackgroundDeletion:MaxAttempts", "0")]
    public void CRS_NFR_06_Invalid_configuration_is_rejected(string key, string value) =>
        Assert.Throws<InvalidOperationException>(() => Options((key, value)));

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void CRS_NFR_03_Operation_block_in_worker_is_permanent_failure_without_retry(int attempts)
    {
        var outcome = CrsCourseDeletionRules.Classify(
            new CrsConflictException("course_operation_in_progress", "secret detail"), attempts, 3);
        Assert.Equal(CrsCourseDeletionJobState.Failed, outcome.State);
        Assert.Equal("course_operation_in_progress", outcome.Code);
        Assert.DoesNotContain("secret detail", outcome.Message);
    }

    [Fact]
    public void CRS_FR_11_Missing_course_is_permanent_failure()
    {
        var outcome = CrsCourseDeletionRules.Classify(new KeyNotFoundException("Ders bulunamadı."), 1, 3);
        Assert.Equal(CrsCourseDeletionJobState.Failed, outcome.State);
        Assert.Equal("course_not_found", outcome.Code);
    }

    [Fact]
    public void CRS_FR_11_Other_conflicts_are_permanent_failures_with_generic_code()
    {
        var outcome = CrsCourseDeletionRules.Classify(new CrsConflictException("other", "x"), 1, 3);
        Assert.Equal(CrsCourseDeletionJobState.Failed, outcome.State);
        Assert.Equal("course_deletion_conflict", outcome.Code);
    }

    [Theory]
    [InlineData(1, 3, CrsCourseDeletionJobState.Pending)]
    [InlineData(2, 3, CrsCourseDeletionJobState.Pending)]
    [InlineData(3, 3, CrsCourseDeletionJobState.Failed)]
    [InlineData(1, 1, CrsCourseDeletionJobState.Failed)]
    public void CRS_NFR_06_Unexpected_errors_retry_until_max_attempts(int attempts, int max, CrsCourseDeletionJobState expected)
    {
        var outcome = CrsCourseDeletionRules.Classify(new InvalidOperationException("boom"), attempts, max);
        Assert.Equal(expected, outcome.State);
        Assert.Equal("deletion_failed", outcome.Code);
    }

    [Fact]
    public void CRS_NFR_06_Stored_error_message_never_contains_exception_text()
    {
        var secret = "Host=db;Password=hunter2; relation crs_courses";
        foreach (var ex in new Exception[]
                 {
                     new InvalidOperationException(secret), new NpgsqlException(secret),
                     new KeyNotFoundException(secret), new CrsConflictException("course_operation_in_progress", secret)
                 })
            foreach (var attempts in new[] { 1, 3 })
            {
                var outcome = CrsCourseDeletionRules.Classify(ex, attempts, 3);
                Assert.DoesNotContain("hunter2", outcome.Message);
                Assert.DoesNotContain("Host=", outcome.Message);
                Assert.True(outcome.Message.Length <= 500);
                Assert.True(outcome.Code.Length <= 64);
            }
    }

    [Fact]
    public void CRS_FR_11_Job_state_values_match_database_check_constraint()
    {
        Assert.Equal(1, (int)CrsCourseDeletionJobState.Pending);
        Assert.Equal(2, (int)CrsCourseDeletionJobState.Running);
        Assert.Equal(3, (int)CrsCourseDeletionJobState.Succeeded);
        Assert.Equal(4, (int)CrsCourseDeletionJobState.Failed);
    }
}
