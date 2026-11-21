using Moodle.Application.Assignments;
using Moodle.Domain.Assignments;
using Moodle.Api.Errors;
namespace Moodle.Api;
/// <summary>ASG-FR-01/02/03 and ASG-NFR-01, WBS-2 §3.4.2.</summary>
public static class AssignmentEndpoints
{
    public static void MapAssignmentEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1").WithTags("ASG").AddEndpointFilter<CookieWriteProtection>();
        api.MapGet("/courses/{courseId:int}/assignments",(int courseId,AssignmentService service,CancellationToken ct)
            => service.CourseAssignmentsAsync(courseId,ct)).Errors(401,404,500);
        api.MapPost("/courses/{courseId:int}/assignments", async (int courseId, CreateAssignment body, AssignmentService service, CancellationToken ct) =>
        {
            var result = await service.CreateAsync(courseId, body, ct);
            return Results.Created($"/api/v1/assignments/{result.Id}", result);
        }).Produces<Assignment>(201).Errors(400, 401, 403, 404, 415, 422, 500);
        api.MapGet("/assignments/{id:int}", (int id, AssignmentService service, CancellationToken ct) => service.GetAsync(id, ct))
            .Errors(401, 404, 500);
        api.MapGet("/assignments/{id:int}/my-submission", (int id, AssignmentService service, CancellationToken ct) => service.MineAsync(id, ct))
            .Errors(401, 403, 404, 500);
        api.MapPut("/assignments/{id:int}/my-submission", async (int id, SaveSubmission body, AssignmentService service, CancellationToken ct) =>
        {
            var result = await service.SaveAsync(id, body, ct);
            return result.Created ? Results.Created($"/api/v1/assignments/{id}/my-submission", result.Submission) : Results.Ok(result.Submission);
        }).Produces<Submission>(200).Produces<Submission>(201).Errors(400, 401, 403, 404, 409, 415, 422, 500, 503);
        api.MapPost("/assignments/{id:int}/my-submission/submit",
            (int id, SubmitSubmission body, AssignmentService service, CancellationToken ct) => service.SubmitAsync(id, body, ct))
            .Errors(400, 401, 403, 404, 409, 415, 500);
        api.MapGet("/assignments/{id:int}/submissions",
            (int id, string? status, int? cursor, int? limit, AssignmentService service, CancellationToken ct)
                => service.ListAsync(id, status, cursor, limit ?? 50, ct))
            .Errors(400, 401, 403, 404, 422, 500);
        api.MapGet("/assignments/{id:int}/submissions/{submissionId:int}",
            (int id, int submissionId, AssignmentService service, CancellationToken ct) => service.ReadAsync(id, submissionId, ct))
            .Errors(401, 403, 404, 500);
    }
    private static RouteHandlerBuilder Errors(this RouteHandlerBuilder endpoint, params int[] statuses)
    {
        foreach (var status in statuses) endpoint.Produces<ApiErrorResponse>(status);
        return endpoint;
    }
}
