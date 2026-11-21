# WBS-3 v2 — main 65cd11d birleşme doğrulaması (21.11.2026)

Bu kayıt önceki PR #39 / 708 test sonucunu değiştirmez; güncel birleşme doğrulamasını ayrı kaydeder.

## Git ve üç çakışmanın çözümü

Başlangıç çalışma ağacı temizdi. Aktif dal codex/asg-file-wbs3-v2, HEAD aa2e62e idi. Fetch sonrası main 65cd11d (Fatma FILE PR #40) incelendi. git merge --no-commit --no-ff origin/main çalıştırıldı. Commit, push, rebase, force push ve main'e birleştirme yapılmadı.

- Program.cs: ASG draft uçları ve antiforgery token ucu ile main'in FileAdmin uçları birlikte korundu. AUTH, CRS, ASG, FILE, GRD servisleri ve endpoint'leri yerinde. Hata middleware'i; authentication; authorization; antiforgery; endpoint eşlemesi sırası korundu.
- NavMenu.razor: ana sayfa, dersler, notlar, ASG ders seçimi, dosya ayarları, giriş/çıkış ve oturum bilgileri korundu. ASG bağlantısı sabit courseId=1 kullanmaz; gerçek ders seçimine gider.
- izlenebilirlik.md: üç yönlü, hücre bazında birleşme. Yerel 53 ve main 55 gereksinim satırından birleşen 55 satır; iki taraftaki değişen hücrelerin kaybolmadığı kontrol edildi. Ekip kayıtları silinmedi.

## Otomatik birleşme sonrası gerekli uyarlamalar

- Ortak FILE tarayıcı taşımasına draft yazımlarında gerçek cookie CSRF token başlığı eklendi. PathBase, same-origin, yönlendirme reddi ve zaman aşımı korunur. Eksik/geçersiz token olduğunda yazma isteği gönderilmez.
- Ortak FileUploadResult.Size, gerçek API'nin sizeBytes alanına bağlandı. Ortak uygulama arayüzlerinin imzaları değiştirilmedi.
- Yeni main testlerinin FileService kurucuları v2 DraftUploadService bağımlılığıyla güncellendi. Test-only temiz tarayıcı kullanılan fixture, gerçek ClamAV testi olarak gösterilmez.
- Yetkilendirme testlerinin düz metin içerikli .pdf dosyaları .txt olarak düzeltildi; tüm erişim, silme, listeleme ve indirilen içerik beklentileri korundu.
- Yükleme endpoint'i artık mevcut olduğundan anonim UI istemci testi upload_unavailable yerine gerçek 401/not_authenticated reddini doğrular.
- DI'da otomatik birleşmeden gelen yinelenen using temizlendi. Seray'ın LocalDiskFileStore kaynağı ve testi değiştirilmedi.

## Veri bütünlüğü düzeltmesi — kullanıcı onayıyla uygulandı

Düzeltme öncesi FileMaintenanceService.cs:142–153, blob satırını transaction içinde silip commit ettikten sonra fiziksel içeriği siler. Bu arada DraftUploadService.cs:71 üzerindeki hash kilidiyle yeni yükleme aynı içeriği yeniden kullanabilir. Temizlik bu kilide katılmadığı için yeni blob/entry kayıtları kalırken içerik diskten kaybolur.

Yeni FileMergeCompatibilityTests.Cleanup_and_reupload_of_same_content_preserve_new_file testi, PostgreSQL 17 ve gerçek LocalDiskFileStore ile bu sıralamayı deterministik olarak oluşturdu. Son Assert.NotNull başarısız: kayıtlar vardır, fiziksel içerik yoktur. Test kaldırılmadı, atlanmadı veya zayıflatılmadı. Ayrı çalıştırma kanıtı coverage/v2-merge-race altındadır.

Kullanıcı 21.11.2026 tarihinde kilitleme uyarlamasını ve regresyon testini açıkça onayladı. Temizlik artık DraftUploadService ile aynı SHA-256 ilk sekiz bayt / big-endian bigint anahtarında PostgreSQL oturum kilidi alıyor. Kilit, blob transaction commit'i ile fiziksel silme arasındaki aralık dahil tutuluyor; finally içinde CancellationToken.None ile bırakılıyor. Yükleyicinin mevcut transaction kilidi ve Seray'ın fiziksel depo kodu değişmedi. Regresyon, başarılı silme yanında disk hatası ve iptalde de yeni yüklemenin tamamlanmasını ve byte bütünlüğünü doğrulayacak üç senaryoya genişletildi. Eski başarısız sonuçlar tarihsel kanıt olarak korunur.

## Entegrasyon sınırları

Yeni FILE DeletedAt index migration'ı ve snapshot korunmuştur. FILE snapshot/model uyumu PostgreSQL 17 üzerinde ayrıca test edildi. Önceki ASG/CRS migration sırası testleri ve bütün modül testleri korunmuştur. Compose gerçek uygulamasında migration/startup başarılıdır.

Gerçek AUTH sağlayıcıları varsayılan DI yoludur. Development sahte sağlayıcıları açık yapılandırma gerektirir; Production kısıtlaması korunur. CRS bölüm modeli ve gerçek ICourseSectionQuery hâlâ yoktur; UnavailableCourseSectionQuery ile güvenli ret sürer. Bu bağımlılık için sahte bölüm veya doğrudan SQL ile gerçek kabul testi izlenimi yaratılmadı.

Fatma'nın ortak dosya yöneticisi main ile geldi; ders dosyaları ve yönetici sayfası korunur. ASG'nin sürüm kontrollü dosyalı teslim adaptörü korunmuştur; genel Commit yoluyla ASG yetki/sürüm denetimi atlanmaz.

## Doğrulama

İlk birleşme denemesi: 436 birim geçti; entegrasyon 396 geçti / 9 başarısız. Sekiz hata geçersiz PDF test verisi, biri eski endpoint-yok beklentisiydi. Bunlar yukarıdaki uyarlamalarla düzeltildi; bu ilk sonuç saklanır.

JavaScript: 54/54 geçti. Mevcut ASG/CRS/GRD 23 teste main'in FILE testleri ve üç geçersiz CSRF token testi eklendi.

Playwright: 4 geçti / 2 atlandı / 0 başarısız. Atlananlar tam A/B AUTH/CRS/ASG/FILE/GRD senaryolarıdır; gerçek CRS bölüm modeli/ICourseSectionQuery bağımlılığı nedeniyle çalışmaz. Geçenler gerçek giriş ve ASG listesi, bölüm yokken ret, gerçek ClamAV temiz/EICAR ve benzersiz ad, başka öğrencinin taslağına rettir.

Compose build/start başarılı, PostgreSQL 17 ve ClamAV sağlıklı. HTTP health/login/courses/OpenAPI 200, anonim draft limits 401. Kaynak salt okunur resmî SDK konteynerinde derleme /tmp altında; test çıktıları Git dışı coverage dizinlerindedir. Ham Playwright trace'leri teslim kapsamına alınmadı.

Release derleme başarılı. Seray'ın mevcut testindeki xUnit2031 analiz uyarısı korunur; test değiştirilmedi. Önceki toplu .NET sayıları DOGRULAMA.json/main65cd11dMerge, son yeşil sonuç main65cd11dLockFix alanında kaydedilir.

Git whitespace/çakışma işareti kontrolü temiz. Teslim adaylarında özel anahtar/token örüntüsü ve build çıktısı bulunmadı; bu tarama her olası sır biçiminin yokluğunu kanıtlamaz.

COCOMO, karşılaştırma, manuel geliştirme ve ekip kod incelemesi bu çalışmada yapılmadı.

Düzeltme öncesi toplu sonuç (tarihsel): **436/436 birim; 406 geçti / 1 başarısız / 407 entegrasyon; toplam 842 geçti / 1 başarısız / 843 .NET; 0 atlanan.** Tek hata yeni temizlik-yükleme veri bütünlüğü regresyonudur. Önceki 708 testin adları TRX sonuçlarıyla karşılaştırıldı; kaybolan test sayısı: 0. JavaScript 54/54; Playwright 4 geçti / 2 atlandı. Son tam çalıştırma: coverage/v2-merge-final.

## Onaylı düzeltme sonrası son sonuç

436/436 birim + 409/409 entegrasyon = **845/845 .NET**, başarısız/atlanan yok. Başarılı silme, disk hatası ve iptalde içerik korunması 3/3; FILE model snapshot kontrolü geçti. Temizlik servisinin önceki sekiz testi, gerçek ClamAV üç testi ve ASG/CRS/AUTH/GRD/FILE regresyonları geçti. Önceki 708 testten kaybolan yok. JavaScript 54/54; gerçek uygulamada Playwright 4 geçti / 2 bölüm bağımlılığı nedeniyle atlandı. Release ve Compose yeniden derlendi, HTTP kontrolleri tekrar geçti. Kanıt: coverage/v2-merge-lock ve coverage/playwright.

Yeni teknik regresyon engeli giderildi. Gerçek CRS bölüm modeli / ICourseSectionQuery ve buna bağlı tam A/B kabulü ekip bağımlılığı olarak kalır. Git metinsel çakışmaları çözülmüş, bütün değişiklikler indekste; HEAD aa2e62e ve MERGE_HEAD 65cd11d. Commit/push/PR merge yapılmadı. Merge commit oluşturmak için kullanıcının ayrı onayı beklenir.
