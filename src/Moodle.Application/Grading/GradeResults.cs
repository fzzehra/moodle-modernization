using Moodle.Domain.Grading;

namespace Moodle.Application.Grading;

/// <summary>
/// GET .../grade yanıtı (WBS-2 §3.5.2): <c>{ assessment, minScore, maxScore, version }</c>.
/// Puan kaydı yoksa <c>Assessment</c> null ve <c>Version</c> 0'dır.
/// </summary>
public sealed record AssignmentGradeView(
    AssignmentGrade? Assessment,
    decimal MinScore,
    decimal MaxScore,
    int Version);

/// <summary>
/// PUT .../grade yanıtı: kaydedilen değerlendirme ve defter aktarım durumu.
/// <c>GradebookUpdated</c> false ise puan daha eski bir denemeye verilmiştir ve defter
/// son denemenin değerini korur (GRD-BR-01).
/// </summary>
public sealed record AssignmentGradeSaveResult(
    AssignmentGrade Assessment,
    bool GradebookUpdated,
    decimal MinScore,
    decimal MaxScore);

/// <summary>
/// GET /courses/{courseId}/my-grades (GRD-FR-02, GRD-NFR-01). Yalnızca oturum sahibinin görünür notları;
/// öğrenci kimliği bile yanıtta yer almaz. Gizli kalem satırı yoktur; gizli not <c>Score=null</c> döner.
/// </summary>
public sealed record MyGradesView(int CourseId, IReadOnlyList<MyGradeRow> Items);

public sealed record MyGradeRow(int ItemId, string Name, decimal MinScore, decimal MaxScore, decimal? Score);

/// <summary>GET /courses/{courseId}/gradebook (GRD-FR-03): izinli öğrenci ve kalem sayfası.</summary>
public sealed record GradebookView(
    int CourseId,
    IReadOnlyList<GradebookItem> Items,
    IReadOnlyList<GradebookRow> Learners,
    string? NextCursor);

/// <summary>Not defteri sütunu. Öğretmen gizli kalemleri de görür; durum bayraklarla bildirilir.</summary>
public sealed record GradebookItem(
    int ItemId, string Name, decimal MinScore, decimal MaxScore, bool Hidden, bool Locked);

/// <summary>Not defteri satırı: hücreler <see cref="GradebookView.Items"/> ile aynı sıradadır.</summary>
public sealed record GradebookRow(int LearnerId, IReadOnlyList<GradebookCell> Grades);

/// <summary>Hücre: nihai puan (yoksa null) ve koruma durumu (kalem veya not düzeyinde).</summary>
public sealed record GradebookCell(int ItemId, decimal? Score, bool Hidden, bool Locked, bool Overridden);
