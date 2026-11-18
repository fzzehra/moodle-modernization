using System.Globalization;

namespace Moodle.Application.Grading;

/// <summary>
/// Not defteri sayfalama (WBS-2 §3.5.2: <c>cursor, limit</c>). Satırlar öğrenci kimliğine göre sıralıdır;
/// imleç önceki sayfanın son öğrenci kimliğidir. Sayfa arasında yeni kayıt eklense de satır tekrar etmez.
/// </summary>
public static class GradebookPaging
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    /// <exception cref="ArgumentException">Limit 1 ile <see cref="MaxLimit"/> arasında değilse (API'de 422).</exception>
    public static int ResolveLimit(int? limit)
    {
        var value = limit ?? DefaultLimit;
        if (value is < 1 or > MaxLimit)
            throw new ArgumentException($"Sayfa boyutu 1 ile {MaxLimit} arasında olmalıdır.");
        return value;
    }

    /// <returns>İmleç yoksa <c>null</c>: ilk sayfa.</returns>
    /// <exception cref="ArgumentException">İmleç geçersizse (API'de 422).</exception>
    public static int? ParseCursor(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor))
            return null;
        if (!int.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out var after))
            throw new ArgumentException("Geçersiz sayfa imleci.");
        return after;
    }

    public static (IReadOnlyList<int> Page, string? NextCursor) Page(
        IEnumerable<int> learnerIds, int? after, int limit)
    {
        var window = learnerIds
            .Distinct()
            .Where(id => after is null || id > after.Value)
            .Order()
            .Take(limit + 1)
            .ToList();

        var page = window.Take(limit).ToList();
        var next = window.Count > limit ? page[^1].ToString(CultureInfo.InvariantCulture) : null;
        return (page, next);
    }
}
