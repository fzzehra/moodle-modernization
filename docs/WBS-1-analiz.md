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