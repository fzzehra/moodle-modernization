# Yapay zekâ ve efor kaydı (Seray)

WBS-2 §4.3 şablonuna göre. Süre canlı tutulmamıştır: **YZ ile çalışma süresi oturum kaydından** (iki mesaj arası 15 dakikadan kısa süreler toplanarak, yani alt sınır), **insan süresi** (yönlendirme, inceleme, commit, PR ve çakışma çözümü) **tahmin** olarak yazılmıştır.

## 1. Altyapı ve iskelet

**Model ve sürüm:** Claude Sonnet 5.5 (Claude Code)
**Kullanılan adım(lar):** Kod üretimi, test yazımı, CI ve Docker dosyaları
**Verilen bağlam:** WBS-2 §2.2–2.3, §3.6.4, "görev dağılım2.pdf" Seray bölümü
**Yapılan iş:** .NET çözümü ve 5 proje, 5 ortak arayüz ve sahte gerçeklemeleri, tek tip hata gövdesi, OpenAPI, Blazor ana düzeni, katman kurallarını denetleyen mimari testi, Docker Compose, GitHub Actions (derleme, test, kapsam) ve CodeQL
**Yapılan düzeltmeler:** OpenAPI ve Mvc.Testing paketleri önizleme sürümü (11.0.0) olarak çözümlenip derleme hatası verdi, 10.0.13'e sabitlendi. Şablondan kalan gereksiz dosyalar silindi. İnsan düzeltmesi: yok.
**Harcanan süre:** İnsan: ~1 saat (tahmin) · YZ ile çalışma: ~0,5 saat (oturum kaydı)
**Kanıt:** PR #19

## 2. Tohumlama

**Model ve sürüm:** Claude Sonnet 5.5 (Claude Code)
**Kullanılan adım(lar):** Kod üretimi, test yazımı
**Verilen bağlam:** `ortam/ornek-veri.feature`, Sinem'in CRS tabloları (`CrsDbContext`)
**Yapılan iş:** `SampleDataset`, `ISeedContributor`, `SeedRunner` ve CRS örnek veri yükleyicisi (`CrsSeedContributor`) ile testleri. Testler, veri kümesinin `ornek-veri.feature` ile aynı kaldığını da denetler.
**Yapılan düzeltmeler:** İlk testte tablo satırlarını sayma hatası bulundu ve düzeltildi. İlk taslakta ayrı bir `AppDbContext` yazılmıştı; modüller kendi bağlamlarını getirdiği için PR çakışmasında silindi. İnsan düzeltmesi: yok.
**Harcanan süre:** İnsan: ~1 saat (tahmin) · YZ ile çalışma: ~0,5 saat (oturum kaydı)
**Kanıt:** PR #25 ve sonraki PR (CRS tohumlama)

## 3. Dokümanlar (WBS-1 §4.6 ve §7; WBS-2 §1, §2, §3.6)

**Model ve sürüm:** Claude Sonnet 5.5 (Claude Code)
**Kullanılan adım(lar):** Kod okuma ve analiz, doküman taslağı, literatür taraması
**Verilen bağlam:** Moodle 5.3.1 kaynak kodu (`cfe123f2`), WBS-1 ve WBS-2'nin mevcut bölümleri, web araması
**Yapılan iş:** Dosya yönetimi analizi ve REST API tasarımı, dil ve teknoloji seçimi, mimari ve dağıtım diyagramları, sürüm planı güncellemesi, literatür bölümü
**Yapılan düzeltmeler:** `assign_pluginfile` yanlış yorumlanmıştı (teslim dosyaları `assignsubmission_file` üzerinden sunuluyor); satır numaralarıyla birlikte koddan doğrulanıp düzeltildi. Testle doğrulanmayan hata maddeleri (FILE-BUG) "aday", test sonuçları "Bekliyor" olarak işaretlendi. Literatür kaynakları web aramasıyla bulundu, makalelerin tamamı okunmadı. İnsan düzeltmesi: yok.
**Harcanan süre:** İnsan: ~1 saat (tahmin) · YZ ile çalışma: ~0,5 saat (oturum kaydı)
**Kanıt:** PR #6, #14, #16

## Elle yazım eforu tahmini (COCOMO)

Hiçbir işlev elle yazılmaz (hoca kararı, 18.11.2026): karşılaştırma, YZ ile harcanan gerçek süre ile aynı kodun elle yazılsaydı gerektireceği eforun tahmini arasındadır. COCOMO hesabı raporda herkes için aynı kurallarla toplu yapılır; aşağıdaki tablo ön tahmindir. Basic COCOMO (organik), `Efor = 2,4 × KLOC^1,05` kişi-ay, 152 saat/kişi-ay. Satır sayısı boş ve yorum satırları hariç.

| İş | Satır | Tahmini elle efor |
| --- | --- | --- |
| Hata gövdesi (`ApiErrorMiddleware`, `AppException`) | 78 | ~25 saat |
| Mimari testi | 42 | ~13 saat |
| Tohumlama iskeleti (`Seeding/`) | 119 | ~39 saat |
| CRS tohumlama (`CrsSeedContributor`) | 73 | ~23 saat |
| `FileCourseDeletedExtension` | 30 | ~9 saat |

COCOMO büyük projeler için geliştirilmiştir; bu kadar küçük kodda tahminler yüksek çıkar. Rakamlar karşılaştırma için göstergedir, kesin ölçüm değildir.

## 4. FILE testlerinin eski Moodle'da çalıştırılması (T-FILE-01–16)

**Model ve sürüm:** Claude Sonnet 5.5 (Claude Code)
**Kullanılan adım(lar):** Ortam kurulumu (moodle-docker), test betiği yazımı (PHP komut satırı, curl, k6), sonuçların dokümana işlenmesi
**Verilen bağlam:** WBS-1 §4.6.10 test senaryoları, `ortam/README.md`, Moodle 5.3.1 kaynak kodu (`cfe123f2`)
**Yapılan iş:** Moodle'ın kurulması ve örnek verinin yüklenmesi; 16 senaryonun çalıştırılması ve eski sistem sonuçlarının `WBS-1` ve `izlenebilirlik.md`'ye yazılması; FILE-BUG-01, 02 ve 03'ün doğrulanması; T-FILE-15 için k6 başlangıç ölçümü
**Yapılan düzeltmeler:** Beklenen sonuçlardan biri yanlış çıktı: T-FILE-02'de "taslak alanı temizlenir" beklenmişti, Moodle taslağı kaydetmeden sonra silmiyor (cron 4 gün sonra siliyor); sonuç "beklenenden farklı" yazıldı. İlk betikte dosya sahibi (`userid`) kaydı ve `get_unused_filename` argümanları hatalıydı, düzeltildi. k6'da önce yanlış dosya yolu kullanıldı (HTTP 500), düzeltildi. k6 ölçümünde 50 kullanıcı tek oturumu paylaştı; bu sınır dokümanda belirtildi. İnsan düzeltmesi: yok.
**Harcanan süre:** İnsan: ~1 saat (tahmin) · YZ ile çalışma: ~0,7 saat (oturum kaydı, alt sınır)
**Kanıt:** `docs/WBS-1-analiz.md` §4.6.10, `docs/izlenebilirlik.md` (FILE satırları)

## 5. `course.deleted` dinleyicisi (FILE ↔ CRS)

**Model ve sürüm:** Claude Sonnet 5.5 (Claude Code)
**Kullanılan adım(lar):** Kod üretimi, test yazımı
**Verilen bağlam:** CRS'nin `ICrsEventExtension` altyapısı, `CourseFilePolicy`, `FileService`
**Yapılan iş:** `FileCourseDeletedExtension` ve gerçek PostgreSQL ile 4 entegrasyon testi
**Yapılan düzeltmeler:** Düzeltme gerekmedi. İnsan düzeltmesi: yok.
**Harcanan süre:** İnsan: ~0,3 saat (tahmin) · YZ ile çalışma: ~0,3 saat (tahmin; oturum kaydı alt sınırı ~0,1 saat)
**Kanıt:** `seray/file-course-deleted`
