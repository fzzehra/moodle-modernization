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
| ADV-BR-01 | K | `public/lib/enrollib.php:2113` | [REST tasarımı](WBS-2-tasarim.md#331-veri-tasarımı) | T-ADV-E01 | Geçti | | YZ | |
| ADV-BR-03 | K | `public/enrol/self/lib.php:334-339` | [REST tasarımı](WBS-2-tasarim.md#331-veri-tasarımı) | T-ADV-E01 | Geçti | | YZ | |
| ADV-FR-01 | Y | Kaynak: ADV-BUG-01 (eski kod referansı yok, Moodle'da karşılığı yok) | [REST tasarımı](WBS-2-tasarim.md#332-rest-api) | T-ADV-01 | — (yeni fonksiyon) | | YZ | |
| ADV-FR-02 | Y | Kaynak: ADV-BUG-01; `public/availability/condition/completion/classes/condition.php:135` (yalnızca ders içi) | [REST tasarımı](WBS-2-tasarim.md#332-rest-api) | T-ADV-02, T-ADV-E02 | Geçti (E02) | | YZ | |
| ADV-FR-03 | Y | Kaynak: ADV-BUG-05 (eski kod referansı yok) | [REST tasarımı](WBS-2-tasarim.md#332-rest-api) | T-ADV-03 | — (yeni fonksiyon) | | YZ | |
| ADV-FR-04 | Y | Kaynak: ADV-BUG-02, 03 (eski kod referansı yok) | [REST tasarımı](WBS-2-tasarim.md#332-rest-api) | T-ADV-04, T-ADV-E03 | Geçti (E03) | | YZ | |
| ADV-FR-05 | Y | Kaynak: ADV-BUG-02 (eski kod referansı yok) | [REST tasarımı](WBS-2-tasarim.md#332-rest-api) | T-ADV-05 | — (yeni fonksiyon) | | YZ | |
| ADV-FR-06 | K | `public/enrol/self/lib.php:334-339` | [REST tasarımı](WBS-2-tasarim.md#332-rest-api) | T-ADV-06, T-ADV-E01 | Geçti (E01) | | YZ | |
| ADV-FR-07 | Y | Kaynak: ADV-BUG-04; `public/enrol/self/lang/en/enrol_self.php:79` | [REST tasarımı](WBS-2-tasarim.md#332-rest-api) | T-ADV-07 | — (yeni fonksiyon) | | YZ | |
| ADV-NFR-01 | Y | Kaynak: WBS-1 §4.1 AUTH-BR-08, 12 | [REST tasarımı](WBS-2-tasarim.md#332-rest-api) | T-ADV-08, T-ADV-E03 | Geçti (E03) | | YZ | |

## Ödev ve teslim – ASG (Aslı Ceren)

| Gereksinim | Sınıf | Eski kod | Yeni kod | Test | Eski sonuç | Yeni sonuç | Üretim | PR |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ASG-FR-01 | K | `public/mod/assign/locallib.php:787` | `src/Moodle.Application/Assignments/AssignmentService.cs` — CreateAsync; `src/Moodle.Api/AssignmentEndpoints.cs`; [dosya haritası](asg-wbs3/TESLIM-RAPORU.md) | T-ASG-02; birim: `tests/Moodle.UnitTests/AssignmentServiceTests.cs` (`T_ASG_02_ASG_FR_01*`); entegrasyon: `tests/Moodle.IntegrationTests/AssignmentApiTests.cs` (`T_ASG_02_ASG_FR_01*`) | Yerel testler geçti (117 toplam; tarihsel 54/97 korundu) | 18.11.2026 regresyon geçti; gerçek AUTH/CRS birleşmesi bekliyor | YZ (ajan düzeltmeleri dahil; insan incelemesi bekliyor) | PR #21 |
| ASG-FR-02 | K | `public/mod/assign/locallib.php:8661` | `src/Moodle.Application/Assignments/AssignmentService.cs` — SaveAsync / SubmitAsync / MineAsync; `src/Moodle.Api/AssignmentEndpoints.cs`; [dosya haritası](asg-wbs3/TESLIM-RAPORU.md) | T-ASG-01; birim: `tests/Moodle.UnitTests/AssignmentServiceTests.cs` (`T_ASG_01_ASG_FR_02*`); entegrasyon: `tests/Moodle.IntegrationTests/AssignmentApiTests.cs` (`T_ASG_01_ASG_FR_02*`) | Yerel testler geçti (117 toplam; tarihsel 54/97 korundu) | 18.11.2026 regresyon geçti; gerçek AUTH/CRS birleşmesi bekliyor | YZ (ajan düzeltmeleri dahil; insan incelemesi bekliyor) | PR #21 |
| ASG-FR-03 | K | `public/mod/assign/locallib.php:5010` | `src/Moodle.Application/Assignments/AssignmentService.cs` — ListAsync; `src/Moodle.Api/AssignmentEndpoints.cs`; [dosya haritası](asg-wbs3/TESLIM-RAPORU.md) | T-ASG-03; birim: `tests/Moodle.UnitTests/AssignmentServiceTests.cs` (`T_ASG_03_ASG_FR_03*`); entegrasyon: `tests/Moodle.IntegrationTests/AssignmentApiTests.cs` (`T_ASG_03_ASG_FR_03*`); yeni: AssignmentServiceTests.Scope* ve SubmissionScopeTests | Yerel testler geçti (117 toplam; tarihsel 54/97 korundu) | 18.11.2026: ASG kapsam/yükümlü öğrenci kabulü geçti; gerçek AUTH/CRS birleşme kabulü bekliyor | YZ (ajan düzeltmeleri dahil; insan incelemesi bekliyor) | PR #21 |
| ASG-BR-01 | K | `public/mod/assign/locallib.php:8701-8705` | `src/Moodle.Application/Assignments/AssignmentService.cs` — SaveAsync / SubmitAsync (gönderme onayı); `src/Moodle.Api/AssignmentEndpoints.cs`; [dosya haritası](asg-wbs3/TESLIM-RAPORU.md) | T-ASG-01; birim: `tests/Moodle.UnitTests/AssignmentServiceTests.cs` (`T_ASG_01_ASG_FR_02_ASG_BR_01*`); entegrasyon: `tests/Moodle.IntegrationTests/AssignmentApiTests.cs` (`T_ASG_01_ASG_FR_02_ASG_BR_01*`) | Yerel testler geçti (117 toplam; tarihsel 54/97 korundu) | 18.11.2026 regresyon geçti; gerçek AUTH/CRS birleşmesi bekliyor | YZ (ajan düzeltmeleri dahil; insan incelemesi bekliyor) | PR #21 |
| ASG-BR-02 | K | `public/mod/assign/locallib.php:6903,7014,8781-8790` | `src/Moodle.Application/Assignments/AssignmentService.cs` — CheckWindowAsync / LearnerAsync (yetki, pencere); `src/Moodle.Api/AssignmentEndpoints.cs`; [dosya haritası](asg-wbs3/TESLIM-RAPORU.md) | T-ASG-04; birim: `tests/Moodle.UnitTests/AssignmentServiceTests.cs` (`T_ASG_04_ASG_BR_02*`); entegrasyon: `tests/Moodle.IntegrationTests/AssignmentApiTests.cs` (`T_ASG_04_ASG_BR_02*`); yeni: AssignmentServiceTests.Scope* ve SubmissionScopeTests | Yerel testler geçti (117 toplam; tarihsel 54/97 korundu) | 18.11.2026: pencere/kilit/yükümlü öğrenci/bağlam testleri geçti; gerçek AUTH/CRS birleşme kabulü bekliyor | YZ (ajan düzeltmeleri dahil; insan incelemesi bekliyor) | PR #21 |
| ASG-NFR-01 | K | `public/mod/assign/locallib.php:5572-5587` | `src/Moodle.Application/Assignments/AssignmentService.cs` — ReadAsync / MineAsync; `src/Moodle.Api/AssignmentEndpoints.cs`; [dosya haritası](asg-wbs3/TESLIM-RAPORU.md) | T-ASG-05; birim: `tests/Moodle.UnitTests/AssignmentServiceTests.cs` (`T_ASG_05_ASG_NFR_01*`); entegrasyon: `tests/Moodle.IntegrationTests/AssignmentApiTests.cs` (`T_ASG_05_ASG_NFR_01*`); yeni: AssignmentServiceTests.Scope* ve SubmissionScopeTests | Yerel testler geçti (117 toplam; tarihsel 54/97 korundu) | 18.11.2026: ASG kapsam/yükümlü öğrenci kabulü geçti; gerçek AUTH/CRS birleşme kabulü bekliyor | YZ (ajan düzeltmeleri dahil; insan incelemesi bekliyor) | PR #21 |

## Değerlendirme – GRD (Aslı Ceren)

| Gereksinim | Sınıf | Eski kod | Yeni kod | Test | Eski sonuç | Yeni sonuç | Üretim | PR |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| GRD-FR-01 | K | `public/mod/assign/locallib.php:10245` | [REST tasarımı](WBS-2-tasarim.md#352-rest-api) | T-GRD-01 | Geçti | | | |
| GRD-FR-02 | K | `public/grade/report/user/index.php:52-55` | [REST tasarımı](WBS-2-tasarim.md#352-rest-api) | T-GRD-02 | Geçti | | | |
| GRD-FR-03 | K | `public/grade/report/grader/index.php:82-83` | [REST tasarımı](WBS-2-tasarim.md#352-rest-api) | T-GRD-03 | Geçti | | | |
| GRD-BR-01 | K | `public/mod/assign/lib.php:1011,1118`, `public/lib/gradelib.php:64` | [REST tasarımı](WBS-2-tasarim.md#352-rest-api) | T-GRD-01 | Geçti | | | |
| GRD-BR-02 | K | `public/mod/assign/gradeform.php:76-102` | [REST tasarımı](WBS-2-tasarim.md#352-rest-api) | T-GRD-04 | Geçti | | | |
| GRD-NFR-01 | K | `public/grade/report/user/index.php:52-73` | [REST tasarımı](WBS-2-tasarim.md#352-rest-api) | T-GRD-05 | Geçti | | | |

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
| PLAN-FR-01 | Y | Kaynak: ADV-FR-01, 02 (eski kod yok, yeni modül) | WBS-4'te tasarlanacak (WBS-1 §5.7) | T-PLAN-01 | — (yeni fonksiyon) | | YZ | |
| PLAN-FR-02 | Y | Kaynak: ADV-FR-03 | WBS-4'te tasarlanacak | T-PLAN-02 | — (yeni fonksiyon) | | YZ | |
| PLAN-FR-03 | Y | Kaynak: ADV-FR-06 | WBS-4'te tasarlanacak | T-PLAN-03 | — (yeni fonksiyon) | | YZ | |
| PLAN-FR-04 | Y | Kaynak: WBS-1 §5.3 | WBS-4'te tasarlanacak | T-PLAN-04 | — (yeni fonksiyon) | | YZ | |
| PLAN-FR-05 | Y | Kaynak: WBS-1 §5.1 | WBS-4'te tasarlanacak | T-PLAN-05 | — (yeni fonksiyon) | | YZ | |
| PLAN-NFR-01 | Y | Kaynak: WBS-1 §4.1 AUTH-BR-08 | WBS-4'te tasarlanacak | T-PLAN-06 | — (yeni fonksiyon) | | YZ | |