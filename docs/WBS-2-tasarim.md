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

Tasarım dilden bağımsızdır (REST + JSON). Gereksinim kimlikleri WBS-1 §4.6.9'dadır. Eski sistemden **korunan** davranışlar K, **düzeltilenler** D, **yeni eklenenler** Y ile işaretlenmiştir.

#### 3.6.1 Tasarım ilkeleri

| İlke | Eski sistemden | Gerekçe |
| --- | --- | --- |
| İçerik adresli depolama: içerik bir kez saklanır, kayıtlar özetle ona bağlanır | K (FILE-BR-01, 03) | Aynı dosyanın çok kopyası disk israfıdır; Moodle'ın en değerli kararlarından biri. |
| Yükleme önce **taslak alanına** gider, kaydedilince kalıcı alana geçer | K (FILE-BR-05) | Yarım kalan form dosya sahipsizliği yaratmaz. |
| Dosya sunumu sahip bileşenin **yetki politikasından** geçer; yetkisiz isteğe "bulunamadı" döner | K (FILE-BR-11, 12) | Dosyanın varlığı bile sızdırılmaz. |
| Özet algoritması **SHA-256** | D (FILE-NFR-02) | Moodle SHA-1 kullanır; yeni sistemde çakışma direnci yüksek bir algoritma seçilir. Dış görünümü değiştirmez. |
| Taslak süresi **son etkinliğe** göre dolar | D (FILE-BUG-02) | Eski sistem oluşturulma tarihine bakar; düzenlenen taslak silinebilir. |
| Kota **her dosya kaydını** sayar | D (FILE-BUG-01) | Eski sistem aynı ad ve içerikli kayıtları tekilleştirerek eksik sayar. |
| Silinen dosya **bekleme süresi** sonunda kalıcı silinir; içerik silinmeden önce kullanım yeniden denetlenir | D (FILE-BUG-03, 04) | Çöp kutusu yaşa göre çalışır, yarış durumu kilitle kapatılır. |

#### 3.6.2 Veri tasarımı

Moodle'ın tek `files` tablosu, "içerik" ve "kayıt" olmak üzere ikiye ayrılır; böylece içerik tekilliği veritabanı kısıtıyla garanti edilir.

| Tablo | Sütunlar | Anahtar / kısıt |
| --- | --- | --- |
| `file_blob` (içerik) | `content_hash` (SHA-256), `size_bytes`, `storage_key`, `created_at`, `orphaned_at` (boşa düştüğü an, boşsa null) | PK `content_hash` |
| `file_entry` (dosya kaydı) | `id`, `content_hash`, `context_id`, `component`, `area`, `item_id`, `path`, `name`, `owner_user_id`, `size_bytes`, `mime_type`, `status`, `source`, `author`, `license`, `sort_order`, `reference_id`, `created_at`, `updated_at`, `deleted_at` | PK `id`; FK `content_hash` → `file_blob`; **benzersiz** (`context_id`, `component`, `area`, `item_id`, `path`, `name`) (FILE-BR-02); dizin (`component`, `area`, `context_id`, `item_id`) |
| `file_draft` (taslak) | `draft_id`, `owner_user_id`, `created_at`, `last_activity_at` | PK `draft_id`; taslak dosyalar `file_entry` içinde `component='user'`, `area='draft'`, `item_id=draft_id` olarak durur |
| `file_setting` | `max_file_bytes`, `user_quota_bytes`, `draft_ttl_hours`, `trash_retention_hours` | Tek satır; yönetici değiştirir |

- Klasörler `file_entry` içinde `name='.'` kaydıdır (Moodle ile aynı).
- `deleted_at` dolu kayıtlar "çöpte"dir; `orphaned_at`, hiçbir kaydın kullanmadığı içeriği işaretler ve kalıcı silme işi bu işaretten **bekleme süresi** sonra çalışır.
- **Kota:** `SELECT SUM(size_bytes) FROM file_entry WHERE owner_user_id = ? AND component='user' AND area <> 'draft' AND deleted_at IS NULL` (her kayıt sayılır).
- **Fiziksel yol:** `storage_key = ab/cd/<özet>`; depo yerel disk veya nesne deposu olabilir (WBS-2 §2).
- Dış depo referansları (Moodle `files_reference`) ilk sürüm kapsamı dışındadır; `reference_id` sütunu yer tutucudur.

#### 3.6.3 Yetki politikası

Her sahip bileşen (ödev, kaynak, profil vb.) iki işlev kaydeder; dosya servisi karar için yalnızca bunları çağırır (Moodle'daki `*_pluginfile` işlevlerinin karşılığı):

- `canRead(kullanıcı, dosya) → bool`
- `canWrite(kullanıcı, alan) → bool`

Hazır politikalar: **taslak** (yalnızca sahibi), **özel dosyalar** (yalnızca sahibi, `manageownfiles`), **ders dosyaları** (düzenleyen öğretmen ve yönetici, `managefiles`). Ödev teslim dosyaları politikası §3.4'te (Aslı Ceren) tanımlanır.

#### 3.6.4 API

Ön ek `/api/v1`. Tüm uçlar oturum gerektirir; misafir yalnızca bileşen politikası izin verirse okuyabilir.

| Metot | Yol | İstek | Yanıt | Hangi rol çağırabilir |
| --- | --- | --- | --- | --- |
| POST | `/file-drafts` | Gövde yok | `201` `{ "draftId", "expiresAt" }` | Giriş yapmış her kullanıcı (misafir hariç) |
| POST | `/file-drafts/{draftId}/files` | `multipart/form-data`: `file`, isteğe bağlı `path`, `overwrite` (varsayılan `false`) | `201` `{ "fileId", "name", "size", "mimeType", "contentHash" }`. Ad çakışırsa ad benzersizleştirilir ve dönen `name` gerçek addır | Taslağın sahibi |
| GET | `/file-drafts/{draftId}/files` | Sorgu: `path` | `200` `{ "files": [ {fileId, name, size, mimeType} ] }` | Taslağın sahibi |
| DELETE | `/file-drafts/{draftId}/files/{fileId}` | — | `204` | Taslağın sahibi |
| POST | `/file-areas/{contextId}/{component}/{area}/{itemId}/commit` | `{ "draftId", "replace": true }` | `200` `{ "files": [...] }`; taslak temizlenir | `canWrite` politikasının izin verdiği kullanıcı |
| GET | `/file-areas/{contextId}/{component}/{area}/{itemId}/files` | Sorgu: `path` | `200` `{ "files": [...] }` | `canRead` politikası |
| GET | `/files/{fileId}` | — | `200` meta veri: ad, boyut, tür, yazar, lisans, tarih | `canRead` politikası |
| GET | `/files/{fileId}/content` | Sorgu: `download=true\|false` | `200` dosya gövdesi, `Content-Disposition`, doğru `Content-Type`. Taslak dosyalar her zaman indirmeye zorlanır | `canRead` politikası; yetkisizse `404` |
| DELETE | `/files/{fileId}` | — | `204`; dosya çöpe taşınır | `canWrite` politikası |
| POST | `/files/{fileId}/restore` | — | `200` meta veri (bekleme süresi dolmadıysa), `410` süresi dolduysa | Dosya sahibi, düzenleyen öğretmen, yönetici |
| GET | `/users/me/quota` | — | `200` `{ "limitBytes", "usedBytes", "unlimited" }` | Giriş yapmış her kullanıcı |
| GET | `/admin/file-settings` | — | `200` `file_setting` | Yönetici |
| PUT | `/admin/file-settings` | `file_setting` alanları | `200` | Yönetici |
| POST | `/admin/file-maintenance/run` | — | `202` `{ "expiredDrafts", "purgedBlobs" }` (temizlik görevini tetikler; test ve bakım için) | Yönetici |

**Hata gövdesi (hepsi için):** `{ "error": { "code": "...", "message": "..." } }`. Hiçbir durumda yığın izi dönmez (K3).

| Durum | Kod | Ne zaman |
| --- | --- | --- |
| 400 | `invalid_name`, `empty_file` | Geçersiz veya 255 karakteri aşan ad; boş dosya veya klasör (FILE-BR-06, 08) |
| 401 | `not_authenticated` | Oturum yok |
| 404 | `not_found` | Dosya yok **veya yetki yok** (FILE-BR-11) |
| 413 | `file_too_large` | Dosya başına sınır aşıldı (FILE-BR-08) |
| 415 | `type_not_allowed` | Alanın kabul etmediği tür (FILE-BR-09) |
| 422 | `quota_exceeded`, `area_limit_exceeded`, `virus_detected` | Kota, alan sınırı veya antivirüs (FILE-BR-08, 09, 10) |
| 410 | `restore_expired` | Geri alma süresi dolmuş |

#### 3.6.5 Gereksinim eşlemesi

| Uç | Gereksinim |
| --- | --- |
| `POST /file-drafts`, `POST .../files` | FILE-FR-01, 07, 08, 09, 10, 11 |
| `POST .../commit` | FILE-FR-02 |
| `GET /files/{id}/content`, `GET /files/{id}` | FILE-FR-03, 04 |
| `DELETE /files/{id}`, `POST .../restore` | FILE-FR-05, 06; FILE-BUG-03, 04 |
| `GET /users/me/quota` | FILE-FR-09; FILE-BUG-01 |
| `POST /admin/file-maintenance/run` | FILE-FR-12; FILE-BUG-02 |
| Ders dosyaları (`component=course`) | FILE-FR-13 |

#### 3.6.6 Açık noktalar

- Ödev teslim dosyalarının yetki politikası §3.4 ile birlikte kesinleştirilecek.
- `restore` ucu eski sistemde yoktur (**Y**, öncelik **C**); ilk sürümde isteğe bağlıdır.
- Yedekleme ve geri yükleme paketleyicileri kapsam dışıdır.

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