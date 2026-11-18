# Varlık yapılandırmaları

Her modül kendi klasörünü açar: `Auth/`, `Courses/`, `Assignments/`, `Grading/`, ...
İçine `IEntityTypeConfiguration<T>` sınıfları konur; `AppDbContext` hepsini otomatik bulur.

Şema değişikliği yalnızca EF Core geçişiyle yapılır (WBS-2 §2.5):

```bash
dotnet ef migrations add <Ad> -p src/Moodle.Infrastructure -s src/Moodle.Api -o Persistence/Migrations
```

Örnek veri yüklemek için `ISeedContributor` uygulayın (bkz. `Moodle.Application/Seeding`).
