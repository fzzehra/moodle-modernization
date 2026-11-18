namespace Moodle.Application.Grading;

/// <summary>GRD'nin saf iş kuralları. Veritabanına ve saate bağlı değildir; zaman parametreyle verilir.</summary>
public static class GradeValidation
{
    /// <summary>Veritabanındaki <c>numeric(10,5)</c> ölçeğiyle aynı; fazlası sessizce yuvarlanmasın diye reddedilir.</summary>
    public const int MaxDecimalPlaces = 5;

    /// <summary>
    /// GRD-BR-02: puan kaleminin alt ve üst sınırı uygulanır. Sınırlar dahildir.
    /// Boş puan (null, "notlandırılmadı") geçerlidir ve sıfırdan ayrıdır.
    /// </summary>
    /// <exception cref="ArgumentException">Sınır dışı veya fazla ondalıklı puan (API'de 422).</exception>
    public static void ValidateScore(decimal? score, decimal minScore, decimal maxScore)
    {
        if (minScore > maxScore)
            throw new ArgumentException("Not kaleminin alt sınırı üst sınırından büyük olamaz.");

        if (score is not { } value)
            return;

        if (value < minScore || value > maxScore)
            throw new ArgumentException(
                $"Puan {minScore:0.#####} ile {maxScore:0.#####} arasında olmalıdır.");

        if (decimal.Round(value, MaxDecimalPlaces) != value)
            throw new ArgumentException($"Puan en fazla {MaxDecimalPlaces} ondalık basamak içerebilir.");
    }

    /// <summary>Deneme numarası Moodle'daki gibi 0'dan başlar.</summary>
    public static void ValidateAttempt(int attempt)
    {
        if (attempt < 0)
            throw new ArgumentException("Deneme numarası negatif olamaz.");
    }

    public static void ValidateVersion(int version)
    {
        if (version < 0)
            throw new ArgumentException("Sürüm negatif olamaz.");
    }

    /// <summary>
    /// Kalem veya not kilitli mi? <c>Locked</c> süresiz kilittir; <c>LockAt</c> zamanı geldiğinde
    /// (sınır dahil) otomatik kilitlenir. Saat sunucudan alınır (WBS-2 §3.5.1).
    /// </summary>
    public static bool IsLocked(bool locked, DateTimeOffset? lockAt, DateTimeOffset now) =>
        locked || (lockAt.HasValue && lockAt.Value <= now);
}
