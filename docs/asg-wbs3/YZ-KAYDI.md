# ASG WBS-3 — Yapay zekâ ve efor kaydı

17 Kasım 2026, Europe/Istanbul. Kullanıcı: Aslı Ceren Can. Başlangıç: `db2948e4aa542b227a83a41b71b97b90d36d0863`; dal: `codex/asg-wbs3-implementation`.

**Model ve sürüm:** OpenAI Codex masaüstü ajanı, GPT-6 tabanlı. Kesin alt model/sürüm ve düşünme seviyesi bu oturumda doğrulanabilir metadata olarak sunulmadı; tahmin edilmedi.

**Kullanılan adım(lar):** Gereksinim/tasarım incelemesi, kod ve test üretimi, yerel doğrulama, dokümantasyon. İnsan kod incelemesi yapılmış sayılmıyor.

**Verilen bağlam:** Kullanıcının “WBS-3 – ASG Modülünün Eksiksiz Geliştirilmesi” başlıklı 23 bölümlü görev metni; WBS-1 §4.4; WBS-2 §2.3, §3.4, §4.3; izlenebilirlik; ortam rehberi ve ortak feature; eski ASG karakterizasyonu ve kanıtları; mevcut beş .NET projesi, ortak arayüzler, sahteler, Docker ve test/CI yapılandırması. Eski kanıtlar değiştirilmedi.

**Prompt/görev özeti:** ASG-FR-01/02/03, ASG-BR-01/02 ve ASG-NFR-01'i alan/uygulama/API/EF/Blazor katmanlarında geliştir; gerçek IAssignmentQuery bağla; her M gereksinimine birim ve PostgreSQL entegrasyon testi ekle; Playwright, diğer modüllerin geliştirilmesi, commit/push/PR/merge yapma; gerçek test sonuçlarını belgele. Tam özgün görev metni Codex sohbetindeki kullanıcı ekidir; gizli hesap veya oturum verisi kopyalanmadı.

**Kullanıcı kararları:**
- “Yeni bölüm sorgusu sözleşmesini ekle”: mevcut ICourseDirectory değiştirilmeden ICourseSectionQuery ve geçici FakeCourseSectionQuery eklendi.
- “WBS-1 kimliklerini koru”: BR-01 gönderme onayı; BR-02 işlem anında yetki ve teslim penceresi. Sürüm çakışması WBS-2 ek davranışı olarak test edildi; gereksinim kimliği yeniden tanımlanmadı.

**Yapılan iş:** Teslim raporundaki yeni ASG kaynakları, migration/model snapshot, 19 yeni birim testi ve 20 yeni entegrasyon testi; sadece ASG izlenebilirlik satırları; kullanım ve kanıt kayıtları. Teslim listesi dahil tüm yeni kod YZ tarafından üretildi. EF migration dosyaları EF CLI ile oluşturuldu.

**Yapılan düzeltmeler:**
1. Windows Git, WSL sahipliği nedeniyle repository'yi güvenilir bulmadı; global safe.directory değiştirilmedi, Git WSL içinde kullanıldı.
2. WSL sandbox erişimi reddedildi; izinli WSL çağrılarıyla devam edildi. WSL'de dotnet bulunmadığından .NET 10 SDK konteyneri kullanıldı.
3. apply_patch UNC yoluna yazamadı; PowerShell dosya yazımı kullanıldı.
4. İlk ASG derlemesinde SQL check constraint metinlerindeki üç raw-string sınırı hatalıydı (9 derleyici tanısı); ifadeler parantezle sınırlandırıldı.
5. İkinci derlemede EF Design 10.0.13 ile transitif EF 10.0.0 sürüm çakıştı (CS1705). Infrastructure'a EF Relational 10.0.13 doğrudan referansı eklenerek sürümler hizalandı. Web'de FrameworkReference ile gereksiz hale gelen Components.Web paket referansı kaldırıldı.
6. Kapsam/işlev incelemesinde gerçek IAssignmentQuery'nin birim testi, Blazor GET ve gerçek SSR form POST testleri, seed idempotans testi, bozuk JSON için standart 400 eklendi. Bunlar insan düzeltmesi değil ajan öz incelemesidir.
7. İlk diff kontrolünde PowerShell kaynaklı EOF boş satırları saptandı ve düzeltildi. .NET File API'ye provider nitelemeli PowerShell yolu verilmesi normalizasyon denemesinde hata verdi; Get-Content/Set-Content kullanıldı. Docker'ın root sahipli migration/coverage dosyaları kullanıcının sahipliğine geçirildi.
8. TRX ek dosya adındaki [1] karakterinin PowerShell wildcard olarak yorumlanması, kanıt özeti çıkarımında hata verdi. LiteralPath kullanılarak JSON gerçek iki TRX dosyasından yeniden oluşturuldu.
9. Sandbox yerel HTTP bağlantısını engelledi; izinli çağrıyla Compose uygulamasına erişilip gerçek sonuçlar alındı.

**Test ve doğrulama sonuçları:** Başlangıç 8 birim/mimari + 7 entegrasyon geçti. İlk ASG turu 26 + 22; ek testlerle 27 + 25; üç odaklı Blazor testi geçti. Son kod turu 27 + 27 = 54 geçti, 0 başarısız, 0 atlandı. İki başarısız derleme turu yukarıda kayıtlıdır. Son derleme/testlerde uyarı veya hata yok. GitHub CI ve CodeQL uzakta çalıştırılmadı. Playwright yazılmadı/çalıştırılmadı.

**Harcanan süre:** İnsan: ölçülmedi. YZ çalışma süresi: ayrı bir kronometre tutulmadığı için toplam süre beyan edilmiyor. Test yürütücüsü son birim koşusunu yaklaşık 55 ms, entegrasyonu yaklaşık 12 sn olarak raporladı; bunlar toplam geliştirme eforu değildir. TRX dosyaları kesin koşu zamanlarını içerir.

**Kanıt:** `coverage/delivery/*.trx` ve iki benzersiz `coverage.cobertura.xml` yerel, gitignore kapsamındadır. Paylaşılabilir özet: `DOGRULAMA.json`; ayrıntılı sonuç ve komutlar: `TESLIM-RAPORU.md`. Commit veya PR oluşturulmadı.

## Manuel/YZ karşılaştırması

Karşılaştırma çifti değiştirilmedi: teslim kaydetme YZ, teslim listesi manuel olarak planlanmıştı. **Bu çalışmada ikisi de YZ ile üretildi; manuel geliştirme şartı karşılanmadı.** Teslim listesini manuel olarak etiketlemek yanlış olur. Kontrollü manuel ölçümün nasıl yapılacağı ekip tarafından ayrıca kararlaştırılmalıdır.

İki başarısız derleme turu, yapılan teknik düzeltmeler, eklenen test sayıları ve yerel TRX/kapsam çıktıları sonradan değerlendirilebilir gerçek verilerdir. İnsan inceleme turu, insan eforu, üretim hata yoğunluğu veya PR sonrası re-work oranı henüz ölçülmedi; bunlar için sıfır değeri de varsayılmadı. Ajanın derleme düzeltmeleri insan yeniden çalışması olarak sayılmamalıdır.

## 18 Kasım 2026 — İnceleme sonrası düzeltme turu

Kullanıcının Y1/Y2/Y3/O1..O5/D1/D2 isteği esas alındı. Y2/Y3/O2..O5/D1/D2 kod ve testleri YZ ajanıyla geliştirildi. Y1/O1 için iki ortak sözleşme önerisi kullanıcı onayına sunuldu; yanıt beklenirken uygulanmadı. Gerçek AUTH/CRS/GRD geliştirilmedi. Manuel geliştirme karşılaştırmasının karşılanmadığı bilgisi aynen geçerlidir.

İlk yeni turda 41 birim geçti, entegrasyonda 52 geçti/2 ortam testi başarısız oldu. Host son yapılandırmasını doğrulama düzeltmesiyle sonraki tur 41+54 geçti. Son SSR sayfalama testi dahil Release/Coverlet turu 41+56=97 geçti; önceki 54 test tarihsel sonuçtur. Docker ilk port publish hatası yeniden denemede çözüldü; HTTP yoklaması sandbox soket kısıtı nedeniyle izinli ortamda tekrarlandı. Playwright/uzak CI/CodeQL çalıştırılmadı; commit/push/PR/merge yok. Kesin insan/YZ efor süresi ölçülmedi. Güncel sınırlar TESLIM-RAPORU.md'nin en üst bölümündedir.

## 18 Kasım 2026 — Sözleşme onayı sonrası son tur

Kullanıcı IAssignmentLearnerQuery.ListRequiredLearnerIdsAsync ve ISubmissionAccessPolicy.CanAssessLearnerAsync sözleşmelerini açıkça onayladı. Önceki bölümdeki onay bekleme durumu sona erdi. Mevcut ortak imzalar bozulmadan iki sözleşme, sınırlı dev/test sahteleri, güvenli boş/false fallback ve ortak List/Read hedef öğrenci denetimi eklendi. Mine/Save/Submit ve sunucu UI uygunluğu da yükümlü öğrenciyi doğrular. Gerçek AUTH/CRS/GRD yazılmadı.

20 yeni vaka eklendi; önceki 97 testin adları son TRX ile karşılaştırıldı, eksik 0. İlk kapsam turu 110, son Release/Coverlet turu 117 (52 birim/mimari + 65 PostgreSQL entegrasyon) başarılı; bu onay sonrası turlarda test başarısızlığı yok. Yeni DI ile Docker build/Compose ve dört HTTP yoklaması başarılı. Kanıt coverage/scope-delivery; önceki 54 ve 97 tarihsel sonuçları korundu.

Kod, test ve belgeler YZ ajanıyla üretildi. İnsan incelemesi yapılmış sayılmadı; manuel karşılaştırma karşılanmadı, süre tahmin edilmedi. Playwright kapsam dışı; uzak CI/CodeQL yok. Commit/push/PR/merge yapılmadı. İrem/Sinem geçiş belgesi ORTAK-SOZLESMELER.md olarak eklendi.
