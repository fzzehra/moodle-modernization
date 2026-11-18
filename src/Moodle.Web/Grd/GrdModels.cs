using System.Globalization;

namespace Moodle.Web.Grd;

// GRD HTTP sözleşmeleri (WBS-2 §3.5.2). Web projesi Domain'e veya Application'a bağlanmaz;
// yalnızca API'nin JSON biçimini bilir.

public sealed record GrdAssessment(
    int Id, int AssignmentId, int LearnerId, int Attempt, int GraderId,
    decimal? Score, string? Feedback, DateTimeOffset GradedAt, int Version);

/// <summary>GET .../grade: değerlendirme yoksa <c>Assessment</c> null ve <c>Version</c> 0.</summary>
public sealed record GrdGradeView(GrdAssessment? Assessment, decimal MinScore, decimal MaxScore, int Version);

/// <summary>PUT .../grade yanıtı. <c>GradebookUpdated</c> false: defter daha yeni bir denemenin puanını koruyor.</summary>
public sealed record GrdGradeSaveResult(GrdAssessment Assessment, bool GradebookUpdated, decimal MinScore, decimal MaxScore);

/// <summary>PUT .../grade gövdesi. Değerlendirici, öğrenci ve deneme gövdede gönderilmez.</summary>
public sealed record GradeWriteRequest(decimal? Score, string? Feedback, int Version);

public sealed record GrdMyGrades(int CourseId, List<GrdMyGradeRow> Items);
public sealed record GrdMyGradeRow(int ItemId, string Name, decimal MinScore, decimal MaxScore, decimal? Score);

public sealed record GrdGradebook(int CourseId, List<GrdGradebookItem> Items, List<GrdGradebookRow> Learners, string? NextCursor);
public sealed record GrdGradebookItem(int ItemId, int? AssignmentId, string Name, decimal MinScore, decimal MaxScore, bool Hidden, bool Locked);
public sealed record GrdGradebookRow(int LearnerId, List<GrdGradebookCell> Grades);
public sealed record GrdGradebookCell(int ItemId, decimal? Score, int? Attempt, bool Hidden, bool Locked, bool Overridden);

/// <summary>
/// Puan formu. Puan sınırları (GRD-BR-02) yalnızca sunucuda denetlenir; iş kuralı tek yerde kalır ve
/// sınır dışı değer 422 ile forma döner. Boş puan "notlandırılmadı" anlamına gelir.
/// </summary>
public sealed class GradeEditModel
{
    public decimal? Score { get; set; }
    public string? Feedback { get; set; }

    public static GradeEditModel From(GrdGradeView view) => new()
    {
        Score = view.Assessment?.Score,
        Feedback = view.Assessment?.Feedback,
    };

    public GradeWriteRequest ToRequest(int version) =>
        new(Score, string.IsNullOrWhiteSpace(Feedback) ? null : Feedback, version);
}

public static class GrdDisplay
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>Boş puan "—" olarak gösterilir; sıfır "0" olarak gösterilir.</summary>
    public static string Score(decimal? value) => value?.ToString("0.#####", Turkish) ?? "—";

    public static string Range(decimal min, decimal max) => $"{Score(min)}–{Score(max)}";

    public static string Date(DateTimeOffset value) => value.ToUniversalTime().ToString("dd.MM.yyyy HH:mm 'UTC'", Turkish);
}
