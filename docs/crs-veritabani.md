# CRS veritabanı ve migration

CRS, PostgreSQL üzerinde `crs_categories`, `crs_courses` ve `crs_enrolments`
tablolarını kullanır. `InitialCrs` migration'ı bu tabloları, yabancı anahtarları
ve benzersiz indeksleri oluşturur. Uygulama başlangıcında otomatik migration
çalıştırılmaz; veritabanı güncellemesi açıkça yapılır.

## Yerel kurulum (PowerShell)

Depo kökünde, .NET 10 SDK ve çalışan Docker Desktop ile:

```powershell
dotnet tool restore
docker compose -f docker/docker-compose.yml up -d modern-db
$env:CRS_CONNECTION_STRING = 'Host=localhost;Port=55432;Database=moodle_modern;Username=moodle;Password=local_dev_only'
dotnet ef database update --project src/Moodle.Infrastructure --startup-project src/Moodle.Infrastructure --context CrsDbContext
dotnet ef migrations list --project src/Moodle.Infrastructure --startup-project src/Moodle.Infrastructure --context CrsDbContext
```

`MODERN_DB_PASSWORD` ile Docker parolasını değiştirdiysen bağlantı dizesinde de
aynı parolayı kullan. PostgreSQL portu yalnızca `127.0.0.1:55432` üzerinden açılır.
PowerShell'de ayarlanan değişkenler yalnızca ilgili terminal ve alt süreçleri için geçerlidir.

Bağlantı önceliği hem çalışma zamanında hem EF araçlarında aynıdır:

1. `CRS_CONNECTION_STRING`
2. `ConnectionStrings__Default` (Docker Compose uygulamaya bunu verir)
3. Önceki geliştirme varsayılanı: localhost:5432, veritabanı/kullanıcı/parola `moodle`.

## Yeni migration oluşturma

```powershell
dotnet ef migrations add DegisiklikAdi --project src/Moodle.Infrastructure --startup-project src/Moodle.Infrastructure --context CrsDbContext --output-dir Courses/Migrations
```

Üretilen migration'ı gözden geçirdikten sonra `database update` çalıştır.
Design-time factory sayesinde web uygulamasını başlatmak gerekmez.

## Gerçek PostgreSQL testleri

```powershell
dotnet test Moodle.slnx
```

`CrsPostgresTests`, Testcontainers ile ayrı ve geçici bir PostgreSQL 17 açar ve
`EnsureCreated` yerine gerçek migration'ları uygular. Testler yerel geliştirme
veritabanını kullanmaz. Şunları doğrular:

- Migration'ın uygulanması ve bekleyen migration kalmaması.
- Kategori ve ders oluşturma, okuma, güncelleme ve silme.
- Kayıt oluşturma, aktiflik sorgusu, askıya alma, rol güncelleme ve kayıt silme.
- Dolu kategorinin silinememesi ve ders silinince kayıtların silinmesi.
- Ders kısa adı, kimlik numarası ve ders/kullanıcı kaydı benzersizliği.
- Olmayan üst kategoriye ilişkin yabancı anahtar kısıtı.

Her test kendi veritabanı işlemini geri alır; geçici konteyner test sonunda
temizlenir. Bu kontroller API, Blazor veya AUTH entegrasyonunu kapsamaz.
`EnrolAsync` hâlâ YZ taslağıdır; test edilmiş olması elle geliştirilmiş olduğu
anlamına gelmez.

## Doğrulama kaydı — 17 Kasım 2026

- `20261117094833_InitialCrs`, yerel `moodle_modern` veritabanına uygulandı.
- PostgreSQL 17.12 üzerinde üç CRS tablosu ve migration geçmişi SQL ile görüldü.
- İkinci `database update`: veritabanı güncel, yeni migration uygulanmadı.
- `migrations has-pending-model-changes`: model farkı yok.
- `dotnet test Moodle.slnx --no-restore`: 11 birim + 12 entegrasyon testi
  başarılı; başarısız veya atlanan test yok. Bunların 5'i yeni CRS PostgreSQL
  testidir.
