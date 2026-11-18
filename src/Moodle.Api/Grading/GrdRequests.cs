using System.Text.Json.Serialization;

namespace Moodle.Api.Grading;

/// <summary>
/// PUT .../grade gövdesi (WBS-2 §3.5.2). Değerlendirici, öğrenci ve deneme gövdeden alınmaz;
/// bilinmeyen alan (ör. <c>graderId</c>) 400 ile reddedilir. İlk kayıtta <c>version</c> 0'dır.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GradeRequest(decimal? Score, string? Feedback = null, int Version = 0);
