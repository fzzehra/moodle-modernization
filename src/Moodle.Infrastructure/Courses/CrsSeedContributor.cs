using Microsoft.EntityFrameworkCore;
using Moodle.Application.Seeding;
using Moodle.Domain.Courses;

namespace Moodle.Infrastructure.Courses;

/// <summary>
/// Örnek veriyi (ortam/ornek-veri.feature) CRS tablolarına yükler: kategoriler, dersler ve ders kayıtları.
/// Tekrar çalıştırılabilir: var olan kayıt atlanır. Kullanıcı kimliği <see cref="SeedDataset.UserId"/> ile gelir.
/// </summary>
public sealed class CrsSeedContributor(CrsDbContext db) : ISeedContributor
{
    public int Order => 20;
    public string Name => "CRS: kategori, ders, kayıt";

    public async Task SeedAsync(SeedDataset data, CancellationToken ct = default)
    {
        var categoryIds = await SeedCategoriesAsync(data, ct);
        var courseIds = await SeedCoursesAsync(data, categoryIds, ct);
        await SeedEnrolmentsAsync(data, courseIds, ct);
    }

    private async Task<Dictionary<string, int>> SeedCategoriesAsync(SeedDataset data, CancellationToken ct)
    {
        var ids = new Dictionary<string, int>(); // idnumber -> veritabanı kimliği
        // Üst kategori önce yazılsın diye ebeveyni olmayanlardan başlanır.
        foreach (var c in data.Categories.OrderBy(c => c.ParentIdNumber is null ? 0 : 1))
        {
            int? parentId = c.ParentIdNumber is null ? null : ids[c.ParentIdNumber];
            var existing = await db.Categories.FirstOrDefaultAsync(x => x.Name == c.Name && x.ParentId == parentId, ct);
            if (existing is null)
            {
                existing = new CourseCategory { Name = c.Name, ParentId = parentId };
                db.Categories.Add(existing);
                await db.SaveChangesAsync(ct);
            }
            ids[c.IdNumber] = existing.Id;
        }
        return ids;
    }

    private async Task<Dictionary<string, int>> SeedCoursesAsync(
        SeedDataset data, Dictionary<string, int> categoryIds, CancellationToken ct)
    {
        var ids = new Dictionary<string, int>(); // kısa ad -> veritabanı kimliği
        foreach (var c in data.Courses)
        {
            var existing = await db.Courses.FirstOrDefaultAsync(x => x.ShortName == c.ShortName, ct);
            if (existing is null)
            {
                existing = new Course
                {
                    CategoryId = categoryIds[c.CategoryIdNumber],
                    FullName = c.FullName,
                    ShortName = c.ShortName,
                    IdNumber = c.ShortName,
                };
                db.Courses.Add(existing);
                await db.SaveChangesAsync(ct);
            }
            ids[c.ShortName] = existing.Id;
        }
        return ids;
    }

    private async Task SeedEnrolmentsAsync(SeedDataset data, Dictionary<string, int> courseIds, CancellationToken ct)
    {
        foreach (var e in data.Enrolments)
        {
            var courseId = courseIds[e.CourseShortName];
            var userId = data.UserId(e.Username);
            if (await db.Enrolments.AnyAsync(x => x.CourseId == courseId && x.UserId == userId, ct)) continue;
            db.Enrolments.Add(new CourseEnrolment { CourseId = courseId, UserId = userId, Role = MapRole(e.Role) });
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Moodle rol kısa adı → CRS rol adı.</summary>
    private static string MapRole(string moodleRole) => moodleRole switch
    {
        "editingteacher" => "EditingTeacher",
        "teacher" => "Teacher",
        "student" => "Student",
        _ => throw new ArgumentException($"Bilinmeyen rol: {moodleRole}", nameof(moodleRole)),
    };
}
