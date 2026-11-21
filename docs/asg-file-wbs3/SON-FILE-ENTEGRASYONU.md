# Son FILE entegrasyonu — 21.11.2026

## Başlangıç ve kapsam

Temiz ve güncel main a254d4a üzerinde codex/asg-file-final-integration dalı oluşturuldu. PR #40 (65cd11d), PR #42 (a254d4a), onaylı içerik kilidi düzeltmesi (91e4ccf) ve izlenebilirlik birleşmesi (6cbcbc2) main geçmişindedir. Bu çalışma commit/push/PR/main merge yapmaz.

Güncel kodda bildirilen iki sorun zaten PR #42 kapsamındaki düzeltmelerle giderilmişti: ortak FILE taşımasında CSRF ve sizeBytes eşlemesi vardır; DraftUploadService MIME değerini içerikten belirleyip kaydeder. Bu tur ikinci bir yükleme sistemi veya gereksiz üretim kodu değişikliği eklemedi. Yeni çalışma gerçek tarayıcı, MIME, yetki, süreçler arası eşzamanlılık testlerini ve belgeleri tamamlar.

## Ortak FILE yöneticisi

Akış, mevcut FileManager → FileApiClient → FileBrowserHttpHandler → file-api.js → DraftEndpoints → FileService.AddToDraftAsync → DraftUploadService üzerinden çalışır.

| İşlem | Uç ve sözleşme |
| --- | --- |
| Taslak oluştur | POST /api/v1/file-drafts → draftId, expiresAt |
| Yükle | POST /api/v1/file-drafts/{id}/files; multipart file, path=/, overwrite; sunucunun FileMeta yanıtı |
| Taslak listele/sil | GET /file-drafts/{id}/files, DELETE /file-drafts/{id}/files/{fileId}; yalnız sahibi |
| Kalıcı ders dosyası | POST /file-areas/{courseId}/course/files/0/commit; draftId, replace=false; mevcut dosyalar korunur |
| Liste/meta/indir/sil | GET /file-areas/.../files, GET /files/{id}, GET /files/{id}/content, DELETE /files/{id} |
| Yönetici | /admin/file-settings ve /admin/file-maintenance/run; gerçek SiteAdmin kontrolü |

Draft yazmaları gerçek aynı-kaynak cookie oturumunda /antiforgery token'ı ve X-CSRF-TOKEN ile çalışır. Ortak istemcideki Size alanı API sizeBytes alanına bağlıdır. İstemci başlığı MIME doğrulamasının kaynağı değildir. Önceki eksik/geçersiz token JavaScript testleri korunur.

Yeni gerçek Playwright testi, öğretmenin /courses/{id}/files sayfasında dosya seçip taslağa yüklemesini, uyarının görünmemesini, kalıcı kaydı, öğrencinin listelemesini ve tarayıcı indirmesini, byte/MIME doğrulamasını, öğrenci silme isteğinin 404 reddini ve öğretmenin silmesini doğrular. Yeni admin tarayıcı testi gerçek öğrencinin ayar okuma/değiştirme/bakım isteklerinde 403 almasını doğrular.

Test-only gerçek AUTH kullanıcısıyla SiteAdmin ayar okuma/kaydetme/bakım pozitif entegrasyonu ve öğretmen retleri eklendi. Uygulama AUTH seed kodu değiştirilmedi; gerçek örnek SiteAdmin hesabı sağlama işi İrem'indir. Bu yüzden tarayıcıda SiteAdmin pozitif uçtan uca akışın çalıştırıldığı iddia edilmez.

ASG kendi sürüm kontrollü teslim adaptörünü korur. Genel FILE commit üzerinden ASG sürüm/yetki/pencere kontrolleri atlanamaz. Mevcut ASG API, gerçek-cookie SSR, gizlilik ve rollback regresyonları korunur.

## PostgreSQL kilitleme ve işlem sınırları

1. DraftUploadService ad/boyut/tür/içerik ve antivirüs doğrulamasını karantina tamponunda yapar; bunlar içerik kilidinden önce gerçekleşir.
2. PostgreSQL transaction açılır; taslak satırı FOR UPDATE ile kilitlenir.
3. İçerik SHA-256 ilk sekiz baytı big-endian bigint anahtarıdır. Yükleyici pg_advisory_xact_lock alır.
4. Bu kilit altında blob satırı bulunur, fiziksel içerik yazılır/yeniden kullanılır, OrphanedAt temizlenir, entry/MIME yazılır ve transaction commit edilir. Kilit commit/rollback'te bırakılır.
5. FileMaintenanceService aynı anahtarda pg_advisory_lock oturum kilidi alır. Blob satırını FOR UPDATE ile yeniden denetleyip siler ve transaction commit eder; içerik kilidi fiziksel DeleteAsync bitene kadar tutulur.
6. finally, iptal token'ından bağımsız unlock yapar. Böylece yükleyici DB commit ile fiziksel silme arasındaki aralığa giremez.

Global temizlik kilidi yalnız temizlik turlarını birbirine karşı korur. Asıl yükleyici-temizlik koordinasyonu ortak içerik anahtarıdır. LocalDiskFileStore'un SemaphoreSlim kilidi süreç içidir; güvenlik kanıtı olarak tek başına kullanılmadı.

EnsureBlobRowAsync, FileService içinde çağrılmayan eski private yardımcıdır (tek arama eşleşmesi kendi tanımıdır). Etkin yükleme yolu bu yardımcıyı çağırmaz; metadata/MIME ve OrphanedAt yönetimi DraftUploadService transaction'ındadır. Başka ekip üyesinin kullanılmayan kodunu temizleme amacıyla değiştirmedik.

### Ayrı süreç kanıtı

tests/Moodle.FileConcurrencyProbe, uygulama paketine dahil olmayan test konsol projesidir. Entegrasyon testi iki ayrı dotnet süreci başlatır; PID'lerin birbirinden ve testhost'tan farklı olduğunu doğrular. Her süreç ayrı DbContext/Npgsql oturumu kullanır; aynı PostgreSQL 17 veritabanını ve fiziksel dizini paylaşır. Bağlantı bilgisi komut satırı/log yerine stdin ile aktarılır.

FileCrossProcessTests altı sırayı doğrular: temizlik önce/yükleme önce × başarı/disk sonucu hatası/iptal. Disk işlemi kontrollü bir kapıda bekletilir; ikinci sürecin veritabanı kilidinde beklediği pg_stat_activity ile gözlenmeden ilk süreç serbest bırakılmaz. Sonunda tek etkin entry, tek blob, OrphanedAt=null ve fiziksel içeriğin byte eşitliği doğrulanır. Başarısız/iptal yükleme yolu rollback sonrası metadata bırakmaz; temizlik biter, aynı taslak yeniden denenir ve içerik korunur.

İptal ve disk hataları test deposunda kontrollü enjekte edilir; bu testler gerçek antivirüs testi değildir. Gerçek ClamAV ayrı VirusScannerTests ve Playwright EICAR senaryosunda çalışır. Süreçler test sonunda kapatılır; protokol beklemeleri sınırlıdır.

Kilit sırası yükleyicide draft→content, orphan temizliğinde content→blob'dur. Taslak temizliği ayrı transaction'da tamamlanır. İçerik kilidi sırasında yeni draft kilidi alınmadığından sınanan yollarda ters kilit bağımlılığı yoktur. Boyut sınırlı fiziksel I/O kilit içinde zorunlu kritik bölgedir; antivirüs dışındadır. Testler deadlock olmadan tamamlanmıştır; bütün olası işlemler için matematiksel deadlock yokluğu iddia edilmez.

DB ve dosya sistemi tek atomik kaynak değildir. Süreç/host çökmesiyle kayıtsız fiziksel içerik kalması FileConsistencyChecker alanıdır; bu testler host elektrik kesintisi testi değildir. Önceki belirsiz commit sonucunda içerik silmeme kuralı korunur.

## MIME

Mevcut UploadValidation PDF/PNG/JPEG/JPG/TXT için uzantı ve içerik imzasını, TXT için UTF-8/boş bayt kurallarını kullanır. İstemcinin Content-Type değeri güvenilir kabul edilmez. Yeni testler yanıltıcı application/x-untrusted-client başlığına rağmen doğru sunucu MIME değerinin taslak yanıtında, DB'de, kalıcı alana geçişte ve indirme Content-Type başlığında korunmasını doğrular.

PDF→application/pdf; PNG→image/png; JPG/JPEG→image/jpeg; TXT→text/plain. ASG dosyalı teslim ve ders dosyaları ayrı ayrı sınanır. Yanlış PDF/PNG içeriği ve izin verilmeyen uzantı 415 verir; entry oluşturulmaz. Görüntü/PDF test verileri küçük bağımsız fixture'lardır; MIME testi tam belge/görüntü decoder güvenlik denetimi iddiası taşımaz. Antivirüs korunmuştur.

## Playwright ve kalan bağımlılıklar

8 senaryo: 6 geçti, gerçek CRS bölüm modeli/ICourseSectionQuery nedeniyle 2 tam A/B senaryosu atlandı. Yeni ortak dosya yöneticisi ve admin ret testi, önceki gerçek AUTH/ASG listesi, bölüm yokken ret, temiz/EICAR/benzersiz ad ve başka öğrenci taslağına ret testlerine eklendi.

Tam AUTH→CRS→ASG oluşturma→dosya/metin teslimi→GRD notlandırma A/B akışı gerçek bölüm modeli olmadan tamamlanamaz. UnavailableCourseSectionQuery güvenli reddi korunur. Sinem'in kodu değiştirilmedi. MIME entegrasyonundaki doğrudan eklenen ASG fixture'ı gerçek ödev oluşturma uçtan uca kabulü olarak sunulmaz.

## YZ ve süre

Bütün yeni test/kod/belge çalışması YZ desteğiyle üretildi. Hocanın güncel kararında ayrı manuel üretim şartı yoktur; geçmiş kayıtlar tarihsel olarak korunur. Toplam YZ/insan çalışma süresini doğrulayan kesintisiz kayıt bulunmadığından süre tahmini yapılmadı. Alan: **Aslı Ceren tarafından doğrulanmış toplam YZ ile çalışma süresi: doldurulacak.** TRX süreleri yalnız test yürütme süresidir.

COCOMO, SLOC/KLOC, karşılaştırma tablosu/raporu ve ekip kod incelemesi yapılmadı. Diğer üyelerin YZ kayıtları değiştirilmedi.

## Doğrulama kayıtları

İlk tam koşu: 468 birim + 467 entegrasyon = 935/935 geçti. Kanıt coverage/final-integration-first. Sonradan eklenen geçerli PDF üreticisinde metin kaçış hatası Release derlemesini durdurdu; test yardımcısı düzeltildi. Bu başarısız ara derleme coverage/final-integration/build.log içinde korunur. Otomatik onay incelemesinin kullanım limiti nedeniyle ilk test başlatma girişimi çalışmadı; kullanıcının devam talimatından sonraki tekrar çalıştı.

Son sonuçlar DOGRULAMA.json/finalFileIntegration alanına TRX dosyalarından aktarılır. Yerel coverage çıktıları ve Playwright trace'leri Git dışındadır. Bu çalışma commit/push oluşturmaz.

## Son sonuç ve dosya envanteri

**468/468 birim + 471/471 entegrasyon = 939/939 .NET; başarısız/atlanan yok.** Önceki 907 testin adları TRX ile karşılaştırıldı: kaybolan yok. 21 yeni entegrasyon vakası bu çalışmaya, kalan artış güncel main/PR #45'e aittir. JavaScript 54/54; Playwright 6 geçti / 2 gerçek bölüm bağımlılığı nedeniyle atlandı. Release'te mevcut LocalDiskFileStoreTests xUnit2031 uyarısı dışında yeni uyarı yok. Beş model snapshot, gerçek ClamAV ve Compose/HTTP geçti. HTTP health/login/courses/OpenAPI 200; anonim draft-limit 401.

Değişen dosyalar:
- Moodle.slnx ve tests/Moodle.IntegrationTests/Moodle.IntegrationTests.csproj: test-only probe projesini derleme bağımlılığına ekler.
- tests/Moodle.FileConcurrencyProbe/Moodle.FileConcurrencyProbe.csproj ve Program.cs: ayrı süreçlerde gerçek uploader/cleanup yürütücüsü.
- tests/Moodle.IntegrationTests/FileCrossProcessTests.cs: altı süreçler arası yarış/rollback/retry vakası.
- tests/Moodle.IntegrationTests/FileMimeIntegrationTests.cs: MIME, snapshot ve gerçek AUTH yönetici testleri.
- tests/e2e/specs/asg-file.spec.mjs: ortak dosya yöneticisi ve admin ret senaryoları.
- docs/WBS-3-gelistirme.md: yalnız Aslı §2.4 güncellendi.
- docs/asg-wbs3/YZ-KAYDI.md: tarihsel yöntemin güncel eksik görev olmadığı açıklandı, gerçek çalışma kaydı eklendi.
- docs/izlenebilirlik.md: yalnız 15 ASG/FILE sorumluluk satırı; 72 benzersiz gereksinim ve dokuz sütun korunur.
- docs/asg-file-wbs3/SON-FILE-ENTEGRASYONU.md, TESLIM-RAPORU.md, TESTLER.md, ENTEGRASYON.md, SON-KONTROL.md, DOGRULAMA.json ve GIT-DURUMU.txt: yeni kanıt ve durum; eski sonuçlar tarihsel olarak saklanır.

Üretim src/ dosyalarında değişiklik yok. Conflict marker/whitespace kontrolleri temiz; teslim adaylarında belirgin token/özel anahtar örüntüsü veya build çıktısı bulunmadı. Örüntü taraması her olası sır biçiminin yokluğuna dair mutlak garanti değildir. Test fixture hesapları sentetiktir; AUTH uygulama seed'i değişmedi.

Bu görev kapsamındaki Aslı'ya ait bağımsız teknik işler tamamlandı. Kalan ekip bağımlılıkları: Sinem'in gerçek bölüm modeli/ICourseSectionQuery ve tam A/B; İrem'in örnek SiteAdmin hesabıyla pozitif admin tarayıcı kabulü. Toplam doğrulanmış YZ efor süresini Aslı ayrıca doldurmalıdır; bu teknik geliştirme eksikliği değildir. Commit/push/PR yapılmadı; HEAD a254d4a.
