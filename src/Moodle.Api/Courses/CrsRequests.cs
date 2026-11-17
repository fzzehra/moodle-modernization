using System.Text.Json;
using System.Text.Json.Serialization;

namespace Moodle.Api.Courses;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CategoryRequest(string Name, int? ParentId = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CourseRequest(
    int CategoryId, string FullName, string ShortName, string? IdNumber = null,
    bool Visible = true, DateTimeOffset? StartDate = null, DateTimeOffset? EndDate = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EnrolmentRequest(
    int UserId, string Role = "Student", bool Suspended = false,
    DateTimeOffset? StartsAt = null, DateTimeOffset? EndsAt = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CategoryPatchRequest
{
    private string? _name;
    private int? _parentId;
    public string? Name
    {
        get => _name;
        init => _name = value ?? throw new JsonException("Name null olamaz.");
    }
    public int? ParentId
    {
        get => _parentId;
        init { _parentId = value; ParentIdSpecified = true; }
    }
    [JsonIgnore] public bool ParentIdSpecified { get; private set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CoursePatchRequest
{
    private int? _categoryId;
    private string? _fullName;
    private string? _shortName;
    private bool? _visible;
    private string? _idNumber;
    private DateTimeOffset? _startDate;
    private DateTimeOffset? _endDate;

    public int? CategoryId
    {
        get => _categoryId;
        init => _categoryId = value ?? throw new JsonException("CategoryId null olamaz.");
    }
    public string? FullName
    {
        get => _fullName;
        init => _fullName = value ?? throw new JsonException("FullName null olamaz.");
    }
    public string? ShortName
    {
        get => _shortName;
        init => _shortName = value ?? throw new JsonException("ShortName null olamaz.");
    }
    public bool? Visible
    {
        get => _visible;
        init => _visible = value ?? throw new JsonException("Visible null olamaz.");
    }
    public string? IdNumber
    {
        get => _idNumber;
        init { _idNumber = value; IdNumberSpecified = true; }
    }
    public DateTimeOffset? StartDate
    {
        get => _startDate;
        init { _startDate = value; StartDateSpecified = true; }
    }
    public DateTimeOffset? EndDate
    {
        get => _endDate;
        init { _endDate = value; EndDateSpecified = true; }
    }
    [JsonIgnore] public bool IdNumberSpecified { get; private set; }
    [JsonIgnore] public bool StartDateSpecified { get; private set; }
    [JsonIgnore] public bool EndDateSpecified { get; private set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class EnrolmentPatchRequest
{
    private bool? _suspended;
    private string? _role;
    public bool? Suspended
    {
        get => _suspended;
        init => _suspended = value ?? throw new JsonException("Suspended null olamaz.");
    }
    public string? Role
    {
        get => _role;
        init => _role = value ?? throw new JsonException("Role null olamaz.");
    }
}
