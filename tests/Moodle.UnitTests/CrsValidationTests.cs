
using Moodle.Application.Courses;

namespace Moodle.UnitTests;

public class CrsValidationTests
{
    [Fact]
    public void RejectsTabInShortName() =>
        Assert.Throws<ArgumentException>(() =>
            CourseValidation.Validate(
                "Ders", "ABC\tD", null, null, null));

    [Fact]
    public void RejectsEndBeforeStart() =>
        Assert.Throws<ArgumentException>(() =>
            CourseValidation.Validate(
                "Ders", "ABC", null,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddDays(-1)));

    [Fact]
    public void AcceptsValidCourse() =>
        CourseValidation.Validate(
            "Ders", "ABC", "C1",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(1));
}
