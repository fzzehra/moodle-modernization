using Moodle.Application.Abstractions;
using Moodle.Application.Errors;
using Moodle.Domain.Assignments;

namespace Moodle.Application.Assignments;

/// <summary>ASG-FR-01/02/03, ASG-BR-01/02, ASG-NFR-01; WBS-2 version conflicts.</summary>
public sealed class AssignmentService(IAssignmentStore store, ICurrentUser user, IAccessPolicy policy,
    ICourseDirectory courses, ICourseSectionQuery sections, IEnrolmentQuery enrolments, TimeProvider clock,
    IAssignmentLearnerQuery learners, ISubmissionAccessPolicy submissionAccess)
{
    private int UserId => user.IsAuthenticated && user.UserId is > 0
        ? user.UserId.Value : throw new AppException("not_authenticated", "Oturum açmanız gerekiyor.", 401);
    private Task<bool> Can(string capability, ContextRef context, CancellationToken ct)
        => policy.HasCapabilityAsync(UserId, capability, context, ct);
    private static AppException Forbidden() => new("forbidden", "Bu işlem için yetkiniz yok.", 403);
    private static AppException Conflict() => AppException.Conflict("version_conflict", "Teslim değişti. Güncel sürümü yükleyip tekrar deneyin.");

    public async Task<IReadOnlyList<Assignment>> CourseAssignmentsAsync(int courseId,CancellationToken ct=default)
    {
        _=UserId;
        if(await courses.FindAsync(courseId,ct) is null || !await Can("mod/assign:view",ContextRef.ForCourse(courseId),ct))
            throw AppException.NotFound();
        return await store.ListCourseAsync(courseId,ct);
    }

    public async Task<Assignment> CreateAsync(int courseId, CreateAssignment request, CancellationToken ct = default)
    {
        _ = UserId;
        if (await courses.FindAsync(courseId, ct) is null) throw AppException.NotFound();
        if (!await Can("moodle/course:manageactivities", ContextRef.ForCourse(courseId), ct)
            || !await Can("mod/assign:addinstance", ContextRef.ForCourse(courseId), ct)) throw Forbidden();
        if (!await sections.BelongsToCourseAsync(request.SectionId, courseId, ct))
            throw AppException.Validation("invalid_section", "Bölüm bu derse ait değil.");
        if (string.IsNullOrWhiteSpace(request.Title))
            throw AppException.Validation("invalid_title", "Ödev başlığı zorunludur.");
        if (request.EnabledSubmissionTypes is null || request.EnabledSubmissionTypes.Length == 0
            || request.EnabledSubmissionTypes.Distinct().Count() != request.EnabledSubmissionTypes.Length
            || request.EnabledSubmissionTypes.Any(t => t is not ("text" or "file")))
            throw AppException.Validation("invalid_submission_types", "Yalnızca metin ve dosya teslim türleri kullanılabilir.");
        if (request.OpensAt > request.DueAt || request.DueAt > request.CutoffAt || request.OpensAt > request.CutoffAt)
            throw AppException.Validation("invalid_dates", "Açılış, son teslim ve kesim tarihleri sıralı olmalıdır.");
        var assignment = new Assignment { CourseId = courseId, SectionId = request.SectionId,
            Title = request.Title.Trim(), Instructions = request.Instructions ?? "",
            OpensAt = request.OpensAt?.ToUniversalTime(), DueAt = request.DueAt?.ToUniversalTime(),
            CutoffAt = request.CutoffAt?.ToUniversalTime(), RequiresConfirmation = request.RequiresConfirmation,
            EnabledSubmissionTypes = request.EnabledSubmissionTypes.ToArray() };
        await store.AddAsync(assignment, ct);
        return assignment;
    }

    private async Task<Assignment> VisibleAsync(int id, CancellationToken ct)
    {
        _ = UserId;
        var assignment = await store.FindAsync(id, ct) ?? throw AppException.NotFound();
        if (await courses.FindAsync(assignment.CourseId, ct) is null
            || !await Can("mod/assign:view", ContextRef.ForModule(id), ct)) throw AppException.NotFound();
        return assignment;
    }

    public async Task<AssignmentView> GetAsync(int id, CancellationToken ct = default)
    {
        var assignment = await VisibleAsync(id, ct);
        var submit = await IsRequiredLearnerAsync(assignment, UserId, ct)
            && await Can("mod/assign:submit", ContextRef.ForModule(id), ct);
        var reason = submit ? (await WindowFailureAsync(assignment, ct))?.Message : "Teslim için aktif öğrenci kaydı ve yetki gerekiyor.";
        var submission = submit ? await store.FindSubmissionAsync(id, UserId, ct) : null;
        if (reason is null && assignment.RequiresConfirmation && submission?.Status == "submitted")
            reason = "Gönderilmiş teslim düzenlenemez.";
        var canSave = submit && reason is null;
        return new(assignment, new(submit, await Can("mod/assign:grade", ContextRef.ForModule(id), ct),
            canSave, canSave && assignment.RequiresConfirmation && submission?.Status == "draft", reason));
    }

    private async Task<Assignment> LearnerAsync(int id, CancellationToken ct)
    {
        var assignment = await VisibleAsync(id, ct);
        if (!await IsRequiredLearnerAsync(assignment, UserId, ct)
            || !await Can("mod/assign:submit", ContextRef.ForModule(id), ct)) throw Forbidden();
        return assignment;
    }

    public async Task<MySubmission> MineAsync(int id, CancellationToken ct = default)
    {
        await LearnerAsync(id, ct);
        var submission = await store.FindSubmissionAsync(id, UserId, ct);
        return new(submission?.Status ?? "notSubmitted", submission);
    }

    public async Task<Submission> ReadAsync(int id, int submissionId, CancellationToken ct = default)
    {
        var assignment = await VisibleAsync(id, ct);
        var submission = await store.FindSubmissionByIdAsync(id, submissionId, ct) ?? throw AppException.NotFound();
        if (submission.LearnerId == UserId)
            await LearnerAsync(id, ct);
        else if (!await CanAssessAsync(assignment, submission.LearnerId, await RequiredLearnersAsync(assignment, ct), ct))
            throw AppException.NotFound();
        return submission;
    }

    public async Task<bool> CanReadFilesAsync(int assignmentId, int submissionId, CancellationToken ct = default)
    {
        try { await ReadAsync(assignmentId, submissionId, ct); return true; }
        catch (AppException ex) when (ex.StatusCode is 401 or 403 or 404) { return false; }
    }
    private async Task<HashSet<int>> RequiredLearnersAsync(Assignment assignment, CancellationToken ct)
        => (await learners.ListRequiredLearnerIdsAsync(assignment.CourseId, ct) ?? [])
            .Where(id => id > 0).ToHashSet();

    private async Task<bool> IsRequiredLearnerAsync(Assignment assignment, int learnerId, CancellationToken ct)
        => (await RequiredLearnersAsync(assignment, ct)).Contains(learnerId)
            && await enrolments.IsActivelyEnrolledAsync(learnerId, assignment.CourseId, ct);

    // Shared by list and detail. Scope is applied BEFORE filtering/paging and never inferred from grade alone.
    private async Task<bool> CanAssessAsync(Assignment assignment, int learnerId, HashSet<int> required, CancellationToken ct)
        => required.Contains(learnerId)
            && await Can("mod/assign:grade", ContextRef.ForModule(assignment.Id), ct)
            && await enrolments.IsActivelyEnrolledAsync(learnerId, assignment.CourseId, ct)
            && await submissionAccess.CanAssessLearnerAsync(UserId, assignment.CourseId, assignment.Id, learnerId, ct);
    private async Task<AppException?> WindowFailureAsync(Assignment assignment, CancellationToken ct)
    {
        var rule = await store.FindRuleAsync(assignment.Id, UserId, ct);
        var now = clock.GetUtcNow();
        var cutoff = assignment.CutoffAt;
        if (cutoff is not null && rule?.ExtendedUntil > cutoff) cutoff = rule.ExtendedUntil;
        if (rule?.Locked == true) return AppException.Conflict("submission_locked", "Teslim düzenlemeye kapalı.");
        if (now < assignment.OpensAt || now > cutoff)
            return AppException.Conflict("submission_closed", "Teslim penceresi kapalı.");
        if (!assignment.EnabledSubmissionTypes.Any(t => t is "text" or "file"))
            return AppException.Conflict("text_disabled", "Metin teslimi etkin değil.");
        return null;
    }

    private async Task CheckWindowAsync(Assignment assignment, CancellationToken ct)
    {
        if (await WindowFailureAsync(assignment, ct) is { } failure) throw failure;
    }

    public async Task<SaveResult> SaveAsync(int id, SaveSubmission request, CancellationToken ct = default)
    {
        await LearnerAsync(id, ct);
        return await store.WriteAsync(id, async () =>
        {
            var assignment = await LearnerAsync(id, ct);
            await CheckWindowAsync(assignment, ct);

            var submission = await store.FindSubmissionAsync(id, UserId, ct);
            var created = submission is null;
            var fileEnabled = assignment.EnabledSubmissionTypes.Contains("file");
            if (request.FileDraftId is not null && !fileEnabled)
                throw AppException.Conflict("file_disabled", "Dosya teslimi etkin değil.");
            if (!string.IsNullOrWhiteSpace(request.Text) && !assignment.EnabledSubmissionTypes.Contains("text"))
                throw AppException.Conflict("text_disabled", "Metin teslimi etkin değil.");
            if (string.IsNullOrWhiteSpace(request.Text) && (!fileEnabled || request.FileDraftId is null && (submission?.FileCount ?? 0) == 0))
                throw AppException.Validation("invalid_text", "Metin veya en az bir dosya gerekiyor.");
            if (request.Version != (submission?.Version ?? 0)) throw Conflict();
            if (assignment.RequiresConfirmation && submission?.Status == "submitted")
                throw AppException.Conflict("already_submitted", "Gönderilmiş teslim düzenlenemez.");
            submission ??= new Submission { AssignmentId = id, LearnerId = UserId };
            submission.Text = request.Text ?? "";
            submission.Status = assignment.RequiresConfirmation ? "draft" : "submitted";
            submission.UpdatedAt = clock.GetUtcNow();
            submission.SubmittedAt = assignment.RequiresConfirmation ? null : submission.UpdatedAt;
            submission.Version++;
            if (created) store.AddSubmission(submission);
            if (request.FileDraftId is long draftId) {
                submission.FileCount = 1;
                submission.FileCount = await store.AttachFilesAsync(UserId, draftId, submission, ct);
                if (submission.FileCount == 0 && string.IsNullOrWhiteSpace(submission.Text))
                    throw AppException.Validation("invalid_text", "Metin veya en az bir dosya gerekiyor.");
            }
            return new SaveResult(submission, created);
        }, ct);
    }

    public async Task<Submission> SubmitAsync(int id, SubmitSubmission request, CancellationToken ct = default)
    {
        await LearnerAsync(id, ct);
        return await store.WriteAsync(id, async () =>
        {
            var assignment = await LearnerAsync(id, ct);
            await CheckWindowAsync(assignment, ct);
            var submission = await store.FindSubmissionAsync(id, UserId, ct) ?? throw AppException.NotFound();
            if (request.Version != submission.Version) throw Conflict();
            if (!assignment.RequiresConfirmation || submission.Status != "draft")
                throw AppException.Conflict("invalid_state", "Gönderilebilir bir taslak yok.");
            submission.Status = "submitted";
            submission.SubmittedAt = submission.UpdatedAt = clock.GetUtcNow();
            submission.Version++;
            return submission;
        }, ct);
    }

    public async Task<SubmissionPage> ListAsync(int id, string? status = null, int? cursor = null,
        int limit = 50, CancellationToken ct = default)
    {
        var assignment = await VisibleAsync(id, ct);
        if (!await Can("mod/assign:grade", ContextRef.ForModule(id), ct)) throw Forbidden();
        if (limit is < 1 or > 100 || cursor < 0 || status is not (null or "draft" or "submitted" or "notSubmitted"))
            throw AppException.Validation("invalid_page", "Durum, imleç veya sayfa boyutu geçersiz.");
        var required = await RequiredLearnersAsync(assignment, ct);
        var allowed = new List<int>();
        foreach (var learnerId in required.Order())
            if (await CanAssessAsync(assignment, learnerId, required, ct)) allowed.Add(learnerId);
        return await store.ListRowsAsync(id, allowed, status, cursor, limit, assignment.DueAt, ct);
    }
}
