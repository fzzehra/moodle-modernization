using System.Net;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moodle.Web.Grd;
using Moodle.Web.Pages;
using static Moodle.UnitTests.GrdWebTestSupport;

namespace Moodle.UnitTests;

/// <summary>GRD Blazor sayfaları: yükleme, veri, boş durum, hata mesajları, kaydetme akışı ve sayfalama.</summary>
public sealed class GrdBlazorTests
{
    private static IRenderedComponent<IComponent> RenderPage(BunitContext context, Type pageType) =>
        context.Render(builder =>
        {
            builder.OpenComponent(0, pageType);
            if (pageType == typeof(GradeEditor))
            {
                builder.AddAttribute(1, nameof(GradeEditor.AssignmentId), 10);
                builder.AddAttribute(2, nameof(GradeEditor.LearnerId), 3);
            }
            else
            {
                builder.AddAttribute(1, "CourseId", 7);
            }
            builder.CloseComponent();
        });

    public static IEnumerable<object[]> Pages() =>
        new[] { typeof(GradeEditor), typeof(MyGrades), typeof(Gradebook) }.Select(x => new object[] { x });

    public static IEnumerable<object[]> PageErrors()
    {
        var errors = new (int Status, string Code, string Text)[]
        {
            (401, "not_authenticated", "oturum açmanız gerekiyor"),
            (403, "forbidden", "yetkiniz yok"),
            (404, "not_found", "bulunamadı"),
            (503, "auth_not_ready", "AUTH"),
            (500, "internal_error", "daha sonra"),
        };
        foreach (var page in new[] { typeof(GradeEditor), typeof(MyGrades), typeof(Gradebook) })
            foreach (var (status, code, text) in errors)
                yield return [page, status, code, text];
    }

    // ---- Ortak durumlar ----

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task GRD_pages_show_loading_until_the_API_answers(Type pageType)
    {
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new GrdWebHandler((_, _) => pending.Task);
        await using var context = Context(handler);
        var page = RenderPage(context, pageType);

        page.WaitForAssertion(() => Assert.Contains("Not bilgileri yükleniyor", page.Markup));
        Assert.Empty(page.FindAll("table"));
        Assert.Empty(page.FindAll("form"));

        page.WaitForAssertion(() => Assert.Single(handler.Requests));
        pending.SetResult(Default(Assert.Single(handler.Requests)));
        page.WaitForAssertion(() => Assert.DoesNotContain("Not bilgileri yükleniyor", page.Markup));
    }

    [Theory]
    [MemberData(nameof(PageErrors))]
    public async Task GRD_pages_explain_each_error_without_internal_details(Type pageType, int status, string code, string text)
    {
        using var handler = Handler(_ => Error((HttpStatusCode)status, code));
        await using var context = Context(handler);
        var page = RenderPage(context, pageType);

        page.WaitForAssertion(() => Assert.Contains(text, page.Find("[role=alert]").TextContent));
        Assert.Equal(code, page.Find("[role=alert]").GetAttribute("data-error-code"));
        Assert.DoesNotContain("private", page.Markup);
        Assert.Empty(page.FindAll("table"));
        Assert.Empty(page.FindAll("form"));
        if (code == "auth_not_ready")
            Assert.Contains("Not işlemleri henüz kullanılamıyor", page.Find("[role=alert] h2").TextContent);

        // Yeniden deneme yalnızca aynı okuma isteğini tekrarlar.
        await page.Find("[role=alert] button").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Equal(2, handler.Requests.Count));
        Assert.All(handler.Requests, x => Assert.Equal("GET", x.Method));
    }

    // ---- GradeEditor (GRD-FR-01, BR-01, BR-02) ----

    [Fact]
    public async Task GRD_FR_01_editor_shows_current_score_bounds_and_feedback()
    {
        using var handler = Handler();
        await using var context = Context(handler);
        var page = RenderPage(context, typeof(GradeEditor));

        page.WaitForAssertion(() => Assert.Equal("85", page.Find("[data-testid=current-score]").TextContent));
        Assert.Equal("0–100", page.Find("[data-testid=score-range]").TextContent);
        Assert.Contains("18.11.2026 09:30 UTC", page.Markup);
        Assert.Contains("Ödev #10 · Öğrenci #3 · Deneme 0", page.Markup);
        Assert.Equal("85", page.Find("#grade-score").GetAttribute("value"));
        Assert.Equal("İyi çalışma", page.Find("#grade-feedback").GetAttribute("value"));
        Assert.Equal("any", page.Find("#grade-score").GetAttribute("step"));
        Assert.Equal(GradePath, Assert.Single(handler.Requests).PathAndQuery);
    }

    [Fact]
    public async Task GRD_FR_01_ungraded_learner_is_shown_and_first_save_uses_version_0()
    {
        using var handler = Handler(request => request.Method == "PUT"
            ? Json(new GrdGradeSaveResult(Assessment with { Score = 70m, Feedback = null }, true, 0m, 100m))
            : Json(EmptyView));
        await using var context = Context(handler);
        var page = RenderPage(context, typeof(GradeEditor));

        page.WaitForAssertion(() => Assert.Equal("Henüz notlandırılmadı", page.Find("[data-testid=current-score]").TextContent));
        page.Find("#grade-score").Change("70");
        await page.Find("form").SubmitAsync(EventArgs.Empty);

        using var body = JsonDocument.Parse(Assert.Single(handler.Requests, x => x.Method == "PUT").Body!);
        Assert.Equal(70m, body.RootElement.GetProperty("score").GetDecimal());
        Assert.Equal(0, body.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("feedback").ValueKind);
    }

    [Fact]
    public async Task GRD_BR_01_save_sends_version_and_reports_gradebook_transfer()
    {
        using var handler = Handler(request => request.Method == "PUT"
            ? Json(new GrdGradeSaveResult(Assessment with { Score = 90.5m, Feedback = "Daha iyi", Version = 2 }, true, 0m, 100m))
            : Default(request));
        await using var context = Context(handler);
        var page = RenderPage(context, typeof(GradeEditor));
        page.WaitForAssertion(() => Assert.Equal("85", page.Find("[data-testid=current-score]").TextContent));

        page.Find("#grade-score").Change("90.5");
        page.Find("#grade-feedback").Change("Daha iyi");
        await page.Find("form").SubmitAsync(EventArgs.Empty);

        page.WaitForAssertion(() => Assert.Contains("Puan kaydedildi ve deftere aktarıldı.", page.Find(".crs-success").TextContent));
        Assert.Equal("90,5", page.Find("[data-testid=current-score]").TextContent);
        var put = Assert.Single(handler.Requests, x => x.Method == "PUT");
        Assert.Equal(GradePath, put.PathAndQuery);
        using var body = JsonDocument.Parse(put.Body!);
        Assert.Equal(90.5m, body.RootElement.GetProperty("score").GetDecimal());
        Assert.Equal("Daha iyi", body.RootElement.GetProperty("feedback").GetString());
        Assert.Equal(1, body.RootElement.GetProperty("version").GetInt32());

        // Sonraki kayıt yanıttaki yeni sürümü kullanır.
        await page.Find("form").SubmitAsync(EventArgs.Empty);
        using var second = JsonDocument.Parse(handler.Requests.Where(x => x.Method == "PUT").Last().Body!);
        Assert.Equal(2, second.RootElement.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task GRD_BR_01_older_attempt_save_reports_that_the_gradebook_is_protected()
    {
        using var handler = Handler(request => request.Method == "PUT"
            ? Json(new GrdGradeSaveResult(Assessment with { Version = 2 }, false, 0m, 100m))
            : Default(request));
        await using var context = Context(handler);
        var page = RenderPage(context, typeof(GradeEditor));
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("form")));

        await page.Find("form").SubmitAsync(EventArgs.Empty);
        page.WaitForAssertion(() => Assert.Contains("Defter korunuyor", page.Find(".crs-success").TextContent));
    }

    [Fact]
    public async Task GRD_BR_02_out_of_range_score_shows_a_form_error_and_keeps_the_input()
    {
        using var handler = Handler(request => request.Method == "PUT"
            ? Error(HttpStatusCode.UnprocessableEntity, "validation_failed", "Puan 0 ile 100 arasında olmalıdır.")
            : Default(request));
        await using var context = Context(handler);
        var page = RenderPage(context, typeof(GradeEditor));
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("form")));

        page.Find("#grade-score").Change("101");
        await page.Find("form").SubmitAsync(EventArgs.Empty);

        page.WaitForAssertion(() => Assert.Equal("Puan 0 ile 100 arasında olmalıdır.", page.Find("form #grade-score-error").TextContent));
        Assert.Equal("true", page.Find("#grade-score").GetAttribute("aria-invalid"));
        Assert.Equal("101", page.Find("#grade-score").GetAttribute("value"));
        Assert.Equal("85", page.Find("[data-testid=current-score]").TextContent);
        Assert.Empty(page.FindAll("section[role=alert]"));
        Assert.False(page.Find("fieldset").HasAttribute("disabled"));
    }

    [Theory]
    [InlineData("grade_locked", "kilitli")]
    [InlineData("grade_overridden", "elle değiştirildiği")]
    public async Task GRD_BR_01_protected_gradebook_value_shows_a_protected_message(string code, string reason)
    {
        using var handler = Handler(request => request.Method == "PUT"
            ? Error(HttpStatusCode.Conflict, code)
            : Default(request));
        await using var context = Context(handler);
        var page = RenderPage(context, typeof(GradeEditor));
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("form")));

        page.Find("#grade-score").Change("60");
        await page.Find("form").SubmitAsync(EventArgs.Empty);

        page.WaitForAssertion(() => Assert.Equal("Bu not defterde korunuyor", page.Find("[role=alert] h2").TextContent));
        Assert.Contains("Bu not defterde korunuyor", page.Find("[role=alert] p").TextContent);
        Assert.Contains(reason, page.Find("[role=alert] p").TextContent);
        Assert.Equal("85", page.Find("[data-testid=current-score]").TextContent);
        Assert.Equal("60", page.Find("#grade-score").GetAttribute("value"));
        Assert.Empty(page.FindAll(".crs-success"));
    }

    [Fact]
    public async Task GRD_FR_01_version_conflict_asks_to_refresh_and_refresh_loads_the_current_value()
    {
        var current = GradedView;
        using var handler = Handler(request => request.Method == "PUT"
            ? Error(HttpStatusCode.Conflict, "version_conflict")
            : Json(current));
        await using var context = Context(handler);
        var page = RenderPage(context, typeof(GradeEditor));
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("form")));

        page.Find("#grade-score").Change("60");
        await page.Find("form").SubmitAsync(EventArgs.Empty);
        page.WaitForAssertion(() => Assert.Contains("başka biri değiştirdi", page.Find("[role=alert] p").TextContent));
        Assert.Equal("Başka biri değiştirdi", page.Find("[role=alert] h2").TextContent);
        Assert.Equal("Yenile", page.Find("[role=alert] button").TextContent);

        current = new GrdGradeView(Assessment with { Score = 77m, Version = 3 }, 0m, 100m, 3);
        await page.Find("[role=alert] button").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Equal("77", page.Find("[data-testid=current-score]").TextContent));
        Assert.Equal("77", page.Find("#grade-score").GetAttribute("value"));
        Assert.Empty(page.FindAll("[role=alert]"));

        await page.Find("form").SubmitAsync(EventArgs.Empty);
        using var body = JsonDocument.Parse(handler.Requests.Where(x => x.Method == "PUT").Last().Body!);
        Assert.Equal(3, body.RootElement.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task GRD_FR_01_pending_save_disables_duplicate_writes()
    {
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new GrdWebHandler((request, _) => request.Method == "PUT" ? pending.Task : Task.FromResult(Default(request)));
        await using var context = Context(handler);
        var page = RenderPage(context, typeof(GradeEditor));
        page.WaitForAssertion(() => Assert.NotEmpty(page.FindAll("form")));

        var submit = page.Find("form").SubmitAsync(EventArgs.Empty);
        page.WaitForAssertion(() => Assert.True(page.Find("fieldset").HasAttribute("disabled")));
        Assert.Single(handler.Requests, x => x.Method == "PUT");
        pending.SetResult(Json(new GrdGradeSaveResult(Assessment with { Version = 2 }, true, 0m, 100m)));
        await submit;
        page.WaitForAssertion(() => Assert.False(page.Find("fieldset").HasAttribute("disabled")));
    }

    [Fact]
    public async Task GRD_FR_01_attempt_is_taken_from_the_query_string()
    {
        using var handler = Handler(_ => Json(GradedView));
        await using var context = Context(handler);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("assignments/10/learners/3/grade?attempt=2");
        var page = RenderPage(context, typeof(GradeEditor));

        page.WaitForAssertion(() => Assert.Contains("Deneme 2", page.Markup));
        page.WaitForAssertion(() => Assert.Equal("/api/v1/assignments/10/learners/3/attempts/2/grade",
            Assert.Single(handler.Requests).PathAndQuery));
    }

    // ---- MyGrades (GRD-FR-02, NFR-01) ----

    [Fact]
    public async Task GRD_FR_02_my_grades_lists_items_with_dash_for_empty_scores()
    {
        using var handler = Handler();
        await using var context = Context(handler);
        var page = RenderPage(context, typeof(MyGrades));

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("tbody tr").Count));
        var first = page.Find("tr[data-item-id='1']");
        Assert.Contains("Ödev 1", first.TextContent);
        Assert.Contains("85", first.QuerySelector(".grd-score")!.TextContent);
        Assert.Contains("0–100", first.TextContent);
        var second = page.Find("tr[data-item-id='2']");
        Assert.StartsWith("—", second.QuerySelector(".grd-score")!.TextContent);
        Assert.Contains("0–20", second.TextContent);
        Assert.Equal(new GrdWebRequest("GET", MyGradesPath, null), Assert.Single(handler.Requests));
    }

    [Fact]
    public async Task GRD_FR_02_my_grades_empty_course_shows_a_message()
    {
        using var handler = Handler(_ => Json(new GrdMyGrades(7, [])));
        await using var context = Context(handler);
        var page = RenderPage(context, typeof(MyGrades));
        page.WaitForAssertion(() => Assert.Contains("görüntülenecek not bulunmuyor", page.Markup));
        Assert.Empty(page.FindAll("table"));
    }

    // ---- Gradebook (GRD-FR-03) ----

    [Fact]
    public async Task GRD_FR_03_gradebook_shows_learners_items_flags_and_editor_links()
    {
        using var handler = Handler();
        await using var context = Context(handler);
        var page = RenderPage(context, typeof(Gradebook));

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("tbody tr").Count));
        Assert.Contains("Öğrenci #3", page.Find("tr[data-learner-id='3'] th").TextContent);
        Assert.Contains("Öğrenci adları kimlik sistemi tamamlandığında", page.Markup);

        var headers = page.FindAll("thead th[data-item-id]");
        Assert.Equal(["1", "2"], headers.Select(x => x.GetAttribute("data-item-id")));
        Assert.Contains("0–100", headers[0].TextContent);
        Assert.Empty(headers[0].QuerySelectorAll("[data-flag]"));
        Assert.NotNull(headers[1].QuerySelector("[data-flag=hidden]"));

        // Ödev hücresi değerlendirme ekranına, en son deneme ile bağlanır.
        var lockedCell = page.Find("tr[data-learner-id='3'] td[data-item-id='1']");
        Assert.Equal("assignments/10/learners/3/grade?attempt=1", lockedCell.QuerySelector("a")!.GetAttribute("href"));
        Assert.Equal("85", lockedCell.QuerySelector("a")!.TextContent);
        Assert.NotNull(lockedCell.QuerySelector("[data-flag=locked]"));
        Assert.Null(lockedCell.QuerySelector("[data-flag=overridden]"));

        var overriddenCell = page.Find("tr[data-learner-id='4'] td[data-item-id='1']");
        Assert.Equal("assignments/10/learners/4/grade", overriddenCell.QuerySelector("a")!.GetAttribute("href"));
        Assert.NotNull(overriddenCell.QuerySelector("[data-flag=overridden]"));
        Assert.Contains("Elle değiştirilmiş", overriddenCell.TextContent);

        // Manuel kalem hücresi bağlantı içermez; boş puan "—".
        var manualCell = page.Find("tr[data-learner-id='3'] td[data-item-id='2']");
        Assert.Null(manualCell.QuerySelector("a"));
        Assert.StartsWith("—", manualCell.TextContent.Trim());
        Assert.NotNull(manualCell.QuerySelector("[data-flag=hidden]"));
    }

    [Fact]
    public async Task GRD_FR_03_next_page_uses_the_cursor_and_first_page_returns()
    {
        using var handler = Handler();
        await using var context = Context(handler);
        var page = RenderPage(context, typeof(Gradebook));
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("tbody tr").Count));
        Assert.DoesNotContain(page.FindAll("button"), x => x.TextContent == "İlk sayfa");

        await page.Find("button.crs-primary").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.NotNull(page.Find("tr[data-learner-id='5']")));
        Assert.Single(page.FindAll("tbody tr"));
        Assert.Equal(GradebookPath + "?cursor=4", handler.Requests.Last().PathAndQuery);
        Assert.Empty(page.FindAll("button.crs-primary"));

        await page.Find(".crs-actions button").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("tbody tr").Count));
        Assert.Equal(GradebookPath, handler.Requests.Last().PathAndQuery);
    }

    [Fact]
    public async Task GRD_FR_03_failed_next_page_keeps_the_current_page()
    {
        using var handler = Handler(request => request.PathAndQuery.Contains("cursor")
            ? Error(HttpStatusCode.InternalServerError, "internal_error")
            : Default(request));
        await using var context = Context(handler);
        var page = RenderPage(context, typeof(Gradebook));
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("tbody tr").Count));

        await page.Find("button.crs-primary").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Contains("daha sonra", page.Find("[role=alert]").TextContent));
        Assert.Equal(2, page.FindAll("tbody tr").Count);

        // Yeniden deneme ilk sayfayı (görünen sayfayı) yeniler, başarısız imleci değil.
        await page.Find("[role=alert] button").ClickAsync(new MouseEventArgs());
        page.WaitForAssertion(() => Assert.Empty(page.FindAll("[role=alert]")));
        Assert.Equal(GradebookPath, handler.Requests.Last().PathAndQuery);
    }

    [Fact]
    public async Task GRD_FR_03_empty_gradebook_states_are_explained()
    {
        using var noLearners = Handler(_ => Json(FirstPage with { Learners = [], NextCursor = null }));
        await using (var context = Context(noLearners))
        {
            var page = RenderPage(context, typeof(Gradebook));
            page.WaitForAssertion(() => Assert.Contains("etkin kayıtlı öğrenci bulunmuyor", page.Markup));
            Assert.Empty(page.FindAll("table"));
        }

        using var noItems = Handler(_ => Json(FirstPage with { Items = [], NextCursor = null }));
        await using (var context = Context(noItems))
        {
            var page = RenderPage(context, typeof(Gradebook));
            page.WaitForAssertion(() => Assert.Contains("henüz not kalemi bulunmuyor", page.Markup));
            Assert.Empty(page.FindAll("table"));
        }
    }

    // ---- Menü hedefi ----

    [Fact]
    public async Task GRD_grades_landing_links_to_both_reports_for_the_entered_course()
    {
        using var handler = Handler();
        await using var context = Context(handler);
        var page = context.Render<Grades>();
        Assert.Empty(page.FindAll("a[href$='my-grades']"));

        page.Find("#grades-course").Input("7");
        Assert.Equal("courses/7/my-grades", page.Find("a[href$='my-grades']").GetAttribute("href"));
        Assert.Equal("courses/7/gradebook", page.Find("a[href$='gradebook']").GetAttribute("href"));
        Assert.Empty(handler.Requests);
    }
}
