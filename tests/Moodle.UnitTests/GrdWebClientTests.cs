using System.Net;
using System.Text;
using System.Text.Json;
using Moodle.Web.Grd;
using static Moodle.UnitTests.GrdWebTestSupport;

namespace Moodle.UnitTests;

/// <summary>GrdApiClient: doğru uçlar, gövde sözleşmesi ve kullanıcıya gösterilen hata mesajları.</summary>
public sealed class GrdWebClientTests
{
    [Fact]
    public async Task GRD_FR_01_grade_is_read_from_the_attempt_endpoint()
    {
        using var handler = Handler(_ => Json(GradedView));
        using var client = Client(handler);
        var view = await client.GradeAsync(10, 3, 2);
        Assert.Equal(85m, view.Assessment!.Score);
        Assert.Equal(new GrdWebRequest("GET", "/api/v1/assignments/10/learners/3/attempts/2/grade", null), Assert.Single(handler.Requests));
    }

    [Fact]
    public async Task GRD_FR_01_grade_is_saved_with_score_feedback_and_version_only()
    {
        using var handler = Handler(_ => Json(new GrdGradeSaveResult(Assessment, true, 0m, 100m)));
        using var client = Client(handler);
        var result = await client.SaveGradeAsync(10, 3, 0, new GradeWriteRequest(90.5m, "Güzel", 1));
        Assert.True(result.GradebookUpdated);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("PUT", request.Method);
        Assert.Equal(GradePath, request.PathAndQuery);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal(["score", "feedback", "version"], body.RootElement.EnumerateObject().Select(x => x.Name));
        Assert.Equal(90.5m, body.RootElement.GetProperty("score").GetDecimal());
        Assert.Equal("Güzel", body.RootElement.GetProperty("feedback").GetString());
        Assert.Equal(1, body.RootElement.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task GRD_NFR_01_my_grades_never_sends_a_learner_or_query()
    {
        using var handler = Handler();
        using var client = Client(handler);
        Assert.Equal(2, (await client.MyGradesAsync(7)).Items.Count);
        Assert.Equal(new GrdWebRequest("GET", MyGradesPath, null), Assert.Single(handler.Requests));
    }

    [Theory]
    [InlineData(null, null, GradebookPath)]
    [InlineData("4", null, GradebookPath + "?cursor=4")]
    [InlineData("4", 25, GradebookPath + "?cursor=4&limit=25")]
    [InlineData(null, 25, GradebookPath + "?limit=25")]
    public async Task GRD_FR_03_gradebook_passes_cursor_and_limit(string? cursor, int? limit, string expected)
    {
        using var handler = Handler(_ => Json(FirstPage));
        using var client = Client(handler);
        var book = await client.GradebookAsync(7, cursor, limit);
        Assert.Equal("4", book.NextCursor);
        Assert.Equal(expected, Assert.Single(handler.Requests).PathAndQuery);
    }

    [Theory]
    [InlineData(401, "not_authenticated", "oturum açmanız gerekiyor")]
    [InlineData(403, "forbidden", "yetkiniz yok")]
    [InlineData(404, "not_found", "bulunamadı")]
    [InlineData(409, "grade_locked", "Bu not defterde korunuyor")]
    [InlineData(409, "grade_overridden", "Bu not defterde korunuyor")]
    [InlineData(409, "version_conflict", "başka biri değiştirdi")]
    [InlineData(409, "conflict", "çakışıyor")]
    [InlineData(400, "learner_selection_not_allowed", "İstek geçersiz")]
    [InlineData(503, "auth_not_ready", "AUTH")]
    [InlineData(500, "internal_error", "daha sonra")]
    public async Task GRD_errors_are_translated_without_showing_internal_messages(int status, string code, string expected)
    {
        using var handler = Handler(_ => Error((HttpStatusCode)status, code));
        using var client = Client(handler);
        var error = await Assert.ThrowsAsync<GrdApiException>(() => client.MyGradesAsync(7));
        Assert.Equal((HttpStatusCode)status, error.Status);
        Assert.Equal(code, error.Code);
        Assert.Contains(expected, error.Message);
        Assert.DoesNotContain("private", error.Message);
        Assert.Equal(code is "grade_locked" or "grade_overridden", error.IsProtectedGrade);
    }

    [Fact]
    public async Task GRD_BR_02_validation_message_from_the_server_is_shown_to_the_form()
    {
        using var handler = Handler(_ => Error(HttpStatusCode.UnprocessableEntity, "validation_failed", "Puan 0 ile 100 arasında olmalıdır."));
        using var client = Client(handler);
        var error = await Assert.ThrowsAsync<GrdApiException>(() => client.SaveGradeAsync(10, 3, 0, new(101m, null, 0)));
        Assert.Equal("Puan 0 ile 100 arasında olmalıdır.", error.Message);
    }

    [Fact]
    public async Task GRD_BR_02_validation_without_message_gets_a_generic_text()
    {
        using var handler = Handler(_ => new(HttpStatusCode.UnprocessableEntity) { Content = new StringContent("{}", Encoding.UTF8, "application/json") });
        using var client = Client(handler);
        var error = await Assert.ThrowsAsync<GrdApiException>(() => client.SaveGradeAsync(10, 3, 0, new(101m, null, 0)));
        Assert.Contains("geçersiz", error.Message);
        Assert.Equal("request_failed", error.Code);
    }

    [Theory]
    [InlineData("<html>private failure</html>")]
    [InlineData("[]")]
    [InlineData("null")]
    public async Task Nonstandard_error_bodies_do_not_crash_the_UI(string body)
    {
        using var handler = Handler(_ => new(HttpStatusCode.BadGateway) { Content = new StringContent(body, Encoding.UTF8) });
        using var client = Client(handler);
        var error = await Assert.ThrowsAsync<GrdApiException>(() => client.GradebookAsync(7));
        Assert.Equal(HttpStatusCode.BadGateway, error.Status);
        Assert.DoesNotContain("private", error.Message);
    }

    [Fact]
    public async Task Invalid_success_body_is_reported_as_invalid_response()
    {
        using var handler = Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent("not json", Encoding.UTF8) });
        using var client = Client(handler);
        Assert.Equal("invalid_response", (await Assert.ThrowsAsync<GrdApiException>(() => client.MyGradesAsync(7))).Code);
    }

    [Fact]
    public async Task Network_failures_produce_a_retryable_message()
    {
        using var handler = new GrdWebHandler((_, _) => throw new HttpRequestException("private socket detail"));
        using var client = Client(handler);
        var error = await Assert.ThrowsAsync<GrdApiException>(() => client.GradeAsync(10, 3, 0));
        Assert.Equal("connection_failed", error.Code);
        Assert.DoesNotContain("private", error.Message);
    }

    [Theory]
    [InlineData(null, "—")]
    [InlineData(0d, "0")]
    [InlineData(85d, "85")]
    [InlineData(85.5, "85,5")]
    [InlineData(12.34567, "12,34567")]
    public void GRD_FR_02_scores_are_shown_with_dash_for_empty_and_Turkish_decimals(double? score, string expected) =>
        Assert.Equal(expected, GrdDisplay.Score((decimal?)score));
}
