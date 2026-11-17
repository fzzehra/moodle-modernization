using System.Reflection;

namespace Moodle.UnitTests;

/// <summary>
/// Katman kurallarını denetler (WBS-2 §2.2). Bağımlılık yönü içeriye doğrudur:
/// Api → Application/Infrastructure/Web, Infrastructure → Application → Domain.
/// Bir kural bozulursa derleme sırasında değil, bu testte yakalanır (K13).
/// </summary>
public class ArchitectureTests
{
    private static IReadOnlySet<string> MoodleRefs(Assembly a) =>
        a.GetReferencedAssemblies()
         .Select(n => n.Name!)
         .Where(n => n.StartsWith("Moodle.", StringComparison.Ordinal))
         .ToHashSet();

    [Fact]
    public void Domain_hicbir_katmana_bagli_degildir() =>
        Assert.Empty(MoodleRefs(typeof(Moodle.Application.Abstractions.ContextRef).Assembly)
            .Except(["Moodle.Domain"]));

    [Fact]
    public void Application_yalnizca_Domaine_baglidir()
    {
        var refs = MoodleRefs(typeof(Moodle.Application.Abstractions.ICurrentUser).Assembly);
        Assert.True(refs.IsSubsetOf(new HashSet<string> { "Moodle.Domain" }),
            "Application yalnızca Domain'e bağlanabilir: " + string.Join(", ", refs));
    }

    [Fact]
    public void Infrastructure_Api_ve_Webe_bagli_degildir()
    {
        var refs = MoodleRefs(typeof(Moodle.Infrastructure.DependencyInjection).Assembly);
        Assert.DoesNotContain("Moodle.Api", refs);
        Assert.DoesNotContain("Moodle.Web", refs);
    }

    [Fact]
    public void Web_diger_Moodle_katmanlarina_bagli_degildir()
    {
        // Arayüz yalnızca API'yi (HTTP) bilir; iş mantığına ve veri erişimine doğrudan erişmez.
        var refs = MoodleRefs(typeof(Moodle.Web.Routes).Assembly);
        Assert.Empty(refs);
    }

    [Fact]
    public void Application_EF_Core_gibi_veri_erisim_paketlerine_bagli_degildir()
    {
        var names = typeof(Moodle.Application.Abstractions.ICurrentUser).Assembly
            .GetReferencedAssemblies().Select(n => n.Name!);
        Assert.DoesNotContain(names, n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
                                          || n.StartsWith("Npgsql", StringComparison.Ordinal));
    }
}
