using Moodle.Application.Grading;

namespace Moodle.UnitTests;

/// <summary>GRD-BR-02 ve kilit hesabı (WBS-1 §4.5.4, T-GRD-04).</summary>
public sealed class GrdValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(50.5)]
    [InlineData(99.99999)]
    public void GRD_BR_02_scores_within_inclusive_bounds_are_accepted(double score) =>
        GradeValidation.ValidateScore((decimal)score, 0m, 100m);

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(-0.00001)]
    [InlineData(100.00001)]
    public void GRD_BR_02_scores_outside_bounds_are_rejected(double score) =>
        Assert.Throws<ArgumentException>(() => GradeValidation.ValidateScore((decimal)score, 0m, 100m));

    [Fact]
    public void GRD_BR_02_empty_score_is_valid_and_distinct_from_zero()
    {
        GradeValidation.ValidateScore(null, 0m, 100m);
        // Alt sınır sıfırdan büyük olsa da boş puan "notlandırılmadı" anlamındadır.
        GradeValidation.ValidateScore(null, 10m, 20m);
        Assert.Throws<ArgumentException>(() => GradeValidation.ValidateScore(0m, 10m, 20m));
    }

    [Fact]
    public void GRD_BR_02_custom_item_bounds_are_applied()
    {
        GradeValidation.ValidateScore(-5m, -5m, 5m);
        GradeValidation.ValidateScore(5m, -5m, 5m);
        Assert.Throws<ArgumentException>(() => GradeValidation.ValidateScore(5.1m, -5m, 5m));
    }

    [Fact]
    public void GRD_BR_02_scores_with_more_than_five_decimals_are_rejected_instead_of_rounded()
    {
        GradeValidation.ValidateScore(12.34500m, 0m, 100m);
        Assert.Throws<ArgumentException>(() => GradeValidation.ValidateScore(12.345678m, 0m, 100m));
    }

    [Fact]
    public void GRD_BR_02_item_with_inverted_bounds_is_rejected() =>
        Assert.Throws<ArgumentException>(() => GradeValidation.ValidateScore(null, 10m, 0m));

    [Theory]
    [InlineData(0, true)]
    [InlineData(3, true)]
    [InlineData(-1, false)]
    public void GRD_FR_01_attempt_numbers_start_at_zero(int attempt, bool valid)
    {
        if (valid) GradeValidation.ValidateAttempt(attempt);
        else Assert.Throws<ArgumentException>(() => GradeValidation.ValidateAttempt(attempt));
    }

    [Fact]
    public void GRD_FR_01_negative_version_is_rejected()
    {
        GradeValidation.ValidateVersion(0);
        Assert.Throws<ArgumentException>(() => GradeValidation.ValidateVersion(-1));
    }

    [Fact]
    public void GRD_BR_01_lock_flag_and_lock_time_are_evaluated_against_server_time()
    {
        var now = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
        Assert.False(GradeValidation.IsLocked(false, null, now));
        Assert.True(GradeValidation.IsLocked(true, null, now));
        Assert.True(GradeValidation.IsLocked(true, now.AddDays(1), now));
        Assert.True(GradeValidation.IsLocked(false, now, now));
        Assert.True(GradeValidation.IsLocked(false, now.AddSeconds(-1), now));
        Assert.False(GradeValidation.IsLocked(false, now.AddSeconds(1), now));
    }
}
