using Moodle.Application.Grading;
using Moodle.Domain.Grading;

namespace Moodle.UnitTests;

/// <summary>Not raporu kuralları: gizleme (GRD-FR-02) ve not defteri sayfalaması (GRD-FR-03).</summary>
public sealed class GrdReportRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    // ---- Gizleme ----

    [Fact]
    public void GRD_FR_02_visible_item_or_grade_is_not_hidden() =>
        Assert.False(GradeVisibility.IsHidden(false, null, Now));

    [Fact]
    public void GRD_FR_02_hidden_flag_hides_permanently_even_after_hidden_until() =>
        Assert.True(GradeVisibility.IsHidden(true, Now.AddDays(-30), Now));

    [Theory]
    [InlineData(1, true)]       // süreli gizleme gelecekte: gizli
    [InlineData(0, false)]      // tam o an: artık görünür (sınır dahil)
    [InlineData(-1, false)]     // süre geçmiş: görünür
    public void GRD_FR_02_hidden_until_is_evaluated_against_server_time(int secondsFromNow, bool hidden) =>
        Assert.Equal(hidden, GradeVisibility.IsHidden(false, Now.AddSeconds(secondsFromNow), Now));

    [Theory]
    [InlineData(GradeItemType.Assignment, true)]
    [InlineData(GradeItemType.Manual, true)]
    [InlineData(GradeItemType.Course, false)]
    [InlineData(GradeItemType.Category, false)]
    public void GRD_FR_02_only_directly_graded_items_are_reported(GradeItemType type, bool reportable) =>
        Assert.Equal(reportable, GradeVisibility.IsReportable(type));

    // ---- Sayfalama ----

    [Theory]
    [InlineData(null, 50)]
    [InlineData(1, 1)]
    [InlineData(200, 200)]
    public void GRD_FR_03_limit_defaults_to_50_and_allows_1_to_200(int? limit, int expected) =>
        Assert.Equal(expected, GradebookPaging.ResolveLimit(limit));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(201)]
    public void GRD_FR_03_limit_outside_1_to_200_is_rejected(int limit) =>
        Assert.Throws<ArgumentException>(() => GradebookPaging.ResolveLimit(limit));

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("0", 0)]
    [InlineData("42", 42)]
    public void GRD_FR_03_cursor_is_the_last_learner_id_of_the_previous_page(string? cursor, int? expected) =>
        Assert.Equal(expected, GradebookPaging.ParseCursor(cursor));

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData(" 5")]
    [InlineData("1.5")]
    [InlineData("99999999999")]
    public void GRD_FR_03_invalid_cursor_is_rejected(string cursor) =>
        Assert.Throws<ArgumentException>(() => GradebookPaging.ParseCursor(cursor));

    [Fact]
    public void GRD_FR_03_pages_are_ordered_without_gaps_or_duplicates()
    {
        int[] learners = [7, 3, 5, 1, 6, 2, 4, 3];

        var (first, cursor1) = GradebookPaging.Page(learners, null, 3);
        Assert.Equal([1, 2, 3], first);
        Assert.Equal("3", cursor1);

        var (second, cursor2) = GradebookPaging.Page(learners, GradebookPaging.ParseCursor(cursor1), 3);
        Assert.Equal([4, 5, 6], second);
        Assert.Equal("6", cursor2);

        var (last, cursor3) = GradebookPaging.Page(learners, GradebookPaging.ParseCursor(cursor2), 3);
        Assert.Equal([7], last);
        Assert.Null(cursor3);
    }

    [Fact]
    public void GRD_FR_03_exact_page_size_has_no_next_cursor()
    {
        var (page, cursor) = GradebookPaging.Page([1, 2, 3], null, 3);
        Assert.Equal([1, 2, 3], page);
        Assert.Null(cursor);
    }

    [Fact]
    public void GRD_FR_03_empty_course_or_cursor_after_last_learner_returns_empty_page()
    {
        Assert.Empty(GradebookPaging.Page([], null, 50).Page);
        var (page, cursor) = GradebookPaging.Page([1, 2], 2, 50);
        Assert.Empty(page);
        Assert.Null(cursor);
    }
}
