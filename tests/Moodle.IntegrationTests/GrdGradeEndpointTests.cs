using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Moodle.Application.Abstractions;
using Moodle.Domain.Grading;
using static Moodle.IntegrationTests.GrdApiTestSupport;

namespace Moodle.IntegrationTests;

/// <summary>
/// Puan okuma ve yazma uçları (WBS-2 §3.5.2): GRD-FR-01, GRD-BR-01, GRD-BR-02.
/// Senaryolar WBS-1 §4.5.10'daki T-GRD-01 ve T-GRD-04'ü izler.
/// </summary>
public sealed class GrdGradeEndpointTests(GrdApiFixture fixture) : IClassFixture<GrdApiFixture>
{
    public static IEnumerable<object[]> Routes()
    {
        yield return ["GET", null!];
        yield return ["PUT", """{"score":50,"version":0}"""];
    }

    private async Task<(AssignmentGrade? Assessment, GradeItem? Item, LearnerGrade? Ledger)> LoadAsync(
        int assignmentId, int learnerId = 3, int attempt = 0)
    {
        await using var db = fixture.Database.CreateContext();
        var item = await db.Items.AsNoTracking().SingleOrDefaultAsync(x => x.AssignmentId == assignmentId);
        var assessment = await db.AssignmentGrades.AsNoTracking().SingleOrDefaultAsync(x =>
            x.AssignmentId == assignmentId && x.LearnerId == learnerId && x.Attempt == attempt);
        var ledger = item is null ? null : await db.LearnerGrades.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ItemId == item.Id && x.LearnerId == learnerId);
        return (assessment, item, ledger);
    }

    private async Task ChangeLedgerAsync(int assignmentId, Action<GradeItem, LearnerGrade> change, int learnerId = 3)
    {
        await using var db = fixture.Database.CreateContext();
        var item = await db.Items.SingleAsync(x => x.AssignmentId == assignmentId);
        var ledger = await db.LearnerGrades.SingleAsync(x => x.ItemId == item.Id && x.LearnerId == learnerId);
        change(item, ledger);
        await db.SaveChangesAsync();
    }

    private async Task<HttpClient> ClientWithGradeAsync(int assignmentId, decimal score)
    {
        var client = fixture.CreateFactory().CreateClient();
        var saved = await ReadJsonAsync(await PutGradeAsync(client, GradePath(assignmentId), score, 0));
        Assert.Equal(1, saved.GetProperty("assessment").GetProperty("version").GetInt32());
        return client;
    }

    // ---- GRD-FR-01: okuma ----

    [Fact]
    public async Task GRD_FR_01_GET_without_assessment_returns_null_version_0_and_default_bounds()
    {
        using var client = fixture.CreateFactory().CreateClient();
        var view = await ReadJsonAsync(await client.GetAsync(GradePath(GrdApiFixture.NextAssignmentId())));
        Assert.Equal(JsonValueKind.Null, view.GetProperty("assessment").ValueKind);
        Assert.Equal(0, view.GetProperty("version").GetInt32());
        Assert.Equal(0m, view.GetProperty("minScore").GetDecimal());
        Assert.Equal(100m, view.GetProperty("maxScore").GetDecimal());
    }

    [Fact]
    public async Task GRD_FR_01_GET_reads_bounds_from_the_grade_item()
    {
        var assignmentId = GrdApiFixture.NextAssignmentId();
        using var client = await ClientWithGradeAsync(assignmentId, 10m);
        await ChangeLedgerAsync(assignmentId, (item, _) => { item.MinScore = 5m; item.MaxScore = 20m; });

        var view = await ReadJsonAsync(await client.GetAsync(GradePath(assignmentId)));
        Assert.Equal(5m, view.GetProperty("minScore").GetDecimal());
        Assert.Equal(20m, view.GetProperty("maxScore").GetDecimal());
        Assert.Equal(1, view.GetProperty("version").GetInt32());
    }

    // ---- GRD-FR-01 + GRD-BR-01: kayıt ve aktarım ----

    [Fact]
    public async Task GRD_FR_01_GRD_BR_01_valid_score_is_saved_for_learner_and_attempt_and_transferred_to_gradebook()
    {
        var assignmentId = GrdApiFixture.NextAssignmentId();
        using var client = fixture.CreateFactory().CreateClient();

        var saved = await ReadJsonAsync(await PutGradeAsync(client, GradePath(assignmentId), 85m, 0, "Güzel çalışma"));
        var assessment = saved.GetProperty("assessment");
        Assert.True(saved.GetProperty("gradebookUpdated").GetBoolean());
        Assert.Equal(85m, assessment.GetProperty("score").GetDecimal());
        Assert.Equal("Güzel çalışma", assessment.GetProperty("feedback").GetString());
        Assert.Equal(GrdApiFixture.GraderId, assessment.GetProperty("graderId").GetInt32());
        Assert.Equal(1, assessment.GetProperty("version").GetInt32());

        // T-GRD-01: değerlendirme ekranı yeniden açıldığında aynı puan görünür.
        var view = await ReadJsonAsync(await client.GetAsync(GradePath(assignmentId)));
        Assert.Equal(85m, view.GetProperty("assessment").GetProperty("score").GetDecimal());
        Assert.Equal(1, view.GetProperty("version").GetInt32());

        // Veritabanında değerlendirme, ana not kalemi ve defter notu eşleşir.
        var (stored, item, ledger) = await LoadAsync(assignmentId);
        Assert.Equal(85m, stored!.Score);
        Assert.Equal(GrdApiFixture.GraderId, stored.GraderId);
        Assert.Equal(GradeItemType.Assignment, item!.Type);
        Assert.Equal(GrdApiFixture.CourseId, item.CourseId);
        Assert.Equal(0m, item.MinScore);
        Assert.Equal(100m, item.MaxScore);
        Assert.Equal(85m, ledger!.RawScore);
        Assert.Equal(85m, ledger.FinalScore);
        Assert.False(ledger.Overridden);
    }

    [Fact]
    public async Task GRD_BR_01_unprotected_gradebook_value_follows_score_updates()
    {
        // T-GRD-01: korumasız not 90'dan 88'e güncellenir.
        var assignmentId = GrdApiFixture.NextAssignmentId();
        using var client = await ClientWithGradeAsync(assignmentId, 90m);

        var updated = await ReadJsonAsync(await PutGradeAsync(client, GradePath(assignmentId), 88m, 1));
        Assert.Equal(2, updated.GetProperty("assessment").GetProperty("version").GetInt32());
        Assert.True(updated.GetProperty("gradebookUpdated").GetBoolean());

        var (stored, _, ledger) = await LoadAsync(assignmentId);
        Assert.Equal(88m, stored!.Score);
        Assert.Equal(2, stored.Version);
        Assert.Equal(88m, ledger!.FinalScore);
    }

    [Fact]
    public async Task GRD_FR_01_empty_score_is_saved_and_transferred_as_empty_not_zero()
    {
        var assignmentId = GrdApiFixture.NextAssignmentId();
        using var client = await ClientWithGradeAsync(assignmentId, 40m);

        var saved = await ReadJsonAsync(await PutGradeAsync(client, GradePath(assignmentId), null, 1));
        Assert.Equal(JsonValueKind.Null, saved.GetProperty("assessment").GetProperty("score").ValueKind);

        var (stored, _, ledger) = await LoadAsync(assignmentId);
        Assert.Null(stored!.Score);
        Assert.Null(ledger!.RawScore);
        Assert.Null(ledger.FinalScore);
    }

    [Fact]
    public async Task GRD_FR_01_grader_is_taken_from_the_session_and_cannot_be_sent_in_the_body()
    {
        var assignmentId = GrdApiFixture.NextAssignmentId();
        using var client = fixture.CreateFactory().CreateClient();

        using var response = await SendAsync(client, "PUT", GradePath(assignmentId),
            """{"score":70,"version":0,"graderId":99}""");
        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "bad_request");
        Assert.Null((await LoadAsync(assignmentId)).Assessment);
    }

    [Fact]
    public async Task GRD_FR_01_attempts_are_stored_separately_and_gradebook_follows_the_latest_attempt()
    {
        var assignmentId = GrdApiFixture.NextAssignmentId();
        using var client = fixture.CreateFactory().CreateClient();

        await ReadJsonAsync(await PutGradeAsync(client, GradePath(assignmentId, attempt: 0), 60m, 0));
        var second = await ReadJsonAsync(await PutGradeAsync(client, GradePath(assignmentId, attempt: 1), 80m, 0));
        Assert.True(second.GetProperty("gradebookUpdated").GetBoolean());

        // Eski denemenin düzeltilmesi kaydedilir ama defterdeki son deneme değerini ezmez.
        var regrade = await ReadJsonAsync(await PutGradeAsync(client, GradePath(assignmentId, attempt: 0), 65m, 1));
        Assert.False(regrade.GetProperty("gradebookUpdated").GetBoolean());

        Assert.Equal(65m, (await LoadAsync(assignmentId, attempt: 0)).Assessment!.Score);
        var (latest, _, ledger) = await LoadAsync(assignmentId, attempt: 1);
        Assert.Equal(80m, latest!.Score);
        Assert.Equal(80m, ledger!.FinalScore);
    }

    [Fact]
    public async Task GRD_FR_01_learners_have_separate_assessments_and_gradebook_rows()
    {
        var assignmentId = GrdApiFixture.NextAssignmentId();
        using var client = fixture.CreateFactory().CreateClient();
        await ReadJsonAsync(await PutGradeAsync(client, GradePath(assignmentId, learnerId: 3), 70m, 0));
        await ReadJsonAsync(await PutGradeAsync(client, GradePath(assignmentId, learnerId: 4), 75m, 0));

        Assert.Equal(70m, (await LoadAsync(assignmentId, learnerId: 3)).Ledger!.FinalScore);
        Assert.Equal(75m, (await LoadAsync(assignmentId, learnerId: 4)).Ledger!.FinalScore);
    }

    // ---- GRD-BR-02: sınırlar ----

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task GRD_BR_02_inclusive_bounds_are_accepted(int score)
    {
        var assignmentId = GrdApiFixture.NextAssignmentId();
        using var client = fixture.CreateFactory().CreateClient();
        await ReadJsonAsync(await PutGradeAsync(client, GradePath(assignmentId), score, 0));
        Assert.Equal(score, (await LoadAsync(assignmentId)).Ledger!.FinalScore);
    }

    [Theory]
    [InlineData(0, -1)]
    [InlineData(100, 101)]
    public async Task GRD_BR_02_out_of_range_score_returns_422_and_keeps_the_previous_score(int valid, int invalid)
    {
        // T-GRD-04: geçerli değerden sonra sınır dışı değer gönderilir; form yeniden açıldığında önceki puan görünür.
        var assignmentId = GrdApiFixture.NextAssignmentId();
        using var client = await ClientWithGradeAsync(assignmentId, valid);

        using var response = await PutGradeAsync(client, GradePath(assignmentId), invalid, 1);
        await AssertErrorAsync(response, HttpStatusCode.UnprocessableEntity, "validation_failed");

        var view = await ReadJsonAsync(await client.GetAsync(GradePath(assignmentId)));
        Assert.Equal(valid, view.GetProperty("assessment").GetProperty("score").GetDecimal());
        Assert.Equal(1, view.GetProperty("version").GetInt32());
        Assert.Equal(valid, (await LoadAsync(assignmentId)).Ledger!.FinalScore);
    }

    [Fact]
    public async Task GRD_BR_02_out_of_range_first_score_creates_nothing()
    {
        var assignmentId = GrdApiFixture.NextAssignmentId();
        using var client = fixture.CreateFactory().CreateClient();
        await AssertErrorAsync(await PutGradeAsync(client, GradePath(assignmentId), 101m, 0),
            HttpStatusCode.UnprocessableEntity, "validation_failed");

        var (assessment, item, ledger) = await LoadAsync(assignmentId);
        Assert.Null(assessment);
        Assert.Null(item);
        Assert.Null(ledger);
    }

    [Fact]
    public async Task GRD_BR_02_bounds_of_the_existing_grade_item_are_applied()
    {
        var assignmentId = GrdApiFixture.NextAssignmentId();
        using var client = await ClientWithGradeAsync(assignmentId, 10m);
        await ChangeLedgerAsync(assignmentId, (item, _) => item.MaxScore = 20m);

        await AssertErrorAsync(await PutGradeAsync(client, GradePath(assignmentId), 25m, 1),
            HttpStatusCode.UnprocessableEntity, "validation_failed");
        await ReadJsonAsync(await PutGradeAsync(client, GradePath(assignmentId), 20m, 1));
        Assert.Equal(20m, (await LoadAsync(assignmentId)).Ledger!.FinalScore);
    }

    [Fact]
    public async Task GRD_BR_02_scores_with_more_than_five_decimals_are_rejected()
    {
        var assignmentId = GrdApiFixture.NextAssignmentId();
        using var client = fixture.CreateFactory().CreateClient();
        await AssertErrorAsync(await PutGradeAsync(client, GradePath(assignmentId), 12.345678m, 0),
            HttpStatusCode.UnprocessableEntity, "validation_failed");
        await ReadJsonAsync(await PutGradeAsync(client, GradePath(assignmentId), 12.34567m, 0));
        Assert.Equal(12.34567m, (await LoadAsync(assignmentId)).Ledger!.FinalScore);
    }

    // ---- GRD-BR-01: korunan defter değerleri ----

    [Theory]
    [InlineData("locked", "grade_locked")]
    [InlineData("lock-at-passed", "grade_locked")]
    [InlineData("item-locked", "grade_locked")]
    [InlineData("item-lock-at-passed", "grade_locked")]
    [InlineData("overridden", "grade_overridden")]
    public async Task GRD_BR_01_protected_gradebook_value_rejects_teacher_write_with_409_and_nothing_changes(
        string protection, string code)
    {
        // T-GRD-01: kilitli not 85 ve elle değiştirilmiş not 72 olarak kalır.
        var assignmentId = GrdApiFixture.NextAssignmentId();
        using var client = await ClientWithGradeAsync(assignmentId, 85m);
        var past = DateTimeOffset.UtcNow.AddMinutes(-1);
        await ChangeLedgerAsync(assignmentId, (item, ledger) =>
        {
            switch (protection)
            {
                case "locked": ledger.Locked = true; break;
                case "lock-at-passed": ledger.LockAt = past; break;
                case "item-locked": item.Locked = true; break;
                case "item-lock-at-passed": item.LockAt = past; break;
                default: ledger.Overridden = true; ledger.FinalScore = 72m; break;
            }
        });
        var expectedFinal = protection == "overridden" ? 72m : 85m;

        using var response = await PutGradeAsync(client, GradePath(assignmentId), 60m, 1);
        await AssertErrorAsync(response, HttpStatusCode.Conflict, code);

        var (assessment, _, ledger) = await LoadAsync(assignmentId);
        Assert.Equal(85m, assessment!.Score);
        Assert.Equal(1, assessment.Version);
        Assert.Equal(85m, ledger!.RawScore);
        Assert.Equal(expectedFinal, ledger.FinalScore);
    }

    [Fact]
    public async Task GRD_BR_01_future_lock_time_does_not_block_writes_yet()
    {
        var assignmentId = GrdApiFixture.NextAssignmentId();
        using var client = await ClientWithGradeAsync(assignmentId, 50m);
        await ChangeLedgerAsync(assignmentId, (_, ledger) => ledger.LockAt = DateTimeOffset.UtcNow.AddDays(1));

        await ReadJsonAsync(await PutGradeAsync(client, GradePath(assignmentId), 55m, 1));
        Assert.Equal(55m, (await LoadAsync(assignmentId)).Ledger!.FinalScore);
    }

    // ---- Sürüm çakışması ----

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task GRD_FR_01_stale_or_unknown_version_returns_409_and_keeps_the_score(int version)
    {
        var assignmentId = GrdApiFixture.NextAssignmentId();
        using var client = await ClientWithGradeAsync(assignmentId, 70m);

        using var response = await PutGradeAsync(client, GradePath(assignmentId), 75m, version);
        await AssertErrorAsync(response, HttpStatusCode.Conflict, "version_conflict");

        var (assessment, _, ledger) = await LoadAsync(assignmentId);
        Assert.Equal(70m, assessment!.Score);
        Assert.Equal(1, assessment.Version);
        Assert.Equal(70m, ledger!.FinalScore);
    }

    [Fact]
    public async Task GRD_FR_01_first_save_requires_version_0()
    {
        var assignmentId = GrdApiFixture.NextAssignmentId();
        using var client = fixture.CreateFactory().CreateClient();
        await AssertErrorAsync(await PutGradeAsync(client, GradePath(assignmentId), 70m, 1),
            HttpStatusCode.Conflict, "version_conflict");
        Assert.Null((await LoadAsync(assignmentId)).Item);
    }

    // ---- Kapsam: ödev, kayıt, deneme ----

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task GRD_FR_01_missing_assignment_returns_404(string method, string? body)
    {
        using var client = fixture.CreateFactory().CreateClient();
        using var response = await SendAsync(client, method, GradePath(GrdApiFixture.MissingAssignmentId), body);
        await AssertErrorAsync(response, HttpStatusCode.NotFound, "not_found");
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task GRD_FR_01_learner_not_enrolled_in_the_assignment_course_returns_404_and_nothing_is_written(
        string method, string? body)
    {
        var assignmentId = GrdApiFixture.NextAssignmentId();
        using var client = fixture.CreateFactory().CreateClient();
        using var response = await SendAsync(client, method,
            GradePath(assignmentId, learnerId: GrdApiFixture.NotEnrolledLearnerId), body);
        await AssertErrorAsync(response, HttpStatusCode.NotFound, "not_found");
        Assert.Null((await LoadAsync(assignmentId, learnerId: GrdApiFixture.NotEnrolledLearnerId)).Item);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task GRD_FR_01_negative_attempt_returns_422(string method, string? body)
    {
        using var client = fixture.CreateFactory().CreateClient();
        using var response = await SendAsync(client, method,
            GradePath(GrdApiFixture.NextAssignmentId(), attempt: -1), body);
        await AssertErrorAsync(response, HttpStatusCode.UnprocessableEntity, "validation_failed");
    }

    // ---- Kimlik ve yetki ----

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task GRD_FR_01_every_endpoint_requires_a_session(string method, string? body)
    {
        using var client = fixture.CreateFactory(authenticated: false).CreateClient();
        using var response = await SendAsync(client, method, GradePath(GrdApiFixture.NextAssignmentId()), body);
        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "not_authenticated");
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task GRD_FR_01_every_endpoint_requires_grade_edit_and_nothing_is_written_without_it(string method, string? body)
    {
        var assignmentId = GrdApiFixture.NextAssignmentId();
        using var client = fixture.CreateFactory(permission: (_, _) => false).CreateClient();
        using var response = await SendAsync(client, method, GradePath(assignmentId), body);
        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "forbidden");
        Assert.Null((await LoadAsync(assignmentId)).Item);
    }

    [Fact]
    public async Task GRD_FR_01_unauthorised_user_cannot_probe_enrolment()
    {
        // Yetki, kayıt denetiminden önce gelir: kayıtsız öğrenci için de 403 döner, 404 değil.
        using var client = fixture.CreateFactory(permission: (_, _) => false).CreateClient();
        using var response = await client.GetAsync(
            GradePath(GrdApiFixture.NextAssignmentId(), learnerId: GrdApiFixture.NotEnrolledLearnerId));
        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "forbidden");
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task GRD_FR_01_grade_edit_is_requested_in_the_assignment_course_context(string method, string? body)
    {
        var calls = new List<(string Capability, ContextRef Context)>();
        using var client = fixture.CreateFactory(permission: (capability, context) =>
        {
            lock (calls) calls.Add((capability, context));
            return capability == "grade.edit" && context == ContextRef.ForCourse(GrdApiFixture.CourseId);
        }).CreateClient();

        using var response = await SendAsync(client, method, GradePath(GrdApiFixture.NextAssignmentId()), body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(("grade.edit", ContextRef.ForCourse(GrdApiFixture.CourseId)), Assert.Single(calls));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{"score":"seksen","version":0}""")]
    public async Task GRD_FR_01_malformed_body_returns_400_without_details(string body)
    {
        var assignmentId = GrdApiFixture.NextAssignmentId();
        using var client = fixture.CreateFactory().CreateClient();
        using var response = await SendAsync(client, "PUT", GradePath(assignmentId), body);
        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "bad_request");
        Assert.Null((await LoadAsync(assignmentId)).Item);
    }
}

public sealed class GrdUnavailableAuthFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Production");
}

/// <summary>Depodaki AUTH sahteleriyle GRD hiçbir ortamda açılmaz.</summary>
public sealed class GrdUnavailableAuthTests(GrdUnavailableAuthFactory factory) : IClassFixture<GrdUnavailableAuthFactory>
{
    [Theory]
    [MemberData(nameof(GrdGradeEndpointTests.Routes), MemberType = typeof(GrdGradeEndpointTests))]
    public async Task GRD_FR_01_production_rejects_grade_endpoints_with_AUTH_placeholders(string method, string? body)
    {
        using var client = factory.CreateClient();
        using var response = await SendAsync(client, method, GradePath(1), body);
        await AssertErrorAsync(response, HttpStatusCode.ServiceUnavailable, "auth_not_ready");
    }

    [Fact]
    public async Task GRD_FR_01_grade_endpoints_are_published_in_OpenAPI()
    {
        using var client = factory.CreateClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/api/v1/openapi.json"));
        var path = document.RootElement.GetProperty("paths")
            .GetProperty("/api/v1/assignments/{assignmentId}/learners/{learnerId}/attempts/{attempt}/grade");
        Assert.True(path.TryGetProperty("get", out _));
        Assert.True(path.TryGetProperty("put", out _));
    }
}
