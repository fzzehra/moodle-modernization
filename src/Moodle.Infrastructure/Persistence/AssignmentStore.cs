using Microsoft.Extensions.DependencyInjection;
using Moodle.Application.Files;
using Moodle.Infrastructure.Files;
using Moodle.Infrastructure.Assignments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moodle.Application.Assignments;
using Moodle.Application.Errors;
using Moodle.Domain.Assignments;
using Npgsql;
namespace Moodle.Infrastructure.Persistence;

/// <summary>ASG-only change tracking, shared transaction with savepoint when owned by caller.</summary>
public sealed class AssignmentStore(MoodleDbContext db, IServiceProvider? services = null) : IAssignmentStore
{
    private MoodleDbContext? writer;
    private MoodleDbContext Current => writer ?? db;
    public Task<Assignment?> FindAsync(int id, CancellationToken ct)
        => Current.Assignments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyList<Assignment>> ListCourseAsync(int courseId,CancellationToken ct)
        => await Current.Assignments.AsNoTracking().Where(a=>a.CourseId==courseId).OrderBy(a=>a.Id).ToArrayAsync(ct);
    public async Task AddAsync(Assignment assignment, CancellationToken ct)
        => await InTransactionAsync(null, async () =>
        {
            await AsgCourseWriteFence.RequireLiveAsync(Current, assignment.CourseId, ct);
            Current.Assignments.Add(assignment);
            return true;
        }, ct);
    public Task<Submission?> FindSubmissionAsync(int assignmentId, int learnerId, CancellationToken ct)
    {
        var query = Current.Submissions.AsQueryable();
        if (writer is null) query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.AssignmentId == assignmentId && x.LearnerId == learnerId && x.Attempt == 0, ct);
    }
    public Task<Submission?> FindSubmissionByIdAsync(int assignmentId, int submissionId, CancellationToken ct)
        => Current.Submissions.AsNoTracking().SingleOrDefaultAsync(x => x.AssignmentId == assignmentId && x.Id == submissionId, ct);
    public Task<SubmissionRule?> FindRuleAsync(int assignmentId, int learnerId, CancellationToken ct)
        => Current.SubmissionRules.AsNoTracking().SingleOrDefaultAsync(x => x.AssignmentId == assignmentId && x.LearnerId == learnerId, ct);
    public async Task<SubmissionPage> ListRowsAsync(int assignmentId, IReadOnlyList<int> learners,
        string? status, int? cursor, int limit, DateTimeOffset? dueAt, CancellationToken ct)
    {
        if (learners.Count == 0) return new([], null);
        var ids = learners.Distinct().ToArray();
        var audience = Current.Database.SqlQuery<int>($"SELECT unnest({ids}) AS \"Value\"");
        var query = from learner in audience
                    join submission in Current.Submissions.AsNoTracking().Where(s => s.AssignmentId == assignmentId && s.Attempt == 0)
                        on learner equals submission.LearnerId into submissions
                    from submission in submissions.DefaultIfEmpty()
                    where learner > (cursor ?? 0)
                    select new {
                        LearnerId = learner, Status = submission == null ? "notSubmitted" : submission.Status,
                        SubmissionId = submission == null ? (int?)null : submission.Id,
                        SubmittedAt = submission == null ? null : submission.SubmittedAt,
                        UpdatedAt = submission == null ? (DateTimeOffset?)null : submission.UpdatedAt,
                        Version = submission == null ? 0 : submission.Version,
                        FileCount = submission == null ? 0 : submission.FileCount,
                        IsLate = submission != null && submission.SubmittedAt > dueAt
                    };
        if (status is not null) query = query.Where(row => row.Status == status);
        var rows = await query.OrderBy(row => row.LearnerId).Take(limit + 1)
            .Select(row => new SubmissionRow(row.LearnerId, row.Status, row.SubmissionId, row.SubmittedAt, row.UpdatedAt, row.Version, row.IsLate, row.FileCount)).ToArrayAsync(ct);
        return new(rows.Take(limit).ToArray(), rows.Length > limit ? rows[limit - 1].LearnerId : null);
    }
    public async Task<int> AttachFilesAsync(int userId,long draftId,Submission submission,CancellationToken ct)
    {
        if(writer is null || services is null) throw new InvalidOperationException("ASG file attachment requires a transaction.");
        var configured=services.GetRequiredService<FilesDbContext>();
        // Cross-database commits cannot be atomic; reject instead of silently splitting the write.
        var fileConnection=new NpgsqlConnectionStringBuilder(configured.Database.GetConnectionString());
        var asgConnection=new NpgsqlConnectionStringBuilder(writer.Database.GetConnectionString());
        if(fileConnection.Host!=asgConnection.Host || fileConnection.Port!=asgConnection.Port || fileConnection.Database!=asgConnection.Database || fileConnection.Username!=asgConnection.Username)
            throw new AppException("file_transaction_unavailable","ASG ve FILE aynı veritabanında olmalı.",503);
        await writer.SaveChangesAsync(ct);
        await using var filesDb=new FilesDbContext(new DbContextOptionsBuilder<FilesDbContext>().UseNpgsql(writer.Database.GetDbConnection()).Options);
        await filesDb.Database.UseTransactionAsync(writer.Database.CurrentTransaction!.GetDbTransaction(),ct);
        await filesDb.Database.ExecuteSqlInterpolatedAsync($"SELECT \"DraftId\" FROM file_draft WHERE \"DraftId\"={draftId} FOR UPDATE",ct);
        var files=new FileService(filesDb,services.GetRequiredService<IFileStore>(),services.GetRequiredService<IFilePolicyRegistry>(),
            services.GetRequiredService<TimeProvider>(),services.GetRequiredService<DraftUploadService>());
        var scope=services.GetRequiredService<AssignmentFileWriteScope>();
        scope.SubmissionId=submission.Id;scope.AssignmentId=submission.AssignmentId;scope.UserId=userId;
        try {
            var candidates=await files.ListDraftAsync(userId,draftId,ct:ct);
            var limits=services.GetRequiredService<UploadLimits>();
            if(candidates.Count>limits.MaxFiles || candidates.Sum(f=>f.SizeBytes)>limits.MaxAreaBytes
                || candidates.Any(f=>f.SizeBytes>limits.MaxFileBytes || !limits.Extensions.Contains(Path.GetExtension(f.Name),StringComparer.OrdinalIgnoreCase)))
                throw AppException.Validation("area_limit_exceeded","Ödev dosya sınırı aşıldı.");
            // Existing draft records created before scanner integration are not trusted.
            foreach(var candidate in candidates) {
                await using var stream=await services.GetRequiredService<IFileStore>().OpenReadAsync(candidate.ContentHash,ct)
                    ?? throw AppException.NotFound();
                var prefix=new byte[(int)Math.Min(candidate.SizeBytes,81920)];
                await stream.ReadExactlyAsync(prefix,ct);
                UploadValidation.Type(candidate.Name,prefix,limits);
                await using var scanStream=await services.GetRequiredService<IFileStore>().OpenReadAsync(candidate.ContentHash,ct)
                    ??throw AppException.NotFound();
                if(await services.GetRequiredService<IVirusScanner>().ScanAsync(scanStream,ct)!=VirusScanResult.Clean)
                    throw AppException.Validation("virus_detected","Dosya kabul edilmedi.");
            }
            var committed=await files.CommitAsync(userId,draftId,new(submission.AssignmentId,"mod_assign","submission",submission.Id),ct:ct);
            return committed.Count;
        }
        finally { scope.SubmissionId=null;scope.UserId=null;scope.AssignmentId=null; }
    }
    public void AddSubmission(Submission submission)
    {
        if (writer is null) throw new InvalidOperationException("ASG writes require a write transaction.");
        writer.Submissions.Add(submission);
    }
    public Task<T> WriteAsync<T>(int assignmentId, Func<Task<T>> action, CancellationToken ct)
        => InTransactionAsync(assignmentId, action, ct);

    private async Task<T> InTransactionAsync<T>(int? assignmentId, Func<Task<T>> action, CancellationToken ct)
    {
        if (writer is not null) throw new InvalidOperationException("Nested ASG repository writes are not supported.");
        var outer = db.Database.CurrentTransaction;
        await using var owned = outer is null ? await db.Database.BeginTransactionAsync(ct) : null;
        var transaction = outer ?? owned!;
        var savepoint = "asg_" + Guid.NewGuid().ToString("N");
        if (outer is not null) await transaction.CreateSavepointAsync(savepoint, ct);
        // The child borrows the connection/transaction, but owns only its ASG tracked entities.
        await using var isolated = new MoodleDbContext(new DbContextOptionsBuilder<MoodleDbContext>()
            .UseNpgsql(db.Database.GetDbConnection()).Options);
        await isolated.Database.UseTransactionAsync(transaction.GetDbTransaction(), ct);
        writer = isolated;
        try
        {
            if (assignmentId is int id)
            {
                var courseId = await isolated.Assignments.Where(a => a.Id == id)
                    .Select(a => (int?)a.CourseId).SingleOrDefaultAsync(ct) ?? throw AppException.NotFound();
                await AsgCourseWriteFence.RequireLiveAsync(isolated, courseId, ct);
                await isolated.Database.ExecuteSqlInterpolatedAsync(
                    $"""SELECT "Id" FROM "Assignments" WHERE "Id" = {id} FOR UPDATE""", ct);
            }
            var result = await action();
            await isolated.SaveChangesAsync(ct);
            if (owned is not null) await owned.CommitAsync(ct);
            else await transaction.ReleaseSavepointAsync(savepoint, ct);
            return result;
        }
        catch (Exception ex)
        {
            // Cancellation must not prevent rollback; a caller-owned transaction stays usable.
            if (owned is not null) await owned.RollbackAsync(CancellationToken.None);
            else await transaction.RollbackToSavepointAsync(savepoint, CancellationToken.None);
            if (ex is DbUpdateConcurrencyException
                || ex is DbUpdateException { InnerException: PostgresException { SqlState: "23505" } })
                throw AppException.Conflict("version_conflict", "Teslim değişti. Güncel sürümü yükleyin.");
            throw;
        }
        finally { writer = null; }
    }
}
