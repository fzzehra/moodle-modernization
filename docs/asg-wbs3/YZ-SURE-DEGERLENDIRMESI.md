# YZ süre kaydı — kanıt ve yöntem (22.11.2026)

**Toplam aktif YZ çalışma süresi: Kullanıcı doğrulaması bekleniyor.** Kullanıcı ek doğrulanmış kayıt bulunmadığını bildirdi. Bu nedenle bir aktif saat değeri atanmadı; ölçülemeyen süre sıfır sayılmadı.

## Kapsam ve kaynak

Codex uygulamasının sohbet listesi, yerel session_index.jsonl ve dokuz sessions JSONL dosyası salt okunur incelendi. İki ana sohbet ve yedi source.subagent.other=guardian oturumu bulundu. Kimlik doğrulama dosyaları okunmadı; mesaj/prompt/komut çıktıları bu belgeye kopyalanmadı.

- “ASG modülünü eksiksiz geliştir”: 01a1e907-e592-7480-acb5-dd8f820e38f8; ASG/FILE geliştirme, hata düzeltme, inceleme, test, entegrasyon, dokümantasyon ve ilgili Git işlemlerinin bulunduğu ana sohbet.
- “Moodle ortamını kur ve doğrula”: 01a1e143-28c9-7a90-b3a2-b5fa64d5bc22; genel ortam kurulumu ayrı tutulur.
- Yerel dosyalar Europe/Istanbul saatine göre 16–22 Kasım 2026 aralığındadır. ASG/FILE için tamamlanmış turlar 17.11.2026 13:22:52–22.11.2026 00:20:12 aralığındadır. Bu takvim aralığının tamamı çalışma süresi değildir.
- Bu süre araştırmasının halen açık turu ve olası dış/kayıp oturumlar toplama alınmadı. Arşiv/başka makine kayıtları için eksiksizlik iddiası yoktur.
- Git a7eb58e (22.11.2026 00:51:23) ve PR #52 merge 129235f (00:56:30), teslimin tarihini destekler; aralarındaki süre çalışma süresine eklenmez.

## Ölçüm ayrımı

| Tür | Sonuç | Anlamı |
| --- | --- | --- |
| Doğrudan doğrulanmış aktif model/insan süresi | Ölçülemiyor | task_complete süreleri saf model etkinliği değildir; token kayıtlarında aktif süre alanı yoktur. |
| Kayıtlı ASG/FILE işlem süresi | 05:19:16 (19,156,067 ms) | 26 tamamlanmış kök turun duration_ms toplamı; araç/test/onay beklemesi içerebilir. |
| Yaklaşık araç aralığı birleşimi | ≈ 01:37:26 | CommandExecution/McpToolCall tamamlanma zamanı ve duration alanından yeniden kurulan, çakışmaları tekilleştirilmiş aralıklar. |
| Yaklaşık araç dışı kalan süre | ≈ 03:41:50 | Yaklaşık 3 saat 42 dakika; aktif YZ süresi olarak KULLANILMAZ. Kuyruk, onay, kayıt boşluğu kalabilir; araç çalışırken model de çalışabilir. Güvenilir alt/üst sınır değildir. |
| Ayrı ortam kurulum sohbeti | 01:30:39 | 7 tamamlanmış tur; ASG/FILE toplamına eklenmedi, bu da aktif süre değildir. |
| İki ana sohbetin kayıtlı işlem toplamı | 06:49:55 | 33 tur için proje çapındaki işlem metriği; ASG eforu veya aktif YZ toplamı değildir. |
| Ölçülemeyen kısım | Kullanıcı doğrulaması bekleniyor | Saf model üretimi/düşünme, insan etkileşimi, görünmeyen beklemeler, eksik/dış oturumlar ve bu açık araştırma turu ayrılamıyor. |

## Hesaplama ve çift saymayı önleme

1. task_started/task_complete kayıtları turn_id ile eşleştirildi; yinelenen bitişler aynı kimlik altında bir kez sayıldı. Yalnız tamamlanmış kök turların duration_ms değerleri toplandı.
2. Kök aralıkların birleşimi kontrol edildi: ASG/FILE toplamı 19.156.067 ms; çakışma 0 ms. Turlar arasındaki boşluklar alınmadı.
3. Guardian alt oturumları ana turun içinde gerçekleşebildiğinden ayrıca eklenmedi. Ortam kurulumu ayrı tutuldu. Tokenlar süreye çevrilmedi.
4. Araç aralığı yeniden kurulurken event timestamp bitiş kabul edilip duration çıkarıldı; kök tur sınırında kırpılıp örtüşen aralıklar birleştirildi. Bu zamanlama varsayımı nedeniyle 5.846.100 ms araç birleşimi yaklaşık değerdir.
5. 19.156.067 − 5.846.100 = 13.309.967 ms artık hesaplanır. Bu fark, ölçülmüş aktif model süresi değildir; söz konusu alanı doldurmak için kullanılmadı.
6. JSON kanıtındaki kaynak SHA256/bayt sayısı okuma anındaki yerel dosyayı tanımlar. Aktif sohbet dosyası büyüyebilir; bu nedenle SHA sabit kalmak zorunda değildir. Tamamlanmış 26 tur kesiti esas alınır. Ham kayıtlar ve çıkarım betikleri yalnız ignored coverage/yz-time-audit altındadır.

## Tamamlanmış ASG/FILE tur dökümü

Başlangıçlar started_at, süreler doğrudan duration_ms alanıdır. Araç/fark hesapları yalnız JSON kanıtında yaklaşık metrik olarak tutulur.

| Tur kimliği | Başlangıç (+03:00) | Kayıtlı işlem süresi (ms) |
| --- | --- | --- |
| 01a1e907-e96b-7a43-8dad-ca9cd67e332c | 2026-11-17T13:22:52+03:00 | 2063743 |
| 01a1e978-0f79-7f22-8a06-e3c518ea74d7 | 2026-11-17T15:25:22+03:00 | 86103 |
| 01a1eebf-72d8-7680-bb55-29b7e288361a | 2026-11-18T16:01:26+03:00 | 229337 |
| 01a1eec4-d3f4-7e51-90a7-1b50e7bf3633 | 2026-11-18T16:07:19+03:00 | 1001801 |
| 01a1eed5-8c87-7363-918b-e3566458cf09 | 2026-11-18T16:25:35+03:00 | 420074 |
| 01a1eee2-ede0-7172-ac5f-2afffdc2aba0 | 2026-11-18T16:40:11+03:00 | 53884 |
| 01a1efe9-bdb4-7820-80e4-2519f75632fc | 2026-11-18T21:27:15+03:00 | 642600 |
| 01a1f455-4e8f-74b3-a928-436d76aa6b3f | 2026-11-19T18:03:13+03:00 | 1349050 |
| 01a1f46a-ef90-7b90-a7c9-f41931a39c35 | 2026-11-19T18:26:51+03:00 | 1031764 |
| 01a1f47b-d6ce-7583-8f3f-55072a1a80ca | 2026-11-19T18:45:19+03:00 | 637520 |
| 01a1fa80-5c47-7471-8f4b-675cf99731d3 | 2026-11-20T22:47:58+03:00 | 377523 |
| 01a1faaf-8b8a-7f10-97fc-d74aaec6eb18 | 2026-11-20T23:39:31+03:00 | 927993 |
| 01a1fac2-4596-7c63-b508-6491632f0f54 | 2026-11-20T23:59:58+03:00 | 560270 |
| 01a1fcee-1e3d-7720-adf6-05f9908cd037 | 2026-11-21T10:07:06+03:00 | 779553 |
| 01a1fd71-3768-7041-9495-201bb3c74489 | 2026-11-21T12:30:17+03:00 | 336769 |
| 01a1fd78-3fae-7e70-8b0f-f86bb522a2e4 | 2026-11-21T12:37:58+03:00 | 123851 |
| 01a1fd7d-d0e8-7cc0-a3de-5faa6eba8446 | 2026-11-21T12:44:03+03:00 | 411416 |
| 01a1fd85-2712-7bf0-ac6d-475c8527dc7a | 2026-11-21T12:52:04+03:00 | 97797 |
| 01a1fd8e-6314-7b53-9a68-1911dd2d302c | 2026-11-21T13:02:09+03:00 | 493234 |
| 01a1fe01-746f-76f1-b2de-b2cb36104262 | 2026-11-21T15:07:50+03:00 | 2822941 |
| 01a1fe2f-675f-7312-8bdc-6bbd16ebe24c | 2026-11-21T15:58:01+03:00 | 1781535 |
| 01a1fe4f-a08b-7562-a3e9-b7722fbccd0d | 2026-11-21T16:33:13+03:00 | 1156 |
| 01a1ff27-d88e-7570-ba1a-903c086afdbb | 2026-11-21T20:29:24+03:00 | 602859 |
| 01a1ff4b-330c-7f33-88ee-e8d84fce4e79 | 2026-11-21T21:08:00+03:00 | 697664 |
| 01a1ff61-5ea4-7e63-a80c-bbcd89d64836 | 2026-11-21T21:32:13+03:00 | 356444 |
| 01a1ffe7-caba-7803-baa4-98128bc611c6 | 2026-11-21T23:59:03+03:00 | 1269186 |

## Teslim kontrolü

PR #52 main 129235f içinde. Teslim commit’i a7eb58e ile main arasında src/tests/docker/Moodle.slnx farkı yoktur. DOGRULAMA.json/finalSectionDelivery: 505 birim + 503 entegrasyon = 1008/1008 .NET, JS 54/54, Playwright 9/9; PostgreSQL 17, gerçek ClamAV, Compose ve snapshot/migration başarılı. Bunlar önceki gerçek koşuların kanıtıdır; belge turunda testler tekrar çalıştırılmadı.
ASG/FILE için bağımsız teknik iş kalmadı. Aktif efor ölçümü doğrulanamadığından yalnız bu kayıt alanı kullanıcı doğrulaması bekler. Manuel kodlama/efor karşılaştırması veya Basic COCOMO hesabı yapılmadı.

Makine kanıtı: [YZ-SURE-KANITI.json](YZ-SURE-KANITI.json).
