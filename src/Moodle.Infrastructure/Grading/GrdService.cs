using Microsoft.EntityFrameworkCore;
using Moodle.Application.Abstractions;
using Moodle.Application.Errors;
using Moodle.Application.Grading;
using Moodle.Domain.Grading;

namespace Moodle.Infrastructure.Grading;

/// <summary>
/// GRD iş mantığı (CRS düzeni: iş mantığı burada, saf kurallar Application'da).
/// Ders, kayıt ve ödev ilişkileri yalnızca arayüzlerle doğrulanır.
/// </summary>
// YZ destekli: puan okuma ve yazma (GRD-FR-01, GRD-BR-01, GRD-BR-02).
public sealed class GrdService(
    GrdDbContext db,
    IAssignmentQuery assignments,
    IEnrolmentQuery enrolments,
    ICourseDirectory courses,
    ICurrentUser currentUser,
    TimeProvider time)
{
    /// <summary>Ödevin ana not kalemi henüz yoksa kullanılan sınırlar; ilk kayıtta kalem bu sınırlarla oluşur.</summary>
    public const decimal DefaultMinScore = 0m;
    public const decimal DefaultMaxScore = 100m;

    /// <summary>Ödevi bulur; yoksa <see cref="KeyNotFoundException"/> (404). Yetki bağlamı için API de kullanır.</summary>
    public async Task<AssignmentInfo> FindAssignmentAsync(int assignmentId, CancellationToken ct = default) =>
        await assignments.FindAsync(assignmentId, ct)
        ?? throw new KeyNotFoundException("Ödev bulunamadı.");

    /// <summary>GET .../grade: kayıt yoksa değerlendirme null, sürüm 0; sınırlar not kaleminden gelir.</summary>
    public async Task<AssignmentGradeView> GetAssignmentGradeAsync(
        int assignmentId, int learnerId, int attempt, CancellationToken ct = default)
    {
        await RequireGradableLearnerAsync(assignmentId, learnerId, ct);
        GradeValidation.ValidateAttempt(attempt);

        var item = await db.Items.AsNoTracking()
            .SingleOrDefaultAsync(x => x.AssignmentId == assignmentId, ct);
        var assessment = await db.AssignmentGrades.AsNoTracking()
            .SingleOrDefaultAsync(x =>
                x.AssignmentId == assignmentId &&
                x.LearnerId == learnerId &&
                x.Attempt == attempt, ct);

        return new AssignmentGradeView(
            assessment,
            item?.MinScore ?? DefaultMinScore,
            item?.MaxScore ?? DefaultMaxScore,
            assessment?.Version ?? 0);
    }

    /// <summary>
    /// PUT .../grade: puanı kaydeder ve aynı işlemde deftere aktarır.
    /// Herhangi bir denetim başarısız olursa hiçbir satır değişmez.
    /// </summary>
    public async Task<AssignmentGradeSaveResult> SaveAssignmentGradeAsync(
        int assignmentId, int learnerId, int attempt,
        decimal? score, string? feedback, int version,
        CancellationToken ct = default)
    {
        // Değerlendirici her zaman oturumdan alınır, istekten alınmaz.
        var graderId = RequireSessionUserId();

        var assignment = await RequireGradableLearnerAsync(assignmentId, learnerId, ct);
        GradeValidation.ValidateAttempt(attempt);
        GradeValidation.ValidateVersion(version);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({0x41534743},{assignment.CourseId})", ct);
        await FindCourseAsync(assignment.CourseId, ct);

        var item = await db.Items.SingleOrDefaultAsync(x => x.AssignmentId == assignmentId, ct);

        // GRD-BR-02: sınır dışı puan hiçbir şey yazılmadan reddedilir; önceki puan korunur.
        GradeValidation.ValidateScore(score,
            item?.MinScore ?? DefaultMinScore,
            item?.MaxScore ?? DefaultMaxScore);

        var assessment = await db.AssignmentGrades.SingleOrDefaultAsync(x =>
            x.AssignmentId == assignmentId &&
            x.LearnerId == learnerId &&
            x.Attempt == attempt, ct);

        // İyimser eşzamanlılık: istemci gördüğü sürümü gönderir; ilk kayıtta 0.
        if ((assessment?.Version ?? 0) != version)
            throw GrdConflictException.Version();

        var learnerGrade = item is null
            ? null
            : await db.LearnerGrades.SingleOrDefaultAsync(x =>
                x.ItemId == item.Id && x.LearnerId == learnerId, ct);

        // GRD-BR-01: kilitli veya elle değiştirilmiş defter değeri korunur; öğretmen yazması reddedilir.
        var now = time.GetUtcNow();
        if ((item is not null && GradeValidation.IsLocked(item.Locked, item.LockAt, now)) ||
            (learnerGrade is not null && GradeValidation.IsLocked(learnerGrade.Locked, learnerGrade.LockAt, now)))
            throw GrdConflictException.Locked();
        if (learnerGrade?.Overridden == true)
            throw GrdConflictException.Overridden();

        if (item is null)
        {
            item = new GradeItem
            {
                CourseId = assignment.CourseId,
                AssignmentId = assignmentId,
                Type = GradeItemType.Assignment,
                Name = assignment.Title.Length > 255 ? assignment.Title[..255] : assignment.Title,
                MinScore = DefaultMinScore,
                MaxScore = DefaultMaxScore,
            };
            db.Items.Add(item);
            await db.SaveChangesAsync(ct);
        }

        // Defter, öğrencinin en son denemesinin puanını taşır (Moodle'daki gibi).
        // Eski bir denemenin düzeltilmesi defteri ezmez.
        var latestAttempt = await db.AssignmentGrades
            .Where(x => x.AssignmentId == assignmentId && x.LearnerId == learnerId)
            .MaxAsync(x => (int?)x.Attempt, ct);
        var transfer = latestAttempt is null || attempt >= latestAttempt.Value;

        if (assessment is null)
        {
            assessment = new AssignmentGrade
            {
                AssignmentId = assignmentId,
                LearnerId = learnerId,
                Attempt = attempt,
            };
            db.AssignmentGrades.Add(assessment);
        }

        assessment.GraderId = graderId;
        assessment.Score = score;
        assessment.Feedback = feedback;
        assessment.GradedAt = now;
        assessment.Version = version + 1;

        if (transfer)
        {
            if (learnerGrade is null)
            {
                learnerGrade = new LearnerGrade { ItemId = item.Id, LearnerId = learnerId };
                db.LearnerGrades.Add(learnerGrade);
            }

            learnerGrade.RawScore = score;
            learnerGrade.FinalScore = score;
            learnerGrade.UpdatedAt = now;
            learnerGrade.Version++;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new AssignmentGradeSaveResult(assessment, transfer, item.MinScore, item.MaxScore);
    }

    /// <summary>Dersi bulur; yoksa <see cref="KeyNotFoundException"/> (404). Yetki bağlamı için API de kullanır.</summary>
    public async Task<CourseInfo> FindCourseAsync(int courseId, CancellationToken ct = default) =>
        await courses.FindAsync(courseId, ct)
        ?? throw new KeyNotFoundException("Ders bulunamadı.");

    // YZ destekli: kişisel not raporu (GRD-FR-02, GRD-NFR-01).
    /// <summary>
    /// Oturum sahibinin dersteki görünür notları. Öğrenci kimliği yalnızca oturumdan alınır;
    /// sorgu yalnızca o öğrencinin satırlarını okur, başka öğrencinin verisi belleğe bile gelmez.
    /// </summary>
    public async Task<MyGradesView> GetMyGradesAsync(int courseId, CancellationToken ct = default)
    {
        var learnerId = RequireSessionUserId();
        await FindCourseAsync(courseId, ct);
        var now = time.GetUtcNow();

        var items = await ReportableItemsAsync(courseId, ct);
        var itemIds = items.Select(x => x.Id).ToList();
        var grades = await db.LearnerGrades.AsNoTracking()
            .Where(x => x.LearnerId == learnerId && itemIds.Contains(x.ItemId))
            .ToDictionaryAsync(x => x.ItemId, ct);

        var rows = items
            // Gizli kalemin satırı da gösterilmez.
            .Where(item => !GradeVisibility.IsHidden(item.Hidden, item.HiddenUntil, now))
            .Select(item =>
            {
                grades.TryGetValue(item.Id, out var grade);
                // Yalnızca not gizliyse satır görünür, puan boş döner.
                var score = grade is null || GradeVisibility.IsHidden(grade.Hidden, grade.HiddenUntil, now)
                    ? null
                    : grade.FinalScore;
                return new MyGradeRow(item.Id, item.Name, item.MinScore, item.MaxScore, score);
            })
            .ToList();

        return new MyGradesView(courseId, rows);
    }

    // YZ destekli: değerlendirici not defteri (GRD-FR-03).
    /// <summary>
    /// Derse etkin kayıtlı öğrenciler (satır) × dersin not kalemleri (sütun), öğrenci kimliğine göre sayfalı.
    /// Kayıtlı olmayan öğrencilerin notları defterde görünmez.
    /// </summary>
    public async Task<GradebookView> GetGradebookAsync(
        int courseId, string? cursor, int? limit, CancellationToken ct = default)
    {
        var after = GradebookPaging.ParseCursor(cursor);
        var take = GradebookPaging.ResolveLimit(limit);
        await FindCourseAsync(courseId, ct);
        var now = time.GetUtcNow();

        var enrolled = await enrolments.ListEnrolledUserIdsAsync(courseId, ct);
        var (page, nextCursor) = GradebookPaging.Page(enrolled, after, take);

        var items = await ReportableItemsAsync(courseId, ct);
        var itemIds = items.Select(x => x.Id).ToList();
        var grades = page.Count == 0 || itemIds.Count == 0
            ? []
            : await db.LearnerGrades.AsNoTracking()
                .Where(x => itemIds.Contains(x.ItemId) && page.Contains(x.LearnerId))
                .ToListAsync(ct);
        var gradeByCell = grades.ToDictionary(x => (x.LearnerId, x.ItemId));

        // Ödev hücrelerinde en son değerlendirilen deneme; arayüz bu denemenin ekranına bağlanır.
        var assignmentIds = items.Where(x => x.AssignmentId.HasValue).Select(x => x.AssignmentId!.Value).ToList();
        var latestAttempts = page.Count == 0 || assignmentIds.Count == 0
            ? new Dictionary<(int AssignmentId, int LearnerId), int>()
            : await db.AssignmentGrades.AsNoTracking()
                .Where(x => assignmentIds.Contains(x.AssignmentId) && page.Contains(x.LearnerId))
                .GroupBy(x => new { x.AssignmentId, x.LearnerId })
                .Select(g => new { g.Key.AssignmentId, g.Key.LearnerId, Attempt = g.Max(x => x.Attempt) })
                .ToDictionaryAsync(x => (x.AssignmentId, x.LearnerId), x => x.Attempt, ct);

        var columns = items
            .Select(item => new GradebookItem(
                item.Id, item.AssignmentId, item.Name, item.MinScore, item.MaxScore,
                GradeVisibility.IsHidden(item.Hidden, item.HiddenUntil, now),
                GradeValidation.IsLocked(item.Locked, item.LockAt, now)))
            .ToList();

        var rows = page
            .Select(learnerId => new GradebookRow(learnerId, columns
                .Select(column =>
                {
                    gradeByCell.TryGetValue((learnerId, column.ItemId), out var grade);
                    int? attempt = column.AssignmentId is int assignmentId &&
                                   latestAttempts.TryGetValue((assignmentId, learnerId), out var latest)
                        ? latest
                        : null;
                    return new GradebookCell(
                        column.ItemId,
                        grade?.FinalScore,
                        attempt,
                        column.Hidden || (grade is not null && GradeVisibility.IsHidden(grade.Hidden, grade.HiddenUntil, now)),
                        column.Locked || (grade is not null && GradeValidation.IsLocked(grade.Locked, grade.LockAt, now)),
                        grade?.Overridden ?? false);
                })
                .ToList()))
            .ToList();

        return new GradebookView(courseId, columns, rows, nextCursor);
    }

    private Task<List<GradeItem>> ReportableItemsAsync(int courseId, CancellationToken ct) =>
        db.Items.AsNoTracking()
            .Where(x => x.CourseId == courseId &&
                        (x.Type == GradeItemType.Assignment || x.Type == GradeItemType.Manual))
            .OrderBy(x => x.Id)
            .ToListAsync(ct);

    private int RequireSessionUserId() =>
        currentUser.UserId is int id && id > 0
            ? id
            : throw new AppException("not_authenticated", "Oturum açmanız gerekiyor.", 401);

    /// <summary>
    /// Ödev var olmalı ve öğrenci ödevin dersine etkin kayıtlı olmalı; aksi halde 404
    /// (kayıtsız öğrencinin varlığı sızdırılmaz).
    /// </summary>
    private async Task<AssignmentInfo> RequireGradableLearnerAsync(
        int assignmentId, int learnerId, CancellationToken ct)
    {
        var assignment = await FindAssignmentAsync(assignmentId, ct);
        if (learnerId <= 0 || !await enrolments.IsActivelyEnrolledAsync(learnerId, assignment.CourseId, ct))
            throw new KeyNotFoundException("Öğrenci bu derse kayıtlı değil.");
        return assignment;
    }
}
