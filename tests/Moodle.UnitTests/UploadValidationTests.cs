using System.Text;
using Moodle.Application.Errors;
using Moodle.Application.Files;
using Moodle.Application.Auth;
namespace Moodle.UnitTests;

public class UploadValidationTests
{
    [Theory]
    [InlineData("../a.txt")] [InlineData("a/b.txt")] [InlineData("a\\b.txt")]
    [InlineData("")] [InlineData(".")] [InlineData("a:evil.txt")] [InlineData("a\0.txt")]
    public void T_FILE_12_invalid_names_are_rejected(string name)
        => Assert.Equal(400,Assert.Throws<AppException>(()=>UploadValidation.Name(name,"/")).StatusCode);
    [Fact]
    public void T_FILE_12_name_length_boundary()
    {
        UploadValidation.Name(new string('x',255),"/");
        Assert.Throws<AppException>(()=>UploadValidation.Name(new string('x',256),"/"));
    }
    [Fact]
    public void T_FILE_09_duplicate_names_keep_extension_and_original()
    {
        var names=new HashSet<string>{"rapor.pdf","rapor (1).pdf"};
        Assert.Equal("rapor (2).pdf",UploadValidation.UniqueName("rapor.pdf",names));
        Assert.Equal(2,names.Count);
        var longName=new string('x',251)+".txt";
        Assert.Equal(255,UploadValidation.UniqueName(longName,new HashSet<string>{longName}).Length);
    }
    [Theory]
    [InlineData("report.pdf","plain text")] [InlineData("report.exe","MZ")]
    [InlineData("image.png","%PDF-1.7")] [InlineData("image.jpg","text")]
    public void T_FILE_13_extension_and_signature_must_match(string name,string content)
        => Assert.Equal(415,Assert.Throws<AppException>(()=>UploadValidation.Type(name,Encoding.UTF8.GetBytes(content),new())).StatusCode);
    [Theory]
    [InlineData("mod/assign:submit","Student",true)]
    [InlineData("mod/assign:submit","Teacher",false)]
    [InlineData("mod/assign:grade","Teacher",true)]
    [InlineData("mod/assign:grade","Student",false)]
    [InlineData("mod/assign:addinstance","EditingTeacher",true)]
    [InlineData("mod/assign:addinstance","Teacher",false)]
    public void Asg_capabilities_respect_course_roles(string capability,string role,bool allowed)
        =>Assert.Equal(allowed,CapabilityRules.Evaluate(capability,new HashSet<string>(),role));
    [Theory]
    [InlineData("a.pdf","255044462d312e37","application/pdf")]
    [InlineData("a.png","89504e470d0a1a0a","image/png")]
    [InlineData("a.jpg","ffd8ffe0","image/jpeg")]
    [InlineData("a.jpeg","ffd8ffe0","image/jpeg")]
    [InlineData("a.txt","74657874","text/plain")]
    public void T_FILE_13_allowed_signatures_have_server_derived_MIME(string name,string hex,string expected)
        =>Assert.Equal(expected,UploadValidation.Type(name,Convert.FromHexString(hex),new()));}
