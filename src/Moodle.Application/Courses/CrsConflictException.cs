namespace Moodle.Application.Courses;

public sealed class CrsConflictException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
