> **19.11.2026 v2 güncellemesi:** Güncel durum [ASG/FILE teslim raporundadır](../asg-file-wbs3/TESLIM-RAPORU.md). Gerçek AUTH ve aktif CRS öğrenci kapsamı bağlıdır; yalnız gerçek bölüm modeli/ICourseSectionQuery hâlâ engeldir. Manuel kod yazma zorunluluğu kaldırılmıştır. Aşağıdaki önceki rapor tarihsel kayıttır; eski AUTH/manuel bekleme ifadeleri güncel durum sayılmamalıdır. V2 sonuçları ve devri [doğrulama](../asg-file-wbs3/DOGRULAMA.json), [sözleşmeler](../asg-file-wbs3/ENTEGRASYON.md) belgelerindedir. V1 PR #21 korunur, v2 PR açılmadı.
# PR #21 — origin/main birleşme raporu

18 Kasım 2026. ASG dalı codex/asg-wbs3-implementation, HEAD 317ceb1; merge hedefi origin/main c7a2b66 (CRS fd3e155 / PR #20). Başlangıçta çalışma ağacı temizdi. git fetch origin ardından git merge --no-commit --no-ff origin/main kullanıldı. Rebase/force push yapılmadı. Çözümler index'e alınarak merge commit öncesinde bırakılır; bu çalışma kapsamında commit, push veya PR merge yapılmadı.

## Dört metinsel çakışma

| Dosya | Birleştirilen davranış |
| --- | --- |
| src/Moodle.Api/Errors/ApiErrorMiddleware.cs | Tek BadHttpRequestException catch; CRS'nin HTTP durumuna göre code/message eşlemesi ve ASG'nin ApiErrorResponse gövdesi birlikte korundu. 400/415, genel hata gövdesi, ayrıntısız 500 ve logging mevcut |
| src/Moodle.Api/Program.cs | ASG endpointleri, güvenli ortam doğrulaması, cookie/redirect korumalı ASG istemcisi ve SSR korundu. CRS AddCrsApi/AddCrsWeb, interactive server, static web assets, CRS endpointleri korundu. Database:Initialize etkinse iki DbContext migration'ı uygulanır |
| src/Moodle.Infrastructure/DependencyInjection.cs | Gerçek CrsService, CrsDbContext, ICourseDirectory/IEnrolmentQuery ve gerçek ASG query/store/service kayıtları birlikte var. Sahte kimlik varsayılan değil. Yeni öğrenci sorgusu gerçek CRS'ye, hedef öğrenci değerlendirme politikası güvenli ret sağlayıcısına bağlı |
| src/Moodle.Infrastructure/Moodle.Infrastructure.csproj | EF Relational/Design 10.0.13, Npgsql 10.0.0, DI Abstractions 11.0.0 tekil kayıtları korundu; Design PrivateAssets ve CRS IncludeAssets korunarak yinelenen Npgsql kaldırıldı |

## Onaylanan CRS uyarlamaları

Kullanıcı uygulamadan önce iki değişikliğe açıkça onay verdi:
1. CrsService, IAssignmentLearnerQuery gerçekleştirir. CourseId eşit, Role tam olarak Student, Suspended=false, StartsAt boş/geçmiş, EndsAt boş/gelecek kayıtlar; benzersiz sıralı UserId döner. Öğretmen, Observer, askıya alınmış, henüz başlamamış veya bitmiş kayıtlar hariç. Bilinmeyen ders boş liste. Mevcut ICourseDirectory/IEnrolmentQuery imzaları ve davranışları değişmedi.
2. CrsAuthorization, UnavailableIdentity tipini de auth_not_ready sayar. AUTH hazır olmadığında CRS'nin mevcut 503 güvenlik sözleşmesi ve bütün CRS testleri korunur. ASG'nin anonim 401 davranışı korunur.

Diğer CRS servis, model, migration, snapshot, UI ve test dosyalarının içerikleri origin/main'deki gibi korundu.

## Anlamsal çakışmalar ve güvenli kararlar

- Gerçek CRS ders/kayıt verisi ile sentetik 1/2/3/4/5 kimliklerinin karışması engellendi. Global ICourseDirectory/IEnrolmentQuery artık gerçek CRS'dir. FakeSubmissionAccessPolicy gerçek derslere otomatik bağlanmaz; ISubmissionAccessPolicy -> UnavailableSubmissionScope. Dolayısıyla modül grade izni tek başına teslim ayrıntılarını açmaz.
- CRS'de bölüm tablosu/modeli bulunmuyor. ICourseSectionQuery -> UnavailableCourseSectionQuery false döner. AUTH yetkili test kimliğiyle mevcut gerçek derste ASG oluşturma invalid_section/422 üretir; sırf sectionId 1..4 diye kabul edilmez. Bu bilinçli entegrasyon engelidir; Sinem bölüm modelini ve sorgusunu gerçekleştirmelidir. Bölüm modeli bu işte geliştirilmedi.
- Gerçek CRS ile varsayılan geliştirme başlangıcında sahte admin kullanılmaması için launchSettings ve Compose UseFakeIdentity=false yapıldı. Production/Staging bayrak doğrulaması kaldırılmadı. Gerçek AUTH hâlâ yok; İrem ICurrentUser/IAccessPolicy ve ISubmissionAccessPolicy sağlamalı.
- AsgSeed gerçek CRS ile sentetik kayıt üretmez: mevcut sağlayıcı-tipi ve açık opt-in kontrolü korunur. ASG_SEED=true bu birleşik gerçek-CRS kurulumunda kullanılmamalı; hazır olmayan fixture'da başlangıç hata verir, sessiz veri üretmez. Mevcut sentetik veriler silinmedi/dönüştürülmedi. Paylaşılan/gerçek veri tabanında eski sentetik kimliklerle test yapılmamalı.
- Eski 117 ASG testinin fixture'ı açıkça FakeCourseDirectory/FakeEnrolmentQuery/FakeCourseSectionQuery kaydeder; API beklentileri gevşetilmedi. Eski fallback birim testi, DI artık gerçek CRS olduğundan gerçek sağlayıcı tipini ve ayrı boş fallback davranışını doğrular; hedef erişimi false kontrolü korunur. Buna ek gerçek PostgreSQL birleşme testi varsayılan hedef ret davranışını ve gerçek öğrenci filtresini doğrular.
- Docker otomatik merge'inde CRS'nin 127.0.0.1:55432 PostgreSQL portu ve ASG'nin 127.0.0.1:8080 uygulama portu birlikte korundu. .dockerignore yeni CRS static varlıklarını dışlamıyor.
- NavMenu otomatik merge'inde CRS Dersler bağlantısı ile ASG bağlantısı korunur. ASG'nin sabit ders1 menüsü hâlâ geçici; gerçek CRS ödev/bölüm gezinmesi sonraki ortak iştir. Menü görülmesi yetki vermez.
- App.razor, static asset ayarları, bUnit bağımlılığı, .config/dotnet-tools.json ve CRS JavaScript/test kaynakları kaybolmadı. CRS UI static/interactive hosting testleri geçti; tarayıcı E2E çalıştırılmış sayılmıyor.

## DbContext, migration ve veri bütünlüğü

İki ayrı DbContext korunur: MoodleDbContext ASG tablolarını; CrsDbContext crs_* tablolarını yönetir. İki ayrı snapshot ve context etiketli başlangıç migration'ı birleştirilmedi, yeniden üretilmedi. Aynı PostgreSQL 17 veritabanında ASG->CRS ve CRS->ASG sıraları, tekrarlı MigrateAsync ve HasPendingModelChanges=false yeni testlerle doğrulandı. Ortak __EFMigrationsHistory içinde migration kimlikleri farklı; test edilen mevcut sürümde çakışma yok.

Compose veritabanında Assignments, SubmissionRules, Submissions, crs_categories, crs_courses, crs_enrolments ve __EFMigrationsHistory birlikte görüldü. Dış Course/User/Section FK'leri eklenmedi. Özellikle CRS ders silme işlemi ASG kayıtlarını otomatik temizlemez; ASG mevcut olmayan dersi görünmez sayar, ancak fiziksel yetim kayıt riski ekipçe FK/silme politikası belirlenene kadar sürer.

ASG yazma transaction'ı CRS DbContext işlemlerini otomatik kapsamaz. Mevcut ASG tracker/savepoint/sürüm çakışması testleri geçti; gelecekte iki modüle birlikte yazan işlem için ayrı transaction koordinasyonu gerekir. CRS bağlantısı CRS_CONNECTION_STRING veya ConnectionStrings__Default ortam değişkeninden gelir; ASG IConfiguration üzerinden Default alır. Compose aynı değeri verir. Yerelde sadece appsettings değiştirildiğinde iki sağlayıcının farklı veritabanına gitmemesi için ortam ayarı açık verilmelidir; CRS bağlantı sınıfı bu onay kapsamı dışında değiştirilmedi.

## Doğrulama sonuçları

- Bağımsız Release build: başarılı, 0 warning / 0 error.
- İlk birleşik tur: 100 birim/mimari + 163 entegrasyon = 263 geçti.
- Son Release/Coverlet turu: **100 birim/mimari + 166 entegrasyon = 266 geçti**, başarısız/atlanan 0.
- Önceki 117 testin TRX testName karşılaştırmasında eksik 0. CRS'nin 146 .NET testi korunur. Üç yeni AsgCrsMergeTests vakası eklendi: iki migration sırası, gerçek CRS roster + ASG hedef gizliliği.
- Node: tests/web/crs-api.test.mjs **7/7** geçti. Playwright/browser smoke çalıştırılmadı.
- Kanıt coverage/merge-delivery/*.trx ve iki Coverlet XML'idir (ignored). Entegrasyon satır kapsamı: Domain %100, Infrastructure %96,73, Application %94,62, API %71,69, Web %51,12. Web bUnit testlerinin çoğu ayrı birim koşumundadır; iki rapor oranları toplanmadı.
- Docker build/Release publish, Compose config/up başarılı. HTTP: health/OpenAPI/courses HTML/ASG HTML/CRS JS 200; CRS API 503 auth_not_ready; ASG API 401 not_authenticated. HTML 200, gerçek oturumlu iş akışının kullanılabilir olduğu anlamına gelmez.
- İlk psql meta-komut denemesi kabuk escape nedeniyle başarısızdı; ardından salt okunur SQL tablo envanteri başarılı. Test/derleme başarısızlığı yaşanmadı. Önceki araç onay kotası engeli tekrar denemede kalktı.
- Çakışma işaretleri, diff whitespace, hassas bilgi ve gereksiz çıktı kontrolleri son Git envanteriyle kaydedilir. CRS'nin mevcut yerel geliştirme bağlantı parolaları gerçek sır diye sunulmadı; yeni gerçek token/anahtar eklenmedi.

## Kalan işler / onay sınırı

Git metinsel çakışmaları çözüldü; merge commit ve push kullanıcı onayına bırakıldı. GitHub PR #21'in durumunun değişmesi için bu yerel merge'in daha sonra commit/push edilmesi gerekir; bu yapılmadı.

Gerçek kullanılabilir birleşik akışın bağımlılıkları: Sinem bölüm modeli/sorgusu ve gerçek ders menüsü; İrem gerçek kimlik, capability ve öğrenci değerlendirme kapsamı; ortak FK/silme politikası, fixture ve bağlantı ayarı; AUTH cookie/CSRF ve bağlam kabulü. Bu eksikler testlerin geçmesiyle kapanmış sayılmaz.

Playwright kapsam dışı. Manuel/YZ karşılaştırması hâlâ karşılanmadı; bu düzeltmeler YZ ajanıyla yapıldı. Uzak CI/CodeQL çalıştırılmadı.

Son Git kontrolü: unmerged index girdisi 0; çakışma işareti 0; git diff --check ve --cached --check başarılı. Yeni build/geçici çıktı yok. Hassas desen eşleşmeleri mevcut yerel geliştirme parolaları, CancellationToken ve test antiforgery değişkenleri olarak değerlendirildi; gerçek token/özel anahtar saptanmadı.
