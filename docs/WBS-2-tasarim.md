# WBS-2 Hedef Mimari ve Modernizasyon Sürecinin Tasarlanması

## 1. Hedef dil ve teknoloji seçimi (Seray)

## 2. Hedef mimari ve deployment diyagramı (Seray)

## 3. Veri ve arayüz tasarımı

API tabloları: Metot · Yol · İstek · Yanıt · Hangi rol çağırabilir

### 3.1 Kimlik ve yetkilendirme (Sinem)

### 3.2 Ders ve kayıt (Sinem)

### 3.3 Danışmanlık (İrem)

### 3.4 Ödev ve teslim (Aslı Ceren)

### 3.5 Değerlendirme (Aslı Ceren)

### 3.6 Dosya yönetimi (Seray)

## 4. Yapay zekâ kullanım yaklaşımı (İrem)

## 5. İzlenebilirlik yöntemi (Fatma Zehra)

Her gereksinim; eski sistemdeki kaynağına, yeni sistemdeki karşılığına ve onu doğrulayan teste bağlanır. Böylece (1) hiçbir gereksinimin unutulmadığı, (2) yazılan her kodun bir gereksinime dayandığı ve (3) eski ile yeni davranışın aynı testle karşılaştırıldığı gösterilebilir. Matris ayrı bir dosyada tutulur: [izlenebilirlik.md](izlenebilirlik.md).

### 5.1 Matrisin sütunları

| Sütun | İçerik | Örnek |
| --- | --- | --- |
| Gereksinim | Kimlik (WBS-1 §2.5) | `ASG-BR-01` |
| Sınıf | K korunacak · D düzeltilecek · Y yeni | K |
| Eski kod | Sabit revizyondaki dosya ve satırlar (`cfe123f2`) | `public/mod/assign/locallib.php:8701-8705` |
| Yeni kod | Yeni sistemdeki dosya veya fonksiyon | Geliştirmede doldurulur |
| Test | Test kimliği; aynı test iki sistemde de çalışır | `T-ASG-01` |
| Eski sonuç | Testin eski sistemdeki sonucu | Geçti / Kaldı (D sınıfında beklenen) |
| Yeni sonuç | Testin yeni sistemdeki sonucu | Geliştirmede doldurulur |
| Üretim | Kod nasıl yazıldı | YZ / Manuel / YZ + düzeltme |
| PR | Kodun eklendiği pull request | `#12` |

### 5.2 Kurallar

1. **Her gereksinimin en az bir testi vardır.** Testi olmayan gereksinim "doğrulanmamış" sayılır.
2. **Her test bir gereksinime bağlıdır.** Test adında gereksinim kimliği geçer, ör. `T-ASG-01 ASG-BR-01 taslak ayarı açıkken teslim taslak kalır`.
3. **Eski kod referansı sabit revizyona göre verilir.** Dosya yolu ve satır aralığı `cfe123f2` revizyonundaki hâline aittir; Moodle güncellense de referans değişmez.
4. **Yeni kod, gereksinim kimliğiyle işaretlenir.** Commit mesajında ve pull request açıklamasında ilgili kimlikler yazılır, ör. `ASG-FR-03: teslim kaydetme`.
5. **D sınıfı gereksinimler** için test eski sistemde **kalır** (hata orada vardır), yeni sistemde **geçer**. Eşleme eShop pilotundaki "beklenen başarısızlık" yöntemiyle yapılır.
6. **Y sınıfı gereksinimlerin** eski kod referansı yoktur; bu sütuna kaynağı yazılır (ör. kullanıcı ihtiyaçları anketi, §3).
7. **Üretim sütunu** YZ / manuel karşılaştırması (WBS-1 §6, K17) için kullanılır ve WBS-2 §4'teki YZ kayıt şablonuyla eşleşir.
8. **Herkes sadece kendi modülünün satırlarını** düzenler; matris dosyasında her modülün ayrı başlığı vardır.

### 5.3 Kontroller

| Kontrol | Ne zaman | Sonuç |
| --- | --- | --- |
| İleri izlenebilirlik: gereksinim → test | Her modül analizi bitince | Testi olmayan gereksinim kalmamalı |
| Geri izlenebilirlik: kod → gereksinim | Her pull request'te | Gereksinim kimliği olmayan değişiklik birleştirilmez (işlevsel olmayan düzenlemeler hariç) |
| Sonuç karşılaştırması | Her geliştirme döngüsü sonunda | K: iki sistemde geçer · D: eski kalır, yeni geçer · Y: yeni geçer |
| Kapsam özeti | Ara ve son raporda | Modül bazında K / D / Y sayıları ve geçme oranları |

## 6. Modernizasyon süreç taslağı (Sinem)