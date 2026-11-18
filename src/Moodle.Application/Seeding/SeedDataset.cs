namespace Moodle.Application.Seeding;

public sealed record SeedCategory(string IdNumber, string Name, string? ParentIdNumber);
public sealed record SeedUser(string Username, string FirstName, string LastName, string Email, string? SystemRole);
public sealed record SeedCourse(string ShortName, string FullName, string CategoryIdNumber);
public sealed record SeedEnrolment(string Username, string CourseShortName, string Role);
public sealed record SeedAssignment(string IdNumber, string Name, string Intro, string CourseShortName, int Section, bool SubmissionDrafts);
public sealed record SeedResource(string IdNumber, string Name, string CourseShortName);
public sealed record SeedSubmission(string AssignmentName, string Username, string OnlineText);
public sealed record SeedGrade(string AssignmentName, string Username, decimal Grade);

/// <summary>
/// <c>ortam/ornek-veri.feature</c> ile aynı örnek veri (WBS-1 §1). Eski ve yeni sistem aynı başlangıç verisiyle
/// karşılaştırılsın diye herkes bu kümeden yükler. Bağlantılar anahtarla kurulur (idnumber, kullanıcı adı, kısa ad),
/// çünkü veritabanı kimlikleri her modülün kendi tablosunda üretilir.
/// </summary>
public sealed record SeedDataset(
    IReadOnlyList<SeedCategory> Categories,
    IReadOnlyList<SeedUser> Users,
    IReadOnlyList<SeedCourse> Courses,
    IReadOnlyList<SeedEnrolment> Enrolments,
    IReadOnlyList<SeedAssignment> Assignments,
    IReadOnlyList<SeedResource> Resources,
    IReadOnlyList<SeedSubmission> Submissions,
    IReadOnlyList<SeedGrade> Grades);
