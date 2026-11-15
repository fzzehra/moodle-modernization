# WBS-1 Kapsamın Belirlenmesi ve Mevcut Sistem Analizi

Moodle sürümü: 5.3 LTS

## 1. Geliştirme ve test ortamı (Fatma Zehra)

Ekibin hepsi Moodle'ı kendi bilgisayarında aynı sürüm, aynı kaynak kod ve aynı örnek veriyle çalıştırır. Kurulum adımları, sıfırlama ve sık karşılaşılan sorunlar: [ortam/README.md](../ortam/README.md).

| | |
| --- | --- |
| İncelenen sürüm | Moodle 5.3.1 (LTS; aktif destek Ekim 2027'ye, güvenlik desteği Ekim 2029'a kadar) |
| Kaynak kod revizyonu | `cfe123f26fc7071981fe949cb16cd5a08a8234f1` (Moodle release 5.3.1, 12.11.2026) |
| Çalıştırma | moodlehq/moodle-docker · PHP 8.3 · PostgreSQL 17 · Apache |
| Windows | WSL 2 (Ubuntu) içinde; kod Linux dosya sisteminde tutulur |
| Örnek veri | `ortam/ornek-veri.feature` (Moodle'ın test senaryosu aracıyla tek komutla yüklenir) |

**Örnek veri seti:** 3 kategori, 3 ders, 15 kullanıcı (yönetici, 2 öğretmen, asistan, danışman, 10 öğrenci), 4 ödev, 1 ders dosyası, 10 teslim ve 5 not. Veri; teslim edilip notlandırılmış, teslim edilip notlandırılmamış, taslakta kalmış ve hiç teslim edilmemiş ödev durumlarının hepsini içerir.

**Tekrarlanabilirlik:** Ortam `docker compose down` ile tamamen silinip yaklaşık 5 dakikada aynı verilerle yeniden kurulabilir. Böylece eski ve yeni sistemin karşılaştırmalı testleri her seferinde aynı başlangıç verisiyle çalıştırılır.

**Ortam kurulumunda öğrenilenler:**
- Windows'ta kodun `C:\` altından Docker'a bağlanması dosya erişimini çok yavaşlatıyor: veritabanı kurulumu yaklaşık 2 saat sürdü ve sayfalar dakikalarca açıldı. Kod WSL'in Linux dosya sistemine taşındığında aynı kurulum 3 dakikaya indi. Bu yüzden Windows kullanan herkes WSL içinde çalışır. Bu durum, eski sistemin performans ölçümlerinin de Linux dosya sisteminde yapılması gerektiğini gösteriyor; aksi hâlde Moodle'ın değil dosya erişiminin yavaşlığı ölçülür.
- Moodle 5.x'te web dosyaları `public/` klasörüne taşınmış, komut satırı kurulum betikleri ise ana klasörde kalmış.
- Moodle'ın test senaryosu aracı, geliştirici kütüphanelerinin (`composer install`) kurulmasını gerektiriyor.
- Ödev teslim durumu (taslak veya gönderildi) teslimin kendisine göre değil, ödevin "öğrenci gönder butonuna basmalı" (`submissiondrafts`) ayarına göre belirleniyor. Bu, ödev modülü analizinde korunacak bir iş kuralı adayı.

## 2. Kapsam ve SRS girişi (Fatma Zehra)

Bu bölüm, WBS-1 ve WBS-2 dokümanlarının birlikte oluşturduğu Yazılım Gereksinim Belirtiminin (SRS) giriş kısmıdır. Yapısı ISO/IEC/IEEE 29148:2018 gereksinim mühendisliği standardının SRS önerisine göre düzenlenmiştir.

### 2.1 Amaç

Bu SRS, Moodle 5.3.1'in seçilen alt fonksiyonlarının yapay zekâ desteğiyle yeni bir mimaride ve farklı bir programlama dilinde yeniden geliştirilmesi için gereksinimleri tanımlar. Hedef okuyucular proje ekibi, danışman öğretim üyesi ve değerlendiricilerdir. Doküman, geliştirmeye başlamadan önce neyin korunacağını, neyin düzeltileceğini ve neyin yeni ekleneceğini netleştirmek için kullanılır.

### 2.2 Kapsam

**Kapsam dahilinde**
- Moodle 5.3.1'in (revizyon `cfe123f2`) aşağıdaki alt fonksiyonlarının analizi; ilk sürümde geliştirilecek olanlar §9'da seçilir:
  - Kimlik ve yetkilendirme
  - Ders ve kayıt
  - Danışmanlık (ön şartlar, ders seçim kısıtları)
  - Ödev ve teslim
  - Değerlendirme
  - Dosya yönetimi
- Yeni modül: Otomatik eğitim planı oluşturma
- Seçilen fonksiyonların yeni dilde ve mimaride yeniden geliştirilmesi, bunun için gereken veri aktarımı
- Eski ve yeni sistemin aynı girdilerle karşılaştırmalı doğrulanması ve kalite ölçümleri

**Kapsam dışında**
- Moodle'ın bütünü ve seçilmeyen etkinlik türleri (sınav, forum, çalıştay, SCORM, H5P vb.)
- Üçüncü taraf eklentiler ve mobil uygulama
- Moodle arayüzünün görsel olarak birebir kopyalanması
- Gerçek kurum verisinin taşınması (yalnızca test verisi kullanılır)
- Eski sistemde yapılacak değişiklikler; eski sistem karşılaştırma için olduğu gibi korunur

### 2.3 Tanımlar ve kısaltmalar

| Terim | Anlamı |
| --- | --- |
| Eski sistem | Moodle 5.3.1, revizyon `cfe123f2` |
| Yeni sistem | Seçilen fonksiyonların hedef dilde yeniden geliştirilmiş hâli |
| Modül / alt fonksiyon | Moodle'ın birlikte analiz edilen bir işlev grubu (ör. Ödev ve teslim) |
| Rol | Kullanıcının bir bağlamda sahip olduğu yetki seti (ör. öğrenci, öğretmen) |
| Bağlam (context) | Moodle'da yetkinin geçerli olduğu kapsam: site, kategori, ders, etkinlik |
| Yetenek (capability) | Moodle'da tek bir işleme verilen izin (ör. `mod/assign:grade`) |
| FR | Fonksiyonel gereksinim |
| NFR | Fonksiyonel olmayan gereksinim (performans, güvenlik, kullanılabilirlik vb.) |
| BR | Korunacak iş kuralı |
| BUG | Eski sistemde düzeltilecek hata veya eksik |
| KPI | Ana performans göstergesi |
| YZ | Yapay zekâ |
| Karakterizasyon testi | Eski sistemin mevcut davranışını kaydeden test |
| İzlenebilirlik | Gereksinim, eski kod, yeni kod ve test arasındaki eşleme |

### 2.4 Kullanıcı rolleri

| Rol | Moodle karşılığı | Örnek veride | Temel işleri |
| --- | --- | --- | --- |
| Site yöneticisi | `admin` | admin | Site ayarları, kullanıcı ve rol yönetimi |
| Yönetici | `manager` | yonetici1 | Kategori, ders ve kullanıcı yönetimi |
| Öğretmen | `editingteacher` | ogretmen1, ogretmen2 | Ders içeriği, ödev oluşturma, notlandırma |
| Düzenleme yetkisiz öğretmen | `teacher` | asistan1 | Notlandırma, içerik değiştiremez |
| Öğrenci | `student` | ogrenci1–ogrenci10 | Derse katılım, ödev teslimi, not görüntüleme |
| Danışman | Standart rolü yok | danisman1 | Ders seçim onayı, eğitim planı takibi (bkz. §4.3, §5) |

### 2.5 Gereksinimlerin yazımı

Her gereksinim aşağıdaki alanlarla yazılır:

| Alan | Açıklama |
| --- | --- |
| Kimlik | `<MODÜL>-<TÜR>-<NO>`, ör. `ASG-FR-03` |
| Sınıf | **K** korunacak · **D** düzeltilecek · **Y** yeni |
| Açıklama | Tek ve test edilebilir bir ifade |
| Kaynak | Eski kodun yeri, kullanıcı ihtiyacı (§3) veya hata kaydı |
| Öncelik | **M** mutlaka · **S** olmalı · **C** olabilir (MoSCoW) |
| Kabul kriteri | Gereksinimin karşılandığı nasıl anlaşılır |
| Test | İlgili test senaryosunun kimliği |

**Modül önekleri:** `AUTH` kimlik ve yetkilendirme · `CRS` ders ve kayıt · `ADV` danışmanlık · `ASG` ödev ve teslim · `GRD` değerlendirme · `FILE` dosya yönetimi · `PLAN` otomatik eğitim planı.
**Tür ekleri:** `FR` fonksiyonel · `NFR` fonksiyonel olmayan · `BR` iş kuralı · `BUG` hata/eksik.

### 2.6 Varsayımlar ve kısıtlar

- Eski sistem sürümü ve revizyonu proje boyunca sabittir; yeni Moodle sürümleri dikkate alınmaz.
- Hedef dil ve mimari WBS-2'de seçilir. Bu bölümdeki gereksinimler dilden bağımsız yazılır.
- Bütün geliştirme ve testler `ortam/ornek-veri.feature` ile üretilen ortak veriyle yapılır.
- YZ ile üretilen her çıktı insan incelemesinden ve testten geçmeden kabul edilmez; kullanımı WBS-2 §4'e göre kaydedilir.
- Ekip 5 kişidir; harcanan efor, YZ destekli ve manuel geliştirmenin karşılaştırılması için kayıt altına alınır.

### 2.7 SRS'in bölümleri

| SRS bileşeni | Yeri |
| --- | --- |
| Giriş, kapsam, roller, gereksinim yazımı | WBS-1 §2 (bu bölüm) |
| Kullanıcı ihtiyaçları | WBS-1 §3 |
| Mevcut fonksiyonların gereksinimleri (K/D) | WBS-1 §4 |
| Yeni fonksiyon gereksinimleri (Y) | WBS-1 §5 |
| Fonksiyonel olmayan gereksinimler ve KPI'lar | WBS-1 §6 |
| Seçilen modüller ve bağımlılıklar | WBS-1 §9 |
| Veri ve arayüz tasarımı | WBS-2 §3 |
| İzlenebilirlik | WBS-2 §5 |

## 3. Kullanıcı ihtiyaçları anketi (İrem)

## 4. Modül analizleri

Her modül için alt başlıklar: Moodle'daki karşılığı · İş akışları · Kullanıcı ihtiyaçları · İş kuralları (korunacak) · Hatalar ve eksikler (düzeltilecek) · Veri modeli · Yetkilendirme · Bağımlılıklar ve entegrasyonlar · Gereksinimler · Test senaryoları · İlk sürüme alınsın mı?

### 4.1 Kimlik ve yetkilendirme (Sinem)

### 4.2 Ders ve kayıt (Sinem)

### 4.3 Danışmanlık (İrem)

### 4.4 Ödev ve teslim (Aslı Ceren)

### 4.5 Değerlendirme (Aslı Ceren)

### 4.6 Dosya yönetimi (Seray)

## 5. Yeni modül: Otomatik eğitim planı (İrem)

## 6. Ana performans göstergeleri ve kalite kriterleri (Fatma Zehra)

## 7. Literatür, mevcut araçlar ve özgünlük hedefi (Seray)

## 8. Eski sistemin başlangıç ölçümleri (Aslı Ceren)

## 9. Seçilen modüller, bağımlılık haritası ve kararlar (Fatma Zehra)