using Microsoft.AspNetCore.Http.Features;
using Moodle.Application.Abstractions;
using Moodle.Application.Errors;
using Moodle.Application.Files;
namespace Moodle.Api.Files;

public static class DraftEndpoints
{
    public static void MapDraftEndpoints(this WebApplication app)
    {
        var group=app.MapGroup("/api/v1/file-drafts").WithTags("FILE").AddEndpointFilter<CookieWriteProtection>();
        group.MapGet("/limits",(ICurrentUser user,UploadLimits limits)=>{ _=User(user);return Results.Ok(limits); });
        group.MapPost("/",async(ICurrentUser user,IFileService files,CancellationToken ct)=>{
            var draft=await files.CreateDraftAsync(User(user),ct);
            return Results.Created($"/api/v1/file-drafts/{draft.DraftId}/files",draft);
        });
        group.MapPost("/{draftId:long}/files",async(long draftId,HttpRequest request,ICurrentUser user,IFileService files,UploadLimits limits,CancellationToken ct)=>{
            var id=User(user);
            if(!request.HasFormContentType) throw new AppException("unsupported_media_type","multipart/form-data gerekli.",415);
            var feature=request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if(feature is {IsReadOnly:false}) feature.MaxRequestBodySize=limits.MaxFileBytes+1024*1024;
            IFormCollection form;
            try { form=await request.ReadFormAsync(new FormOptions{MultipartBodyLengthLimit=limits.MaxFileBytes+1024*1024},ct); }
            catch(InvalidDataException) { throw new AppException("file_too_large","Yükleme sınırı aşıldı.",413); }
            if(form.Files.Count!=1 || form.Files.GetFile("file") is not {} file)
                throw new AppException("empty_file","Tek bir dosya seçin.",400);
            await using var stream=file.OpenReadStream();
            var meta=await files.AddToDraftAsync(id,draftId,file.FileName,stream,form["path"].FirstOrDefault()??"/",
                bool.TryParse(form["overwrite"],out var replace)&&replace,ct);
            return Results.Created($"/api/v1/files/{meta.FileId}",meta);
        });
        group.MapGet("/{draftId:long}/files",async(long draftId,ICurrentUser user,IFileService files,CancellationToken ct)
            => Results.Ok(new{files=await files.ListDraftAsync(User(user),draftId,ct:ct)}));
        group.MapDelete("/{draftId:long}/files/{fileId:long}",async(long draftId,long fileId,ICurrentUser user,IFileService files,CancellationToken ct)=>{
            await files.DeleteFromDraftAsync(User(user),draftId,fileId,ct);return Results.NoContent();
        });
    }
    private static int User(ICurrentUser user)=>user.IsAuthenticated && user.UserId is >0 ? user.UserId.Value
        :throw new AppException("not_authenticated","Oturum açmanız gerekiyor.",401);
}
