namespace Moodle.Application.Abstractions;

/// <summary>Moodle'daki bağlam düzeyleri (WBS-1 §4.1).</summary>
public enum ContextLevel
{
    System = 10,
    User = 30,
    CourseCategory = 40,
    Course = 50,
    Module = 70,
}

/// <summary>Bir yetkinin sorgulandığı bağlam: düzey + o düzeydeki nesnenin kimliği.</summary>
public readonly record struct ContextRef(ContextLevel Level, int InstanceId)
{
    public static ContextRef Site { get; } = new(ContextLevel.System, 0);
    public static ContextRef ForCourse(int courseId) => new(ContextLevel.Course, courseId);
    public static ContextRef ForModule(int moduleId) => new(ContextLevel.Module, moduleId);
}
