using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moodle.Application.Files;
using Moodle.Domain.Assignments;
using Moodle.Domain.Courses;
using Moodle.Infrastructure.Auth;
using Moodle.Infrastructure.Courses;
using Moodle.Infrastructure.Files;
using Moodle.Infrastructure.Grading;
using Moodle.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
namespace Moodle.IntegrationTests;

public sealed class V2Fixture : WebApplicationFactory<Program>,IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres=new PostgreSqlBuilder("postgres:17").Build();
    private readonly string root=Path.Combine(Path.GetTempPath(),"asg-v2-"+Guid.NewGuid());
    public TestScanner Scanner {get;}=new();
    public int StudentId {get;private set;} public int OtherId {get;private set;} public int TeacherId {get;private set;}
    public int CourseId {get;private set;}
    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        using(var scope=Services.CreateScope()) {
            foreach(var db in new DbContext[]{scope.ServiceProvider.GetRequiredService<AuthDbContext>(),scope.ServiceProvider.GetRequiredService<CrsDbContext>(),
                scope.ServiceProvider.GetRequiredService<MoodleDbContext>(),scope.ServiceProvider.GetRequiredService<FilesDbContext>(),scope.ServiceProvider.GetRequiredService<GrdDbContext>()})
                await db.Database.MigrateAsync();
        }
        StudentId=(await AuthPostgresFixture.SeedUserAsync(this,"v2-student","test-pass")).Id;
        OtherId=(await AuthPostgresFixture.SeedUserAsync(this,"v2-other","test-pass")).Id;
        TeacherId=(await AuthPostgresFixture.SeedUserAsync(this,"v2-teacher","test-pass")).Id;
        using var data=Services.CreateScope();var crs=data.ServiceProvider.GetRequiredService<CrsDbContext>();
        var cat=new CourseCategory{Name="V2"};crs.Categories.Add(cat);await crs.SaveChangesAsync();
        var course=new Course{CategoryId=cat.Id,FullName="V2 course",ShortName="V2"};crs.Courses.Add(course);await crs.SaveChangesAsync();CourseId=course.Id;
        crs.Enrolments.AddRange(new CourseEnrolment{CourseId=CourseId,UserId=StudentId,Role="Student"},
            new CourseEnrolment{CourseId=CourseId,UserId=OtherId,Role="Student"},
            new CourseEnrolment{CourseId=CourseId,UserId=TeacherId,Role="EditingTeacher"});await crs.SaveChangesAsync();
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing").ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>{
            ["Development:UseFakeIdentity"]="false",["Database:Initialize"]="false",["SEED_SAMPLE_DATA"]="false"
        })).ConfigureTestServices(services=>{
            Replace<AuthDbContext>(services);Replace<CrsDbContext>(services);Replace<MoodleDbContext>(services);
            Replace<FilesDbContext>(services);Replace<GrdDbContext>(services);
            services.RemoveAll<IFileStore>();services.AddSingleton<IFileStore>(new LocalDiskFileStore(new(root)));
            services.RemoveAll<IVirusScanner>();services.AddSingleton<IVirusScanner>(Scanner);
            services.AddHttpClient<Moodle.Web.Assignments.AsgClient>(c=>c.BaseAddress=new Uri("http://localhost"))
                .ConfigurePrimaryHttpMessageHandler(()=>Server.CreateHandler());
        });
    }
    private void Replace<T>(IServiceCollection services) where T:DbContext
    {
        services.RemoveAll<T>();services.RemoveAll<DbContextOptions<T>>();services.RemoveAll<IDbContextOptionsConfiguration<T>>();
        services.AddDbContext<T>(o=>o.UseNpgsql(postgres.GetConnectionString()));
    }
    public async Task<HttpClient> Login(string name="v2-student")
    {
        var client=CreateClient();
        var response=await client.PostAsJsonAsync("/api/v1/auth/login",new{username=name,password="test-pass"});
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var token=await client.GetFromJsonAsync<Token>("/api/v1/antiforgery");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",token!.Value);
        return client;
    }
    public async Task<Assignment> ExistingAssignment(string[]? types=null)
    {
        using var scope=Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<MoodleDbContext>();
        var a=new Assignment{CourseId=CourseId,SectionId=1,Title="V2 "+Guid.NewGuid(),RequiresConfirmation=true,EnabledSubmissionTypes=types??["text","file"]};
        db.Assignments.Add(a);await db.SaveChangesAsync();return a;
    }
    async Task IAsyncLifetime.DisposeAsync() {await DisposeAsync();await postgres.DisposeAsync();if(Directory.Exists(root))Directory.Delete(root,true);}
    private sealed record Token([property:System.Text.Json.Serialization.JsonPropertyName("token")]string Value);
    public sealed class TestScanner : IVirusScanner
    {
        public VirusScanResult Result=VirusScanResult.Clean; public bool Unavailable;
        public Task<VirusScanResult> ScanAsync(Stream content,CancellationToken ct=default)
        {
            if(Unavailable)throw new Moodle.Application.Errors.AppException("scanner_unavailable","Test scanner unavailable",503);
            return Task.FromResult(Result);
        }
    }
}

public class FileUploadV2Tests(V2Fixture fixture) : IClassFixture<V2Fixture>
{
    private static async Task<long> Draft(HttpClient client)
    {
        var response=await client.PostAsync("/api/v1/file-drafts/",null);
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DraftInfo>())!.DraftId;
    }
    public static async Task<HttpResponseMessage> Upload(HttpClient client,long draft,string name,byte[] content)
    {
        using var body=new MultipartFormDataContent();body.Add(new ByteArrayContent(content),"file",name);
        return await client.PostAsync($"/api/v1/file-drafts/{draft}/files",body);
    }
    [Fact]
    public async Task T_FILE_01_09_owner_only_and_concurrent_names()
    {
        using var owner=await fixture.Login();using var other=await fixture.Login("v2-other");var draft=await Draft(owner);
        Assert.Equal(HttpStatusCode.NotFound,(await Upload(other,draft,"same.txt","data"u8.ToArray())).StatusCode);
        var responses=await Task.WhenAll(Enumerable.Range(0,3).Select(_=>Upload(owner,draft,"same.txt","data"u8.ToArray())));
        Assert.All(responses,r=>Assert.Equal(HttpStatusCode.Created,r.StatusCode));
        var names=new List<string>();foreach(var r in responses)names.Add((await r.Content.ReadFromJsonAsync<FileMeta>())!.Name);
        Assert.Equal(new[]{"same (1).txt","same (2).txt","same.txt"},names.Order());
        using var scope=fixture.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<FilesDbContext>();
        Assert.Equal(1,await db.Entries.Where(e=>e.ItemId==draft && e.Area=="draft").Select(e=>e.ContentHash).Distinct().CountAsync());
    }
    [Theory]
    [InlineData("bad.pdf","text",415)] [InlineData("empty.txt","",400)]
    [InlineData("../bad.txt","text",400)] [InlineData("bad.exe","MZ",415)]
    public async Task T_FILE_12_13_rejected_input_creates_no_entries(string name,string data,int status)
    {
        using var client=await fixture.Login();var draft=await Draft(client);
        Assert.Equal(status,(int)(await Upload(client,draft,name,Encoding.UTF8.GetBytes(data))).StatusCode);
        using var scope=fixture.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<FilesDbContext>().Entries.AnyAsync(e=>e.ItemId==draft && e.Area=="draft"));
    }
    [Fact]
    public async Task T_FILE_11_server_limit_and_area_count()
    {
        using var scope=fixture.Services.CreateScope();var limits=scope.ServiceProvider.GetRequiredService<UploadLimits>();
        var original=limits.MaxFileBytes;var count=limits.MaxFiles;
        try {
            limits.MaxFileBytes=4;limits.MaxFiles=1;
            using var client=await fixture.Login();var draft=await Draft(client);
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge,(await Upload(client,draft,"a.txt","12345"u8.ToArray())).StatusCode);
            Assert.Equal(HttpStatusCode.Created,(await Upload(client,draft,"a.txt","1234"u8.ToArray())).StatusCode);
            Assert.Equal(HttpStatusCode.UnprocessableEntity,(await Upload(client,draft,"b.txt","1"u8.ToArray())).StatusCode);
        } finally {limits.MaxFileBytes=original;limits.MaxFiles=count;}
    }
    [Theory]
    [InlineData(false,422)] [InlineData(true,503)]
    public async Task T_FILE_14_scanner_rejection_never_writes_durable_content(bool unavailable,int status)
    {
        using var client=await fixture.Login();var draft=await Draft(client);
        fixture.Scanner.Result=VirusScanResult.Infected;fixture.Scanner.Unavailable=unavailable;
        try {
            Assert.Equal(status,(int)(await Upload(client,draft,"virus.txt","test"u8.ToArray())).StatusCode);
            using var scope=fixture.Services.CreateScope();
            Assert.False(await scope.ServiceProvider.GetRequiredService<FilesDbContext>().Entries.AnyAsync(e=>e.ItemId==draft && e.Area=="draft"));
        } finally {fixture.Scanner.Result=VirusScanResult.Clean;fixture.Scanner.Unavailable=false;}
    }
    [Fact]
    public async Task Cookie_writes_require_CSRF()
    {
        using var client=await fixture.Login();client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/api/v1/file-drafts/",null)).StatusCode);
        var a=await fixture.ExistingAssignment();
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync($"/api/v1/assignments/{a.Id}/my-submission",new{Text="CSRF",Version=0})).StatusCode);
    }
    [Fact]
    public async Task Real_AUTH_file_submission_scope_version_and_atomic_rollback()
    {
        using var student=await fixture.Login();using var teacher=await fixture.Login("v2-teacher");using var other=await fixture.Login("v2-other");
        var a=await fixture.ExistingAssignment(["file"]);var draft=await Draft(student);
        var uploaded=await Upload(student,draft,"work.txt","file-only submission"u8.ToArray());Assert.Equal(HttpStatusCode.Created,uploaded.StatusCode);
        var file=(await uploaded.Content.ReadFromJsonAsync<FileMeta>())!;
        var path=$"/api/v1/assignments/{a.Id}/my-submission";
        var saved=await student.PutAsJsonAsync(path,new{Text="",Version=0,FileDraftId=draft});
        Assert.Equal(HttpStatusCode.Created,saved.StatusCode);var submission=(await saved.Content.ReadFromJsonAsync<Submission>())!;
        Assert.Equal(1,submission.FileCount);
        Assert.Equal(HttpStatusCode.OK,(await teacher.GetAsync($"/api/v1/files/{file.FileId}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync($"/api/v1/files/{file.FileId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync($"/api/v1/files/{file.FileId}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await student.DeleteAsync($"/api/v1/files/{file.FileId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await student.PutAsJsonAsync(path,new{Text="",Version=0,FileDraftId=draft})).StatusCode);
        var rejectedDraft=await Draft(student);
        await Upload(student,rejectedDraft,"new.txt","replacement"u8.ToArray());
        fixture.Scanner.Unavailable=true;
        try {Assert.Equal(HttpStatusCode.ServiceUnavailable,(await student.PutAsJsonAsync(path,new{Text="",Version=1,FileDraftId=rejectedDraft})).StatusCode);}
        finally {fixture.Scanner.Unavailable=false;}
        var mine=await student.GetFromJsonAsync<Moodle.Application.Assignments.MySubmission>(path);
        Assert.Equal(1,mine!.Submission!.Version);Assert.Equal(1,mine.Submission.FileCount);
        Assert.Equal(HttpStatusCode.OK,(await teacher.GetAsync($"/api/v1/files/{file.FileId}")).StatusCode);
        var sent=await student.PostAsJsonAsync(path+"/submit",new{Version=1});Assert.Equal(HttpStatusCode.OK,sent.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await student.PutAsJsonAsync(path,new{Text="",Version=2,FileDraftId=rejectedDraft})).StatusCode);
    }
    [Theory]
    [InlineData("locked")] [InlineData("closed")] [InlineData("disabled")]
    public async Task File_save_preserves_draft_when_assignment_rejects(string reason)
    {
        using var client=await fixture.Login();var a=await fixture.ExistingAssignment(reason=="disabled"?["text"]:["file"]);
        using(var scope=fixture.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<MoodleDbContext>();
            if(reason=="locked") db.SubmissionRules.Add(new(){AssignmentId=a.Id,LearnerId=fixture.StudentId,Locked=true});
            if(reason=="closed") {var row=await db.Assignments.FindAsync(a.Id);row!.CutoffAt=DateTimeOffset.UtcNow.AddDays(-1);}
            await db.SaveChangesAsync();
        }
        var draft=await Draft(client);await Upload(client,draft,"keep.txt","keep"u8.ToArray());
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync($"/api/v1/assignments/{a.Id}/my-submission",new{Text="",Version=0,FileDraftId=draft})).StatusCode);
        var files=await client.GetStringAsync($"/api/v1/file-drafts/{draft}/files");Assert.Contains("keep.txt",files);
        using var check=fixture.Services.CreateScope();
        Assert.False(await check.ServiceProvider.GetRequiredService<MoodleDbContext>().Submissions.AnyAsync(x=>x.AssignmentId==a.Id));
    }
    [Fact]
    public async Task Concurrent_file_saves_commit_once_and_retain_losing_draft()
    {
        using var client=await fixture.Login();var a=await fixture.ExistingAssignment(["file"]);
        var drafts=new[]{await Draft(client),await Draft(client)};
        foreach(var id in drafts) Assert.Equal(HttpStatusCode.Created,(await Upload(client,id,"work.txt",Encoding.UTF8.GetBytes("draft "+id))).StatusCode);
        var responses=await Task.WhenAll(drafts.Select(id=>client.PutAsJsonAsync($"/api/v1/assignments/{a.Id}/my-submission",new{Text="",Version=0,FileDraftId=id})));
        Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Created);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict);
        using var scope=fixture.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<FilesDbContext>();
        Assert.Equal(1,await db.Entries.CountAsync(e=>e.Component=="user" && e.Area=="draft" && drafts.Contains(e.ItemId) && e.DeletedAt==null));
        Assert.Equal(1,await db.Entries.CountAsync(e=>e.Component=="mod_assign" && e.ContextId==a.Id && e.DeletedAt==null));
    }
    [Fact]
    public async Task Real_AUTH_course_list_and_evaluator_scope_reject_foreign_teacher()
    {
        using var student=await fixture.Login();using var teacher=await fixture.Login("v2-teacher");
        var a=await fixture.ExistingAssignment();
        Assert.Equal(HttpStatusCode.OK,(await student.GetAsync($"/api/v1/courses/{fixture.CourseId}/assignments")).StatusCode);
        var save=await student.PutAsJsonAsync($"/api/v1/assignments/{a.Id}/my-submission",new{Text="scope",Version=0});
        var submission=(await save.Content.ReadFromJsonAsync<Submission>())!;
        Assert.Equal(HttpStatusCode.OK,(await teacher.GetAsync($"/api/v1/assignments/{a.Id}/submissions/{submission.Id}")).StatusCode);
        using(var scope=fixture.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<CrsDbContext>();
            var enrolment=await db.Enrolments.SingleAsync(e=>e.UserId==fixture.TeacherId && e.CourseId==fixture.CourseId);
            db.Enrolments.Remove(enrolment);await db.SaveChangesAsync();
        }
        try {
            Assert.Equal(HttpStatusCode.NotFound,(await teacher.GetAsync($"/api/v1/assignments/{a.Id}/submissions/{submission.Id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await teacher.GetAsync($"/api/v1/assignments/{a.Id}/submissions")).StatusCode);
        } finally {
            using var scope=fixture.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CrsDbContext>();
            db.Enrolments.Add(new(){CourseId=fixture.CourseId,UserId=fixture.TeacherId,Role="EditingTeacher"});await db.SaveChangesAsync();
        }
    }    [Fact]
    public async Task Failed_store_result_rolls_back_metadata_and_new_content()
    {
        using var client=await fixture.Login();var draft=await Draft(client);
        using var scope=fixture.Services.CreateScope();var sp=scope.ServiceProvider;
        var store=sp.GetRequiredService<IFileStore>();var faulty=new IncorrectSizeStore(store);
        var uploader=new DraftUploadService(sp.GetRequiredService<FilesDbContext>(),faulty,fixture.Scanner,sp.GetRequiredService<UploadLimits>(),TimeProvider.System);
        using var data=new MemoryStream(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
        await Assert.ThrowsAsync<IOException>(()=>uploader.AddAsync(fixture.StudentId,draft,"work.txt",data,"/",false,default));
        Assert.NotNull(faulty.Written);Assert.False(await store.ExistsAsync(faulty.Written!.ContentHash));
        Assert.False(await sp.GetRequiredService<FilesDbContext>().Entries.AnyAsync(e=>e.ItemId==draft && e.Area=="draft"));
        Assert.False(await sp.GetRequiredService<FilesDbContext>().Blobs.AnyAsync(b=>b.ContentHash==faulty.Written.ContentHash));
    }
    [Fact]
    public async Task Area_total_boundary_uses_server_configuration()
    {
        using var scope=fixture.Services.CreateScope();var limits=scope.ServiceProvider.GetRequiredService<UploadLimits>();var previous=limits.MaxAreaBytes;
        try {
            limits.MaxAreaBytes=5;using var client=await fixture.Login();var draft=await Draft(client);
            Assert.Equal(HttpStatusCode.Created,(await Upload(client,draft,"a.txt","123"u8.ToArray())).StatusCode);
            Assert.Equal(HttpStatusCode.Created,(await Upload(client,draft,"b.txt","45"u8.ToArray())).StatusCode);
            var response=await Upload(client,draft,"c.txt","6"u8.ToArray());Assert.Equal(422,(int)response.StatusCode);
            Assert.Contains("area_limit_exceeded",await response.Content.ReadAsStringAsync());
        } finally {limits.MaxAreaBytes=previous;}
    }
    private sealed class IncorrectSizeStore(IFileStore inner):IFileStore
    {
        public StoredBlob? Written;
        public async Task<StoredBlob> WriteAsync(Stream s,CancellationToken ct=default) {Written=await inner.WriteAsync(s,ct);return Written with{SizeBytes=Written.SizeBytes+1};}
        public Task<Stream?> OpenReadAsync(string h,CancellationToken ct=default)=>inner.OpenReadAsync(h,ct);
        public Task<bool> ExistsAsync(string h,CancellationToken ct=default)=>inner.ExistsAsync(h,ct);
        public Task DeleteAsync(string h,CancellationToken ct=default)=>inner.DeleteAsync(h,ct);
        public IAsyncEnumerable<string> ListHashesAsync(CancellationToken ct=default)=>inner.ListHashesAsync(ct);
    }    [Fact]
    public async Task T_FILE_01_one_MiB_PDF_is_private_and_keeps_original_bytes()
    {
        using var owner=await fixture.Login();using var other=await fixture.Login("v2-other");var draft=await Draft(owner);
        var bytes=new byte[1024*1024];Array.Fill(bytes,(byte)' ');"%PDF-1.7\n"u8.CopyTo(bytes);
        var response=await Upload(owner,draft,"report.pdf",bytes);Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var meta=(await response.Content.ReadFromJsonAsync<FileMeta>())!;
        Assert.Equal("application/pdf",meta.MimeType);Assert.Equal(bytes.Length,meta.SizeBytes);
        Assert.Equal(bytes,await owner.GetByteArrayAsync($"/api/v1/files/{meta.FileId}/content"));
        Assert.Contains("report.pdf",await owner.GetStringAsync($"/api/v1/file-drafts/{draft}/files"));
        Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync($"/api/v1/file-drafts/{draft}/files")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync($"/api/v1/files/{meta.FileId}/content")).StatusCode);
    }
    [Theory]
    [InlineData(255,201)] [InlineData(256,400)]
    public async Task T_FILE_12_HTTP_name_length_boundary(int length,int expected)
    {
        using var client=await fixture.Login();var draft=await Draft(client);
        var response=await Upload(client,draft,new string('a',length-4)+".txt","data"u8.ToArray());
        Assert.Equal(expected,(int)response.StatusCode);
        if(expected!=201)Assert.Contains("invalid_name",await response.Content.ReadAsStringAsync());
    }
    [Theory]
    [InlineData("folder/",400)] [InlineData("invalid.txt",415)]
    public async Task T_FILE_13_folder_and_non_UTF8_text_rejected(string name,int expected)
    {
        using var client=await fixture.Login();var draft=await Draft(client);
        var response=await Upload(client,draft,name,new byte[]{0xff,0xfe,0xff});
        Assert.Equal(expected,(int)response.StatusCode);
        using var scope=fixture.Services.CreateScope();Assert.False(await scope.ServiceProvider.GetRequiredService<FilesDbContext>().Entries.AnyAsync(e=>e.Area=="draft" && e.ItemId==draft));
    }
    [Fact]
    public async Task T_FILE_11_database_limit_is_stricter_than_server_limit()
    {
        using var scope=fixture.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<FilesDbContext>();var settings=await db.Settings.SingleAsync();var old=settings.MaxFileBytes;
        try {
            settings.MaxFileBytes=3;await db.SaveChangesAsync();using var client=await fixture.Login();var draft=await Draft(client);
            var r=await Upload(client,draft,"work.txt","1234"u8.ToArray());Assert.Equal(413,(int)r.StatusCode);
            Assert.Contains("file_too_large",await r.Content.ReadAsStringAsync());Assert.False(await db.Entries.AnyAsync(e=>e.Area=="draft" && e.ItemId==draft));
        } finally {settings.MaxFileBytes=old;await db.SaveChangesAsync();}
    }
    [Fact]
    public async Task Real_cookie_SSR_file_form_conflict_list_detail_and_download()
    {
        using var student=await fixture.Login();using var teacher=await fixture.Login("v2-teacher");using var other=await fixture.Login("v2-other");
        var a=await fixture.ExistingAssignment(["file"]);var draft=await Draft(student);
        var uploaded=await Upload(student,draft,"visible.txt","SSR file"u8.ToArray());var file=(await uploaded.Content.ReadFromJsonAsync<FileMeta>())!;
        var page=$"/assignments/{a.Id}/submit";
        var html=await student.GetStringAsync(page);Assert.Contains("data-asg-upload",html);Assert.Contains("Form.FileDraftId",html);
        static string Token(string source)=>WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Match(source,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        async Task<string> Save(long version,long draftId,string token) {
            var r=await student.PostAsync(page,new FormUrlEncodedContent(new Dictionary<string,string>{["__RequestVerificationToken"]=token,["_handler"]="saveSubmission",["Form.Version"]=version.ToString(),["Form.FileDraftId"]=draftId.ToString(),["Form.Text"]=""}));
            Assert.Equal(HttpStatusCode.OK,r.StatusCode);return WebUtility.HtmlDecode(await r.Content.ReadAsStringAsync());
        }
        html=await Save(0,draft,Token(html));Assert.Contains("Teslim kaydedildi",html);Assert.Contains("visible.txt",html);
        var mine=(await student.GetFromJsonAsync<Moodle.Application.Assignments.MySubmission>($"/api/v1/assignments/{a.Id}/my-submission"))!;
        var next=await Draft(student);await Upload(student,next,"preserved.txt","pending file"u8.ToArray());
        html=await Save(0,next,Token(html));Assert.Contains("yüklediğiniz dosya taslağı",html);Assert.Contains("preserved.txt",html);
        Assert.Contains($"value=\"{next}\" data-draft-id",html);
        var list=WebUtility.HtmlDecode(await teacher.GetStringAsync($"/assignments/{a.Id}/submissions"));Assert.Contains("Dosya sayısı",list);Assert.Contains($"/submissions/{mine.Submission!.Id}",list);
        var detail=$"/assignments/{a.Id}/submissions/{mine.Submission.Id}";
        html=await teacher.GetStringAsync(detail);Assert.Contains("visible.txt",html);Assert.Contains($"/api/v1/files/{file.FileId}/content?download=true",html);
        Assert.Equal("SSR file",await teacher.GetStringAsync($"/api/v1/files/{file.FileId}/content"));
        html=await other.GetStringAsync(detail);Assert.DoesNotContain("visible.txt",html);Assert.DoesNotContain($"/api/v1/files/{file.FileId}",html);
    }}
