# CRS-NFR-03 — Yedekleme/geri yükleme sırasında ders silme koruması

Tarih: 19 Kasım 2026. Branch: `sinem/crs-nfr03-delete-guard`.
Başlangıç: temiz ve güncel `main` / `origin/main`,
`6f7486efadcfcbe2154cce3f2184a5fb8c9450a5`.

## 1. Git ve önceki CRS çalışmalarının doğrulanması

`git fetch origin main` başarılı. `599d8a5`, `09e9484` ve `d1f64c5` commit'lerinin
`origin/main` geçmişinde olduğu `git merge-base --is-ancestor` ile doğrulandı.
Yeni branch bu main commit'inden oluşturuldu; yerel main değiştirilmedi.

GitHub CLI `gh pr view 31 --json ...` sonucuna göre
[PR #31](https://github.com/fzzehra/moodle-modernization/pull/31) **MERGED**,
merge commit'i `6f7486e`. GitHub'ın raporladığı merge zamanı 19 Kasım 2026
14:54:53 (Türkiye). PR head'inin kontrolleri:

- [CI build-test ve docker-build](https://github.com/fzzehra/moodle-modernization/actions/runs/44518652790): SUCCESS.
- [CodeQL analyze](https://github.com/fzzehra/moodle-modernization/actions/runs/44518652736): SUCCESS.
- GitHub'ın ek `CodeQL` check kaydı: SUCCESS.

Bu sonuçlar PR #31'e aittir; henüz commit/push yapılmamış yeni NFR-03 çalışmasının
uzak CI sonucu olarak sunulmaz.

## 2. Gereksinimin tam kapsamı

[WBS-1 §4.2.9](WBS-1-analiz.md), CRS-NFR-03 (D/S, Güvenilirlik):
“Sistem, devam eden veya bekleyen yedekleme/geri yükleme işlemleriyle çakışabilecek
ders silme işlemlerini engellemelidir.”

Aynı belgenin CRS-BR-06 kuralı: “Bekleyen veya devam eden yedekleme/geri yükleme
işlemi bulunan ders silinemez.” §4.2.5 ders silme akışının 4. adımı bu kontrolü
silme başlamadan önce ister. [WBS-2 §3.2.6](WBS-2-tasarim.md), NFR-03'ü
“Yedekleme ve geri yükleme süreçleriyle çakışmaların önlenmesi” olarak §3.2.5'e bağlar.
§3.2.5 transaction ve hata durumunda kısmi değişikliği önleme ilkelerini belirtir.

Kapsam bütün devam eden ödev, not veya dosya işlemleri değildir. Bu işlere ait
engeller varsayılarak eklenmedi. CRS-FR-11/NFR-06 arka planda silme, AUTH-FR-06/09
ve önceki sürümün ilişkili veri temizliği eksikleri bu çalışmaya alınmadı.

## 3. Mevcut kod ve bağımlılık incelemesi

Önceden `CrsService.DeleteCourseAsync`, dersi `FindAsync` ile bulup Remove/SaveChanges
çağırıyordu; yedekleme/geri yükleme kontrolü yoktu. `CrsDbContext` ilişkileri:
kategori → ders Restrict; ders → kayıt Cascade. Outbox ders ve kayıt değişikliklerini
aynı PostgreSQL transaction'ında kaydeder. NFR-04 cache'i yalnızca görünür outbox
damgası değiştiğinde ilgili veriyi yeniden okur.

Salt okunur incelenen diğer modül sınırları:

- `Application/Abstractions/IAssignmentQuery.cs`, `ICourseDirectory.cs`,
  `IEnrolmentQuery.cs`, `IAssignmentLearnerQuery.cs`, `ICourseSectionQuery.cs`:
  mevcut sorgular var; backup/restore durumunu sağlayan sözleşme yok.
- `Application/Files/IFileService.cs`, `IFilePolicy.cs`: dosya işlemleri/yetki
  politikaları var; ders yedekleme/geri yükleme veya silme kabul sözleşmesi yok.
- `Infrastructure/Persistence/MoodleDbContext.cs`, `Grading/GrdDbContext.cs`,
  `Files/FilesDbContext.cs`: modül içi ilişkiler var; bu çalışma bunlara ders silme
  kancası veya CRS FK'sı eklemedi. Dersle ilişkili ödev/not/dosya temizliğinin
  tamamlandığı iddia edilmez.

Gerçek yedekleme/geri yükleme yürütücüsü veya iş kuyruğu durum sağlayıcısı mevcut
CRS kodunda bulunamadı. Böyle bir sağlayıcının sahipliği bu koddan doğrulanamadığı
için başka bir ekip üyesine görev ataması yapılmadı ve diğer modüller geliştirilmedi.

## 4. Bağımsız CRS çözümü

Yeni Application sözleşmesi `ICrsCourseOperationRegistry`, yalnızca Backup (1) ve
Restore (2) için dahili işlem koruması sağlar. Yeni PostgreSQL tablosu
`crs_course_operations` yalnızca aktif koruma kayıtlarını tutar:

| Alan | Anlam |
|---|---|
| Id | İşlemi korele eden UUID; kullanıcıya erişim yetkisi veren bir token değildir |
| CourseId | CRS dersine FK, ON DELETE RESTRICT; indeksli |
| Kind | 1 Backup / 2 Restore; veritabanında CHECK |
| State | 1 Pending / 2 Running; veritabanında CHECK |
| CreatedAt | PostgreSQL tarafından oluşturulan zaman |

Kalıcı işlem tablosu EF entity modeline eklenmez; mevcut outbox yaklaşımı gibi
parametreli EF SQL ile erişilir. Bu nedenle mevcut snapshot ve eski migration'lar
değişmedi. Yeni migration designer'ı değişmeyen mevcut EF hedef modelini taşır.

### Üretici sözleşmesi

1. `RegisterPendingAsync(courseId, kind)`: ders varlığı kilit altında doğrulanır,
   Pending kaydı oluşturulur ve UUID döner.
2. Gerçek yürütücü **işi kuyruğa almadan veya başlatmadan önce kayıt transaction'ını
   commit etmelidir**. Bu commit tamamlanmadan iş başlatmak sözleşmeye aykırıdır.
3. `MarkRunningAsync(courseId, operationId)`: Pending → Running; zaten Running ise
   yazım yapılmaz. Ders/UUID uyuşmazsa başka dersin koruması değiştirilemez.
4. İş gerçekten sona erince (başarı, iptal veya hata sonrası artık çalışmıyorken)
   `ReleaseAsync` korumayı kaldırır. Eksik UUID için mevcut derste tekrar release
   no-op'tur. Birden fazla kayıt varsa hepsi kaldırılmadan ders silinemez.

Bu yöntemler gerçek işi çalıştırmaz; worker, görev kuyruğu, retry sistemi, yedek
dosyası üretimi veya geri yükleme uygulaması değildir. Completed/Failed/Cancelled
iş geçmişi tutulmaz; bu iş geçmişi gerçek yürütücünün sorumluluğunda kalır.
Koruma otomatik sona ermez. Süre doldu diye hâlâ çalışan işin korumasını kaldırmak
yerine güvenli tarafta kalır. Çöken üreticiden kalan korumayı doğru biçimde
uzlaştıracak gerçek yönetim akışı henüz eklenmedi.

### Silme, kilitleme ve hata davranışı

`CrsService.DeleteCourseAsync` artık aynı CRS registry'sinin kilitli işlem sınırını kullanır:

1. Çağıranın açık/ambient/manuel enlisted transaction'ı yoksa CRS kendi transaction'ını açar.
2. İlgili `crs_courses` satırı `SELECT ... FOR UPDATE` ile kilitlenir.
3. Koruma kaydı varsa, Remove/SaveChanges çağrılmadan
   `CrsConflictException("course_operation_in_progress", ...)` fırlatılır.
   Mevcut `CrsEndpointFilter` bunu HTTP 409'a çevirir; API dosyası değiştirilmedi.
4. Engel yoksa mevcut course Remove/SaveChanges akışı sürer. Yalnızca CRS'nin açtığı
   transaction commit edilir; çağıranın transaction'ı CRS tarafından commit edilmez.
5. Ders yoksa önceki KeyNotFound/HTTP 404 davranışı korunur (0 kimliği dahil).

Registry mutasyonları da önce **aynı ders satırını** kilitler:

- Kayıt açma önce kilit aldıysa silme bekler; kayıt commit edildiğinde 409 alır,
  kayıt rollback edilirse silme ilerleyebilir.
- Silme önce kilit aldıysa yeni kayıt açma bekler; silme commit edildiğinde ders
  bulunamaz ve iş kabul edilmez. Silme rollback edilirse işlem kaydı açılabilir.
- Registry çağrıları dışında doğrudan SQL ile ders silinse bile Restrict FK aktif
  koruma varken silmeyi engeller; kayıt cascade'i ve olaylar da statement rollback'iyle korunur.
- Varsayılan Read Committed altında servis kontrolü belirli 409 kodunu verir.
  Daha sıkı izolasyon veya farklı eşzamanlı yazımlarda PostgreSQL serialization/FK
  hatası oluşabilir; güvenlik gevşetilmez, hata çağırana iletilir. Otomatik retry eklenmedi.
- Kilit bekleme iptali/SQL hatası başarılı silme olayı bırakmaz. İşlem tablosuna
  erişilemiyorsa “engel yok” varsayılmaz; silme durur.

Migration Down önce tabloyu exclusive kilitler; aktif koruma varsa düşürmeyi reddeder.
Koruma kayıtlarını sessizce silerek downgrade yapılmaz. Koruma kalmadığında Down/Up
gerçek PostgreSQL'de sınanır. Geliştirme veritabanına migration uygulanmadı;
yalnızca geçici test konteynerlerinde uygulandı.

## 5. Olay, cache ve mevcut sözleşmeler

Engellenen silmede ders/kayıt DML'i yok; course.deleted veya enrolment.deleted olayı
yok. Cache damgası değişmediği için ısınmış ders/kayıt snapshot'ları geçerli kalır.
Başarılı silmede mevcut outbox ve cascade olayları damgayı değiştirir; sonraki
cache okumaları güncel yokluk/boş liste sonucunu alır. Silme rollback edilirse veri,
olaylar ve dış okuyucunun cache görünümü birlikte korunur.

İşlem kayıtlarının kendi INSERT/UPDATE/DELETE'leri ders/kayıt değişikliği olarak
olaylaştırılmaz; bu işlerin durum geçmişi için yeni bir olay sistemi kurulmadı.
Tekrarlanan MarkRunning ve Release çağrılarında gereksiz DML üretilmediği ayrıca ölçülür.
Önceki NFR-05/NFR-04 kodları değiştirilmez; `CrsService` içinde yalnızca ders silme yolu değişir.

`ICourseDirectory`, `IEnrolmentQuery` (main'deki mevcut `FindActiveRoleAsync` dahil),
`IAssignmentLearnerQuery`, API rotaları ve mevcut AUTH davranışı korunur.
`Program.cs` veya ortak `DependencyInjection.cs` değiştirilmez. Registry mevcut
DbContext ile açıkça oluşturulabilir; başka modül tüketicisi veya ortak DI kaydı eklenmedi.

## 6. Testler ve kanıt sınırı

`CrsNfr03DeleteGuardTests` (10 birim): engel kuralı/kodu, engelsiz kontrol,
izin verilen türler, geçersiz tür/ders/UUID'nin bağlantı açmadan reddedilmesi.

`CrsNfr03DeleteGuardPostgresTests` ve aynı dosyadaki API sınıfı:

- Backup/Restore × Pending/Running: veri, kayıtlar, olaylar ve cache korunur.
- Tüm korumalar kaldırılınca silme/cascade/olay/cache yenilemesi gerçekleşir.
- Başka derse ait koruma ilgisiz silmeyi engellemez ve yanlış kapsamdan kaldırılamaz.
- Doğrudan SQL silmesine FK engeli; gerçek yazım sayacıyla tekrar çağrıların no-op olması.
- İki bağlantılı yarışların her iki sırası ve commit/rollback kombinasyonları.
  Sabit bir bekleme süresine göre sonuç tahmin edilmez: `pg_stat_activity` üzerinden
  hedef bağlantının gerçekten `Lock` beklediği gözlenir.
- Kilit beklemesinin iptali, release/deletion rollback, eksik ders, koruma tablosunun
  erişilememesi, ambient transaction ve manuel enlist rollback.
- Migration Down'un aktif korumayı reddetmesi ve release sonrası Up/Down.
- Mevcut test host'u üzerinden HTTP 409, ardından release sonrası HTTP 204.

Testler gerçek PostgreSQL 17'de gerçek koruma kayıtları oluşturur. Bunlar çalışan bir
yedekleme/geri yükleme motorunun uçtan uca testi değildir; gerçek motor varmış gibi
fake iş durumu sunulmaz. Diğer modüllerin test dosyaları değiştirilmedi.

İlk `--no-restore` derlemesi güncel main'deki mevcut Identity paketine ait eski yerel
assets nedeniyle durdu. Mevcut csproj değiştirilmeden `dotnet restore Moodle.slnx`
yapıldı; sandbox'ta NuGet erişimi engellendiği için izinli ağ erişimiyle tekrarlandı ve
başarılı oldu. Yeni paket eklenmedi. Son build 0 hata, 1 NU1900 (Application paket
güvenlik verisine erişim) uyarısıyla başarılıdır.

İlk hedefli koşu: 10 birim + 17 PostgreSQL/API testi başarılı. Ardından fail-closed,
ambient transaction ve manuel enlist için üç ek test tam çözüm koşusuna eklendi.
Tam çözüm koşusu tamamlandı (çıkış kodu 0); TRX kayıtları ayrıca incelendi:

| Kapsam | Başarılı | Başarısız | Atlanan / başarılı olmayan | Yeni NFR-03 | Mevcut NFR-05 | Mevcut NFR-04 | Mevcut olay/genişletme |
|---|---:|---:|---:|---:|---:|---:|---:|
| Birim | 305 | 0 | 0 | 10 | 8 | 8 | 9 |
| PostgreSQL/API dahil entegrasyon | 356 | 0 | 0 | 20 | 17 | 18 | 17 |
| Toplam | **661** | **0** | **0** | **30** | **25** | **26** | **26** |

Güncel main'den gelen 631 mevcut test ve 30 yeni NFR-03 testi geçti. Önceki
594 toplamı eski branch'in test sayısıydı; güncel main ek testler içerir.
Son koşuda çalıştırılamayan veya atlanan .NET testi yoktur. Yeni 20 entegrasyon/API
testi gerçek PostgreSQL 17 Testcontainers ile çalıştı; üç son ek test de bu koşudadır.
Tam koşudan sonra yalnızca bu belgeye sonuçlar eklendi.

TRX dosyaları:

- `TestResults/crs-nfr03/regression/SİNEM_SARIÇİÇEK_LAPTOP-AC1FKB38_2026-11-19_15_21_10_net10.0.trx`
- `TestResults/crs-nfr03/regression/SİNEM_SARIÇİÇEK_LAPTOP-AC1FKB38_2026-11-19_15_21_13_net10.0.trx`

```powershell
dotnet restore Moodle.slnx
dotnet build Moodle.slnx --no-restore
dotnet test tests/Moodle.UnitTests/Moodle.UnitTests.csproj --no-build --no-restore --filter FullyQualifiedName~CrsNfr03DeleteGuardTests
dotnet test tests/Moodle.IntegrationTests/Moodle.IntegrationTests.csproj --no-build --no-restore --filter FullyQualifiedName~CrsNfr03DeleteGuard
dotnet test Moodle.slnx --no-build --no-restore --logger trx --results-directory TestResults/crs-nfr03/regression
```

Yerel, Git ignore altındaki kanıtlar: `TestResults/crs-nfr03/guard-unit.trx`,
`guard-postgres.trx` (ilk 17 entegrasyon testi), `regression/` (son tam koşu).
Docker sunucusu 29.6.2 olarak doğrulandı.

## 7. Tamamlanan kısım ve kalan bağımlılık

**Tamamlanan:** CRS'nin kayıtlı Pending/Running yedekleme/geri yükleme işlemleri için
sunucu ve veritabanı düzeyinde yarışa dayanıklı silme engeli; mevcut hata, olay ve
cache davranışıyla birlikte test edilen bağımsız CRS bileşeni.

**Tamamlanmayan:** Gerçek yedekleme/geri yükleme yürütücüsünün/queue kabul noktasının
registry'ye bağlanması ve onun gerçek iş durumu ile kayıtların yaşam döngüsünü
güvenceye alması. Bu üretici henüz bulunmadığından NFR-03 uçtan uca bütünüyle
tamamlandı denemez. Yürütücü sahipliği ve çağrı sınırları netleştirilmeli; başka
modül veya ortak DI değişikliği gerekirse ayrıca onay alınmalıdır. Koruma tablosunun
boş olması, sistemde bilinmeyen bir dış işin bulunmadığının kanıtı değildir.

Birinci sürümden kalan ASG/GRD/FILE ilişkili veri temizliği, kayıt yöntemi/zaman
güncellemesi, teslim belgeleri ve YZ/manuel karşılaştırması bu çalışma kapsamında
çözülmüş sayılmaz. AUTH görevlerine ve arka planda ders silmeye başlanmadı.

> **Güncelleme (21.11.2026):** Kayıt zamanı güncellemesi PR #45, arka planda ders silme
> PR #44, AUTH-FR-09 PR #46 ile tamamlandı; `course.deleted` olayının diğer modüllere
> iletimi PR #47 ile eklendi. ASG/GRD/FILE veri temizliği bu modüllerin uzantılarını
> beklemektedir; kayıt yöntemi modeli WBS-2'den belgelenmiş bir sapmadır (`docs/crs-api.md`).
> YZ/manuel karşılaştırması 18.11.2026 yöntem değişikliğiyle elle geliştirme yerine
> Basic COCOMO tahminiyle yapılır (`docs/WBS-3-gelistirme.md` §3).

## 8. Dosya kapsamı ve üretim kaydı

- Değişti: `src/Moodle.Infrastructure/Courses/CrsService.cs` (yalnızca DeleteCourseAsync).
- Yeni: `src/Moodle.Application/Courses/ICrsCourseOperationRegistry.cs`.
- Yeni: `src/Moodle.Infrastructure/Courses/CrsCourseOperationRegistry.cs`.
- Yeni: `src/Moodle.Infrastructure/Courses/Migrations/20261119000200_CrsCourseOperations.cs`
  ve `.Designer.cs`.
- Yeni: `tests/Moodle.UnitTests/CrsNfr03DeleteGuardTests.cs`.
- Yeni: `tests/Moodle.IntegrationTests/CrsNfr03DeleteGuardPostgresTests.cs`.
- Yeni: `docs/crs-nfr03-delete-guard.md`.

AUTH/FILE/ASG/GRD kodları, migration'ları ve testleri; ortak kayıt dosyaları;
eski CRS migration/test dosyaları ve WBS belgeleri değiştirilmedi.
Kod/test/belge YZ desteğiyle üretildi; manuel geliştirme kanıtı değildir.
Commit, push, PR veya merge oluşturulmadı; insan incelemesi beklenir.
