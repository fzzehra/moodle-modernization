> **19.11.2026 v2 güncellemesi:** Güncel durum [ASG/FILE teslim raporundadır](../asg-file-wbs3/TESLIM-RAPORU.md). Gerçek AUTH ve aktif CRS öğrenci kapsamı bağlıdır; yalnız gerçek bölüm modeli/ICourseSectionQuery hâlâ engeldir. Manuel kod yazma zorunluluğu kaldırılmıştır. Aşağıdaki önceki rapor tarihsel kayıttır; eski AUTH/manuel bekleme ifadeleri güncel durum sayılmamalıdır. V2 sonuçları ve devri [doğrulama](../asg-file-wbs3/DOGRULAMA.json), [sözleşmeler](../asg-file-wbs3/ENTEGRASYON.md) belgelerindedir. V1 PR #21 korunur, v2 PR açılmadı.
# Güncel durum — PR #21 yerel merge, 18 Kasım 2026

ASG commit 317ceb1 üzerine origin/main c7a2b66 (CRS PR #20) commit oluşturmadan birleştirildi. Dört çakışma çözüldü; 266 .NET + 7 JavaScript testi geçti. Gerçek CRS öğrenci sorgusu bağlandı; bölüm modeli ve gerçek AUTH henüz yok. Bu nedenle gerçek ASG oluşturma/bütünleşik oturum akışı hazır sayılmıyor. Yerel merge commit/push yapılmadı.

Ayrıntılı dosya kararları, kullanıcı tarafından onaylanan CRS uyarlamaları, migration/DI uyumu, testler ve kalan riskler: [PR #21 birleşme raporu](BIRLESME-RAPORU.md).

Aşağıdaki 117/97/54 testli teslim anlatımları merge öncesi tarihsel sonuçlardır. Güncel DI davranışı için birleşme raporu esas alınmalıdır.

---
# ASG WBS-3 — Son düzeltme teslimi, 18 Kasım 2026

**On bulgunun ASG tarafındaki düzeltmeleri tamamlandı.** Kullanıcı IAssignmentLearnerQuery ve ISubmissionAccessPolicy sözleşmelerini açıkça onayladı; onay bekleyen iş kalmadı. Gerçek AUTH/CRS/GRD geliştirilmedi. Dal codex/asg-wbs3-implementation; commit/push/PR/merge yok.

Aşağıdaki “Tarihsel ilk teslim” bölümü 17 Kasımın 54 testini ve ilk uygulamayı korur. Ara turdaki 97 test DOGRULAMA.json/revision içinde korunmuştur. Güncel kod, kabul ve sonuçlar bu üst bölümdür.

## On bulgunun son durumu

| Bulgu | Düzeltme / dosyalar | Test / sonuç | Durum ve kalan risk |
| --- | --- | --- | --- |
| Y1 | Application/Abstractions/ISubmissionAccessPolicy.cs; AssignmentService.cs:99 ortak CanAssessAsync, liste ve ayrıntıya aynı hedef öğrenci denetimi. Infrastructure/Fakes/FakeSubmissionAccessPolicy.cs ve UnavailableSubmissionScope.cs; DI | AssignmentServiceTests.Scope_is_identical_for_list_and_detail; SubmissionScopeTests: iki farklı değerlendirici, belirsiz kapsam, yanlış ders/modül, sağlayıcı kesintisi | Tamamlandı. Gerçek AUTH grup/bağlam/cookie/CSRF kabulü birleşmede |
| O1 | Application/Abstractions/IAssignmentLearnerQuery.cs; AssignmentService yükümlü + aktif öğrenci kontrolü; FakeAssignmentLearnerQuery; güvenli boş fallback | SubmissionScopeTests.Mixed_enrolments_exclude_teacher_observer_and_inactive_learner_before_paging; Missing_roster_or_module_capability_never_reveals_submission; fake/default birim testleri | Tamamlandı. Gerçek CRS kayıt/rol sorgusu Sinem tarafından bağlanacak |
| Y2 | Infrastructure/Persistence/AssignmentStore.cs: ASG yazıları ayrı tracker, ortak bağlantı/transaction; sahip olunan transaction commit/rollback, dış transaction savepoint; ChangeTracker.Clear kaldırıldı | AssignmentRevisionTests.Y2*: bekleyen ekleme/değişiklikler korunuyor, erken flush yok, dış rollback ve savepoint sonrası commit geçiyor; mevcut sürüm yarışları da geçti | Tamamlandı. Aynı ASG varlığını çağıran taraf önceden takip ediyorsa daha sonra açık reload gerekir; gelecekte interceptor/connection seçenekleri ortaklaştırılmalı |
| Y3 | Api/DevelopmentConfiguration.cs, Program.cs, Infrastructure/DependencyInjection.cs, Fakes/UnavailableIdentity.cs; dev/test + açık bayrak; diğer ortamda anon/deny, sahte bayrakla başlangıç reddi. Compose loopback | AsgConfigurationTests, AsgEnvironmentTests; Production/Staging reddi, AUTH yokken 401; Docker 127.0.0.1:8080 | Tamamlandı. Gerçek AUTH ve diğer modüllerin DI tüketicileri birleşmede doğrulanmalı |
| O2 | Api/Properties/launchSettings.json, Program.cs, Web/Assignments/AsgClient.cs, Compose: profille eşleşen hedef, farklı scheme/host/port için istek ve cookie gönderimi reddi | AsgConfigurationTests: 5202/8080/7098 eşleşmesi; üç yanlış origin senaryosu. Gerçek Docker sayfaları HTTP 200 | Tamamlandı. Reverse proxy/gerçek AUTH cookie ve CSRF kabulü birleşmede |
| O3 | Infrastructure/Persistence/AsgSeed.cs: açık opt-in, yalnız kesin fake sağlayıcı tipleri ve sabit 3/4/5 fixture kimlikleri; gerçek kullanıcı sıralamasından seçim yok | O3_seed_requires_explicit_synthetic_fixture; önceki idempotency testi korundu/geçti | Tamamlandı. Gerçek ortak veri seti ertelendi; veritabanı gerçek kullanıcı içeriyorsa seed etkinleştirilmemeli |
| O4 | Application/Assignments/Contracts.cs ve AssignmentService.cs; Web DTO ve SubmissionForm.razor: sunucu CanSave/CanSend/UnavailableReason; UI düğmeleri kapalı ve açıklama | AssignmentRevisionTests: locked/disabled/future/closed API + SSR ve zorlanmış POST; önceki SSR kaydet/çakışma/gönder testleri | Tamamlandı. Zaman değişirse son karar yazma anında sunucunun |
| O5 | IAssignmentStore.cs, AssignmentStore.cs, AssignmentService.cs: text içermeyen SQL projection; filtre, sıralama, limit+1 DB; teslimsiz satırlar LEFT JOIN | O5_projection_filters_and_pages_without_loading_text_entities; büyük metinli teslim, boş öğrenci kümesi, cursor ve teslimsiz filtre | Tamamlandı; Y1/O1 kapsamı da uygulanıyor. Öğrenci ID dizisi CRS sözleşmesi nedeniyle bellekte |
| D1 | Web/Pages/SubmissionList.razor: explicit selected; sonraki URL filtreyi taşır | O5 testi gerçek SSR seçili filtre; D1_SSR_next_page_keeps_filter_and_cursor iki HTTP sayfası ve API sorguları | Tamamlandı |
| D2 | Api/AssignmentEndpoints.cs, Errors/ApiErrorResponse.cs, ApiErrorMiddleware.cs: endpoint bazında hata yanıtları ve standart şema; zorunlu version JSON metadata | D2_OpenAPI_documents_exact_error_sets_and_standard_schema; blank/missing/type/content-type/paging/stale/missing-draft testleri | Tamamlandı. Routing/host kaynaklı genel hatalar endpoint iş kuralı listesi değildir |

Dosyalar src/ altındadır; testler tests/Moodle.UnitTests ve tests/Moodle.IntegrationTests altındadır. Y2/Y3/O2..O5/D1/D2 testlerinin tamamı yeni kapsam denetimleriyle yeniden geçti. Eski testler kaldırılmadı veya beklentileri gevşetilmedi; yalnız bağımlılık kurulumlarına yeni sözleşmeler eklendi.

## Doğrulama

- **Release: 52 birim/mimari + 65 entegrasyon = 117 geçti; 0 başarısız, 0 atlandı.** PostgreSQL 17 Testcontainers ve Coverlet çalıştırıldı. TRX ad karşılaştırması: önceki 97 testin tamamı mevcut, eksik 0; yeni 20 test. İlk kapsam turu 47+63=110 geçti; ek güvenli varsayılan testleriyle son tur 117 oldu.
- Kanıtlar coverage/scope-delivery/*.trx ve iki coverage.cobertura.xml; Git dışında ignored. Önceki 54 ve 97 test sonuçları ayrı tarihsel sonuçtur.
- Entegrasyon satır kapsamı: Domain %100; Infrastructure %95,54; Application %92,77; API %54,38; Web %93,75. Birim ve entegrasyon yüzdeleri birbiriyle toplanmadı. API toplu oranında generated OpenAPI kodu baskın; anlamlı ASG hata ve kapsam senaryoları test edildi, generated kod için kapsam şişirme testi yok.
- Yeni schema/migration yok. Temiz PostgreSQL üzerinde mevcut migration, model/constraint ve concurrency testleri tekrar geçti. Y2 dış transaction ve bekleyen tracker değişikliklerini koruma testleri de geçti.
- Docker Release publish/build ve Compose config başarılı; yeni DI ile uygulama başladı. Health, ödev sayfası, teslim listesi SSR ve teslim listesi API HTTP 200. Geliştirme portu yalnız 127.0.0.1:8080.
- .dockerignore bin/obj/coverage/TestResults/.git/.env/anahtar dosyalarını, docs ve ortamı build context dışında tutar. İleride ortak Dockerfile bu son iki dizini COPY yapacaksa dışlamalar ekipçe gözden geçirilmeli.
- Hassas bilgi/yeni çıktı taraması ve git diff --check son envanterle doğrulandı. Gerçek anahtar/token saptanmadı; Compose local_dev_only sentetik geliştirme parolası ve test çerezleri gerçek sır değildir. Önceden izlenen ortam kanıt logları değiştirilmedi. Tarama mutlak sır yokluğu garantisi değildir.

## Kabul ve ekip sınırları

**FR-03:** ASG teslim listesi; kapsam, yükümlü öğrenci ayrımı, filtre/sayfalama kabulü geçti. **BR-02:** aktif yükümlü öğrenci + modül yetkisi, pencere/kilit/metin türü ve işlem anı kontrolü geçti. **NFR-01:** kendi teslimi ve hedef öğrenci değerlendirme kapsamı, yanlış bağlam ve belirsizliği reddetme testleri geçti. Bu yerel ASG kabulüdür; gerçek AUTH/CRS sağlayıcılarıyla birleşme kabulünün yerine geçmez. WBS-1 kimlikleri değişmedi.

İrem ve Sinem için imzalar, tüm parametreler/dönüş değerleri, hata/cancellation davranışı, domain-ID/context eşlemesi, sınırlı sahteler, DI değiştirme örnekleri ve kabul senaryoları [Ortak sözleşmeler](ORTAK-SOZLESMELER.md) belgesindedir.

PR öncesi bu on bulgu kapsamında bilinen açık ASG teknik engeli kalmadı. İnsan incelemesi bekleniyor. Birleşmeye kalanlar: gerçek AUTH/CRS sağlayıcıları; Course/User/Section FK'leri ve ortak migration snapshot koordinasyonu; tam ortak fixture; sabit ders1 menü bağlantısının CRS gezinmesine taşınması; gerçek oturum, cookie/CSRF, reverse proxy ve AUTH context kabulü. Yeni politikalar scoped; büyük derslerde aday başına politika/aktiflik sorgusunun maliyeti gerçek sağlayıcılarda ölçülmeli. Y2 ayrı yazma tracker'ı, çağıranın önceden takip ettiği aynı ASG varlığını otomatik yenilemez; sonraki okuma için açık reload gerekir.

Playwright kapsam dışı, çalıştırılmadı. Uzak CI/CodeQL çalıştırılmadı. Manuel karşılaştırma koşulu **karşılanmadı**: teslim listesi dahil geliştirme YZ ajanıyla yapıldı, yöntem veya efor uydurulmadı.

---

# Tarihsel ilk teslim — 17 Kasım 2026

Aşağıdaki ilk teslim anlatımı güncel düzeltmelerden önceki durumdur.
# ASG WBS-3 teslim raporu

17 Kasım 2026 — Europe/Istanbul. Modül sahibi: Aslı Ceren Can.

ASG'nin ödev oluşturma, metin teslimi, onayla gönderme, teslim penceresi, sürüm çakışması, teslim gizliliği ve değerlendirici teslim listesi uygulandı. Son doğrulamada **54/54 test geçti**. Bu, mevcut ortak sahteler ve test kimlik/yetki politikaları altında doğrulanmış bir modül teslimidir; gerçek AUTH/CRS ile üretim kabulü değildir. Aşağıdaki dış bağımlılıklar ve manuel karşılaştırma şartı açık kalmaktadır.

## A. Başlangıç durumu

- Çalışma dizini: `/home/asl_ceren_can/moodle-modernization` (Windows'tan WSL UNC yolu).
- İlk yerel commit: `2dbad7f08686ef6959e72224454ac1892680e213`. Çalışma ağacı temiz, dal `main` idi.
- `git pull --ff-only origin main` başarılı; geliştirme başlangıcı: `db2948e4aa542b227a83a41b71b97b90d36d0863`.
- Yeni dal: `codex/asg-wbs3-implementation`. Bu adla önceden dal yoktu.
- Repository/üst dizin taramasında AGENTS.md bulunmadı. WBS dokümanlarının katman kuralları ve mevcut testler esas alındı.
- Seray'ın beş projesi, yedi ortak arayüz/kontekst kaynağı, AUTH/CRS/ASG sahteleri, standart hata middleware'i, Blazor düzeni, Docker Compose, CI ve CodeQL yapılandırmaları mevcut.
- **Altyapı sınırı:** başlangıçta DbContext, EF paketleri, migration, tohumlama ve gerçek AUTH/CRS tabloları yoktu. ASG veri katmanı bu çalışmada eklendi. Diğer modüllerin gerçeklemeleri yazılmadı.
- WSL'de `dotnet` kurulu değildi. Docker Desktop çalışıyordu. Test/derleme için `mcr.microsoft.com/dotnet/sdk:10.0` kullanıldı; koşuda SDK 10.0.404 görüldü.
- Başlangıç derleme/restore başarılı; 8 birim/mimari ve 7 entegrasyon testi geçti. PostgreSQL 17 Testcontainers testi dahil.

### Onaylanan açıklıklar

1. Bölümün derse ait olduğunu sorgulamak için mevcut ICourseDirectory yetersizdi. Kullanıcı onayıyla **ICourseSectionQuery** ve geçici **FakeCourseSectionQuery** eklendi. Mevcut ortak imzalar değişmedi.
2. Kullanıcının ilk görev metni BR-01'i pencere, BR-02'yi sürüm olarak adlandırıyordu. WBS-1'de BR-01 gönderme onayı, BR-02 yetki/pencere olduğundan kullanıcı onayıyla **WBS-1 kimlikleri korundu**. Sürüm çakışması WBS-2 §3.4.1 ek kabul davranışıdır.

## B. Tamamlanan gereksinimler

Birim test dosyası: `tests/Moodle.UnitTests/AssignmentServiceTests.cs`.
Entegrasyon test dosyası: `tests/Moodle.IntegrationTests/AssignmentApiTests.cs`.
Aşağıdaki test ön ekleri ilgili dosyalardaki yöntemlere karşılık gelir; mevcut T-ASG kimlikleri korunmuştur.

| Gereksinim | Uygulama / davranış | Birim testi | Entegrasyon testi | Kabul durumu |
| --- | --- | --- | --- | --- |
| ASG-FR-01 | AssignmentService.CreateAsync; ders/bölüm sorgusu, oluşturma yetkisi, başlık/tarih/metin türü doğrulaması, EF kayıt ve oluşturma formu | T_ASG_02_ASG_FR_01* | T_ASG_02_ASG_FR_01*; ASG_Blazor_create_form_posts_to_api_and_persists | Geçti; gerçek CRS ders/bölüm ekranıyla birleşme bekliyor |
| ASG-FR-02 | SaveAsync, SubmitAsync, MineAsync; ilk kayıt/güncelleme, kalıcı metin, sahip kimliğinin oturumdan alınması | T_ASG_01_ASG_FR_02* | T_ASG_01_ASG_FR_02*; ASG_Blazor_submission_form_saves_conflict_preserves_input_and_confirmation_sends | Geçti |
| ASG-FR-03 | ListAsync; kayıtlı kullanıcılar, teslimli/teslimsiz ayrımı, durum filtresi, öğrenci kimliğine göre artan imleçli sayfalama | T_ASG_03_ASG_FR_03* | T_ASG_03_ASG_FR_03_list_missing_filter_page_empty_and_access | Geçti; ertelenmedi |
| ASG-BR-01 | Onay açık: draft, açıkça gönderme sonrası submitted; onay kapalı: kaydetmek submitted yapar | T_ASG_01_ASG_FR_02_ASG_BR_01* | T_ASG_01_ASG_FR_02_ASG_BR_01* | Geçti |
| ASG-BR-02 | LearnerAsync/CheckWindowAsync; işlem içi yetki, aktif kayıt, açılış/kesim, uzatma ve kilit | T_ASG_04_ASG_BR_02* | T_ASG_04_ASG_BR_02* | Geçti |
| ASG-NFR-01 | ReadAsync/MineAsync; sahip veya modül değerlendiricisi, yanlış ödev/teslim ve diğer öğrenciye 404, oturumsuza 401 | T_ASG_05_ASG_NFR_01* | T_ASG_05_ASG_NFR_01_direct_api_owner_grader_other_student_and_anonymous | ASG denetimleri geçti; gerçek AUTH oturum kabulü kapsamda değil |
| WBS-2 sürüm sözleşmesi | İstek sürümü, EF concurrency token, transaction ve PostgreSQL satır kilidi | WBS2_version_conflict_preserves_text | WBS2_version_conflict_two_concurrent_requests_one_winner; ASG_database_foreign_keys_unique_and_optimistic_token | Geçti; aynı sürümle iki istekte yalnız biri yazar |
| IAssignmentQuery | Gerçek kayıt okuma ve yalnız submitted teslim için true | IAssignmentQuery_returns_real_assignment_and_only_submitted_status | T_ASG_01_ASG_FR_02_ASG_BR_01_save_update_reopen_and_query; submit testi | Geçti |

WBS-1 ASG tablosunda başka M gereksinimi saptanmadı. Başlık uzunluğu gibi dokümanda bulunmayan keyfi alan limitleri eklenmedi. Metin ve başlık boş/yalnız boşluk olamaz; ilk sürüm teslim türü tam olarak `text`tir.

### Önceki Moodle davranışıyla karşılaştırma

- T-ASG-01: kalıcılık, onay açık taslak/ayrı gönderme ve onay kapalı doğrudan gönderme korundu.
- T-ASG-02: ders/bölüm ilişkisi sorgulanıp yeni kayıt oluşturuluyor; gerçek CRS ders ekranında görünme birleşme testidir.
- T-ASG-03: teslim kaydı olmayan kullanıcı da listede `notSubmitted` olarak bulunuyor.
- T-ASG-04: kesimden sonra doğrudan API yazması reddediliyor ve veri değişmiyor. Eski Moodle'ın 404 davranışı yerine WBS-2'nin durum çakışması için 409 sözleşmesi kullanıldı.
- T-ASG-05: görünmeyen başka öğrenci teslimi 404. Eski geliştirici ortamındaki 500/stack trace davranışı taşınmadı.
- WBS-2 sayfalama biçimini ayrıntılandırmadığından, sabit artan learnerId imleci seçildi: varsayılan 50, sınırlar 1–100; imleç son görülen öğrenci kimliği. Yeni işlev kapsamı eklenmedi.

## C. Teknik geliştirmeler

### Dosya haritası

| Katman | Dosyalar | İş |
| --- | --- | --- |
| Alan | `src/Moodle.Domain/Assignments/Assignment.cs`, `Submission.cs`, `SubmissionRule.cs` | Ödev, bireysel metin teslimi, kullanıcı bazlı uzatma/kilit |
| Uygulama | `src/Moodle.Application/Assignments/AssignmentService.cs`, `Contracts.cs` | Kullanım senaryoları, DTO'lar, IAssignmentStore, doğrulama/yetki |
| Ortak ek sözleşme | `src/Moodle.Application/Abstractions/ICourseSectionQuery.cs` | Bölüm–ders sahipliği; kullanıcı onaylı |
| API | `src/Moodle.Api/AssignmentEndpoints.cs` | WBS-2'nin yedi işlemi |
| Veri erişimi | `src/Moodle.Infrastructure/Persistence/MoodleDbContext.cs`, `AssignmentStore.cs`, `AssignmentQuery.cs` | EF mapping, transaction, gerçek IAssignmentQuery |
| Şema | `Persistence/DesignTimeDbContextFactory.cs`, `Persistence/Migrations/20261117103930_AsgInitial.cs`, designer, snapshot | CLI migration üretimi ve sürümlü model |
| Örnek veri | `Persistence/AsgSeed.cs`, `Fakes/FakeCourseSectionQuery.cs` | Mevcut sahtelerle uyumlu, tekrar çalıştırılabilir ASG örneği |
| Web | `src/Moodle.Web/Assignments/AsgClient.cs`, `Pages/AssignmentPage.razor`, `CreateAssignmentPage.razor`, `SubmissionForm.razor`, `SubmissionList.razor`, `SubmissionDetail.razor` | REST istemcisi ve ekranlar |
| Bağlantılar | Api/Program.cs, Infrastructure/DependencyInjection.cs, Web/Layout/NavMenu.razor | DI, rotalar, menü, isteğe bağlı migration/seed |

### API sözleşmeleri

| Metot | Yol | Başarı |
| --- | --- | --- |
| POST | `/api/v1/courses/{courseId}/assignments` | 201, ödev, Location |
| GET | `/api/v1/assignments/{id}` | 200, assignment ve permissions |
| GET | `/api/v1/assignments/{id}/my-submission` | 200, status ve submission/null |
| PUT | `/api/v1/assignments/{id}/my-submission` | 201 ilk kayıt / 200 güncelleme |
| POST | `/api/v1/assignments/{id}/my-submission/submit` | 200, gönderilmiş teslim |
| GET | `/api/v1/assignments/{id}/submissions?status=&cursor=&limit=` | 200, items ve nextCursor |
| GET | `/api/v1/assignments/{id}/submissions/{submissionId}` | 200, yetkili teslim |

İlk teslim sürümü istekte 0; başarılı kayıtta 1 olur, her yazmada artar. JSON hataları mevcut `{ error: { code, message } }` biçimindedir. Ortak middleware'e yalnız malformed HTTP/JSON için güvenli 400 dönmesini sağlayan BadHttpRequestException yakalaması eklendi. Beklenmeyen hata ayrıntıları istemciye aktarılmaz. OpenAPI'de yedi işlem ve oluşturma/güncelleme yanıt türleri doğrulandı.

### Yetki ve zaman

Oluşturma: ders bağlamında `moodle/course:manageactivities` ve `mod/assign:addinstance`.
Görüntüleme: modül bağlamında `mod/assign:view`.
Teslim: aktif ders kaydı ve `mod/assign:submit`.
Liste/değerlendirici okuması: `mod/assign:grade`; başka öğrenci teslimi için ilgili dersin aktif kayıt kontrolü de yapılır.

Öğrenci kimliği istek gövdesinden veya query parametresinden alınmaz. Sunucu TimeProvider kullanır; saklama UTC'dir. Açılış ve kesim anları dahildir; kesimden sonraki an reddedilir. DueAt gecikmeyi etkiler, tek başına yazmayı kapatmaz. CutoffAt varsa daha ileri kullanıcı uzatması kapanışı uzatır. Kilit tüm yazmaları reddeder. Onayla gönderilmiş teslim yeniden düzenlenemez; onaysız akış pencere açıkken sürümle güncellenebilir.

### Kalıcılık ve bütünlük

- Üç tablo: Assignments, Submissions, SubmissionRules.
- Teslim ve kural tablolarından ödeve restrictive FK; teslimde `(AssignmentId, LearnerId, Attempt)` unique; kuralda `(AssignmentId, LearnerId)` bileşik anahtar.
- Pozitif kimlik/sürüm, ilk sürümde attempt=0, geçerli durum/zaman ilişkisi, boş olmayan metin/başlık ve kronolojik ödev tarihleri için check constraint.
- Ders/bölüm ve ödev/öğrenci/deneme indeksleri.
- Sürüm `long`, EF `IsConcurrencyToken`; PostgreSQL ile doğrulandı. API yazıları ödev satırını parametrik `FOR UPDATE` ile kilitler; yetki/pencere/durum/sürüm transaction içinde tekrar kontrol edilir. İlk kayıt yarışları da seri hale gelir. Aynı ödevin farklı öğrencilerinin yazıları da kısa süre sıralanır; ilk sürüm için basitlik tercihidir.
- Migration `Up` yalnız yeni ASG tabloları/indeksleri oluşturur. Mevcut başka tabloları değiştirmez/silmez. `Down`, standart geri alma olarak yalnız bu üç yeni tabloyu kaldırır; **çalıştırılmadı**.
- **Dış FK sınırı:** CourseId, SectionId ve LearnerId için gerçek CRS/AUTH tabloları henüz bulunmadığından fiziksel FK yoktur. Uygulama ortak sorgularla kontrol eder. Diğer modüllerin tablo/anahtarları netleştiğinde ortak migration ile tamamlanmalıdır; var olmayan tablolar taklit edilmedi.

### Örnek veri ve modül bağımlılıkları

Başlangıçta mevcut .NET seed altyapısı yoktu. AsgSeed, SENG101 Ödev 1/2 başlıkları ve onay ayarlarıyla `ortam/ornek-veri.feature` davranışlarının küçük bir alt kümesini oluşturur: gönderilmiş, taslak ve hiç teslim edilmemiş durumlar. Mevcut FakeEnrolmentQuery'nin üç sentetik kimliğini kullanır; farklı kullanıcı tabloları yazmaz. Transaction/advisory lock ve mevcut ödevi değiştirmeme yaklaşımıyla tekrar çalıştırılabilir.

Bu, feature dosyasındaki üç ders/10 öğrencili tam veri setinin birebir kurulumu değildir. SENG201/CENG301 ve gerçek kullanıcı eşlemesi CRS/AUTH sağlandığında genişletilmelidir. Diğer modüllerin sahteleri değiştirilmedi. FakeAssignmentQuery kaldırılıp gerçek AssignmentQuery DI'ya bağlandı. FakeCourseSectionQuery geçicidir. GRD sözleşmesi korunmuştur.

Blazor katmanı yalnız HTTP API'yi kullanır; Application/Infrastructure referansı yoktur. Statik SSR formlarında antiforgery bulunur. Kimlik çerezi sadece yapılandırılmış API adresine taşınır; paylaşılan cookie jar kullanılmaz. Metin Razor tarafından encode edilir. Çakışmada kullanıcının yazdığı metin korunur ve güncel sürüm yükleme bağlantısı gösterilir.

## D. Test ve kalite sonuçları

| Kontrol | Sonuç |
| --- | --- |
| NuGet restore ve Release derleme | Başarılı; son koşuda uyarı/hata yok |
| Birim/mimari | 27 geçti: 8 mevcut + 19 yeni ASG test vakası |
| Entegrasyon | 27 geçti: 7 mevcut + 20 yeni ASG test vakası |
| Mimari | Mevcut 5 test geçti; Web'in diğer Moodle katmanlarına referansı yok |
| Toplam | 54 geçti, 0 başarısız, 0 atlandı |
| PostgreSQL | Gerçek PostgreSQL 17 Testcontainers; migration uygulanarak API testleri |
| Migration | API fixture MigrateAsync; HasPendingModelChanges=false; Compose başlangıcında uygulandı |
| Eşzamanlılık | İki bağımsız HTTP isteği ilk kayıt/güncellemede bir başarı, bir 409; bağımsız EF context'lerinde concurrency istisnası |
| Güvenlik | Sahip/değerlendirici olumlu; diğer öğrenci/anonim/yanlış ödev olumsuz; bozuk JSON 400; metin HTML encode |
| Blazor | Sayfa GET'leri ve gerçek SSR form POST'ları test edildi; Playwright kullanılmadı |
| Compose config | `config --quiet` başarılı |
| Docker imajı | Projenin Dockerfile'ı ile build/publish başarılı |
| Yerel HTTP | Sağlık 200, ödev oluşturma başarılı (id=1), Blazor ödev 200, liste 3 satır |
| CI/CodeQL dosyaları | Değiştirilmedi; GitHub üzerinde çalıştırılmadı |
| Git diff --check | Son kontrolde başarılı |

### Coverlet

Son raporlar `coverage/delivery/` altındadır; TRX eki kopyaları ayrı koşu sayılmadı. Aşağıdaki değerler iki ayrı test projesinin ölçümleridir; birbirine eklenmiş/ortalaması alınmış değildir.

| Kapsam | Satır | Dal |
| --- | ---: | ---: |
| Entegrasyon koşusu, tüm ölçülen kod | %82,62 | %59,96 |
| Entegrasyon — Domain | %100 | %100 |
| Entegrasyon — Application | %92,15 | %91,66 |
| Entegrasyon — Infrastructure | %96,03 | %78,57 |
| Entegrasyon — Api | %51,43 | %33,46 |
| Entegrasyon — Web | %93,84 | %68,33 |
| Birim koşusu — Application | %86,27 | %85,25 |

Kapsam %100 değildir; özellikle API projesinin dal/satır kapsamı düşüktür. Bu sayılar bütün olası hata yollarının veya üretim güvenliğinin kanıtı sayılmaz. Beş proje için tek bir birleştirilmiş coverage raporu oluşturulmadı.

### Tekrar çalıştırma

WSL'de .NET 10 varsa:

```bash
dotnet restore Moodle.slnx
dotnet build Moodle.slnx -c Release --no-restore
dotnet test Moodle.slnx -c Release --collect:"XPlat Code Coverage" --results-directory coverage/delivery --logger:trx
docker compose -p asg-wbs3 -f docker/docker-compose.yml config --quiet
docker compose -p asg-wbs3 -f docker/docker-compose.yml up -d --build
git diff --check
```

Bu makinede SDK olmadığı için kullanılan test çalıştırıcısı:

```bash
docker run --rm \
  -v /home/asl_ceren_can/moodle-modernization:/workspace \
  -v asg-nuget:/root/.nuget/packages \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -w /workspace -e TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet test Moodle.slnx -c Release --collect:"XPlat Code Coverage" \
  --results-directory coverage/delivery --logger:trx
```

Compose `Database__Initialize=true` ile migration uygular. Seed varsayılan kapalıdır; geliştirme örneği için `ASG_SEED=true docker compose -p asg-wbs3 -f docker/docker-compose.yml up -d` kullanılabilir. Mevcut testler seed tekrarını ayrıca doğruladı. `AsgApi:BaseUrl` varsayılanı `http://localhost:8080`; uygulama başka portta çalıştırılırsa bu adres de ayarlanmalıdır.

**Çalışır yerel uygulama:** `http://localhost:8080`. Oluşturma `/courses/1/assignments/new`, doğrulama ödevi `/assignments/1`, liste `/assignments/1/submissions`. `asg-wbs3-modern-1` ve sağlıklı PostgreSQL konteyneri inceleme için çalışır bırakıldı. Veri silmeden durdurma: `docker compose -p asg-wbs3 -f docker/docker-compose.yml stop`.

**Gerçek kimlik sınırı:** Mevcut FakeCurrentUser her isteği yönetici 2 olarak kabul ediyor. Bu nedenle yerel uygulama gerçek öğrenci oturum akışını sunmaz. Testler yalnız test hostunda ayrı kullanıcı/politika gerçeklemeleri enjekte eder; X-Test-User başlığı üretim uygulamasında kullanılmaz. AUTH/CRS tamamlanmadan bu kurulum üretim yetkilendirmesi olarak değerlendirilmemelidir.

## E. Dokümantasyon

- `docs/izlenebilirlik.md`: sadece altı ASG satırı güncellendi; eski kod/test kimlikleri korundu. AUTH/CRS/GRD satırları değişmedi.
- [YZ kaydı](YZ-KAYDI.md): WBS-2 §4.3 şablonu, prompt özeti, onaylar, derleme hataları/düzeltmeler, ölçüm sınırları.
- [Doğrulama özeti](DOGRULAMA.json): makine tarafından okunabilir paylaşılabilir sonuçlar.
- Üretim yöntemi tüm ASG işlevlerinde YZ; insan incelemesi bekliyor. Teslim listesi teknik olarak geliştirildi fakat manuel karşılaştırma koşulu sağlanmadı.
- İnsan ve toplam YZ süresi ölçülmedi; uydurulmadı. Test süreleri geliştirme eforu olarak yazılmadı.
- PR yok; izlenebilirlikte “PR açılınca eklenecek” yazıyor.

## F. Git değişiklikleri

Commit, push, PR ve merge yapılmadı. Başlangıçtan sonra yeni commit yoktur. Değişiklikler inceleme için çalışma ağacındadır. Envanter: **28 yeni dosya, 8 değiştirilen dosya, 1 silinen sahte gerçekleme**. Yeni dosyalar kaynak, EF üretimi, test ve dokümantasyonu birlikte içerir.

Değiştirilen mevcut dosyalar:

- `docker/docker-compose.yml`: migration başlangıç ve isteğe bağlı seed ayarları.
- `docs/izlenebilirlik.md`: altı ASG satırı.
- `src/Moodle.Api/Errors/ApiErrorMiddleware.cs`: bozuk HTTP/JSON güvenli 400.
- `src/Moodle.Api/Program.cs`: ASG/EF/HTTP istemcisi bağlantıları ve isteğe bağlı DB hazırlığı.
- `src/Moodle.Infrastructure/DependencyInjection.cs`: gerçek AssignmentQuery, ASG servis/depo ve bölüm sorgusu kayıtları.
- `src/Moodle.Infrastructure/Moodle.Infrastructure.csproj`: gerekli EF/Npgsql paketleri.
- `src/Moodle.Web/Layout/NavMenu.razor`: mevcut sahte SENG101 için oluşturma bağlantısı.
- `src/Moodle.Web/Moodle.Web.csproj`: HTTP context destekli framework reference; yinelenen paket referansı kaldırıldı.

Silinen tek dosya `src/Moodle.Infrastructure/Fakes/FakeAssignmentQuery.cs`; yerine gerçek uygulama bağlandı. Başka ekip üyelerinin sahteleri korunmuştur.

Yeni kaynaklar C bölümündeki dosya haritası, iki test dosyası ve bu rapor/YZ kaydı/doğrulama özeti/dosya envanteridir. Tam liste ve satır sayıları [Git envanterinde](GIT-DURUMU.txt) bulunur. `git diff --stat` yalnız izlenen dosyaları gösterdiği için yeni dosyalar envanterde ayrıca sayılmıştır.

`docs/WBS-1-analiz.md`, `docs/WBS-2-tasarim.md`, `.github/workflows/ci.yml`, `.github/workflows/codeql.yml` ve `ortam/` için diff boştur. Eski Moodle test kanıtlarına dokunulmadı. Son `git diff --check` başarılıdır.

## G. Kalan işler ve gerçek sınırlar

1. **Playwright uçtan uca akış testi:** açık talimatla kapsam dışı; yazılmadı veya çalıştırılmadı.
2. **AUTH/CRS birleşmesi:** gerçek oturum, rol/bağlam, bölüm sahipliği, kayıt yaşam döngüsü ve varsa değerlendirici öğrenci/grup kapsamının ortak politikada doğrulanması. Mevcut IAccessPolicy tek kullanıcı/yetenek/bağlam sorgusudur; yeni grup servisi geliştirilmedi.
3. **Dış veri bütünlüğü:** gerçek Course/User/Section tablolarına FK eklenmesi; ilgili tablo ve anahtarlar henüz yok. Bu kısım tamamlandı diye sunulmuyor.
4. **Tam ortak veri seti:** mevcut sahteler SENG101/üç kimlik sağlıyor. Üç ders ve 10 öğrencilik birebir örneğin kurulumu gerçek CRS/AUTH eşlemesiyle tamamlanmalı. Başka modüllerin seed verileri değiştirilmedi.
5. **CRS ekranı:** yeni ödevin gerçek ders/bölüm sayfasından keşfi, mevcut ASG sayfasına yönlendirme ve menüdeki geçici ders 1 bağlantısının gerçek CRS gezinmesine bağlanması.
6. **Manuel/YZ karşılaştırması:** teslim listesi de YZ ile üretildi; manuel koşul karşılanmadı. Karşılaştırma çifti değiştirilmedi. Ekip kontrolünde ayrıca çözülmeli.
7. **İnsan kod incelemesi**, ardından kullanıcı tarafından yetkilendirildiğinde commit/PR ve PR numarasının matrise işlenmesi.
8. **Uzak CI/CodeQL** ve modüller birleşince regresyon/güvenlik kabulü. Yerel testlerin geçmesi bunların çalıştığı anlamına gelmez.

## H. Görev dağılımı kontrol tablosu

Görev metnindeki BR adları, kullanıcı onayıyla WBS-1 kimliklerine eşlenmiştir.

| Görev | Durum | Kod/test kanıtı veya sınır |
| --- | --- | --- |
| ASG-FR-01 ödev oluşturma | Tamamlandı | CreateAsync; T_ASG_02*; SSR oluşturma testi. CRS ekran birleşmesi ayrı |
| ASG-FR-02 metin teslimi kaydetme ve gönderme | Tamamlandı | SaveAsync/SubmitAsync; T_ASG_01*; SSR kaydet/gönder |
| Gönderme onayı ayarı (WBS-1 BR-01) | Tamamlandı | İki ayar için birim ve PostgreSQL theory vakaları |
| Teslim penceresi (görevde BR-01, WBS-1 BR-02) | Tamamlandı | CheckWindowAsync; başlangıç/kesim sınırı ve uzatma testleri |
| Sürüm çakışması (görevde BR-02, WBS-2 sözleşmesi) | Tamamlandı | WBS2_version_conflict*; iki HTTP isteği ve EF concurrency testi |
| ASG-FR-03 değerlendirici teslim listesi | Tamamlandı | ListAsync/SubmissionList.razor; T_ASG_03* |
| ASG-NFR-01 teslim gizliliği | Tamamlandı | ReadAsync/MineAsync; T_ASG_05*; gerçek AUTH birleşmesi bekliyor |
| Gerçek IAssignmentQuery | Tamamlandı | AssignmentQuery + DI; birim ve entegrasyon testi |
| Ödev sayfası | Tamamlandı | AssignmentPage.razor; GET testi ve Compose HTTP 200 |
| Teslim formu | Tamamlandı | SubmissionForm.razor; gerçek SSR form POST ve çakışma testi |
| Teslim listesi sayfası | Tamamlandı | SubmissionList.razor; yetkili/teslimsiz GET testi |
| Alan, iş mantığı ve API katmanları | Tamamlandı | C bölümündeki kaynaklar; mimari ve API testleri |
| Her M gereksinimi için birim testi | Tamamlandı | B bölümündeki altı eşleştirme |
| Her M gereksinimi için entegrasyon testi | Tamamlandı | B bölümündeki altı eşleştirme, gerçek PostgreSQL |
| İzlenebilirlik matrisi | Tamamlandı | Altı ASG satırı; PR henüz yok bilgisi |
| Yapay zekâ kullanım kaydı | Tamamlandı | YZ-KAYDI.md |
| YZ ve manuel geliştirme karşılaştırması | Kısmen tamamlandı | YZ kayıtları var; manuel üretim/ölçüm yok |
| Playwright uçtan uca testi | Kapsam dışı | Kullanıcının açık kapsam istisnası |
| AUTH/CRS tablolarına fiziksel FK | Engellendi | Hedef tablolar/anahtarlar henüz mevcut değil |
| Birebir tam ortak örnek veri | Kısmen tamamlandı | ASG davranış alt kümesi var; gerçek CRS/AUTH veri eşlemesi yok |

## Teknik kaynaklar

Uygulama tarafından yönetilen EF concurrency token ve migrations yaklaşımı resmi belgelerle karşılaştırıldı: [EF Core concurrency](https://learn.microsoft.com/en-us/ef/core/modeling/concurrency), [EF Core migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing), [Npgsql concurrency](https://www.npgsql.org/efcore/modeling/concurrency.html). Bu uygulama xmin yerine açık `long Version` concurrency token kullanır; istemci sözleşmesinin sürüm 0/artan sürüm davranışını korur.

Son whitespace doğrulaması (18.11.2026): git diff --check başarılı. Tam dosya envanteri GIT-DURUMU.txt içinde güncellendi.
