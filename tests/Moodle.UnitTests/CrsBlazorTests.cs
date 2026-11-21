using System.Net;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Moodle.Web.Crs;
using Moodle.Web.Pages;
using static Moodle.UnitTests.CrsWebTestSupport;

namespace Moodle.UnitTests;

public sealed class CrsBlazorTests
{
    [Theory]
    [InlineData(typeof(Courses))]
    [InlineData(typeof(CourseDetails))]
    [InlineData(typeof(CourseParticipants))]
    public async Task AUTH_unavailable_is_explained_and_mutations_are_disabled(Type pageType)
    {
        using var handler = Handler(_ => Json(new { error = new { code = "auth_not_ready", message = "Internal placeholder" } }, HttpStatusCode.ServiceUnavailable));
        await using var context = Context(handler);
        var page = context.Render(builder =>
        {
            builder.OpenComponent(0, pageType);
            if (pageType != typeof(Courses)) builder.AddAttribute(1, "CourseId", 1);
            builder.CloseComponent();
        });
        page.WaitForAssertion(() => Assert.Contains("AUTH", page.Find("[role=alert]").TextContent));
        Assert.DoesNotContain("Internal placeholder", page.Markup);
        Assert.Empty(page.FindAll("form"));
        Assert.True(page.Find(".crs-heading button").HasAttribute("disabled"));
        await page.Find("[role=alert] button").ClickAsync(new MouseEventArgs());
        Assert.All(handler.Requests, request => Assert.Equal("GET", request.Method));
    }

    [Fact]
    public async Task Course_list_filters_by_category_and_keeps_navigation_links()
    {
        using var handler = Handler();
        await using var context = Context(handler);
        var page = context.Render<Courses>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("tbody tr").Count));
        page.Find("#category-filter").Change("1");
        Assert.Single(page.FindAll("tbody tr"));
        Assert.Contains("Cebir", page.Find("tbody").TextContent);
        Assert.Equal("courses/1", page.Find("tbody td a").GetAttribute("href"));
        Assert.NotNull(page.Find("a[href='courses/1/participants']"));
        page.Find("#category-filter").Change("2");
        Assert.Contains("Fizik", page.Find("tbody").TextContent);
        page.Find("#category-filter").Change("0");
        Assert.Equal(2, page.FindAll("tbody tr").Count);
    }

    [Fact]
    public async Task Course_creation_and_editing_send_the_API_contract()
    {
        using var handler = Handler(request => request.Method is "POST" or "PATCH" ? Json(First, HttpStatusCode.Created) : Default(request));
        await using var context = Context(handler);
        var page = context.Render<Courses>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("tbody tr").Count));
        await page.Find(".crs-heading button").ClickAsync(new MouseEventArgs());
        page.Find("#course-name").Change("Yeni ders");
        page.Find("#course-short-name").Change("NEW101");
        page.Find("#course-category").Change("1");
        await page.Find("form").SubmitAsync(EventArgs.Empty);
        page.WaitForAssertion(() => Assert.Contains("Ders kaydedildi", page.Markup));
        var created = Assert.Single(handler.Requests, x => x.Method == "POST");
        using (var body = JsonDocument.Parse(created.Body!))
        {
            Assert.Equal("/api/v1/courses", created.Path);
            Assert.Equal("Yeni ders", body.RootElement.GetProperty("fullName").GetString());
            Assert.Equal(1, body.RootElement.GetProperty("categoryId").GetInt32());
        }
        await page.Find("tr[data-course-id='1'] button[aria-label$='düzenle']").ClickAsync(new MouseEventArgs());
        page.Find("#course-name").Change("Düzenlendi");
        await page.Find("form").SubmitAsync(EventArgs.Empty);
        var updated = Assert.Single(handler.Requests, x => x.Method == "PATCH");
        Assert.Equal("/api/v1/courses/1", updated.Path);
        using var patch = JsonDocument.Parse(updated.Body!);
        Assert.Equal("Düzenlendi", patch.RootElement.GetProperty("fullName").GetString());
        Assert.Equal("MAT101", patch.RootElement.GetProperty("shortName").GetString());
    }

    [Fact]
    public async Task Course_deletion_requires_confirmation_and_cancel_does_not_write()
    {
        using var handler = Handler(request => request.Method == "DELETE" ? new(HttpStatusCode.NoContent) : Default(request));
        await using var context = Context(handler);
        var page = context.Render<Courses>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("tbody tr").Count));
        await page.Find("tr[data-course-id='1'] button[aria-label$='sil']").ClickAsync(new MouseEventArgs());
        Assert.DoesNotContain(handler.Requests, x => x.Method == "DELETE");
        await page.Find("[role=alertdialog] button:last-child").ClickAsync(new MouseEventArgs());
        Assert.Empty(page.FindAll("[role=alertdialog]"));
        Assert.DoesNotContain(handler.Requests, x => x.Method == "DELETE");
        await page.Find("tr[data-course-id='1'] button[aria-label$='sil']").ClickAsync(new MouseEventArgs());
        await page.Find("[role=alertdialog] button:first-child").ClickAsync(new MouseEventArgs());
        Assert.Equal("/api/v1/courses/1", Assert.Single(handler.Requests, x => x.Method == "DELETE").Path);
        Assert.Contains("Ders silindi", page.Markup);
    }

    [Fact]
    public async Task Details_show_category_visibility_dates_and_editable_information()
    {
        using var handler = Handler(request => request.Method == "PATCH" ? Json(First with { FullName = "Güncel" }) : Default(request));
        await using var context = Context(handler);
        var page = context.Render<CourseDetails>(p => p.Add(x => x.CourseId, 1));
        page.WaitForAssertion(() => Assert.Contains("Cebir", page.Markup));
        Assert.Contains("Matematik", page.Markup);
        Assert.Contains("Görünür", page.Markup);
        Assert.Contains("01.01.2026 00:00 UTC", page.Markup);
        Assert.Contains("Belirtilmedi", page.Markup);
        var links = page.FindAll("section[aria-label='Ders bilgileri'] a").Select(x => x.GetAttribute("href")).ToList();
        Assert.Contains("/courses/1/files", links);
        Assert.Contains("/courses/1/gradebook", links);
        Assert.Contains("/courses/1/my-grades", links);
        await page.Find(".crs-heading button").ClickAsync(new MouseEventArgs());
        page.Find("#course-name").Change("Güncel");
        await page.Find("form").SubmitAsync(EventArgs.Empty);
        Assert.Contains("Güncel", page.Find("section[aria-label='Ders bilgileri']").TextContent);
    }

    [Fact]
    public async Task Participants_register_users_edit_status_without_resending_role_and_unenrol()
    {
        using var handler = Handler(request => request.Method is "POST" or "PATCH" ? Json(Enrolment)
            : request.Method == "DELETE" ? new(HttpStatusCode.NoContent) : Default(request));
        await using var context = Context(handler);
        var page = context.Render<CourseParticipants>(p => p.Add(x => x.CourseId, 1));
        page.WaitForAssertion(() => Assert.Contains("Kullanıcı #42", page.Markup));
        Assert.Contains("Student", page.Markup);
        Assert.Contains("Aktif", page.Markup);
        await page.Find(".crs-heading button").ClickAsync(new MouseEventArgs());
        page.Find("#enrolment-user").Change("55");
        await page.Find("form").SubmitAsync(EventArgs.Empty);
        var created = Assert.Single(handler.Requests, x => x.Method == "POST");
        using (var body = JsonDocument.Parse(created.Body!)) Assert.Equal(55, body.RootElement.GetProperty("userId").GetInt32());
        await page.Find("tbody button[aria-label$='düzenle']").ClickAsync(new MouseEventArgs());
        Assert.True(page.Find("#enrolment-user").HasAttribute("disabled"));
        page.Find("input[type=checkbox]").Change(true);
        await page.Find("form").SubmitAsync(EventArgs.Empty);
        using (var body = JsonDocument.Parse(Assert.Single(handler.Requests, x => x.Method == "PATCH").Body!))
        {
            Assert.True(body.RootElement.GetProperty("suspended").GetBoolean());
            Assert.False(body.RootElement.TryGetProperty("role", out _));
            Assert.False(body.RootElement.TryGetProperty("userId", out _));
        }
        await page.Find("tbody button[aria-label$='kaldır']").ClickAsync(new MouseEventArgs());
        Assert.DoesNotContain(handler.Requests, x => x.Method == "DELETE");
        await page.Find("[role=alertdialog] button:first-child").ClickAsync(new MouseEventArgs());
        Assert.Equal("/api/v1/courses/1/enrolments/11", Assert.Single(handler.Requests, x => x.Method == "DELETE").Path);
    }

    [Fact]
    public async Task Role_changes_are_sent_without_an_unchanged_status()
    {
        using var handler = Handler(request => request.Method == "PATCH" ? Json(Enrolment with { Role = "Teacher" }) : Default(request));
        await using var context = Context(handler);
        var page = context.Render<CourseParticipants>(p => p.Add(x => x.CourseId, 1));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("tbody tr")));
        await page.Find("tbody button[aria-label$='düzenle']").ClickAsync(new MouseEventArgs());
        page.Find("#enrolment-role").Change("Teacher");
        await page.Find("form").SubmitAsync(EventArgs.Empty);
        using var body = JsonDocument.Parse(Assert.Single(handler.Requests, x => x.Method == "PATCH").Body!);
        Assert.Equal("Teacher", body.RootElement.GetProperty("role").GetString());
        Assert.False(body.RootElement.TryGetProperty("suspended", out _));
    }

    [Fact]
    public async Task Invalid_forms_do_not_call_the_API()
    {
        using var handler = Handler();
        await using var context = Context(handler);
        var page = context.Render<Courses>();
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("tbody tr").Count));
        await page.Find(".crs-heading button").ClickAsync(new MouseEventArgs());
        await page.Find("form").SubmitAsync(EventArgs.Empty);
        Assert.Contains("Ders adı zorunludur", page.Markup);
        Assert.Contains("Bir kategori seçin", page.Markup);
        Assert.All(handler.Requests, x => Assert.Equal("GET", x.Method));
    }

    [Fact]
    public async Task Conflict_is_shown_and_unsaved_form_values_are_retained()
    {
        using var handler = Handler(request => request.Method == "PATCH"
            ? Json(new { error = new { code = "course_short_name_conflict", message = "Kısa ad zaten kullanılıyor." } }, HttpStatusCode.Conflict)
            : Default(request));
        await using var context = Context(handler);
        var page = context.Render<CourseDetails>(p => p.Add(x => x.CourseId, 1));
        page.WaitForAssertion(() => Assert.Contains("Cebir", page.Markup));
        await page.Find(".crs-heading button").ClickAsync(new MouseEventArgs());
        page.Find("#course-name").Change("Kaybolmasın");
        await page.Find("form").SubmitAsync(EventArgs.Empty);
        Assert.Contains("Kısa ad zaten kullanılıyor", page.Find("[role=alert]").TextContent);
        Assert.Equal("Kaybolmasın", page.Find("#course-name").GetAttribute("value"));
    }

    [Fact]
    public async Task Invalid_course_dates_are_shown_before_an_HTTP_write()
    {
        using var handler = Handler();
        await using var context = Context(handler);
        var model = CourseEditModel.From(First);
        model.EndDateUtc = model.StartDateUtc!.Value.AddDays(-1);
        var saved = false;
        var editor = context.Render<CourseEditor>(p => p.Add(x => x.Model, model)
            .Add(x => x.Categories, Categories).Add(x => x.OnSave, _ => saved = true));
        await editor.Find("form").SubmitAsync(EventArgs.Empty);
        Assert.Contains("Bitiş tarihi başlangıç tarihinden önce olamaz", editor.Markup);
        Assert.False(saved);
    }

    [Fact]
    public async Task Participant_form_validates_user_and_role()
    {
        using var handler = Handler();
        await using var context = Context(handler);
        var saved = false;
        var editor = context.Render<EnrolmentEditor>(p => p.Add(x => x.Model, new EnrolmentEditModel { Role = "" })
            .Add(x => x.OnSave, _ => saved = true));
        await editor.Find("form").SubmitAsync(EventArgs.Empty);
        Assert.Contains("Geçerli bir kullanıcı kimliği girin", editor.Markup);
        Assert.Contains("Rol zorunludur", editor.Markup);
        Assert.False(saved);
    }

    [Fact]
    public async Task Pending_submission_disables_duplicate_writes()
    {
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new CrsWebHandler((request, _) => request.Method == "PATCH" ? pending.Task : Task.FromResult(Default(request)));
        await using var context = Context(handler);
        var page = context.Render<CourseDetails>(p => p.Add(x => x.CourseId, 1));
        page.WaitForAssertion(() => Assert.Contains("Cebir", page.Markup));
        await page.Find(".crs-heading button").ClickAsync(new MouseEventArgs());
        var submit = page.Find("form").SubmitAsync(EventArgs.Empty);
        page.WaitForAssertion(() => Assert.True(page.Find("fieldset").HasAttribute("disabled")));
        Assert.Single(handler.Requests, request => request.Method == "PATCH");
        pending.SetResult(Json(First));
        await submit;
        page.WaitForAssertion(() => Assert.Empty(page.FindAll("form")));
    }
}
