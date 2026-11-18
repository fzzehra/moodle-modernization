# CRS olayları ve genişletme noktaları — WBS-3 ikinci sürüm

Tarih: 19 Kasım 2026. Branch: `sinem/crs-events-extensions`.
Başlangıç: temiz `sinem/crs-nfr05-noop`, HEAD
`599d8a52fd86b9d0d03241acd7166b3a70d00784`. Bu branch NFR-05 commit'ine bağımlıdır;
main'den bağımsız bir değişiklik kümesi değildir. Commit/push/PR yapılmadı.

## Gereksinimler ve kapsam kararı

Okunan kaynaklar: [WBS-1 §4.2.9](WBS-1-analiz.md) ve
[WBS-2 §3.2.1, §3.2.3, §3.2.5, §3.2.6](WBS-2-tasarim.md).

| Gereksinim | Belgelerdeki tanım / karşılık | Bu çalışmanın karşılığı |
|---|---|---|
| CRS-FR-10 | Ders oluşturma, güncelleme, silme ve kullanıcı kayıt değişikliklerinde ilgili olayların tetiklenmesi; CRS-BR-13 | Altı yaşam döngüsü olayı, CRS verisiyle aynı transaction içinde kalıcı kayıt |
| CRS-NFR-07 | Ders ve kullanıcı kayıt değişikliklerinde ilgili olayların oluşturulması; izlenebilirlik | Kimlik, tür, zaman, sürüm ve değişen alan adlarıyla kalıcı olay geçmişi |
| CRS-NFR-08 | Moodle eklenti/hook mekanizmalarından bağımsız modüler servisler ve tanımlı genişletme noktaları | `ICrsEventExtension`, deterministik pipeline ve açıkça çağrılan kalıcı teslim mekanizması |

WBS-2 olay veya denetim kaydına izin verir; dış mesaj kuyruğu, merkezi event bus,
otomatik worker, olay tüketen diğer modüller veya pre-save hook şart koşmaz.
Bu nedenle yalnızca CRS'ye ait yerel outbox ve post-commit genişletmeler uygulandı.
Kategori olayları, önbellek, silme engeli, arka planda ders silme ve AUTH kapsam dışıdır.

## Dosyalar

Tümü yeni dosyadır; mevcut üretim, test, migration, seed ve model snapshot dosyaları değiştirilmedi.

- `src/Moodle.Application/Courses/CrsChangeEvent.cs`: sürümlü bildirim ve `ICrsEventExtension`.
- `src/Moodle.Application/Courses/CrsEventExtensionPipeline.cs`: tüketici kimliği doğrulama ve sıralama.
- `src/Moodle.Infrastructure/Courses/CrsEventDispatcher.cs`: açık çağrıyla, sınırlı sayıda olayın teslimi.
- `src/Moodle.Infrastructure/Courses/Migrations/20261119000100_CrsEvents.cs`: CRS tabloları ve PostgreSQL trigger fonksiyonu.
- `src/Moodle.Infrastructure/Courses/Migrations/20261119000100_CrsEvents.Designer.cs`: mevcut EF modelinin değişmeyen migration hedef modeli.
- `tests/Moodle.UnitTests/CrsEventExtensionTests.cs`.
- `tests/Moodle.IntegrationTests/CrsEventsPostgresTests.cs`.
- `docs/crs-events-extensions.md`: bu belge.

## Olay üretimi ve transaction tutarlılığı

Yeni migration `crs_events`, `crs_event_receipts` ve yalnızca CRS ders/kayıt
tablolarında çalışan `crs_record_change()` AFTER ROW trigger'larını oluşturur.
Bu yaklaşım PostgreSQL'e özgüdür; projede zaten seçilmiş PostgreSQL 17 dışında
bir provider veya yeni paket eklenmez.

| Servis işlemi / veri değişikliği | Olay |
|---|---|
| `SaveCourseAsync`: yeni ders | `course.created` |
| `SaveCourseAsync`: değişen mevcut ders | `course.updated` |
| `DeleteCourseAsync` | `course.deleted` |
| `EnrolAsync` | `enrolment.created` |
| `UpdateEnrolmentAsync`: durum/rol değişimi | `enrolment.updated` |
| `UnenrolAsync` veya ders silme cascade'i | `enrolment.deleted` |

Olay bir servis çağrısının girişine değil, gerçekleşen veri değişikliğine bağlıdır.
Mevcut CRS seed'i veya doğrudan CRS tablo DML'i de olay üretir; seed dosyası
değiştirilmez. Eski veriler için migration sırasında geçmiş olay uydurulmaz.
Silme geçmişi korunabilsin diye olaydan canlı ders/kayıt satırına FK yoktur.
Receipt, olay kimliği ve tüketici kimliğinden oluşan birleşik birincil anahtara sahiptir.

- Trigger ve iş verisi aynı transaction içindedir. Olay INSERT'i başarısızsa iş yazımı da başarısızdır.
- Dış transaction rollback'i veya savepoint'e geri dönüş olay kayıtlarını da geri alır.
- Başka bağlantılar commit edilmemiş olayı göremez; genişletmeler commit öncesi çalışmaz.
- EF Core no-op güncellemede DML üretmez; NFR-05 üretim kodu değişmedi.
- Açık SQL UPDATE aynı satır değerlerini tekrar yazsa bile `NEW IS NOT DISTINCT FROM OLD` olay INSERT'ini önler.
- Kimlik sequence'i rollback sonrasında boşluk içerebilir. Olay ID sırası küresel commit sırası garantisi değildir.
- `OccurredAt`, UTC olarak okunabilen olay oluşum zamanıdır; commit zamanı veya kullanıcı işlem zamanı olarak yorumlanmaz.
- Cascade sırasında ders ve kayıt silme olaylarının kendi aralarındaki sırasına tüketici bağımlı olmamalıdır.

Şema nesneleri EF varlığı olarak map edilmez; dispatcher EF Core'un parametreli
`SqlQuery`/`ExecuteSqlInterpolatedAsync` desteğiyle erişir. EF entity modeli değişmediğinden
mevcut snapshot korunur; yeni migration'ın designer hedef modeli öncekiyle aynıdır.
Trigger/outbox şema değişiklikleri gelecekte de açık CRS SQL migration'larıyla yönetilmelidir.
`EnsureCreated` bu SQL nesnelerini kurmaz; gerçek kurulumda migration uygulanmalıdır.
Migration Up/Down gerçek PostgreSQL testinde sınanır; Down olay/receipt geçmişini siler.
Geliştiricinin mevcut veritabanına migration uygulanmadı; testler geçici konteyner kullandı.

## Veri sınırı ve güvenlik

Olay alanları: `Id`, `Kind`, `CourseId`, `EnrolmentId`, `UserId`, `OccurredAt`,
`Version` (1), `ChangedFields`. İç kullanıcı kimliği yalnızca kayıt olayında bulunur;
ad, e-posta, parola, token, rol değeri, ders başlığı veya başka alan değeri saklanmaz.
`ChangedFields` yalnızca değişen sütun adlarını içerir. Olay, AUTH aktörünü belirleyen
bir güvenlik denetim kaydı değildir. İç kimliklere erişim yine veritabanı erişim yetkisine tabidir.

API/Blazor sözleşmeleri ve `503 auth_not_ready` davranışı aynıdır; yeni erişilebilir
endpoint veya kimlik doğrulamayı atlatan yol oluşturulmadı.
`ICourseDirectory`, `IEnrolmentQuery`, `IAssignmentLearnerQuery` değiştirilmedi.

## Genişletme sözleşmesi ve hata davranışı

`ICrsEventExtension` yalnızca Application'daki olay DTO'sunu alır; başka bir modülün
somut sınıfına, Moodle hook'una veya plugin paketine bağlı değildir.

1. `Id`, dağıtımlar arasında sabit, benzersiz, boş olmayan, en çok 100 karakterlik tüketici kimliğidir.
2. Pipeline `Order` artan, eşitlikte `Id` ordinal artan sırayı kullanır. Yapılandırılmış tüketicilerin sırası sabit tutulmalıdır.
3. `CrsEventDispatcher.DispatchAsync(limit, ct)` ayrı bir DbContext ile transaction dışında çağrılır.
   `limit` 1–1000 arasında olup teslim sayısını değil seçilecek olay sayısını sınırlar; varsayılan 100'dür.
4. Bildirilmemiş olaylar ID sırasıyla seçilir. Her olay için ayrı transaction açılır ve olay satırı `FOR UPDATE` kilitlenir.
5. Her tüketicinin kalıcı receipt'i yeniden kontrol edilir. Başarılı handler sonrasında receipt yazılır;
   olayın tüm tüketicileri tamamlanınca transaction commit edilir.
6. Hata/iptalde aynı olayın tüm yeni receipt'leri rollback olur; sonraki tüketiciler ve sonraki olaylar çalışmaz.
   Önceki olayların tamamlanmış teslimleri korunur. Hata çağırana iletilir, başarılı teslim diye yutulmaz.
7. Yeniden çağrı aynı `(extension.Id, event.Id)` anahtarını kullanır. Önceden commit edilmiş receipt tekrar teslimi önler.
   Eşzamanlı dispatcher'lar satır kilidi ve transaction sonrası receipt kontrolüyle seri çalışır.
8. Handler'a verilen alan adları dizisi her çağrıda kopyalanır; bir handler diğerinin bildirimini değiştiremez.

**Teslim garantisinin sınırı:** Handler dış sistemde etki oluşturup receipt commit'inden
önce hata/crash yaşanabilir. Bu nedenle handler idempotent olmalı ve kendi kalıcı veri
alanında `(extension.Id, event.Id)` anahtarını yan etkisiyle atomik olarak kaydetmelidir.
Testte idempotent örnek tüketicinin yeniden çağrıldığı hâlde tek yan etki oluşturduğu gösterilir;
bu, diğer modüllere üretim tüketicisi yazıldığı anlamına gelmez. Harici etkiler için tam bir kez
çalışma garantisi verilmez. Handler içinde aynı dispatcher'ı yeniden çağırmak/ders yazımını
senkron şekilde geri tetiklemek sözleşme kapsamında değildir.

Hatalı bir olay sonraki olayları bekletir; otomatik backoff, dead-letter, olay temizleme
ve arka plan polling eklenmedi. Yeni tüketici eski olayları da alır; receipt'leri var olan
tüketici tekrar almaz. Yapılandırılmayan tüketiciler için olaylar kaybolmaz, outbox'ta kalır.

## Başka modüllere bağımlı kalan işler

Olay üretimi migration uygulandığında otomatik çalışır. Teslim ise açıkça çağrılan CRS
bileşenidir: mevcut uygulamaya otomatik worker veya tüketici kaydı eklenmedi.
`Program.cs` ve ortak `DependencyInjection.cs` değiştirilmedi.
Gerçek tüketicilerin sahipleriyle olay sözleşmesinin benimsenmesi, tüketicilerin kendi
idempotency kayıtları ve çağırıcı/çalıştırma zamanlaması ayrıca kararlaştırılmalıdır.
Ortak kayıt/host değişikliği gerekirse kullanıcı onayı gerekir. AUTH/FILE/ASG/GRD
tüketicileri, tabloları veya testleri bu çalışmada geliştirilmedi.

## Test kanıtları

`CrsEventExtensionTests`: sıralama, boş/tekrarlanan/uzun kimliklerin reddi,
pipeline'ın kayıt listesinden bağımsızlığı, limit doğrulaması ve tüketicisiz
çağrıda bağlantı gerekmemesi. Birim testleri SQL tutarlılığının kanıtı olarak sunulmaz.

`CrsEventsPostgresTests`: gerçek servis çağrıları ve PostgreSQL 17 migration'larıyla
altı olay türü, değişen alanlar, no-op/normalizasyon, açık SQL no-op, görünürlük/rollback,
constraint ve outbox hatası, cascade silme, receipt kalıcılığı, sıralama, hata/iptal sonrası
yeniden deneme, idempotent tüketici örneği, eşzamanlı dispatcher, transaction reddi,
yeni tüketiciye geçmiş olay teslimi, limit, payload sınırı, dizi izolasyonu,
savepoint rollback ve migration Up/Down sınanır.

Tam çözüm koşusu tamamlandı (çıkış kodu 0). TRX kayıtları ayrıca incelendi:

| Kapsam | Başarılı | Başarısız | Atlanan / başarılı olmayan | Yeni olay testleri | Mevcut NFR-05 testleri |
|---|---:|---:|---:|---:|---:|
| Birim | 265 | 0 | 0 | 9 | 8 |
| PostgreSQL dahil entegrasyon | 303 | 0 | 0 | 17 | 17 |
| Toplam | **568** | **0** | **0** | **26** | **25** |

Önceki 542 testin tamamı ve 26 yeni test geçti. Yeni 17 entegrasyon testi,
gerçek PostgreSQL 17/Testcontainers üzerinde çalıştı; Docker erişim sorunu veya
çalıştırılamayan test yoktur. SQL no-op için NFR-05'in 25 testi de tam koşuda geçti.
Son derlemeden sonra yalnızca bu belgeye sonuçlar eklendi.

Sonuçlar (Git tarafından ignore edilen yerel kanıtlar):

- `TestResults/crs-events/events-unit.trx` (ilk hedefli koşu).
- `TestResults/crs-events/events-postgres.trx` (ilk 15 entegrasyon testi).
- `TestResults/crs-events/regression/SİNEM_SARIÇİÇEK_LAPTOP-AC1FKB38_2026-11-19_00_55_49_net10.0.trx` (265 birim).
- `TestResults/crs-events/regression/SİNEM_SARIÇİÇEK_LAPTOP-AC1FKB38_2026-11-19_00_55_52_net10.0.trx` (303 entegrasyon, yeni 17 testin tamamı dahil).

Komutlar:

```powershell
dotnet build Moodle.slnx --no-restore
dotnet test tests/Moodle.UnitTests/Moodle.UnitTests.csproj --no-build --no-restore --filter FullyQualifiedName~CrsEventExtensionTests
dotnet test tests/Moodle.IntegrationTests/Moodle.IntegrationTests.csproj --no-build --no-restore --filter FullyQualifiedName~CrsEventsPostgresTests
dotnet test Moodle.slnx --no-build --no-restore --logger trx --results-directory TestResults/crs-events/regression
```

İlk hedefli koşuda 9 birim ve 15 PostgreSQL testi geçti; ardından migration ve savepoint
testleri eklendi. Son derleme: 0 hata, 5 NU1900 (NuGet güvenlik verisine erişim) uyarısı.
Başlangıçtaki test analyzer uyarısı yeni testte düzeltildi; son derlemede tekrarlanmadı.

## YZ ve teslim kaydı

Bu çalışma YZ desteğiyle üretildi; elle geliştirilmiş olarak sınıflandırılamaz.
WBS kaynakları veya ortak izlenebilirlik belgesi değiştirilmedi; bu belge yalnızca
CRS değişikliklerinin teknik kanıtıdır. İnsan kod incelemesi, sözleşmelerin diğer modül
sahiplerince kabulü ve teslim belgelerindeki merkezi kayıtlar tamamlanmış sayılmaz.
