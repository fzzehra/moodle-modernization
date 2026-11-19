namespace Moodle.Application.Courses;

/// <summary>Deterministic extension ordering, independent of plugins, DI and other modules.</summary>
public sealed class CrsEventExtensionPipeline
{
    public IReadOnlyList<ICrsEventExtension> Extensions { get; }

    public CrsEventExtensionPipeline(IEnumerable<ICrsEventExtension> extensions)
    {
        var ordered = extensions.OrderBy(x => x.Order)
            .ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
        if (ordered.Any(x => string.IsNullOrWhiteSpace(x.Id) || x.Id.Length > 100) ||
            ordered.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
            throw new ArgumentException("CRS extension IDs must be unique, nonblank and at most 100 characters.", nameof(extensions));
        Extensions = Array.AsReadOnly(ordered);
    }
}
