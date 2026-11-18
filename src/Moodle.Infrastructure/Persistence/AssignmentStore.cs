using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moodle.Application.Assignments;
using Moodle.Application.Errors;
using Moodle.Domain.Assignments;
using Npgsql;
namespace Moodle.Infrastructure.Persistence;

/// <summary>ASG-only change tracking, shared transaction with savepoint when owned by caller.</summary>
public sealed class AssignmentStore(MoodleDbContext db) : IAssignmentStore
{
    private MoodleDbContext? writer;
    private MoodleDbContext Current => writer ?? db;
    public Task<Assignment?> FindAsync(int id, CancellationToken ct)
        => Current.Assignments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task AddAsync(Assignment assignment, CancellationToken ct)
        => await InTransactionAsync(null, () => { Current.Assignments.Add(assignment); return Task.FromResult(true); }, ct);
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
                        IsLate = submission != null && submission.SubmittedAt > dueAt
                    };
        if (status is not null) query = query.Where(row => row.Status == status);
        var rows = await query.OrderBy(row => row.LearnerId).Take(limit + 1)
            .Select(row => new SubmissionRow(row.LearnerId, row.Status, row.SubmissionId, row.SubmittedAt, row.UpdatedAt, row.Version, row.IsLate)).ToArrayAsync(ct);
        return new(rows.Take(limit).ToArray(), rows.Length > limit ? rows[limit - 1].LearnerId : null);
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
                await isolated.Database.ExecuteSqlInterpolatedAsync(
                    $"""SELECT "Id" FROM "Assignments" WHERE "Id" = {id} FOR UPDATE""", ct);
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
