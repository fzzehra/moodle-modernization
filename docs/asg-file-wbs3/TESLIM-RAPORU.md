> **20.11.2026 — PR #39 sonrası güncel durum:** Seray e2b50d0 düzeltmesi main 1424b04'ten yeni commit oluşturmadan fast-forward ile alındı; bütün yerel çalışmalar yedeklenip korundu. Eşzamanlılık 10/10, .NET 708/708, JS 23/23 geçti. Playwright 4 geçti / bölüm modeli nedeniyle 2 skip. Önceki kırmızı FileStore sonucu giderilmiştir; aşağıdaki önceki sonuçlar tarihsel kayıttır. [Güncel ayrıntılı sonuç](SON-KONTROL.md), [makine kanıtı](DOGRULAMA.json), [tekrarlar](ESZAMANLILIK-TEKRARLARI.json).
> **20.11.2026 son kontrol:** [Güncel kabul raporu](SON-KONTROL.md) ve [izole tekrar kanıtı](ESZAMANLILIK-TEKRARLARI.json) önceki sonuçları tamamlar. Yeni regresyon: 707/708 .NET geçti; tek hata güncel main'de de bulunan fiziksel depo eşzamanlılığı. 23 JS geçti. Dosyalı SSR form/list/detail ve çakışmada taslağı gösterme doğrulandı. Aşağıdaki önceki toplamlar tarihsel kayıttır.
# ASG / FILE WBS-3 v2 teslimi — 19.11.2026

Dal: `codex/asg-file-wbs3-v2`. Başlangıç: güncel main `3b9ccd6`. Commit, push, PR, merge veya rebase yapılmadı. V1 PR #21 kaydı korunur; v2 PR numarası henüz yoktur.

Bu çalışma Aslı Ceren'in kaynak kodu, test, entegrasyon ve geliştirme dokümantasyonunu kapsar. Kullanıcının kapsam düzeltmesine göre COCOMO, SLOC/KLOC ölçümü, karşılaştırma raporu ve kod incelemesi yapılmaz. Manuel kod üretme zorunluluğu kaldırılmıştır; geçmiş kayıtlar tarihsel olarak korunur, yeni bir manuel geliştirme iddiası yoktur.

## Gerçek modüller ve birinci sürüm

Başlangıçta main'e birleşmiş olan AUTH PR #28, GRD #23, FILE #27/#29/#33/#35 ve CRS gerçek servisleri kullanıldı. AUTH kimliği çerezden gelir. Kullanıcının açık onayıyla `CapabilityRules` ASG yeteneklerini, `AuthAccessPolicy` ASG modül kimliğinden ders bağlamını çözümlemeyi destekler. Gerçek `AssignmentSubmissionAccessPolicy`, ders/ödev eşleşmesi, değerlendiricinin yetkisi ve hedef öğrencinin aktif teslim yükümlülüğünü birlikte doğrular. Liste, ayrıntı ve dosya okuma aynı ASG kapsam kontrolüne dayanır. Grup modeli olmadığı için grup yetkisi varsayılmaz.

`IAssignmentLearnerQuery` gerçek CrsService'e bağlı kalır. CRS'de bölüm modeli hâlâ yoktur: `ICourseSectionQuery` güvenli olarak false döner. Gerçek ödev oluşturma ve ortak ASG seed bu nedenle engellidir. Sinem'in yerine bölüm modeli oluşturulmadı. Ders sayfasından `/courses/{CourseId}/assignments` listesine ve oluşturma ekranına gerçek kimlikle gidilir; sabit ders 1 menüsü kaldırıldı.

## Gereksinim durumu

| Gereksinim | Uygulama / kaynak | Test / sonuç kapsamı | Durum |
| --- | --- | --- | --- |
| FILE-FR-01 | DraftEndpoints, FileService.AddToDraftAsync, DraftUploadService; gerçek kullanıcı sahipliği, taramadan sonra fiziksel yazma | FileUploadV2Tests: sahiplik, metadata, hata geri alma; gerçek PostgreSQL | Uygulandı |
| FILE-FR-07, FILE-BR-07 | UploadValidation.UniqueName; `ad (1).uzantı`; taslak satır kilidi | UploadValidationTests; T_FILE_01_09 eşzamanlı adlar | Uygulandı |
| FILE-FR-08, FILE-BR-08 | akış üzerinde sınır, sıfır bayt ret, dosya sayısı/toplam alan | T_FILE_11; Area_total_boundary; T_FILE_12_13 | Uygulandı |
| FILE-FR-10, FILE-BR-06 | ad/yol/uzantı/imza ve TXT UTF-8 kontrolü | UploadValidationTests; T_FILE_12_13 | Uygulandı |
| FILE-FR-11, FILE-BR-09 | IVirusScanner; clamd INSTREAM, ClamAvVirusScanner; erişilemez/zaman aşımı güvenli ret | VirusScannerTests: gerçek temiz/EICAR, zaman aşımı, erişilememe; T_FILE_14 kalıcı kayıt yok | Uygulandı |
| ASG-FR-02, ASG-BR-01/02 | metin/dosya türü, sürüm, pencere, kilit, onay; AssignmentService ve AssignmentStore | Eski ASG testleri + FileUploadV2Tests dosyalı işlem testleri | Uygulandı |
| ASG-FR-03, ASG-NFR-01 | ortak değerlendirici kapsamı; AssignmentFilePolicy; doğrudan dosya URL'sinde 404 | gerçek AUTH testleri, diğer öğrenci/öğretmenin erişim kaybı | Uygulandı |
| ASG-FR-01 | gerçek AUTH ve ders bağlantısı hazır | Bölüm yokken güvenli ret; gerçek oluşturma kabulü engelli | Kısmen tamamlandı |
| Dosyalı Blazor | SubmissionForm/Detail/List, CreateAssignmentPage, AsgClient, asg-files.js | Hosting ve API testleri; tarayıcı kapsamı ayrı belgede | Uygulandı; tam E2E engelli |
| Playwright A/B | Gerçek çerezli giriş ve modül API/UI akışları hazır | Bölüm adaptörü olmadan tam senaryolar açıkça skip | Kısmen tamamlandı |

Son doğrulama: 695 .NET testi geçti, Seray deposuna ait 1 test başarısız (toplam 696); 21 JS geçti; Playwright 4 geçti, bölüm bağımlılığı nedeniyle 2 skip. Release/Docker derlemeleri başarılıdır. Tam regresyon yeşil değildir.

Kesin çalıştırma sonuçları [DOGRULAMA.json](DOGRULAMA.json) ve [TESTLER.md](TESTLER.md) içindedir; “uygulandı” tüm bağımlılıklı kabul akışlarının geçtiği anlamına gelmez.

## Dosya kuralları ve transaction

Kullanıcı onaylı başlangıç ayarları: PDF/PNG/JPEG/TXT, en çok 10 dosya, toplam 50 MiB. Dosya başına üst sınır FileSetting ve FileUpload.MaxFileBytes değerlerinin küçüğüdür (varsayılan 50 MiB). WBS'de sayısal sabit oldukları iddia edilmez. Sunucu her zaman doğrular; istemci MIME beyanına güvenmez, metadata MIME içerik/uzantı kontrolünden üretilir. İmza kontrolü tam belge ayrıştırması değildir; ClamAV ayrıca çalışır.

Yükleme sınırlı bellekle işletim sistemi geçici karantina dosyasına akar. Başarılı taramadan önce Seray'ın deposuna yazılmaz. Aynı taslak satır kilidi altında ad/sayı/alan doğrulanır; içerik özeti için transaction advisory lock kullanılır. İrem'in mevcut kota sorgusu yeniden kullanılır; bu sorgu taslak/ASG alanını kalıcı kişisel dosya kotasına dahil etmez. Kullanıcı genelindeki tüm taslakların toplam kotasını tanımlama/yeniden yazma bu çalışmada yapılmadı.

ASG dosya ekleme, mevcut AssignmentStore transaction/savepoint'i ve aynı bağlantıya katılan ayrı FilesDbContext üzerinden İrem'in CommitAsync metodunu kullanır. Caller ChangeTracker'ı kaydedilmez. ASG ve FILE farklı veritabanlarına ayarlanırsa 503 ile reddedilir. Sürüm veya tarama hatasında dosya alanı taşıması ve teslim değişikliği birlikte geri alınır. Generic FILE commit/delete üzerinden ASG sürüm kontrolünü atlamak engellenir; yalnız ASG işlem kapsamı yazabilir.

DB ile dosya sistemi dağıtık transaction değildir: bilinen yazma hatasında yeni içerik temizlenir; commit sonucunun ağ hatası nedeniyle belirsiz olduğu durumda olası kayıtlı içeriği silmemek için otomatik silme yapılmaz. Ani süreç kesintisindeki yetim içerik için mevcut FileConsistencyChecker/ekip temizlik işi gerekir. Bu fiziksel sınır tam atomiklik olarak raporlanmaz.

Yeni `20261119151545_AsgFileSubmission` migration'ı FileCount ekler ve metin VEYA dosya koşulunu destekler; eski migration'lar korunur. Dosya-only teslim varken Down eski metin-zorunlu constraint'e dönemeyebilir; veri temizleme/geri dönüş planı olmadan downgrade uygulanmamalıdır. AUTH/CRS dış FK ve ders/kullanıcı silme politikası ekip kararı bekler; bu çalışmada onaysız cascade eklenmedi. Silinmiş ders veya kaydı kaldırılmış kullanıcı için ASG erişimi reddedilir; eşzamanlı modüller arası silme ve oluşturmanın global atomik garantisi yoktur.

## Seed ve geçici bağlantılar

AsgSampleSeedContributor yalnız `ASG_SEED_SAMPLE=true` ve Development/Testing altında çalışır; ortak fixture kullanıcılarının ad/e-posta ve sentetik parola eşleşmesini doğrular, gerçek kimlikleri sorgular. Bütün bölümler doğrulanmadan yazma başlamaz; mevcut ödevler korunur. Mevcut gerçek CRS nedeniyle seed'i etkinleştirmek bölüm engeli verir. Testler var olan ödev fixture'ını doğrudan kurar; bu, gerçek bölüm doğrulamasından geçen oluşturma/seed testi değildir.

Fakes yalnız mevcut açık geliştirme ayarı veya test fixture'larında kullanılır. Üretim DI'da gerçek AUTH/CRS/FILE/ClamAV vardır. Fake tarayıcı yalnız test servis değişimidir. ASG'ye özel dosya yükleme JS'si ortak dosya yöneticisi hazır olduğunda değiştirilecek küçük adaptördür; Fatma Zehra'nın genel bileşeni yeniden yazılmadı.

## Kalan ekip bağımlılıkları ve PR öncesi durum

- **Sinem:** gerçek bölüm modeli ve ICourseSectionQuery; gerçek ödev oluşturma, ortak seed ve tam A/B E2E için zorunlu.
- **Seray:** LocalDiskFileStore eşzamanlı yazma testinde `AlreadyExisted=false` birden fazla sonuç gözlendi. Mevcut test korunmuştur; fiziksel depo değiştirilmemiştir. Test yeşil olmadan tüm regresyon geçti denemez.
- **İrem:** ASG yetenek uyarlaması onayla tamamlandı. Gelecek grup modeli/rol kapsamı ve genel kota/çerez güvenliği kararları ilgili modül sahipleriyle sürdürülmeli; bu çalışma yalnız ASG/draft yazılarına CSRF filtresi ekler.
- **Fatma Zehra:** ortak dosya yöneticisi ve temizlik entegrasyonu; GRD gerçek E2E kabulü bölüm bağımlılığı açıldıktan sonra yürütülecek. Karşılaştırma işleri bütünüyle kendisine aittir.
- **Ekip:** dış FK/silme, farklı DB dağıtımı, belirsiz commit sonrası onarım ve deploy antivirüs ayarları. PR öncesinde test başarısızlığı çözülmeli veya ekipçe açık engel olarak kabul edilmeli; gerçek tam E2E başarılı gösterilmemeli.
