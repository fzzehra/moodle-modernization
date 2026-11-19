# CRS-NFR-04 — CRS sorgu önbelleği ve geçersiz kılma

Tarih: 19 Kasım 2026. Branch: `sinem/crs-nfr04-cache`.
Başlangıç: temiz `sinem/crs-events-extensions`, HEAD
`09e9484dc79a1a02ddec092a22e03747f7e5daa5`.
Geçmişte hem `599d8a5` (NFR-05) hem `09e9484` (olaylar/genişletmeler) doğrulandı.
Bu branch iki CRS commit'ine bağımlıdır; commit/push/PR oluşturulmadı.

## A. Kaynak gereksinim ve mevcut durum

[WBS-1 §4.2.9](WBS-1-analiz.md), CRS-NFR-04 (D/S, Performans):
“Sistem, ders ve kullanıcı kayıt bilgilerindeki değişikliklerden sonra ilgili
önbellekleri geçersiz kılmalıdır.” Kaynaklar: `public/course/lib.php`, `public/lib/enrollib.php`.
WBS-1 §4.2.4 ve kayıt iş akışları ders/görünürlük ve kullanıcı kayıt değişikliklerindeki
önbellek temizliğini açıklar. Değerlendirme sınırı, Moodle mekanizmasının birebir taşınmasını şart koşmaz.
[WBS-2 §3.2.6](WBS-2-tasarim.md), NFR-04'ü §3.2.5 ile ilişkili “Gerekli durumlarda
önbellek geçersiz kılma mekanizması” olarak eşler.

İncelemede CRS sunucu kodunda `IMemoryCache`, dağıtık cache veya veri snapshot cache'i yoktu.
`CrsService` sorguları `AsNoTracking` ile doğrudan PostgreSQL'e gidiyordu.
EF change tracker sorgu sonucu önbelleği olarak değerlendirilmedi.
Tarayıcıdaki `crs-api.js` HTTP istekleri `cache: "no-store"` kullanır; bu dosya değiştirilmedi.
Mevcut DI, `CrsService` ve `CrsDbContext` için scoped ömür kullanır.

## B. Seçilen küçük çözüm

`CrsService` kendi özel `CrsQueryCache` örneğini oluşturur. Singleton/statik veri deposu,
Redis, yeni NuGet paketi, ortak DI kaydı, yeni tablo veya migration yoktur.
Normal HTTP akışında ömür istek kapsamıdır; iki servis örneği aynı snapshot'ı paylaşmaz.
Maksimum 64 anahtar tutulur; kapasitede en eski giriş çıkarılır. Anahtar başına satır sayısı
sınırı eklenmez; mevcut listeleme sözleşmesi ve sıralama korunur. Kaynak nesneler detached'tır;
mutable entity/listeler saklanırken ve hit dönerken kopyalanır.

| Sorgu | Anahtar / kapsam | Kullanım |
|---|---|---|
| `ListCoursesAsync` | `courses` | Ham ders listesi, mevcut FullName sıralaması |
| `FindCourseAsync(id)` | `course:{id}` | Ham ders detayı; bulunamama sonucu da cache'lenir |
| `ICourseDirectory.FindAsync(id)` | Aynı `course:{id}` | Mevcut `CourseInfo` alanlarıyla projeksiyon |
| `ListEnrolmentsAsync(courseId)` | `enrolments:{courseId}` | Ham kayıt listesi, aktiflik filtresi içermez |
| `FindEnrolmentAsync(courseId,id)` | `enrolment:{courseId}:{id}` | Ders kapsamı anahtarın parçasıdır |
| Kategori sorguları | Cache yok | NFR-04 için yeni kategori cache'i eklenmedi |
| `IsActivelyEnrolledAsync` | Cache yok | Kullanıcı/ders filtresi ve zaman her çağrıda SQL'de değerlendirilir |
| `ListEnrolledUserIdsAsync` | Cache yok | Başlangıç dahil, bitiş hariç aktiflik koşulları aynıdır |
| `IAssignmentLearnerQuery.ListRequiredLearnerIdsAsync` | Cache yok | Student filtresi, distinct ve ID sıralaması aynıdır |

Aktif kayıt sonucu, veri değişmese bile zamanla değişebilir. Bu nedenle bu sonuçları
TTL ile tahmin ederek cache'lemek yerine mevcut SQL davranışı korundu. Ham katılımcı
snapshot'ları bu sorgulara kaynak yapılmaz; rol/durum değişikliği de anında veritabanından görülür.

### İlgili anahtarların mantıksal invalidation'ı

Her cache okuması önce mevcut `crs_events` outbox'ında ilgili olayların
`(count(*), max(Id))` damgasını okur. Eski damga eşleşmezse yalnızca o anahtarın
snapshot'ı yeniden yüklenir. Tüm cache'i temizleyen bir mutasyon hook'u yoktur.
Bu tembel doğrulama, commit sonrasındaki bir sonraki okumada geçersiz veriyi kullanmaz.

| Değişiklik | Geçersiz olan kapsam | Etkilenmeyen kapsam |
|---|---|---|
| Ders oluşturma/güncelleme/silme | Global ders listesi, ilgili ders detayı | Diğer ders detayları; bağımsız ham kayıt verisi |
| Kayıt oluşturma/durum-rol güncelleme/silme | İlgili dersin ham kayıt listesi/detay anahtarları | Diğer ders kayıtları; ders listesi/detayları |
| Ders silme cascade'i | Yukarıdaki ders kapsamı ve gerçekten silinen kayıtlara ait ders kapsamı | İlgisiz dersler |
| No-op veya başarısız/rollback işlem | Yok; görünür olay damgası değişmez | Mevcut geçerli snapshot'lar |
| Outbox teslimi / receipt yeniden kontrolü | Yok; veri değişikliği olayı eklenmez | Tüm geçerli snapshot'lar |

Tek kayıt değişiminde aynı dersin kayıt detayları ders düzeyinde birlikte doğrulanır;
başka derslerin anahtarları etkilenmez. Olay türleri mevcut altı v1 yaşam döngüsü türüdür.
Dersin kategori/görünürlük/tarih değişimi ders olayıyla kapsanır; kategori adları ise cache'lenmez.
Mevcut servis kayıt güncellemesinde ders/kullanıcı kimliğini taşımaz; key kapsamı bu sözleşmeye dayanır.

### Commit sırası ve transaction sınırı

- Yalnızca `MAX(Id)` yeterli değildir: sequence ID'si küçük olan transaction daha geç commit edebilir.
  `count(*)` de kontrol edilir; bu senaryo iki gerçek PostgreSQL bağlantısıyla test edilir.
- Damga cache miss'te veri okunmadan önce alınır. Arada commit olursa snapshot eski damgayla
  etiketlenir ve sonraki okumada yeniden doğrulanır. Hit'in doğrulama SELECT'iyle eşzamanlı bir
  commit, normal bir veritabanı okumasında olduğu gibi o çağrıyla örtüşebilir; çağrı bittikten
  sonra commit edilen her değişiklik için çağrının en yeni veriyi döndürdüğü iddia edilmez.
- EF açık transaction'ı, ambient `TransactionScope` ve manuel enlisted transaction varsa cache
  tamamen atlanır: damga okunmaz, cache'e yeni snapshot yazılmaz, eski snapshot da değiştirilmez.
  Transaction içindeki SQL okumaları o transaction'ın verisini ve izolasyonunu kullanır.
- Commit sonrasında görünür outbox damgası yenilenir; rollback'te eski damga/snapshot korunur.
- Olay INSERT'i önceki CRS migration'ının trigger'ıyla iş yazımıyla aynı transaction içindedir.
  Bu çalışma üretim yazım metotlarını veya trigger/migration kodunu değiştirmez.
- Damga veya veri SELECT'i başarısızsa hata çağırana iletilir; cache'teki eski sonuç fallback olarak sunulmaz.
- Cache okuması INSERT/UPDATE/DELETE veya ek olay üretmez.

Outbox'ı keyfi silmek, identity'yi sıfırlamak veya trigger'ları devre dışı bırakmak desteklenen
yazım akışı değildir. Gelecekte outbox retention/taşıma eklenirse damga stratejisi birlikte
yeniden değerlendirilmelidir; mevcut olay altyapısında retention görevi yoktur.

### Performans sınırı

Hit, tekrar entity/liste SELECT'ini önler; sıfır SQL anlamına gelmez. Her okumada outbox
aggregate sorgusu vardır. Mevcut outbox'ta bu filtrelere özel yeni indeks eklenmedi;
olay geçmişi büyüdükçe aggregate maliyeti büyüyebilir. Bu çözüm istekler arası hızlanma
veya p95 kazanımı iddia etmez; ölçülen kanıt tekrarlı veri SELECT'inin atlanması ve doğru
invalidation'dır. Geniş paylaşımlı cache veya ayrı revision altyapısı kurmadan küçük
bir CRS cache'inin doğruluğunu sağlar. Dağıtık cache/ek indeks/retention bu göreve sessizce eklenmedi.

## C. Değiştirilen ve eklenen dosyalar

- Değişti: `src/Moodle.Infrastructure/Courses/CrsService.cs` — yalnızca cache'lenen okuma yolları,
  damga sorgusu, transaction guard ve kopyalama. Yazım/doğrulama metotları aynıdır.
- Yeni: `src/Moodle.Infrastructure/Courses/CrsQueryCache.cs` — bounded instance cache.
  Internal yardımcıyı sınamak için yalnızca mevcut unit test assembly'sine friend erişim tanımlanır.
- Yeni: `tests/Moodle.UnitTests/CrsNfr04CacheTests.cs`.
- Yeni: `tests/Moodle.IntegrationTests/CrsNfr04CachePostgresTests.cs` — PostgreSQL ve API test sınıfları.
- Yeni: `docs/crs-nfr04-cache.md` — bu belge.

## D. Güvenlik ve kapsam

Cache'lenen veri ham CRS tablosu verisidir; kullanıcıya göre filtrelenmiş HTTP yanıtı,
izin kararı, kimlik bilgisi veya aktif kayıt yetkisi değildir. Servis örneğine özel
saklandığından farklı isteklerin sonuçları paylaşılmaz. API her istekte mevcut AUTH
kontrollerini tekrar çalıştırır; gizli ders kontrolü ve kayıt yönetimi yetkileri korunur.
Kayıt detay anahtarı hem ders hem kayıt kimliğini içerir. Ham cache'e erişim sağlayan
yeni endpoint veya modüller arası yetki oluşturulmadı.

`ICourseDirectory`, `IEnrolmentQuery`, `IAssignmentLearnerQuery` sözleşmeleri aynıdır.
`Program.cs`, ortak `DependencyInjection.cs`, AUTH/FILE/ASG/GRD kodları, tabloları,
migration ve testleri değiştirilmedi. Gerçek AUTH bulunmadığında `503 auth_not_ready` korunur.
Sahte giriş veya üretimde yetki atlama eklenmedi; API izin değişimi testi yalnızca
mevcut test host'unun test policy'sini kullanır.

## E. Test kanıtları

8 birim testi: aynı damgada hit, aynı MAX ile değişen COUNT, bypass, defensive copy,
revision/reload hata aktarımı, örnekler arası izolasyon, kapasite ve negatif sonuç invalidation'ı.

18 PostgreSQL/API testi: gerçek servis yazımları sonrasında güncel veri, seçici invalidation,
create/status-role/delete/cascade, aynı değerler ve başarısız yazımda korunma, açık transaction
commit/rollback, başka bağlantıdaki commit edilmemiş verinin görünmemesi, geç commit eden küçük
olay ID'si, başlangıç/bitiş sınırı, ders kapsamı, receipt/replay, outbox erişim hatası,
ambient/manuel enlist rollback ve istekler arasında yetkinin kaldırılması.
DbCommandInterceptor veri SELECT'lerini sayar; yalnızca mock çağrı sayısına güvenilmez.

İlk PostgreSQL denemesinde Docker kapalı olduğundan 13 test fixture kurulumunda başarısız
oldu; işlev senaryoları o koşuda çalışmadı. Kurulu Docker Desktop başlatıldı (sunucu 29.6.2),
testler genişletilip yeniden çalıştırıldı: 18/18 başarılı. Bu ilk başarısız koşu gizlenmez.
İlk derlemede transaction API kullanımı derleme hatası verdi; EF'in mevcut transaction manager
arayüzüne geçirilerek giderildi. Son derleme 0 hata, 5 NU1900 uyarısıyla başarılıdır;
uyarılar NuGet güvenlik verisine erişimle ilgilidir.

Tam çözüm koşusu tamamlandı, çıkış kodu **0**. TRX kayıtları ayrıca incelendi:

| Kapsam | Başarılı | Başarısız | Atlanan / başarılı olmayan | Yeni NFR-04 | Mevcut NFR-05 | Mevcut olay/genişletme |
|---|---:|---:|---:|---:|---:|---:|
| Birim | 273 | 0 | 0 | 8 | 8 | 9 |
| PostgreSQL/API dahil entegrasyon | 321 | 0 | 0 | 18 | 17 | 17 |
| Toplam | **594** | **0** | **0** | **26** | **25** | **26** |

Önceki 568 testin tamamı ve 26 yeni test başarılıdır. Son koşuda çalıştırılamayan
veya atlanan .NET testi yoktur; ilk Docker hatası sonraki gerçek PostgreSQL koşusuyla
giderildi. Mevcut üretim AUTH placeholder'ları için 503 testleri de tam çözüm kapsamındadır.
Son derleme ve testten sonra yalnızca bu teknik belgeye sonuçlar eklendi.

Tam koşu kanıtları:

- `TestResults/crs-nfr04/regression/SİNEM_SARIÇİÇEK_LAPTOP-AC1FKB38_2026-11-19_14_24_26_net10.0.trx`
- `TestResults/crs-nfr04/regression/SİNEM_SARIÇİÇEK_LAPTOP-AC1FKB38_2026-11-19_14_24_28_net10.0.trx`

```powershell
dotnet build Moodle.slnx --no-restore
dotnet test tests/Moodle.UnitTests/Moodle.UnitTests.csproj --no-build --no-restore --filter FullyQualifiedName~CrsNfr04CacheTests
dotnet test tests/Moodle.IntegrationTests/Moodle.IntegrationTests.csproj --no-build --no-restore --filter FullyQualifiedName~CrsNfr04Cache
dotnet test Moodle.slnx --no-build --no-restore --logger trx --results-directory TestResults/crs-nfr04/regression
```

TRX kanıtları `TestResults/crs-nfr04/` altında (Git ignore): `cache-unit.trx`, ilk Docker
hatasını içeren `cache-postgres.trx`, başarılı `cache-postgres-retry.trx` ve `regression/`.
Entegrasyon testleri yalnızca geçici PostgreSQL 17 konteynerlerini kullanır;
geliştirme veritabanına şema veya veri değişikliği yapılmadı.

## F. Bağımlılıklar ve teslim

Önceki `CrsEvents` migration'ının kurulmuş olması ve mevcut CRS veritabanı bağlantısının
`crs_events` okuyabilmesi gerekir. Başka modüllerin veri erişim yetkileri değiştirilmedi.
Outbox tüketicisi/worker/receipt teslimi cache doğruluğu için gerekli değildir. Başka
ekip üyesinin geliştirmesini bekleyen bir runtime bileşeni eklenmedi. Gerçek AUTH entegrasyonu
ve diğer modül cache'leri bu çalışmanın dışındadır.

Bu dosyalar YZ desteğiyle geliştirildi; manuel geliştirme kanıtı olarak sınıflandırılamaz.
Ortak izlenebilirlik/WBS belgelerine veya başka ekip üyelerinin dosyalarına dokunulmadı.
İnsan incelemesi ve ortak teslim kaydı bu teknik test kanıtıyla tamamlanmış sayılmaz.
