> **22.11.2026 güncel teslim:** Gerçek bölüm entegrasyonu tamamlandı. Release, .NET **1008/1008**, JS **54/54**, Playwright **9/9**; 0 skip. Aşağıdaki eski bölüm engeli/açık PR kayıtları tarihseldir. [Son mimari ve kanıt](SON-BOLUM-ENTEGRASYONU.md).

> **21.11.2026 — güncel son FILE entegrasyonu:** PR #40/#42 main'de; codex/asg-file-final-integration dalında 939/939 .NET, 54/54 JS, Playwright 6 geçti / gerçek CRS bölüm bağımlılığı nedeniyle 2 atlandı. Ortak dosya yöneticisi, süreçler arası PostgreSQL kilidi ve MIME davranışı doğrulandı. Yeni üretim kaynağı değişikliği gerekmedi; testler ve kayıtlar tamamlandı. Commit/push yapılmadı. [Son rapor](SON-FILE-ENTEGRASYONU.md); DOGRULAMA.json → finalFileIntegration. Aşağıdaki önceki durumlar tarihseldir.

> **21.11.2026 — son durum:** Kullanıcı onayıyla temizlik/yükleme veri kaybı düzeltildi. 845/845 .NET ve 54/54 JavaScript geçti; Playwright 4 geçti / CRS bölüm bağımlılığı nedeniyle 2 atlandı. Üç metinsel çakışma çözülmüş durumda; commit/push yapılmadı, merge commit onayı bekleniyor. Ayrıntılar [BIRLESME-65cd11d.md](BIRLESME-65cd11d.md), makine özeti DOGRULAMA.json → main65cd11dLockFix. Aşağıdaki önceki sonuçlar tarihsel kayıttır.

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


## 21.11.2026 — 02017af sonrası nihai ASG entegrasyonu
Release başarılı; **501 birim + 494 entegrasyon = 995/995 .NET**, 0 başarısız/atlanan. Önceki 939 test adı yeni TRX içinde korundu. JavaScript **54/54**; Playwright **6 geçti / 2 gerçek CRS bölüm bağımlılığı nedeniyle atlandı**. PostgreSQL 17, gerçek ClamAV temiz/EICAR, beş context migration/snapshot ve HTTP 200/401 kontrolleri geçti.
Yeni ASG/GRD temizleme testleri: 8/8; gerçek CRS öğrenci rol/tarih/suspension: 6/6; gerçek AUTH HTTP ders silme + DI worker: 1/1. İlk hedefli ASG koşusu 7/7; sonraki test eklenirken namespace derleme hatası düzeltildi, final çözüm build/test geçti.
91 benzersiz gereksinim, 9 sütun; çakışma işareti/hassas anahtar kalıbı/gereksiz build çıktısı yok. Ham TRX ve loglar ignored coverage/asg-cleanup-regression altındadır.
[Güncel entegrasyon ve kalan bağımlılıklar](NIHAI-ASG-FILE-ENTEGRASYONU.md), [makine özeti](DOGRULAMA.json) / finalAsgCleanupIntegration. Önceki sonuçlar tarihsel kayıttır.


## Güncel GitHub doğrulaması — main a21e18a
Release ve **995/995 .NET (501 birim + 494 entegrasyon)**, **54/54 JavaScript**, **Playwright 7 geçti / 2 CRS bölüm bağımlılığı nedeniyle atlandı**. Önceki 995 test korundu. PostgreSQL 17, gerçek ClamAV, beş model snapshot/migration ve Compose/HTTP kontrolleri başarılı.
Pozitif SiteAdmin tarayıcı kabulü test-only gerçek Identity kullanıcıyla tamamlandı; kullanıcı koşudan sonra silindi. Seed hâlâ SiteAdmin örnek hesabı sağlamaz. İlk iki fixture giriş hatası gizlenmedi: e-posta eklemek tek başına çözmedi; kesin neden ValueGeneratedNever modelinde Id=0 bırakılmasıydı. Pozitif benzersiz kimlikle düzeltildi; fixture Release derlemesi ve tüm Playwright tekrarlandı.
Main'e yeni commit gelmedi; açık PR #50 (b0c7cdd) genel ders dosyası silme temizliğini içerir, main'e alınmadığından tamamlanmış sayılmadı. Gerçek bölüm için Sinem'in entity/migration/query/DI uygulaması gerekir.
[Ayrıntılı modül ve eksik görev tablosu](GUNCEL-GITHUB-DOGRULAMASI.md); [PR taslağı](PR-TASLAGI-GUNCEL-DOGRULAMA.md); ham kanıt coverage/current-verification. Commit/push/PR yok.

## ASG–CRS WBS kontrolü (21.11.2026)
[Güncel kapsam kararı](ASG-CRS-WBS-KARARI.md): WBS-1 ASG-FR-01/T-ASG-02 ve WBS-2 §3.4.1/2 bölüm ilişkisini zorunlu kılar. CourseSection adlı sınıf veya ICourseSectionQuery adlı arayüz WBS-2'de isimle zorunlu değildir; arayüz önceki onaylı ek sözleşmedir. Yalnız CourseId ile kabul edilmesi gereksinim değişikliği onayı ister. Genel ödev güncelleme/silme endpoint'i mevcut ASG WBS kapsamında zorunlu değildir. Yeni CRS modeli/üretim değişikliği yapılmadı.
Önceki doğrudan “Sinem görevi” ifadesi, CRS WBS'sinde açık CourseSection görevi bulunduğu anlamına gelmez; gerçek bölüm sağlayıcısının sorumluluğu ekip kararıdır.
Fetch sonrası PR #50 artık main 4d53353 içinde merged; önceki “açık PR #50” ifadeleri önceki kontrolün tarihsel sonucudur. Mevcut dal a21e18a tabanında kaldı; yeni main merge edilmedi, bu yüzden yeni FILE uzantısının testleri yerel toplamda sayılmaz.


Son yeniden koşu (a21e18a tabanlı dal): Release başarılı; .NET 995/995 (501 birim + 494 entegrasyon), JS 54/54; Playwright 7 geçti / 2 bölüm bağımlılığı nedeniyle atlandı, 0 başarısız. PostgreSQL 17, gerçek ClamAV ve migration/snapshot regresyonları geçti. SiteAdmin fixture silindi. Whitespace/conflict kontrolleri temiz. Yeni main 4d53353 bu dala merge edilmedi; yeni PR #50 testleri bu toplamda yoktur.


## 22.11.2026 — son kalıcı bölüm entegrasyonu (güncel sonuç)
Main **4d53353** stash/hash yedeğiyle alındı; önceki çalışmalar korundu. Gerçek kalıcı bölüm sorgusu ve yetkili/CSRF korumalı GET/POST uçları, CRS migration/snapshot, açık course.managefiles ve güvenli isteğe bağlı yerel SiteAdmin seed'i tamamlandı. PR #50 dosya temizliği içerik kilidi/transaction ile, ders dosyası commit/silme yarışı satır kilidiyle tamamlandı.
**Release başarılı; 505 birim + 503 entegrasyon = 1008/1008 .NET**, 0 başarısız/atlanan. Önceki 995 test korundu; +4 PR #50 ve +9 bu görev. **JS 54/54; Playwright 9/9, 0 skip.** PostgreSQL 17, gerçek ClamAV, beş migration/model snapshot, Compose/HTTP ve kilit/rollback/retry regresyonları geçti.
ASG-FR-01/T-ASG-02 gerçek bölümle kapandı; A/B gerçek kullanıcı/kayıt/ders/bölüm/teslim/GRD akışıdır. Eski bölüm bağımlılığı ve 7/2 sonuçları tarihsel kayıttır. Test kullanıcıları/ders/kategoriler temizlendi. Varsayılan yönetici parolası eklenmedi.
[Ayrıntılı mimari, migration ve riskler](SON-BOLUM-ENTEGRASYONU.md); [PR taslağı](PR-TASLAGI-SON-TESLIM.md); DOGRULAMA.json/finalSectionDelivery; ham kanıt coverage/section-delivery.
Olay temizliği sonunda tutarlıdır; CRS/FILE/ASG/GRD aynı PostgreSQL'de olmalıdır. Bölüm/grup bazlı ayrı öğrenci üyeliği kapsam dışıdır. Mevcut WBS kapsamındaki bağımsız ASG/FILE eksikleri kapanmıştır. Commit/push/PR yok.
