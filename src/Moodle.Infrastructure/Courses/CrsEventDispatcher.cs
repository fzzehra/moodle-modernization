using Microsoft.EntityFrameworkCore;
using Moodle.Application.Courses;

namespace Moodle.Infrastructure.Courses;

/// <summary>
/// Explicit, bounded post-commit dispatch; not a hosted worker or a public endpoint.
/// Use a dedicated context. A row lock serializes concurrent dispatch of one event.
/// </summary>
public sealed class CrsEventDispatcher(CrsDbContext db, CrsEventExtensionPipeline pipeline)
{
    public async Task<int> DispatchAsync(int limit = 100, CancellationToken ct = default)
    {
        if (limit is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(limit));
        if (db.Database.CurrentTransaction is not null || System.Transactions.Transaction.Current is not null)
            throw new InvalidOperationException("Dispatch requires a dedicated context outside an existing transaction.");
        if (pipeline.Extensions.Count == 0) return 0;

        var delivered = 0;
        // One transaction per event: earlier events stay delivered if a later extension fails.
        var ids = await db.Database.SqlQuery<long>($"""
            SELECT e."Id" AS "Value" FROM crs_events e
            WHERE EXISTS (
                SELECT 1 FROM unnest({pipeline.Extensions.Select(x => x.Id).ToArray()}) AS h(id)
                WHERE NOT EXISTS (SELECT 1 FROM crs_event_receipts r
                    WHERE r."EventId" = e."Id" AND r."ExtensionId" = h.id))
            ORDER BY e."Id" LIMIT {limit}
            """).ToListAsync(ct);

        foreach (var id in ids)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var row = (await db.Database.SqlQuery<EventRow>($"""
                SELECT * FROM crs_events WHERE "Id" = {id} FOR UPDATE
                """).ToListAsync(ct)).Single();
            foreach (var extension in pipeline.Extensions)
            {
                var acknowledged = await db.Database.SqlQuery<int>($"""
                    SELECT 1 AS "Value" FROM crs_event_receipts
                    WHERE "EventId" = {id} AND "ExtensionId" = {extension.Id}
                    """).AnyAsync(ct);
                if (acknowledged) continue;
                await extension.HandleAsync(new CrsChangeEvent(row.Id, row.Kind, row.CourseId,
                    row.EnrolmentId, row.UserId, row.OccurredAt, row.Version,
                    (string[])row.ChangedFields.Clone()), ct);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO crs_event_receipts ("EventId", "ExtensionId") VALUES ({id}, {extension.Id})
                    """, ct);
                delivered++;
            }
            await transaction.CommitAsync(ct);
        }
        return delivered;
    }

    private sealed class EventRow
    {
        public long Id { get; set; }
        public string Kind { get; set; } = "";
        public int CourseId { get; set; }
        public int? EnrolmentId { get; set; }
        public int? UserId { get; set; }
        public DateTimeOffset OccurredAt { get; set; }
        public int Version { get; set; }
        public string[] ChangedFields { get; set; } = [];
    }
}
