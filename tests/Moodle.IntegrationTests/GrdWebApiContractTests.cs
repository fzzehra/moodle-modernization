using System.Net;
using Moodle.Web.Grd;

namespace Moodle.IntegrationTests;

/// <summary>Arayüzün HTTP modelleri gerçek API ve PostgreSQL ile uyumlu (CrsWebApiContractTests deseni).</summary>
public sealed class GrdWebApiContractTests(GrdApiFixture fixture) : IClassFixture<GrdApiFixture>
{
    [Fact]
    public async Task GRD_FR_01_GRD_FR_03_UI_models_support_grading_and_gradebook_workflow()
    {
        var assignmentId = GrdApiFixture.NextAssignmentId();
        using var factory = fixture.CreateFactory();
        using var client = new GrdApiClient(factory.CreateClient());

        var empty = await client.GradeAsync(assignmentId, 3, 0);
        Assert.Null(empty.Assessment);
        Assert.Equal(0, empty.Version);
        Assert.Equal(100m, empty.MaxScore);

        var saved = await client.SaveGradeAsync(assignmentId, 3, 0, new GradeWriteRequest(60m, "İlk deneme", 0));
        Assert.True(saved.GradebookUpdated);
        Assert.Equal(GrdApiFixture.GraderId, saved.Assessment.GraderId);
        var second = await client.SaveGradeAsync(assignmentId, 3, 1, new GradeWriteRequest(80.5m, null, 0));
        Assert.True(second.GradebookUpdated);
        var regrade = await client.SaveGradeAsync(assignmentId, 3, 0, new GradeWriteRequest(65m, null, 1));
        Assert.False(regrade.GradebookUpdated);

        var view = await client.GradeAsync(assignmentId, 3, 1);
        Assert.Equal(80.5m, view.Assessment!.Score);
        Assert.Equal(1, view.Version);

        // Not defteri hücresi en son denemeyi ve defterdeki puanı taşır; sütun ödeve bağlıdır.
        var book = await client.GradebookAsync(GrdApiFixture.CourseId);
        var column = book.Items.FindIndex(x => x.AssignmentId == assignmentId);
        Assert.True(column >= 0);
        var cell = book.Learners.Single(x => x.LearnerId == 3).Grades[column];
        Assert.Equal(80.5m, cell.Score);
        Assert.Equal(1, cell.Attempt);
        Assert.Null(book.Learners.Single(x => x.LearnerId == 4).Grades[column].Attempt);
    }

    [Theory]
    [InlineData(101, 0, HttpStatusCode.UnprocessableEntity, "validation_failed")]
    [InlineData(50, 7, HttpStatusCode.Conflict, "version_conflict")]
    public async Task GRD_BR_02_UI_client_receives_form_and_conflict_errors(int score, int version, HttpStatusCode status, string code)
    {
        using var factory = fixture.CreateFactory();
        using var client = new GrdApiClient(factory.CreateClient());
        var error = await Assert.ThrowsAsync<GrdApiException>(() =>
            client.SaveGradeAsync(GrdApiFixture.NextAssignmentId(), 3, 0, new GradeWriteRequest(score, null, version)));
        Assert.Equal(status, error.Status);
        Assert.Equal(code, error.Code);
        if (status == HttpStatusCode.UnprocessableEntity) Assert.Contains("0 ile 100", error.Message);
    }

    [Fact]
    public async Task GRD_FR_02_UI_model_reads_the_students_own_report()
    {
        var assignmentId = GrdApiFixture.NextAssignmentId();
        using (var teacherFactory = fixture.CreateFactory())
        using (var teacher = new GrdApiClient(teacherFactory.CreateClient()))
            await teacher.SaveGradeAsync(assignmentId, 3, 0, new GradeWriteRequest(88m, null, 0));

        using var studentFactory = fixture.CreateFactory(userId: 3, permission: (capability, _) => capability == "grade.view");
        using var student = new GrdApiClient(studentFactory.CreateClient());
        var report = await student.MyGradesAsync(GrdApiFixture.CourseId);
        Assert.Contains(report.Items, x => x.Name == $"Ödev {assignmentId}" && x.Score == 88m);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Assert.ThrowsAsync<GrdApiException>(() => student.GradebookAsync(GrdApiFixture.CourseId))).Status);
    }
}
