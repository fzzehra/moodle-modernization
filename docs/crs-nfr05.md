# CRS-NFR-05 — değişikliksiz güncellemede SQL yazımı

İnceleme ve ölçüm tarihi: **19 Kasım 2026**.
Branch: `sinem/crs-nfr05-noop`.
Başlangıç: uzak `main` fetch edilip eşitliği doğrulanan `57ae3467132d91b30abdccd9e0193d6ec48d229b`;
çalışma ağacı branch oluşturulmadan önce temizdi.

## Gereksinim ve kapsam

- WBS-1 §4.2.7: değişiklik yoksa kayıt güncellemesi yapılmadan işlem sonlandırılır.
- WBS-1 CRS-BR-08: mevcut ders kaydı yalnız ilgili alanlar değiştiğinde güncellenir.
- WBS-1 CRS-NFR-05: kayıt bilgileri değişmediğinde gereksiz veritabanı güncellemesi yapılmamalıdır.
- WBS-2 §3.2.5 ve §3.2.6: değişmeyen kayıt bilgilerinde gereksiz güncellemelerin önlenmesi.

Asıl hedef `CrsService.UpdateEnrolmentAsync` durum/rol güncellemesidir. Mevcut
diğer güncelleme yolları `SaveCategoryAsync` ve `SaveCourseAsync` de SQL düzeyinde
regresyona alınmıştır. Mevcut kayıt tarihlerini güncelleme işlevi bu teslimde
eklenmemiştir; API/servis sözleşmeleri korunmuştur.

## Bulgular

**Mevcut üretim kodunda gereksiz SQL yazımı tespit edilmedi; üretim kodu değiştirilmedi.**

Servisler, güncellenecek satırı EF Core change tracker üzerinden bulup alanlarını
atıyor ve `SaveChangesAsync` çağırıyor. Denenen aynı değerlerde değişiklik
takibi yazılacak alan bulmadığı için SQL INSERT/UPDATE/DELETE gönderilmiyor.
`SaveChangesAsync` çağrısı bulunması, tek başına SQL UPDATE çalıştığı anlamına
gelmiyor. Gerçek değişiklikte UPDATE çalışıyor ve sonuç ayrı DbContext ile
veritabanından doğrulanıyor.

Bu kanıt, güncel EF Core/Npgsql modeli ve mevcut servis yolları içindir.
İleride doğrudan SQL veya bütün entity'yi zorla Modified yapan bir güncelleme
yolu eklenirse aynı regresyon testleri korunmalıdır.

## SQL ölçümü

Yeni `CrsNfr05NoopPostgresTests` gerçek PostgreSQL 17 Testcontainers bağlamında
çalışır; mevcut `CrsPostgresFixture` değiştirilmeden kullanılmıştır.

- Test verisi hazırlama ve migration işlemleri ölçülen bağlamın dışındadır.
- Ölçüm doğrudan `CrsService` çağrısını kapsar; Blazor'un istek atlamasına dayanmaz.
- Önceden yüklenmiş entity ve taze DbContext yolları ayrı sınanır.
- DbCommandInterceptor, Reader/Scalar/NonQuery senkron ve asenkron yollarını
  gözlemler. Npgsql'nin RETURNING içeren yazımları Reader yolundan da sayılır.
- INSERT/UPDATE/DELETE SQL ifadeleri sayılır; SELECT ve transaction komutları
  yazım kabul edilmez. Entegrasyon testlerinde hiçbir komut bastırılmaz.
- Sayaç kontrolünde gerçek kategori oluşturma/güncelleme/silme yapılarak
  toplam **1 INSERT, 1 UPDATE, 1 DELETE** yakalandığı doğrulanır.
- Kaydedilen değerler ölçüm sonrası ayrı DbContext ile okunur.

| Senaryo | INSERT | UPDATE | DELETE |
| --- | --- | --- | --- |
| Aynı kategori değerleri; taze/önceden yüklenmiş entity | 0 | 0 | 0 |
| Aynı ders değerleri; taze/önceden yüklenmiş entity | 0 | 0 | 0 |
| Aynı kayıt durumu ve rolü; taze/önceden yüklenmiş entity | 0 | 0 | 0 |
| Gerçek değişiklik: kategori adı veya üst kategori | 0 | 1 | 0 |
| Gerçek değişiklik: ders adı, görünürlük, bitiş tarihi veya kimlik numarasını temizleme | 0 | 1 | 0 |
| Gerçek değişiklik: kayıt durumu, rol veya ikisi birlikte | 0 | 1 | 0 |
| Kaydedilen aynı değişikliği yeni bağlamdan tekrar gönderme | 0 | 0 | 0 |
| Aynı anı gösteren farklı saat dilimi offset'i ve null'a normalleşen boş kimlik numarası | 0 | 0 | 0 |

## Testler ve kanıtlar

- `tests/Moodle.UnitTests/CrsNfr05NoopTests.cs`: **8 başarılı** birim senaryosu.
  Aynı değerlerin tracker'da Unchanged kalması, gerçek değişiklikte yalnız
  ilgili alanların işaretlenmesi ve aynı geçersiz değerin doğrulamayı
  atlamaması sınanır. Bağlantı açılması hata sayılır. Pozitif birim senaryoları
  yalnız change tracker durumunu ölçmek için kaydetmeyi bastırır; gerçek SQL
  kanıtı PostgreSQL testlerindedir.
- `tests/Moodle.IntegrationTests/CrsNfr05NoopPostgresTests.cs`: **17 başarılı**
  PostgreSQL senaryosu. Test çıktıları her ölçümün DML sayılarını içerir.
- Sonuç dosyaları: Git tarafından dışlanan `TestResults/crs-nfr05/` altında
  `crs-nfr05-unit.trx`, `crs-nfr05-postgres.trx`.
- Çözüm regresyonunun sonuç dosyaları `TestResults/crs-nfr05/regression/`
  altındadır; nihai sonuç aşağıdaki doğrulama kaydında belirtilir.

### Tam çözüm regresyonu

`dotnet test Moodle.slnx --no-build --no-restore` sonucu:

| Proje | Başarılı | Başarısız | Atlanan | Yeni NFR-05 | Mevcut CRS |
| --- | --- | --- | --- | --- | --- |
| Moodle.UnitTests | 256 | 0 | 0 | 8 | 48 |
| Moodle.IntegrationTests | 286 | 0 | 0 | 17 | 99 |
| Toplam | **542** | **0** | **0** | **25** | **147** |

TRX kayıtları ayrıca incelendi: mevcut 147 CRS testinin tamamı geçti.
Çözümdeki 517 mevcut test ve 25 yeni NFR-05 testi başarılıdır. Son koşularda
çalıştırılamayan veya atlanan .NET testi yoktur. NFR-05 SQL ölçümü geçici gerçek
PostgreSQL'de yapılmıştır; mevcut geliştirme veritabanı kullanılmamıştır.

Kullanılan komutlar:

```powershell
dotnet restore Moodle.slnx
dotnet build Moodle.slnx --no-restore
dotnet test tests/Moodle.UnitTests/Moodle.UnitTests.csproj --no-build --no-restore --filter FullyQualifiedName~CrsNfr05NoopTests
dotnet test tests/Moodle.IntegrationTests/Moodle.IntegrationTests.csproj --no-build --no-restore --filter FullyQualifiedName~CrsNfr05NoopPostgresTests
dotnet test Moodle.slnx --no-build --no-restore --logger trx --results-directory TestResults/crs-nfr05/regression
```

Docker başlangıçta kapalıydı; kurulu Docker Desktop başlatılarak gerçek
PostgreSQL koşusu yapıldı. İlk `--no-restore` test denemesi, güncel main'in
Logging paketine ait eski yerel assets kaydı nedeniyle derlemede durdu;
paketlerin geri yüklenmesi sonrası kaynak dosyası değiştirmeden giderildi.
Derleme **0 hata, 5 NU1900 uyarısı** ile başarılı oldu: NuGet güvenlik verisi
erişimi geri yüklemede doğrulanamadı. Bu uyarı bastırılmadı.

## Kapsam sınırı

Yalnızca yeni CRS testleri ve bu ölçüm notu eklendi. Mevcut kaynak/test,
migration, seed, Program.cs ve DependencyInjection.cs dosyaları değiştirilmedi.
AUTH/FILE/ASG/GRD geliştirmesi yapılmadı. Olay, önbellek veya arka plan silme
başlatılmadı. `503 auth_not_ready` güvenlik davranışı korunmuştur.
Commit, push ve PR oluşturulmadı. Bu çalışma manuel geliştirme kanıtı olarak
sınıflandırılmamalıdır; yeni testler ve bu not YZ desteğiyle hazırlanmıştır.
