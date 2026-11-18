using Moodle.Application.Abstractions;
using Moodle.Application.Errors;
using Moodle.Infrastructure.Grading;

namespace Moodle.Api.Grading;

public static class GrdEndpoints
{
    private const string GradeRoute =
        "/assignments/{assignmentId:int}/learners/{learnerId:int}/attempts/{attempt:int}/grade";

    public static IServiceCollection AddGrdApi(this IServiceCollection services)
    {
        services.AddScoped<GrdAuthorization>();
        return services;
    }

    public static IEndpointRouteBuilder MapGrdEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/api/v1").WithTags("GRD")
            .AddEndpointFilter<GrdEndpointFilter>();

        // GRD-FR-01: değerlendirici ekranının okuması.
        api.MapGet(GradeRoute, async (int assignmentId, int learnerId, int attempt,
            GrdService service, GrdAuthorization auth, CancellationToken ct) =>
        {
            await RequireGradeEditAsync(assignmentId, service, auth, ct);
            return TypedResults.Ok(await service.GetAssignmentGradeAsync(assignmentId, learnerId, attempt, ct));
        });

        // GRD-FR-01, GRD-BR-01, GRD-BR-02: puan kaydı ve deftere aktarım.
        api.MapPut(GradeRoute, async (int assignmentId, int learnerId, int attempt, GradeRequest request,
            GrdService service, GrdAuthorization auth, CancellationToken ct) =>
        {
            await RequireGradeEditAsync(assignmentId, service, auth, ct);
            return TypedResults.Ok(await service.SaveAssignmentGradeAsync(
                assignmentId, learnerId, attempt, request.Score, request.Feedback, request.Version, ct));
        });

        // GRD-FR-02, GRD-NFR-01: kişisel not raporu. Öğrenci kimliği yalnızca oturumdan alınır.
        api.MapGet("/courses/{courseId:int}/my-grades", async (int courseId, HttpRequest request,
            GrdService service, GrdAuthorization auth, CancellationToken ct) =>
        {
            RejectLearnerSelection(request);
            var course = await service.FindCourseAsync(courseId, ct);
            await auth.RequireAsync(GrdAuthorization.GradeView, ContextRef.ForCourse(course.Id), ct);
            return TypedResults.Ok(await service.GetMyGradesAsync(course.Id, ct));
        });

        // GRD-FR-03: değerlendirici not defteri.
        api.MapGet("/courses/{courseId:int}/gradebook", async (int courseId, string? cursor, int? limit, int? groupId,
            GrdService service, GrdAuthorization auth, CancellationToken ct) =>
        {
            // groupId WBS-2 §3.5.2 sözleşmesinde yer alır ve kabul edilir; ancak 1. sürümde grup modu
            // yoktur (CRS'de grup tablosu bulunmaz), bu yüzden değeri yok sayılır ve dersin tüm etkin
            // kayıtlı öğrencileri listelenir. Gruplar eklendiğinde filtre burada uygulanacaktır.
            _ = groupId;
            var course = await service.FindCourseAsync(courseId, ct);
            await auth.RequireAsync(GrdAuthorization.GradeViewAll, ContextRef.ForCourse(course.Id), ct);
            return TypedResults.Ok(await service.GetGradebookAsync(course.Id, cursor, limit, ct));
        });

        return endpoints;
    }

    /// <summary>
    /// GRD-NFR-01: kişisel raporda başka bir öğrenci seçilemez. Sessizce yok saymak yerine,
    /// sorguda herhangi bir parametre (ör. <c>userId</c>, <c>learnerId</c>) ya da gövde gelirse 400 döner;
    /// böylece istemci yanlışlıkla başka birinin raporunu aldığını sanamaz.
    /// </summary>
    private static void RejectLearnerSelection(HttpRequest request)
    {
        if (request.Query.Count > 0 || request.ContentLength > 0 || request.Headers.TransferEncoding.Count > 0)
            throw new AppException("learner_selection_not_allowed",
                "Kişisel not raporu yalnızca oturum sahibinin notlarını gösterir; istekte öğrenci seçilemez.", 400);
    }

    /// <summary>
    /// Yetki ödevin ders bağlamında sorulur. Öğrencinin kayıt durumu yetkiden sonra denetlenir;
    /// böylece yetkisiz kullanıcı kayıtlı öğrencileri yoklayamaz.
    /// </summary>
    private static async Task RequireGradeEditAsync(
        int assignmentId, GrdService service, GrdAuthorization auth, CancellationToken ct)
    {
        var assignment = await service.FindAssignmentAsync(assignmentId, ct);
        await auth.RequireAsync(GrdAuthorization.GradeEdit, ContextRef.ForCourse(assignment.CourseId), ct);
    }
}
