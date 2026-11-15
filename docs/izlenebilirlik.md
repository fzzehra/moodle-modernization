# İzlenebilirlik Matrisi

Kurallar: [WBS-2 §5](WBS-2-tasarim.md). Eski kod referansları Moodle 5.3.1, revizyon `cfe123f2` içindir. Herkes sadece kendi modülünün tablosuna satır ekler.

| Gereksinim | Sınıf | Eski kod | Yeni kod | Test | Eski sonuç | Yeni sonuç | Üretim | PR |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |

## Kimlik ve yetkilendirme – AUTH (Sinem)

| Gereksinim | Sınıf | Eski kod | Yeni kod | Test | Eski sonuç | Yeni sonuç | Üretim | PR |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |

## Ders ve kayıt – CRS (Sinem)

| Gereksinim | Sınıf | Eski kod | Yeni kod | Test | Eski sonuç | Yeni sonuç | Üretim | PR |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |

## Danışmanlık – ADV (İrem)

| Gereksinim | Sınıf | Eski kod | Yeni kod | Test | Eski sonuç | Yeni sonuç | Üretim | PR |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |

## Ödev ve teslim – ASG (Aslı Ceren)

| Gereksinim | Sınıf | Eski kod | Yeni kod | Test | Eski sonuç | Yeni sonuç | Üretim | PR |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ASG-BR-01 (örnek) | K | `public/mod/assign/locallib.php:8701-8705` | | T-ASG-01 | | | | |

> **Örnek satırın açıklaması:** Ödevde "öğrenci gönder butonuna basmalı" (`submissiondrafts`) ayarı açıksa kaydedilen teslim **taslak**, kapalıysa **gönderildi** durumuna geçer. Kural örnek veri hazırlanırken bulundu (WBS-1 §1). Aslı Ceren modül analizinde bu satırı kendi kimlik numarasıyla güncelleyebilir.

## Değerlendirme – GRD (Aslı Ceren)

| Gereksinim | Sınıf | Eski kod | Yeni kod | Test | Eski sonuç | Yeni sonuç | Üretim | PR |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |

## Dosya yönetimi – FILE (Seray)

| Gereksinim | Sınıf | Eski kod | Yeni kod | Test | Eski sonuç | Yeni sonuç | Üretim | PR |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| FILE-FR-01 | K | `public/repository/upload/lib.php:81-250` | | T-FILE-01 | Bekliyor | | YZ | |
| FILE-FR-02 | K | `public/lib/filelib.php:1066` | | T-FILE-02 | Bekliyor | | YZ | |
| FILE-FR-03 | K | `public/lib/filelib.php:4443`, `public/lib/filelib.php:2674` | | T-FILE-03 | Bekliyor | | YZ | |
| FILE-FR-04 | K | `public/lib/filelib.php:4443-4620`; `public/mod/assign/submission/file/lib.php:38-88` | | T-FILE-04, T-FILE-05 | Bekliyor | | YZ | |
| FILE-FR-05 | K | `public/lib/filestorage/stored_file.php:364-404` | | T-FILE-06 | Bekliyor | | YZ | |
| FILE-FR-06 | K | `public/lib/filestorage/file_system.php:226-243` | | T-FILE-07 | Bekliyor | | YZ | |
| FILE-FR-07 | K | `public/repository/upload/lib.php:237-245` | | T-FILE-09 | Bekliyor | | YZ | |
| FILE-FR-08 | K | `public/repository/upload/lib.php:220-231` | | T-FILE-11 | Bekliyor | | YZ | |
| FILE-FR-09 | K | `public/lib/filelib.php:639-651` | | T-FILE-10 | Bekliyor | | YZ | |
| FILE-FR-10 | K | `public/repository/upload/lib.php:148-175, 209-214` | | T-FILE-12, T-FILE-13 | Bekliyor | | YZ | |
| FILE-FR-11 | K | `public/repository/upload/lib.php:202` | | T-FILE-14 | Bekliyor | | YZ | |
| FILE-FR-12 | K | `public/lib/filestorage/file_storage.php:2261-2330` | | T-FILE-08 | Bekliyor | | YZ | |
| FILE-FR-13 | K | `public/files/index.php:55-56` | | T-FILE-05 | Bekliyor | | YZ | |
| FILE-BR-01 | K | `public/lib/filestorage/file_system_filedir.php:185-190` | | T-FILE-07 | Bekliyor | | YZ | |
| FILE-BR-02 | K | `public/lib/db/install.xml:2660, 2688` | | T-FILE-09 | Bekliyor | | YZ | |
| FILE-BR-03 | K | `public/lib/filestorage/file_system.php:226-243` | | T-FILE-07 | Bekliyor | | YZ | |
| FILE-BR-04 | K | `public/lib/filestorage/file_system_filedir.php:326-333` | | T-FILE-08 | Bekliyor | | YZ | |
| FILE-BR-05 | K | `public/lib/filestorage/file_storage.php:2268-2278` | | T-FILE-08 | Bekliyor | | YZ | |
| FILE-BR-06 | K | `public/repository/upload/lib.php:148-152, 173-175` | | T-FILE-12 | Bekliyor | | YZ | |
| FILE-BR-07 | K | `public/repository/upload/lib.php:237-245` | | T-FILE-09 | Bekliyor | | YZ | |
| FILE-BR-08 | K | `public/lib/moodlelib.php:6337-6364` | | T-FILE-11, T-FILE-13 | Bekliyor | | YZ | |
| FILE-BR-09 | K | `public/repository/upload/lib.php:177-202` | | T-FILE-14 | Bekliyor | | YZ | |
| FILE-BR-10 | K | `public/admin/settings/security.php:63-65` | | T-FILE-10 | Bekliyor | | YZ | |
| FILE-BR-11 | K | `public/lib/filelib.php:4443-4620` | | T-FILE-04 | Bekliyor | | YZ | |
| FILE-BR-12 | K | `public/draftfile.php:33-36, 69-72, 88` | | T-FILE-01, T-FILE-04 | Bekliyor | | YZ | |
| FILE-BR-13 | K | `public/lib/filestorage/stored_file.php:364-389` | | T-FILE-07 | Bekliyor | | YZ | |
| FILE-BR-14 | K | `public/lib/filestorage/stored_file.php:391-399` | | — (testi yok, doğrulanmamış) | Bekliyor | | YZ | |
| FILE-BUG-01 | D | `public/lib/filelib.php:639-651` | | T-FILE-10 | Bekliyor | | YZ | |
| FILE-BUG-02 | D | `public/lib/filestorage/file_storage.php:2268-2278` | | T-FILE-08 | Bekliyor | | YZ | |
| FILE-NFR-01 | Y | Kaynak: WBS-1 §6, K8 (eski kod referansı yok) | | T-FILE-15 | Bekliyor | | YZ | |
| FILE-NFR-02 | Y | Kaynak: FILE-BUG-04, `public/lib/filestorage/file_system.php:234-241` | | T-FILE-16 | Bekliyor | | YZ | |

## Otomatik eğitim planı – PLAN (İrem)

| Gereksinim | Sınıf | Eski kod | Yeni kod | Test | Eski sonuç | Yeni sonuç | Üretim | PR |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |