namespace Moodle.Domain.Grading;

// WBS-2 §3.5.1. Puanlar sabit hassasiyetli ondalıktır; boş puan (null) sıfırdan ayrıdır.
// Ders, öğrenci, değerlendirici ve ödev kimlikleri başka modüllere aittir: bu tablolar
// crs_/asg_ tablolarına yabancı anahtar kurmaz, ilişkiler arayüzlerle doğrulanır.

/// <summary>Not kategorisinin alt kalemleri toplama yöntemi (Moodle <c>grade_categories.aggregation</c>).</summary>
public enum GradeAggregation
{
    Natural,
    Mean,
    WeightedMean,
    Median,
    Lowest,
    Highest,
}

/// <summary>Not kalemi türü (Moodle <c>grade_items.itemtype</c>: course, category, manual, mod).</summary>
public enum GradeItemType
{
    Course,
    Category,
    Manual,
    Assignment,
}

/// <summary>Ödev değerlendirmesi (Moodle <c>assign_grades</c>). Ödev, öğrenci ve deneme üçlüsü benzersizdir.</summary>
public sealed class AssignmentGrade
{
    public int Id { get; set; }
    public int AssignmentId { get; set; }
    public int LearnerId { get; set; }
    public int Attempt { get; set; }

    /// <summary>Değerlendirici her zaman oturumdan alınır, istekten alınmaz.</summary>
    public int GraderId { get; set; }

    /// <summary>Boş: henüz notlandırılmadı. Sıfır geçerli puandır.</summary>
    public decimal? Score { get; set; }

    public string? Feedback { get; set; }
    public DateTimeOffset GradedAt { get; set; }

    /// <summary>İyimser eşzamanlılık sürümü; ilk kayıt öncesi 0.</summary>
    public int Version { get; set; }
}

/// <summary>Not kategorisi (Moodle <c>grade_categories</c>). Ebeveyn aynı derstedir.</summary>
public sealed class GradeCategory
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public int? ParentId { get; set; }
    public string Name { get; set; } = "";
    public GradeAggregation Aggregation { get; set; } = GradeAggregation.Natural;
}

/// <summary>Not kalemi (Moodle <c>grade_items</c>). Alt sınır üst sınırı aşamaz; bir ödev için tek ana kalem.</summary>
public sealed class GradeItem
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public int? CategoryId { get; set; }

    /// <summary>Yalnızca <see cref="GradeItemType.Assignment"/> kalemlerinde dolu.</summary>
    public int? AssignmentId { get; set; }

    public GradeItemType Type { get; set; }
    public string Name { get; set; } = "";
    public decimal MinScore { get; set; }
    public decimal MaxScore { get; set; } = 100m;
    public bool Hidden { get; set; }
    public DateTimeOffset? HiddenUntil { get; set; }
    public bool Locked { get; set; }
    public DateTimeOffset? LockAt { get; set; }
}

/// <summary>Öğrenci notu (Moodle <c>grade_grades</c>). Kalem ve öğrenci çifti benzersizdir.</summary>
public sealed class LearnerGrade
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public int LearnerId { get; set; }

    /// <summary>Kaynak etkinlikten gelen ham puan; boş ile sıfır ayrıdır.</summary>
    public decimal? RawScore { get; set; }

    /// <summary>Defterdeki nihai puan; boş ile sıfır ayrıdır.</summary>
    public decimal? FinalScore { get; set; }

    public bool Hidden { get; set; }
    public DateTimeOffset? HiddenUntil { get; set; }
    public bool Locked { get; set; }
    public DateTimeOffset? LockAt { get; set; }

    /// <summary>Nihai değer elle değiştirildi; ödev aktarımı bu değeri ezmez (GRD-BR-01).</summary>
    public bool Overridden { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; }
}
