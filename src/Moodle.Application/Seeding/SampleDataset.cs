namespace Moodle.Application.Seeding;

/// <summary><c>ortam/ornek-veri.feature</c> verisi. Kaynak dosya değişirse <c>SampleDatasetTests</c> farkı yakalar.</summary>
public static class SampleDataset
{
    public static SeedDataset Create()
    {
        var students = Enumerable.Range(1, 10)
            .Select(i => new SeedUser($"ogrenci{i}", "Öğrenci", i.ToString(), $"ogrenci{i}@example.com", null))
            .ToList();

        var users = new List<SeedUser>
        {
            new("yonetici1", "Yönetici", "Bir", "yonetici1@example.com", "manager"),
            new("ogretmen1", "Ayşe", "Yılmaz", "ogretmen1@example.com", null),
            new("ogretmen2", "Mehmet", "Demir", "ogretmen2@example.com", null),
            new("asistan1", "Elif", "Kaya", "asistan1@example.com", null),
            new("danisman1", "Can", "Öztürk", "danisman1@example.com", null),
        };
        users.AddRange(students);

        var enrolments = new List<SeedEnrolment>
        {
            new("ogretmen1", "SENG101", "editingteacher"),
            new("ogretmen1", "SENG201", "editingteacher"),
            new("ogretmen2", "CENG301", "editingteacher"),
            new("asistan1", "SENG101", "teacher"),
        };
        enrolments.AddRange(Enumerable.Range(1, 10).Select(i => new SeedEnrolment($"ogrenci{i}", "SENG101", "student")));
        enrolments.AddRange(Enumerable.Range(1, 5).Select(i => new SeedEnrolment($"ogrenci{i}", "SENG201", "student")));
        enrolments.AddRange(Enumerable.Range(1, 5).Select(i => new SeedEnrolment($"ogrenci{i}", "CENG301", "student")));

        return new SeedDataset(
            Categories:
            [
                new("MUH", "Mühendislik Fakültesi", null),
                new("SENG", "Yazılım Mühendisliği", "MUH"),
                new("CENG", "Bilgisayar Mühendisliği", "MUH"),
            ],
            Users: users,
            Courses:
            [
                new("SENG101", "Programlamaya Giriş", "SENG"),
                new("SENG201", "Veri Yapıları", "SENG"),
                new("CENG301", "Veritabanı Sistemleri", "CENG"),
            ],
            Enrolments: enrolments,
            Assignments:
            [
                new("seng101a1", "SENG101 Ödev 1", "Değişkenler ve döngüler", "SENG101", 1, false),
                new("seng101a2", "SENG101 Ödev 2", "Fonksiyonlar", "SENG101", 2, true),
                new("seng201a1", "SENG201 Ödev 1", "Bağlı listeler", "SENG201", 1, false),
                new("ceng301a1", "CENG301 Ödev 1", "ER diyagramı", "CENG301", 1, false),
            ],
            Resources: [new("seng101r1", "SENG101 Ders Notları", "SENG101")],
            Submissions:
            [
                new("SENG101 Ödev 1", "ogrenci1", "Öğrenci 1 teslim metni"),
                new("SENG101 Ödev 1", "ogrenci2", "Öğrenci 2 teslim metni"),
                new("SENG101 Ödev 1", "ogrenci3", "Öğrenci 3 teslim metni"),
                new("SENG101 Ödev 1", "ogrenci4", "Öğrenci 4 teslim metni"),
                new("SENG101 Ödev 1", "ogrenci5", "Öğrenci 5 teslim metni"),
                new("SENG101 Ödev 1", "ogrenci6", "Öğrenci 6 teslim metni"),
                new("SENG101 Ödev 2", "ogrenci6", "Yarım kalmış taslak"),
                new("SENG201 Ödev 1", "ogrenci1", "Bağlı liste çözümü"),
                new("SENG201 Ödev 1", "ogrenci2", "Bağlı liste çözümü"),
                new("CENG301 Ödev 1", "ogrenci1", "ER diyagramı açıklaması"),
            ],
            Grades:
            [
                new("SENG101 Ödev 1", "ogrenci1", 85),
                new("SENG101 Ödev 1", "ogrenci2", 72),
                new("SENG101 Ödev 1", "ogrenci3", 90),
                new("SENG101 Ödev 1", "ogrenci4", 55),
                new("SENG201 Ödev 1", "ogrenci1", 78),
            ]);
    }
}
