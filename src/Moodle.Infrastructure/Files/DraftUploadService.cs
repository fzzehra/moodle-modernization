using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moodle.Application.Errors;
using Moodle.Application.Files;
using Moodle.Domain.Files;
namespace Moodle.Infrastructure.Files;

/// <summary>Aslı Ceren: validated/scanned draft uploads. Reuses Seray's store; no durable content before validation.</summary>
public sealed class DraftUploadService(FilesDbContext db, IFileStore store, IVirusScanner scanner, UploadLimits limits, TimeProvider clock)
{
    public async Task<FileMeta> AddAsync(int userId, long draftId, string name, Stream content, string path, bool overwrite, CancellationToken ct, long quotaRemaining = long.MaxValue)
    {
        if(db.Database.CurrentTransaction is not null) throw new InvalidOperationException("Draft uploads own their transaction; upload before opening a module transaction.");
        UploadValidation.Name(name, path);
        if (userId <= 0) throw AppException.NotFound();
        var owner = await db.Drafts.AsNoTracking().SingleOrDefaultAsync(x=>x.DraftId==draftId && x.OwnerUserId==userId,ct);
        if (owner is null) throw AppException.NotFound();
        var settings = await db.Settings.AsNoTracking().SingleAsync(ct);
        var max = Math.Min(settings.MaxFileBytes, limits.MaxFileBytes);
        if(max <= 0 || limits.MaxAreaBytes <= 0 || limits.MaxFiles <= 0)
            throw new AppException("upload_unavailable","Yükleme yapılandırması geçersiz.",503);
        // OS temporary file is a bounded quarantine buffer, not another physical content store.
        await using var temp = new FileStream(Path.Combine(Path.GetTempPath(),Path.GetRandomFileName()),
            FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,81920,FileOptions.Asynchronous|FileOptions.DeleteOnClose);
        var buffer=new byte[81920]; long size=0; int read;
        while((read=await content.ReadAsync(buffer,ct))>0) {
            size+=read; if(size>max) throw new AppException("file_too_large","Dosya boyutu sınırı aşıldı.",413);
            await temp.WriteAsync(buffer.AsMemory(0,read),ct);
        }
        if(size==0) throw new AppException("empty_file","Boş dosya yüklenemez.",400);
        temp.Position=0; var prefix=new byte[Math.Min(81920,size)]; await temp.ReadExactlyAsync(prefix,ct);
        var mime=UploadValidation.Type(name,prefix,limits);
        if(mime=="text/plain") {
            temp.Position=0;
            using var text=new StreamReader(temp,new UTF8Encoding(false,true),false,4096,leaveOpen:true);
            var chars=new char[4096];
            try { while((read=await text.ReadAsync(chars,ct))>0)
                if(chars.AsSpan(0,read).Contains('\0')) throw UploadValidation.TypeError(); }
            catch(DecoderFallbackException) { throw UploadValidation.TypeError(); }
        }
        temp.Position=0;
        if(await scanner.ScanAsync(temp,ct)!=VirusScanResult.Clean)
            throw AppException.Validation("virus_detected","Dosyada zararlı içerik tespit edildi.");
        temp.Position=0;
        var hashBytes=await SHA256.HashDataAsync(temp,ct);
        var hash=Convert.ToHexStringLower(hashBytes); temp.Position=0;

        var outer=db.Database.CurrentTransaction;
        await using var owned=outer is null ? await db.Database.BeginTransactionAsync(ct) : null;
        var transaction=outer??owned!;
        var point="upload_"+Guid.NewGuid().ToString("N");
        if(outer is not null) await transaction.CreateSavepointAsync(point,ct);
        await using var isolated=new FilesDbContext(new DbContextOptionsBuilder<FilesDbContext>().UseNpgsql(db.Database.GetDbConnection()).Options);
        await isolated.Database.UseTransactionAsync(transaction.GetDbTransaction(),ct);
        StoredBlob? written=null; bool hadRow=false, committing=false;
        try {
            await isolated.Database.ExecuteSqlInterpolatedAsync($"SELECT \"DraftId\" FROM file_draft WHERE \"DraftId\"={draftId} FOR UPDATE",ct);
            var draft=await isolated.Drafts.SingleOrDefaultAsync(x=>x.DraftId==draftId && x.OwnerUserId==userId,ct)??throw AppException.NotFound();
            if(draft.LastActivityAt.AddHours(settings.DraftTtlHours)<clock.GetUtcNow()) throw AppException.NotFound();
            var entries=await isolated.Entries.Where(e=>e.Component=="user" && e.Area=="draft" && e.ItemId==draftId && e.DeletedAt==null).ToListAsync(ct);
            var old=overwrite ? entries.SingleOrDefault(e=>e.Path==path && e.Name==name) : null;
            if(entries.Sum(e=>e.SizeBytes)-(old?.SizeBytes??0)+size>quotaRemaining)
                throw AppException.Validation("quota_exceeded","Kullanıcı kotası aşıldı.");
            if(entries.Count-(old is null?0:1)>=limits.MaxFiles || entries.Sum(e=>e.SizeBytes)-(old?.SizeBytes??0)+size>limits.MaxAreaBytes)
                throw AppException.Validation("area_limit_exceeded","Dosya sayısı veya toplam alan sınırı aşıldı.");
            // Serialize content reuse across concurrent draft uploads before durable store mutation.
            var key=BinaryPrimitives.ReadInt64BigEndian(hashBytes);
            await isolated.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({key})",ct);
            var row=await isolated.Blobs.SingleOrDefaultAsync(b=>b.ContentHash==hash,ct);
            hadRow=row is not null;
            written=await store.WriteAsync(temp,ct);
            if(written.ContentHash!=hash || written.SizeBytes!=size) throw new IOException("Content store result mismatch.");
            if(row is null) isolated.Blobs.Add(new(){ContentHash=hash,SizeBytes=size,StorageKey=written.StorageKey,CreatedAt=clock.GetUtcNow()});
            else row.OrphanedAt=null;
            if(old is not null) {
                old.DeletedAt=clock.GetUtcNow();
                // Flush the filtered unique-index removal before inserting the replacement.
                await isolated.SaveChangesAsync(ct);
                if(old.ContentHash!=hash && !await isolated.Entries.AnyAsync(e=>e.ContentHash==old.ContentHash && e.DeletedAt==null,ct))
                    (await isolated.Blobs.SingleAsync(b=>b.ContentHash==old.ContentHash,ct)).OrphanedAt=clock.GetUtcNow();
            }
            else name=UploadValidation.UniqueName(name,entries.Select(e=>e.Name).ToHashSet(StringComparer.Ordinal));
            var entry=new FileEntry{ContentHash=hash,ContextId=userId,Component="user",Area="draft",ItemId=draftId,
                Path=path,Name=name,OwnerUserId=userId,SizeBytes=size,MimeType=mime,CreatedAt=clock.GetUtcNow(),UpdatedAt=clock.GetUtcNow()};
            isolated.Entries.Add(entry); draft.LastActivityAt=clock.GetUtcNow();
            await isolated.SaveChangesAsync(ct);
            committing=true;
            if(owned is not null) await owned.CommitAsync(ct); else await transaction.ReleaseSavepointAsync(point,ct);
            return new(entry.Id,entry.Name,entry.Path,entry.SizeBytes,entry.MimeType,entry.ContentHash,entry.OwnerUserId,entry.CreatedAt);
        }
        catch {
            // Do not delete potentially committed content after an ambiguous commit/network failure.
            if(!committing && written is {AlreadyExisted:false} && !hadRow) await store.DeleteAsync(written.ContentHash,CancellationToken.None);
            if(owned is not null) await owned.RollbackAsync(CancellationToken.None);
            else await transaction.RollbackToSavepointAsync(point,CancellationToken.None);
            throw;
        }
    }
}
