> **22.11.2026 güncel teslim:** Gerçek bölüm entegrasyonu tamamlandı. Release, .NET **1008/1008**, JS **54/54**, Playwright **9/9**; 0 skip. Aşağıdaki eski bölüm engeli/açık PR kayıtları tarihseldir. [Son mimari ve kanıt](SON-BOLUM-ENTEGRASYONU.md).

# Güncel GitHub ve modül doğrulaması — 21.11.2026

## GitHub durumu
Başlangıç main temiz, HEAD a21e18a. git fetch origin --prune sonrası main...origin/main farkı 0/0; pull gerektiren yeni main commit'i yoktur. İki yeni uzak ref görünmüştür: newseray ve sinem/crs-wbs3-docs-final. İkincisinin commitleri zaten main'dedir.
GitHub REST API: PR #42 merged (a254d4a), PR #51 merged (a21e18a). Tek açık PR #50: newseray, b0c7cdd, başlık "new". Bu PR main'e alınmadı; tamamlanmış main işlevi olarak sayılmadı.
Yeni dal codex/asg-file-final-verification, a21e18a üzerinden oluşturuldu. Commit, push veya PR açılmadı.

## Kod ve test üzerinden durum
| Modül | Tamamlanan işler | Eksikler | Sorumlu | ASG/FILE etkisi |
|---|---|---|---|---|
| AUTH | Gerçek Identity giriş/oturum, sistem/ders rolleri, ASG bağlam çözümü, AUTH–CRS, zorunlu parola değişimi; AuthEndpointTests, AuthCrsIntegrationTests, AuthFr09ForcedPasswordTests | Seed SiteAdmin rolünü oluşturur, yalnız manager kullanıcıya rol atar; SiteAdmin örnek hesabı yok. course.managefiles ayrı CapabilityRules kuralı değil, yönetici fallback'i ve CourseFilePolicy gerçek EditingTeacher sorgusu birlikte çalışır | İrem; parola değişimi/CRS bağlantılarında Sinem | Çalışan dosya yetkisi korunur. Pozitif yönetici tarayıcı testi test-only gerçek Identity hesabıyla tamamlanır |
| CRS | Ders CRUD, kayıt/rol/tarih, silme koruması, background deletion, olay outbox/dispatcher/worker, gerçek yükümlü öğrenci sorgusu; CRS Postgres/API ve AUTH–CRS testleri | Gerçek bölüm entity/migration/query yok. ICourseSectionQuery sözleşmesi var; DI UnavailableCourseSectionQuery | Sinem | ASG oluşturma invalid_section ile reddedilir; gerçek tam A/B kabulü engellenir |
| FILE | Ortak yönetici, ders dosyaları, ayarlar, süreli draft/trash/orphan cleanup, PostgreSQL içerik kilitleri, sahiplik, MIME, ClamAV ve yükleme/indirme/silme; FileCrossProcessTests ve FileMimeIntegrationTests | Silinen derse ait genel course/files alanı temizliği açık PR #50'de; main'de henüz yok | Fatma Zehra / Seray | ASG mod_assign temizliği mevcut; genel ders dosyası referansları için PR #50'nin alınması ve ortak regresyon gerekir |
| ASG | Oluşturma/okuma, dosyalı/metin teslim, gerçek öğrenci ve değerlendirici kapsamı, sürüm/transaction güvenliği, idempotent ders temizliği; AssignmentApiTests, AsgCourseDeletionTests, AsgRealCrsIntegrationTests | Gerçek oluşturma ve tam A/B tarayıcı kabulü bölüm bağımlılığına takılır. Ödev güncelleme/silme endpoint'i mevcut değil; mevcut ASG WBS kapsamı oluşturma/okuma ve teslimdir, genel CRUD tamamlandı iddiası yapılmaz | Aslı Ceren; bölüm için Sinem | Bağımsız mevcut WBS uygulamaları korunur; bölüm olmadan yeni gerçek ödev oluşturulamaz |
| GRD | Notlandırma/rapor, kilit/override/sürüm, course.deleted uzantısı, not/kategori/kalem temizliği ve ortak ders kilidi; GrdGradeEndpointTests, AsgCourseDeletionTests | Baştan sona yeni gerçek ödev üzerinden tarayıcı notlandırma A/B'ye bağlı | Fatma Zehra; onaylı ASG temizleme entegrasyonu Aslı Ceren | Mevcut değerlendirme ve silme testleri gerçek PostgreSQL'de çalışır; modüller arası tek atomik transaction iddiası yoktur |

Bu sonuç, her modülün bütün olası fonksiyonlarının tamamlandığına dair genel onay değildir. Kodlanan gereksinimler ile eksik bölüm/seed ve main'e alınmamış PR ayrı tutulmuştur. Ekip kod incelemesi veya COCOMO yapılmadı.

## Eksik görev ve teknik talep
| Görev | Sorumlu | Eksik / yapılacak iş | Güvenle bu dalda yapılabilir mi? |
|---|---|---|---|
| ASG-FR-01 bölüm sahipliği / ICourseSectionQuery | Sinem | Gerçek CourseSection(courseId bağlantısı, kalıcı kimlik), migration/snapshot, BelongsToCourseAsync(sectionId, courseId, ct) ve DI; yanlış ders, bilinmeyen/silinmiş bölüm için false ve PostgreSQL testleri. Bölüm kimliğini elde etmeye yönelik gerçek API/fixture yolu da sağlanmalı | Önemli CRS üretim değişikliği; onaysız yapılmaz |
| Tam A/B kabulü: AUTH→CRS→ASG→FILE→GRD | Aslı Ceren; CRS'ye bağlı | Gerçek bölüm gelince E2E_SECTION_ID gerçek kaydı göstermeli, oluşturma/teslim/değerlendirme çalıştırılmalı | Bölüm yokken güvenliği gevşetmeden tamamlanamaz; sırf ID vermek yetmez |
| Pozitif /admin/files Playwright | Aslı Ceren | Üretim seed'ini değiştirmeden rastgele kimlikli test-only Identity bootstrap, gerçek giriş/kaydetme/maintenance, sonunda kullanıcı silme | Bu dalda eklenmiştir; test altyapısı eksikliği giderilir. Örnek SiteAdmin hesabı hâlâ yoktur |
| course.deleted genel ders FILE alanı | Seray, PR #50 | FileCourseDeletedExtension ve testleri main'e alınmalı; ASG/GRD uzantılarıyla birlikte sonra yeniden doğrulanmalı | Başka dal kendiliğinden merge edilmez; PR sahibi/entegrasyon onayı gerekir |
| course.managefiles merkezi AUTH kuralı | İrem | İstenirse yönetici + EditingTeacher açık kuralı ve yetki matrisi; mevcut CourseFilePolicy davranışını korumalı | Şu an FILE işlevi için engel değil; AUTH üretim davranışı bu dalda değiştirilmedi |
| Genel ASG update/delete | Kapsam kararı | Endpointler yok; yeni gereksinim sayılacaksa tasarım/kabul kriterleri belirlenmeli | Mevcut WBS oluşturma/teslimini gerekçesiz genişletme yapılmadı |

## Kanıt dosyaları
- src/Moodle.Infrastructure/Auth/AuthSeedContributor.cs: SiteAdmin rolü / Manager seed.
- src/Moodle.Application/Auth/CapabilityRules.cs ve Infrastructure/Files/CourseFilePolicy.cs: yetki iş bölümü.
- src/Moodle.Infrastructure/DependencyInjection.cs: gerçek learner query, unavailable section, ASG/GRD event uzantıları.
- src/Moodle.Application/Assignments/AssignmentService.cs: bölüm doğrulaması.
- tests/Moodle.E2eFixtures/Program.cs: test-only gerçek Identity kullanıcı oluşturma/silme.
- tests/e2e/specs/asg-file.spec.mjs: gerçek tarayıcı kabulü.
- coverage/current-verification: yerel ignored ham log/TRX, GitHub API kayıtları; bağlantı ve rastgele fixture parolası Git'e eklenmez.

Son sonuçlar DOGRULAMA.json/currentGithubVerification alanına gerçek koşudan işlenir. Önceki 995/995, JS 54/54 ve Playwright 6/2 sonuçları tarihsel olarak korunur.


## Güncel GitHub doğrulaması — main a21e18a
Release ve **995/995 .NET (501 birim + 494 entegrasyon)**, **54/54 JavaScript**, **Playwright 7 geçti / 2 CRS bölüm bağımlılığı nedeniyle atlandı**. Önceki 995 test korundu. PostgreSQL 17, gerçek ClamAV, beş model snapshot/migration ve Compose/HTTP kontrolleri başarılı.
Pozitif SiteAdmin tarayıcı kabulü test-only gerçek Identity kullanıcıyla tamamlandı; kullanıcı koşudan sonra silindi. Seed hâlâ SiteAdmin örnek hesabı sağlamaz. İlk iki fixture giriş hatası gizlenmedi: e-posta eklemek tek başına çözmedi; kesin neden ValueGeneratedNever modelinde Id=0 bırakılmasıydı. Pozitif benzersiz kimlikle düzeltildi; fixture Release derlemesi ve tüm Playwright tekrarlandı.
Main'e yeni commit gelmedi; açık PR #50 (b0c7cdd) genel ders dosyası silme temizliğini içerir, main'e alınmadığından tamamlanmış sayılmadı. Gerçek bölüm için Sinem'in entity/migration/query/DI uygulaması gerekir.
[Ayrıntılı modül ve eksik görev tablosu](GUNCEL-GITHUB-DOGRULAMASI.md); [PR taslağı](PR-TASLAGI-GUNCEL-DOGRULAMA.md); ham kanıt coverage/current-verification. Commit/push/PR yok.
