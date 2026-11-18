using System.Net;
using System.Text.Json;
using Moodle.Application.Abstractions;
using Moodle.Domain.Grading;
using static Moodle.IntegrationTests.GrdApiTestSupport;

namespace Moodle.IntegrationTests;

/// <summary>
/// Kişisel not raporu ve not defteri (WBS-2 §3.5.2): GRD-FR-02, GRD-FR-03, GRD-NFR-01.
/// Senaryolar WBS-1 §4.5.10'daki T-GRD-02, T-GRD-03 ve T-GRD-05'i izler.
/// </summary>
public sealed class GrdReportEndpointTests(GrdApiFixture fixture) : IClassFixture<GrdApiFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
    private const int Student = 3;
    private const int OtherStudent = 4;

    public static IEnumerable<object[]> ReportPaths()
    {
        yield return ["my-grades"];
        yield return ["gradebook"];
    }

    private static string MyGradesPath(int courseId) => $"/api/v1/courses/{courseId}/my-grades";
    private static string GradebookPath(int courseId, string query = "") => $"/api/v1/courses/{courseId}/gradebook{query}";
    private static string ReportPath(string report, int courseId) => $"/api/v1/courses/{courseId}/{report}";

    private HttpClient StudentClient(int learnerId = Student) =>
        fixture.CreateFactory(userId: learnerId, now: Now,
            permission: (capability, _) => capability == "grade.view").CreateClient();

    private HttpClient TeacherClient() =>
        fixture.CreateFactory(now: Now,
            permission: (capability, _) => capability is "grade.view" or "grade.viewall").CreateClient();

    private async Task<int> AddItemAsync(int courseId, string name,
        GradeItemType type = GradeItemType.Manual, Action<GradeItem>? configure = null)
    {
        await using var db = fixture.Database.CreateContext();
        var item = new GradeItem
        {
            CourseId = courseId, Type = type, Name = name, MinScore = 0m, MaxScore = 100m,
            AssignmentId = type == GradeItemType.Assignment ? GrdApiFixture.NextAssignmentId() : null,
        };
        configure?.Invoke(item);
        db.Items.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    private async Task AddGradeAsync(int itemId, int learnerId, decimal? score, Action<LearnerGrade>? configure = null)
    {
        await using var db = fixture.Database.CreateContext();
        var grade = new LearnerGrade
        {
            ItemId = itemId, LearnerId = learnerId, RawScore = score, FinalScore = score, UpdatedAt = Now,
        };
        configure?.Invoke(grade);
        db.LearnerGrades.Add(grade);
        await db.SaveChangesAsync();
    }

    private static Dictionary<string, JsonElement> RowsByName(JsonElement view) =>
        view.GetProperty("items").EnumerateArray().ToDictionary(x => x.GetProperty("name").GetString()!);

    // ---- GRD-FR-02: kişisel not raporu ----

    [Fact]
    public async Task GRD_FR_02_student_sees_own_grades_with_item_bounds_and_empty_for_ungraded()
    {
        // T-GRD-02: ogrenci1 SENG101 kullanıcı raporunda Ödev 1 için 85 puanı görür.
        var courseId = GrdApiFixture.NextCourseId();
        var assignment = await AddItemAsync(courseId, "Ödev 1", GradeItemType.Assignment);
        var quiz = await AddItemAsync(courseId, "Kısa sınav", configure: x => { x.MinScore = 0m; x.MaxScore = 20m; });
        await AddGradeAsync(assignment, Student, 85m);
        await AddGradeAsync(assignment, OtherStudent, 61.75m);
        await AddGradeAsync(quiz, OtherStudent, 17.25m);

        using var client = StudentClient();
        using var response = await client.GetAsync(MyGradesPath(courseId));
        var body = await response.Content.ReadAsStringAsync();
        var view = await ReadJsonAsync(response);

        Assert.Equal(courseId, view.GetProperty("courseId").GetInt32());
        var rows = RowsByName(view);
        Assert.Equal(["Ödev 1", "Kısa sınav"], view.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("name").GetString()));
        Assert.Equal(assignment, rows["Ödev 1"].GetProperty("itemId").GetInt32());
        Assert.Equal(85m, rows["Ödev 1"].GetProperty("score").GetDecimal());
        Assert.Equal(100m, rows["Ödev 1"].GetProperty("maxScore").GetDecimal());
        Assert.Equal(JsonValueKind.Null, rows["Kısa sınav"].GetProperty("score").ValueKind);
        Assert.Equal(20m, rows["Kısa sınav"].GetProperty("maxScore").GetDecimal());

        // GRD-NFR-01: başka öğrencinin hiçbir verisi yanıtta yer almaz.
        Assert.DoesNotContain("61.75", body);
        Assert.DoesNotContain("17.25", body);
        Assert.DoesNotContain("learnerId", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GRD_FR_02_each_student_sees_only_their_own_score_for_the_same_item()
    {
        var courseId = GrdApiFixture.NextCourseId();
        var item = await AddItemAsync(courseId, "Ödev 1", GradeItemType.Assignment);
        await AddGradeAsync(item, Student, 85m);
        await AddGradeAsync(item, OtherStudent, 61.75m);

        using var other = StudentClient(OtherStudent);
        var view = await ReadJsonAsync(await other.GetAsync(MyGradesPath(courseId)));
        Assert.Equal(61.75m, Assert.Single(view.GetProperty("items").EnumerateArray()).GetProperty("score").GetDecimal());
    }

    [Fact]
    public async Task GRD_FR_02_hidden_items_are_omitted_and_hidden_grades_return_empty_scores()
    {
        var courseId = GrdApiFixture.NextCourseId();
        var visible = await AddItemAsync(courseId, "Görünür");
        var hidden = await AddItemAsync(courseId, "Gizli kalem", configure: x => x.Hidden = true);
        var hiddenUntilFuture = await AddItemAsync(courseId, "Süreli gizli kalem", configure: x => x.HiddenUntil = Now.AddHours(1));
        var hiddenUntilPast = await AddItemAsync(courseId, "Süresi geçmiş kalem", configure: x => x.HiddenUntil = Now.AddHours(-1));
        var hiddenGrade = await AddItemAsync(courseId, "Gizli not");
        var hiddenGradeFuture = await AddItemAsync(courseId, "Süreli gizli not");
        var hiddenGradePast = await AddItemAsync(courseId, "Süresi geçmiş not");

        await AddGradeAsync(visible, Student, 11m);
        await AddGradeAsync(hidden, Student, 12.5m);
        await AddGradeAsync(hiddenUntilFuture, Student, 13.5m);
        await AddGradeAsync(hiddenUntilPast, Student, 14m);
        await AddGradeAsync(hiddenGrade, Student, 15.5m, x => x.Hidden = true);
        await AddGradeAsync(hiddenGradeFuture, Student, 16.5m, x => x.HiddenUntil = Now.AddHours(1));
        await AddGradeAsync(hiddenGradePast, Student, 17m, x => x.HiddenUntil = Now.AddHours(-1));

        using var client = StudentClient();
        using var response = await client.GetAsync(MyGradesPath(courseId));
        var body = await response.Content.ReadAsStringAsync();
        var rows = RowsByName(await ReadJsonAsync(response));

        Assert.Equal(["Görünür", "Süresi geçmiş kalem", "Gizli not", "Süreli gizli not", "Süresi geçmiş not"], rows.Keys);
        Assert.Equal(11m, rows["Görünür"].GetProperty("score").GetDecimal());
        Assert.Equal(14m, rows["Süresi geçmiş kalem"].GetProperty("score").GetDecimal());
        Assert.Equal(JsonValueKind.Null, rows["Gizli not"].GetProperty("score").ValueKind);
        Assert.Equal(JsonValueKind.Null, rows["Süreli gizli not"].GetProperty("score").ValueKind);
        Assert.Equal(17m, rows["Süresi geçmiş not"].GetProperty("score").GetDecimal());

        // Gizli puanlar ve gizli kalem adları gövdenin hiçbir yerinde yoktur.
        foreach (var secret in new[] { "12.5", "13.5", "15.5", "16.5", "Gizli kalem", "Süreli gizli kalem" })
            Assert.DoesNotContain(secret, body);
    }

    [Fact]
    public async Task GRD_FR_02_hidden_until_uses_server_time()
    {
        var courseId = GrdApiFixture.NextCourseId();
        var item = await AddItemAsync(courseId, "Yarın açılacak", configure: x => x.HiddenUntil = Now.AddDays(1));
        await AddGradeAsync(item, Student, 90m);

        using var today = StudentClient();
        Assert.Empty((await ReadJsonAsync(await today.GetAsync(MyGradesPath(courseId)))).GetProperty("items").EnumerateArray());

        using var tomorrow = fixture.CreateFactory(userId: Student, now: Now.AddDays(1),
            permission: (capability, _) => capability == "grade.view").CreateClient();
        var row = Assert.Single((await ReadJsonAsync(await tomorrow.GetAsync(MyGradesPath(courseId)))).GetProperty("items").EnumerateArray());
        Assert.Equal(90m, row.GetProperty("score").GetDecimal());
    }

    [Fact]
    public async Task GRD_FR_02_course_and_category_totals_are_not_listed_and_other_courses_are_excluded()
    {
        var courseId = GrdApiFixture.NextCourseId();
        var otherCourseId = GrdApiFixture.NextCourseId();
        var own = await AddItemAsync(courseId, "Ödev 1", GradeItemType.Assignment);
        await AddItemAsync(courseId, "Ders toplamı", GradeItemType.Course);
        await AddItemAsync(courseId, "Kategori toplamı", GradeItemType.Category);
        var foreign = await AddItemAsync(otherCourseId, "Başka dersin ödevi", GradeItemType.Assignment);
        await AddGradeAsync(own, Student, 80m);
        await AddGradeAsync(foreign, Student, 33.25m);

        using var client = StudentClient();
        using var response = await client.GetAsync(MyGradesPath(courseId));
        var body = await response.Content.ReadAsStringAsync();
        var row = Assert.Single((await ReadJsonAsync(response)).GetProperty("items").EnumerateArray());
        Assert.Equal("Ödev 1", row.GetProperty("name").GetString());
        Assert.DoesNotContain("33.25", body);
    }

    // ---- GRD-NFR-01: başka öğrenci seçilemez ----

    [Theory]
    [InlineData("?userId=4")]
    [InlineData("?learnerId=4")]
    [InlineData("?UserID=4")]
    [InlineData("?learner_id=4")]
    [InlineData("?id=4")]
    public async Task GRD_NFR_01_selecting_another_learner_in_the_query_returns_400_without_grade_data(string query)
    {
        // T-GRD-05: öğrenci başka bir öğrencinin raporunu ister; not tablosu dönmez.
        var courseId = GrdApiFixture.NextCourseId();
        var item = await AddItemAsync(courseId, "Ödev 1", GradeItemType.Assignment);
        await AddGradeAsync(item, Student, 85m);
        await AddGradeAsync(item, OtherStudent, 61.75m);

        using var client = StudentClient();
        using var response = await client.GetAsync(MyGradesPath(courseId) + query);
        var body = await response.Content.ReadAsStringAsync();
        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "learner_selection_not_allowed");
        Assert.DoesNotContain("61.75", body);
        Assert.DoesNotContain("85", body);
        Assert.DoesNotContain("items", body);
    }

    [Fact]
    public async Task GRD_NFR_01_selecting_another_learner_in_the_body_returns_400_without_grade_data()
    {
        var courseId = GrdApiFixture.NextCourseId();
        var item = await AddItemAsync(courseId, "Ödev 1", GradeItemType.Assignment);
        await AddGradeAsync(item, OtherStudent, 61.75m);

        using var client = StudentClient();
        using var response = await SendAsync(client, "GET", MyGradesPath(courseId), """{"learnerId":4}""");
        var body = await response.Content.ReadAsStringAsync();
        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "learner_selection_not_allowed");
        Assert.DoesNotContain("61.75", body);
    }

    [Fact]
    public async Task GRD_NFR_01_student_cannot_open_the_course_gradebook()
    {
        var courseId = GrdApiFixture.NextCourseId();
        var item = await AddItemAsync(courseId, "Ödev 1", GradeItemType.Assignment);
        await AddGradeAsync(item, OtherStudent, 61.75m);

        using var client = StudentClient();
        using var response = await client.GetAsync(GradebookPath(courseId));
        var body = await response.Content.ReadAsStringAsync();
        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "forbidden");
        Assert.DoesNotContain("61.75", body);
    }

    // ---- GRD-FR-03: not defteri ----

    [Fact]
    public async Task GRD_FR_03_teacher_sees_enrolled_learners_and_course_items_with_protection_flags()
    {
        // T-GRD-03: ogretmen1 SENG101 defterinde Ödev 1 kalemini ve izinli öğrenci satırlarını görür.
        var courseId = GrdApiFixture.NextCourseId();
        GrdApiFixture.SetEnrolledLearners(courseId, 5, 3, 4);
        var assignment = await AddItemAsync(courseId, "Ödev 1", GradeItemType.Assignment);
        var manual = await AddItemAsync(courseId, "Gizli ve kilitli", configure: x =>
        {
            x.Hidden = true;
            x.LockAt = Now.AddHours(-1);
        });
        await AddItemAsync(courseId, "Ders toplamı", GradeItemType.Course);
        await AddGradeAsync(assignment, 3, 85m, x => x.Locked = true);
        await AddGradeAsync(assignment, 4, 72m, x => { x.Overridden = true; x.RawScore = 90m; });
        await AddGradeAsync(manual, 4, 10m);
        await AddGradeAsync(assignment, 99, 99.5m); // derse kayıtlı değil

        using var client = TeacherClient();
        using var response = await client.GetAsync(GradebookPath(courseId));
        var body = await response.Content.ReadAsStringAsync();
        var view = await ReadJsonAsync(response);

        Assert.Equal(courseId, view.GetProperty("courseId").GetInt32());
        Assert.Equal(JsonValueKind.Null, view.GetProperty("nextCursor").ValueKind);

        var items = view.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([assignment, manual], items.Select(x => x.GetProperty("itemId").GetInt32()));
        Assert.False(items[0].GetProperty("hidden").GetBoolean());
        Assert.False(items[0].GetProperty("locked").GetBoolean());
        Assert.True(items[1].GetProperty("hidden").GetBoolean());
        Assert.True(items[1].GetProperty("locked").GetBoolean());

        var learners = view.GetProperty("learners").EnumerateArray().ToList();
        Assert.Equal([3, 4, 5], learners.Select(x => x.GetProperty("learnerId").GetInt32()));

        var cells = learners.ToDictionary(
            x => x.GetProperty("learnerId").GetInt32(),
            x => x.GetProperty("grades").EnumerateArray().ToList());
        Assert.All(cells.Values, row => Assert.Equal([assignment, manual], row.Select(c => c.GetProperty("itemId").GetInt32())));

        Assert.Equal(85m, cells[3][0].GetProperty("score").GetDecimal());
        Assert.True(cells[3][0].GetProperty("locked").GetBoolean());
        Assert.False(cells[3][0].GetProperty("overridden").GetBoolean());

        Assert.Equal(72m, cells[4][0].GetProperty("score").GetDecimal());
        Assert.True(cells[4][0].GetProperty("overridden").GetBoolean());
        Assert.False(cells[4][0].GetProperty("locked").GetBoolean());
        Assert.Equal(10m, cells[4][1].GetProperty("score").GetDecimal());
        Assert.True(cells[4][1].GetProperty("hidden").GetBoolean());
        Assert.True(cells[4][1].GetProperty("locked").GetBoolean());

        Assert.All(cells[5], cell => Assert.Equal(JsonValueKind.Null, cell.GetProperty("score").ValueKind));
        Assert.DoesNotContain("99.5", body);
    }

    [Fact]
    public async Task GRD_FR_03_gradebook_is_paged_by_cursor_and_limit_and_group_is_ignored()
    {
        var courseId = GrdApiFixture.NextCourseId();
        GrdApiFixture.SetEnrolledLearners(courseId, 7, 6, 5, 4, 3, 2, 1);
        await AddItemAsync(courseId, "Ödev 1", GradeItemType.Assignment);
        using var client = TeacherClient();

        async Task AssertPageAsync(string query, int[] expectedLearners, string? expectedNext)
        {
            var view = await ReadJsonAsync(await client.GetAsync(GradebookPath(courseId, query)));
            var next = view.GetProperty("nextCursor");
            Assert.Equal(expectedLearners,
                view.GetProperty("learners").EnumerateArray().Select(x => x.GetProperty("learnerId").GetInt32()));
            Assert.Equal(expectedNext, next.ValueKind == JsonValueKind.Null ? null : next.GetString());
        }

        await AssertPageAsync("?limit=3", [1, 2, 3], "3");
        await AssertPageAsync("?limit=3&cursor=3", [4, 5, 6], "6");
        await AssertPageAsync("?limit=3&cursor=6", [7], null);

        // 1. sürümde grup yok: groupId kabul edilir ve sonucu değiştirmez.
        await AssertPageAsync("?limit=3&groupId=12", [1, 2, 3], "3");
    }

    [Fact]
    public async Task GRD_FR_03_default_page_size_is_50()
    {
        var courseId = GrdApiFixture.NextCourseId();
        GrdApiFixture.SetEnrolledLearners(courseId, Enumerable.Range(1, 60).ToArray());
        using var client = TeacherClient();

        var view = await ReadJsonAsync(await client.GetAsync(GradebookPath(courseId)));
        Assert.Equal(50, view.GetProperty("learners").GetArrayLength());
        Assert.Equal("50", view.GetProperty("nextCursor").GetString());
    }

    [Theory]
    [InlineData("?limit=0")]
    [InlineData("?limit=201")]
    [InlineData("?cursor=abc")]
    [InlineData("?cursor=-1")]
    public async Task GRD_FR_03_invalid_paging_returns_422(string query)
    {
        using var client = TeacherClient();
        using var response = await client.GetAsync(GradebookPath(GrdApiFixture.NextCourseId(), query));
        await AssertErrorAsync(response, HttpStatusCode.UnprocessableEntity, "validation_failed");
    }

    [Fact]
    public async Task GRD_FR_03_non_numeric_limit_returns_400()
    {
        using var client = TeacherClient();
        using var response = await client.GetAsync(GradebookPath(GrdApiFixture.NextCourseId(), "?limit=abc"));
        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "bad_request");
    }

    // ---- Kimlik, yetki ve kapsam ----

    [Theory]
    [MemberData(nameof(ReportPaths))]
    public async Task GRD_NFR_01_reports_require_a_session(string report)
    {
        using var client = fixture.CreateFactory(authenticated: false).CreateClient();
        using var response = await client.GetAsync(ReportPath(report, GrdApiFixture.NextCourseId()));
        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "not_authenticated");
    }

    [Theory]
    [MemberData(nameof(ReportPaths))]
    public async Task GRD_NFR_01_reports_require_their_capability(string report)
    {
        using var client = fixture.CreateFactory(permission: (_, _) => false).CreateClient();
        using var response = await client.GetAsync(ReportPath(report, GrdApiFixture.NextCourseId()));
        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "forbidden");
    }

    [Theory]
    [MemberData(nameof(ReportPaths))]
    public async Task GRD_FR_02_GRD_FR_03_missing_course_returns_404(string report)
    {
        using var client = TeacherClient();
        using var response = await client.GetAsync(ReportPath(report, GrdApiFixture.MissingCourseId));
        await AssertErrorAsync(response, HttpStatusCode.NotFound, "not_found");
    }

    [Theory]
    [InlineData("my-grades", "grade.view")]
    [InlineData("gradebook", "grade.viewall")]
    public async Task GRD_FR_02_GRD_FR_03_capability_is_requested_in_the_course_context(string report, string capability)
    {
        var courseId = GrdApiFixture.NextCourseId();
        var calls = new List<(string Capability, ContextRef Context)>();
        using var client = fixture.CreateFactory(userId: Student, permission: (requested, context) =>
        {
            lock (calls) calls.Add((requested, context));
            return true;
        }).CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(ReportPath(report, courseId))).StatusCode);
        Assert.Equal((capability, ContextRef.ForCourse(courseId)), Assert.Single(calls));
    }

    [Theory]
    [MemberData(nameof(ReportPaths))]
    public async Task GRD_NFR_01_reports_are_closed_with_AUTH_placeholders(string report)
    {
        using var factory = new GrdUnavailableAuthFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(ReportPath(report, 1));
        await AssertErrorAsync(response, HttpStatusCode.ServiceUnavailable, "auth_not_ready");
    }
}
