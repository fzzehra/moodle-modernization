# WBS-3 Mevcut Alt Fonksiyonların Yapay Zekâ Desteğiyle Yeniden Geliştirilmesi

Sürüm planı ve kararlar: [WBS-1 §9](WBS-1-analiz.md). Yapay zekâ kayıt şablonu ve karşılaştırma tasarımı: [WBS-2 §4](WBS-2-tasarim.md). İzlenebilirlik: [izlenebilirlik.md](izlenebilirlik.md).

Herkes yalnızca kendi bölümünü düzenler. Her bölümde WBS-2 §4.3 şablonuyla yapay zekâ kaydı bulunur.

## 1. Birinci sürüm: AUTH, CRS, ASG, GRD (M gereksinimleri) ve altyapı

### 1.1 Altyapı ve ortak bileşenler (Seray)

### 1.2 Kimlik ve yetki – AUTH (İrem)

### 1.3 Ders ve kayıt – CRS (Sinem)

### 1.4 Ödev ve teslim – ASG ve uçtan uca akış testi (Aslı Ceren)

### 1.5 Değerlendirme – GRD (Fatma Zehra)

**Kapsam:** GRD-FR-01, FR-02, FR-03, BR-01, BR-02, NFR-01 (WBS-1 §4.5, WBS-2 §3.5).

**Yazılanlar**

| Katman | Dosyalar |
| --- | --- |
| Alan | `src/Moodle.Domain/Grading/GradingEntities.cs` |
| İş kuralları | `src/Moodle.Application/Grading/` (`GradeValidation`, `GradeVisibility`, `GradebookPaging`, `GradeResults`, `GrdConflictException`) |
| Veri ve servis | `src/Moodle.Infrastructure/Grading/` (`GrdDbContext`, `GrdService`, `InitialGrd` geçişi) |
| API | `src/Moodle.Api/Grading/`: `GET`/`PUT /assignments/{id}/learners/{learnerId}/attempts/{attempt}/grade`, `GET /courses/{id}/my-grades`, `GET /courses/{id}/gradebook` |
| Sayfalar | `src/Moodle.Web/Grd/`, `Pages/GradeEditor.razor`, `MyGrades.razor`, `Gradebook.razor`, `Grades.razor` |

**Testler:** 126 birim testi, 97 entegrasyon testi (Testcontainers ile PostgreSQL 17), 14 Node testi ve tarayıcı duman testi. Test adlarında gereksinim kimlikleri geçer.

**Tasarım kararları ve belgeden sapmalar**
- Düzen CRS ile aynıdır: iş mantığı `Infrastructure` içindeki serviste, saf kurallar `Application` içinde.
- Yetkisiz istek `403`, AUTH sahte gerçeklemedeyken `503 auth_not_ready` döner (CRS ile aynı).
- Kilitli veya elle değiştirilmiş nota puan yazma `409` ile reddedilir. Moodle'da da değerlendirme formu bu durumda puanı değiştirmez (`public/mod/assign/locallib.php:10025-10032`, `grading_disabled`).
- Daha yeni denemesi olan öğrencide eski denemenin puanı defteri ezmez (Moodle'daki gibi).
- Ders ve kategori toplamları 1. sürümde hesaplanmaz; toplam politikası tanımlanınca eklenecek.
- Not defterinde öğrenciler kimlikle görünür; AUTH'ta kullanıcı bilgisi arayüzü gelince ad gösterilecek.

**Yapay zekâ kaydı**

**Model ve sürüm:** Claude Code (Claude Opus 5.5, düşünme seviyesi: Medium). Adım promptları ve çıktıların incelenmesi için ayrıca Claude (claude.ai) sohbet asistanı kullanıldı.
**Kullanılan adım(lar):** Kodun anlaşılması, hedef dilde kod üretme, test senaryosu üretme (WBS-2 §4.1).
**Verilen bağlam:** WBS-1 §4.5, WBS-2 §2 ve §3.5, CRS modülünün kodu (desen olarak), `Application/Abstractions` arayüzleri ve sahte gerçeklemeleri.
**Yapılan iş:** Beş adımda veri modeli ve geçiş, puan okuma ve yazma, öğrenci not raporu, not defteri ve Blazor sayfaları ile bunların testleri üretildi.
**Yapılan düzeltmeler:**
- Yapay zekânın kendi düzelttikleri: Geçiş gereksiz iki indeks nedeniyle bir kez yeniden üretildi. Test verisindeki bir hata mesajı düzeltildi. Bir testte dizilerin karşılaştırma biçimi düzeltildi. Sayfa adımında iki derleme öncesi düzeltme ve bir test verisi tür hatası giderildi. Bir gerçek hata yakalandı: sunucudan `422` geldiğinde puan alanı ekran okuyucuya geçersiz olarak bildirilmiyordu.
- İnsan incelemesi: Her adımın raporu belgeyle karşılaştırıldı. Belgeden sapmalar tek tek değerlendirilip kabul edildi (yukarıdaki kararlar). Koda elle düzeltme gerekmedi.

**Harcanan süre:** Adım süreleri toplamı yaklaşık 1,1 saat (18.11.2026, 20:30–21:52). Bu süre Claude Code'un çalışmasını ve insanın prompt hazırlama, okuma ve karar verme süresini birlikte içerir; modelin kendi çalışma süresi ayrıca ölçülmedi. Doküman adımları bu süreye dahil değildir.

| Adım | İş | Süre |
| --- | --- | --- |
| 1 | Ortamın ve CRS deseninin incelenmesi | ~16 dk |
| 2 | Veri modeli, tablolar, geçiş | ~9 dk |
| 3 | Puan okuma ve yazma | ~12 dk |
| 4 | Öğrenci not raporu ve not defteri | ~8 dk |
| 5 | Blazor sayfaları | ~19 dk |

**Elle geliştirme:** GRD'de elle yazılmış işlev yoktur. Karşılaştırma için elle yazım eforu COCOMO II ile **tahmin** edilmiştir (§3). Hocanın şartı "seçilmiş bazı alt fonksiyonlar" olduğundan karşılaştırma diğer modüllerin çiftleriyle yapılır.

**Kanıt:** PR #__

## 2. İkinci sürüm: FILE, ödeve dosya teslimi, AUTH ve CRS'nin S/C gereksinimleri

### 2.1 Dosya altyapısı (Seray)

### 2.2 Dosya kaydetme, indirme, silme ve kota (İrem)

### 2.3 AUTH ve CRS'nin kalan gereksinimleri (Sinem)

### 2.4 Dosya yükleme, antivirüs, ödeve dosya teslimi ve uçtan uca test (Aslı Ceren)

### 2.5 Temizlik görevi, ders dosyaları ve rapor (Fatma Zehra)

## 3. Yapay zekâ destekli ve elle geliştirme karşılaştırması

Tasarım: WBS-2 §4.4, kriter K17 (eşik yok, raporlanır). Veriler herkesin yapay zekâ kaydından ve PR geçmişinden alınır. Bu tablo herkesin kaydı tamamlanınca doldurulur.

| Modül | İşlev | Üretim | İnsan eforu (saat) | Satır | İnceleme ve testte bulunan hata | Hata / 1.000 satır | İnceleme turu | Değişen satır oranı | Kaynak |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Altyapı | Tohumlama | YZ | | | | | | | |
| Altyapı | Hata gövdesi ve mimari testi | Elle | | | | | | | |
| AUTH | Giriş ve çıkış | YZ | | | | | | | |
| AUTH | Hesap durumu ve başarısız giriş kaydı | Elle | | | | | | | |
| CRS | Ders oluşturma | YZ | | | | | | | |
| CRS | Derse kayıt | Elle | | | | | | | |
| ASG | Teslim kaydetme | YZ | | | | | | | |
| ASG | Teslim listesi | Elle | | | | | | | |
| GRD | Puan yazma | YZ | | | | | | | |
| GRD | Kişisel not raporu | Elle yapılmadı; efor COCOMO II tahmini | | | — | — | — | — | |

2. sürümün çiftleri o sürüm bitince bu tabloya eklenir.

**Yorum:** Doldurulduktan sonra yazılacak. Sınırlılıklar da burada belirtilecek: örneklem küçük, geliştiriciler farklı, GRD'de elle veri yok.
