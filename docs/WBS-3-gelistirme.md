# WBS-3 Mevcut Alt Fonksiyonların Yapay Zekâ Desteğiyle Yeniden Geliştirilmesi

> **Yöntem değişikliği (18.11.2026, hocanın onayıyla):** Hiçbir işlev elle (YZ'siz) yazılmaz; bütün kod YZ ile yazılır. K17 karşılaştırması, YZ ile harcanan gerçek efor ile aynı kodun elle yazılsaydı gerektireceği eforun Basic COCOMO tahmini arasında yapılır (WBS-2 §4.4). Hata oranı ve yeniden çalışma yalnızca YZ ile yazılan kod için ölçülür. Herkes YZ kaydına yalnızca YZ ile harcadığı süreyi yazar; COCOMO hesabı §3'te herkes için aynı kurallarla toplu yapılır. Bölümlerdeki "elle geliştirme" ve "karşılaştırma çifti" ifadeleri bu karardan önce yazılmıştır.

Sürüm planı ve kararlar: [WBS-1 §9](WBS-1-analiz.md). Yapay zekâ kayıt şablonu ve karşılaştırma tasarımı: [WBS-2 §4](WBS-2-tasarim.md). İzlenebilirlik: [izlenebilirlik.md](izlenebilirlik.md).

Herkes yalnızca kendi bölümünü düzenler. Her bölümde WBS-2 §4.3 şablonuyla yapay zekâ kaydı bulunur.

## 1. Birinci sürüm: AUTH, CRS, ASG, GRD (M gereksinimleri) ve altyapı

### 1.1 Altyapı ve ortak bileşenler (Seray)

### 1.2 Kimlik ve yetki – AUTH (İrem)

**Kapsam:** AUTH-FR-01, 02, 03, 04, 05, 07, 08, 09; AUTH-BR-02, 03 (WBS-1 §4.1, WBS-2 §3.1). Giriş sayfası.

**Yazılanlar**

| Katman | Dosyalar |
| --- | --- |
| Alan | `src/Moodle.Domain/Auth/SecurityEvent.cs` (güvenlik olayı günlüğü; `AppUser` çerçeveye bağımlı olduğu için Infrastructure'dadır, bkz. aşağıdaki sapma notu) |
| İş kuralları | `src/Moodle.Application/Auth/CapabilityRules.cs` — saf rol+bağlam+yetenek motoru (AUTH-FR-02, 03, 07); EF Core/HTTP'ye bağımlı değildir |
| Veri ve servis | `src/Moodle.Infrastructure/Auth/` — `AppUser` (Identity kullanıcısı), `AuthDbContext` (`IdentityDbContext`, `InitialAuth` geçişi), `AuthService` (giriş/çıkış/parola değişikliği, `SignInManager`/`UserManager` üzerinden), `AuthCurrentUser` (gerçek `ICurrentUser`), `AuthAccessPolicy` (gerçek `IAccessPolicy`), `AuthSeedContributor` (`Order=10`, kimlikler `SeedDataset.UserId` ile aynı sırada) |
| API | `src/Moodle.Api/Auth/AuthEndpoints.cs`: `POST/GET /api/v1/auth/login, logout, session, change-password, permissions`; düz `GET /logout` (sayfa gezintisi) |
| Sayfa | `src/Moodle.Web/Pages/Login.razor`, `src/Moodle.Web/Auth/` (`AuthApiClient`, `AuthBrowserHttpHandler`), `wwwroot/auth-api.js`; `NavMenu.razor`'a `AuthorizeView` ile giriş/çıkış bağlantısı eklendi |

**Testler:** 17 yeni birim testi (`CapabilityRulesTests`, saf rol/bağlam/yetenek mantığı), 9 yeni entegrasyon testi (`AuthEndpointTests`, gerçek PostgreSQL + gerçek HTTP çerezi ile giriş/çıkış/oturum/kilitlenme/parola değişikliği/yetki sorgulama; WBS-1 §4.1.10'daki T-AUTH-01, 04, 05, 06, 09 senaryolarını izler). Toplam paket (tüm modüller) 265 birim + 278 entegrasyon testiyle yeşil.

**Tasarım kararları ve belgeden sapmalar**

- **`AppUser`, Domain'de değil Infrastructure'da.** WBS-2 §3.1.2 tasarımı `AspNetUsers`'ı Domain'in bir parçası gibi anlatır; ama `IdentityUser<int>`'ten türeyen bir sınıf ASP.NET Core Identity çerçevesine bağımlıdır ve Domain'in "hiçbir şeye bağlı değildir" kuralını (WBS-2 §2.2, `ArchitectureTests.Domain_hicbir_katmana_bagli_degildir`) bozar. Bu yüzden `AppUser` bilerek Infrastructure'da tutuldu; Domain'de yalnızca çerçeveden bağımsız `SecurityEvent` kaldı.
- **`CourseRoleAssignments` ayrı bir tablo değil.** WBS-2 §3.1.2 ders bazlı rol için yeni bir tablo öneriyordu; ama CRS (Sinem) zaten kendi `crs_enrolments.Role` alanını üretmişti. İkinci bir kaynak açmak yerine `IEnrolmentQuery`'ye tek bir salt okunur sorgu eklendi: `FindActiveRoleAsync(userId, courseId)`. CRS'nin gerçek gerçeklemesi (`CrsService`) ve beş test sahtesi bu imzayı aldı. AUTH, CRS'nin arayüzüne bağlanır, uygulamasına değil (ortak kural).
- **Kontenjan/onay yerine basit rol modeli.** Yetki motoru (`CapabilityRules`) sistem rolleri (`SiteAdmin`/`Manager`, ASP.NET Identity rolleri) ile ders bağlamındaki rolü (`EditingTeacher`/`Teacher`/`Student`, CRS'den) birlikte değerlendirir. Moodle'daki düzenleme yetkisiz öğretmen ("teacher" archetype) ile düzenleme yetkili öğretmen ("editingteacher") ayrımı korunmuştur (WBS-1 §4.1.7): yalnızca `EditingTeacher` ders ayarlarını ve kayıtları yönetebilir, ikisi de notlandırabilir.
- **Yetkisiz isteğe 404 değil 401/403 döner (Sinem'in inceleme notuyla doğrulandı).** WBS-3 görev listesi "yetkisiz isteğe... 404 dönmesi" diyordu; ama WBS-2 §3.1.3'teki (Sinem'in kendi AUTH tasarımı) hata tablosu üç ayrı kodu açıkça birbirinden ayırır: **401** oturum yokken, **403** oturum var ama yetki yokken, **404** yalnızca *kaynak* bulunamadığında veya güvenlik politikası gereği gizlendiğinde. AUTH'un kendi uçlarının (login/logout/session/permissions) gizlenecek bir "kaynağı" yoktur; bu yüzden kodda `not_authenticated` (401) ve — CRS/GRD'nin zaten kullandığı — `forbidden` (403) kullanılmıştır. Bu, WBS-2 §3.1.3 ile **tam uyumludur**; fark WBS-3 görev metninin ifadesiyledir, koddaki bir sapma değildir. (404'ün gerçekten gerektiği yer FILE modülüdür — orada "dosya" diye somut bir kaynak vardır ve varlığı sızdırılmaması gerekir; bkz. §2.2.)
- **Yanlış parola ile askıya alınmış hesap aynı mesajı alır.** Gerçek Moodle'da da doğrulanan davranış budur (WBS-1 §4.1.10, T-AUTH-04); hangisinin doğru olduğu sızdırılmaz (AUTH-NFR-01/02). Kilitlenme (`account_locked`) ayrı ve belirgin bir mesaj alır — bu da Moodle'ın gerçek davranışıyla örtüşür (T-AUTH-06).
- **`/logout` oturumsuz çağrıda da `204` döner (401 değil).** Çıkışın idempotent olması REST'te iyi bilinen bir yaklaşımdır; zaten oturumu olmayanı hata ile karşılamanın güvenlik ya da bilgi sızdırma açısından bir yararı yoktur.
- **Parola politikası geliştirme/test odaklı gevşetildi** (`RequireDigit`/`RequireUppercase`/vb. kapalı, en az 4 karakter) ki örnek veri kuralı (`ortam/ornek-veri.feature`: şifre kullanıcı adıyla aynı, ör. `ogrenci1`/`ogrenci1`) AUTH'ta da çalışsın. Bu üretim şifre politikası değildir, yalnızca ortak örnek veriyle uyum içindir.
- **Depodaki AUTH sahteleri (`FakeCurrentUser`/`FakeAccessPolicy`) silinmedi**, yalnızca açıklamaları güncellendi: artık geçici bir "İrem bitirince kaldırır" notu değil, `Development:UseFakeIdentity` ile açıkça istenen, yalnızca Development/Testing ortamında izinli bir geliştirici kolaylığı. Varsayılan (bu bayrak kapalıyken) her ortamda — Production dâhil — gerçek `AuthCurrentUser`/`AuthAccessPolicy` kullanılır.
- **CRS/GRD'nin "auth_not_ready" (503) testleri güncellendi.** AUTH artık gerçek olduğundan oturumsuz istek "auth_not_ready" değil, doğrudan "not_authenticated" (401) alır (`CrsUnavailableAuthTests`, `GrdUnavailableAuthTests`, `GrdReportEndpointTests`, `CrsBlazorHostingTests`, `GrdBlazorHostingTests`). `CrsAuthorization`/`GrdAuthorization`'ın kendisi değiştirilmedi; sahte/placeholder tespiti aynen kaldı, yalnızca varsayılan artık o tespite takılmıyor.
- **`AsgConfigurationTests.Default_infrastructure_does_not_impersonate_admin`** artık çıplak bir `ServiceCollection` üzerinden `IAccessPolicy.HasCapabilityAsync`'i çağırmıyor (gerçek gerçekleme artık veritabanına bağlı olduğundan bağlantısız ortamda anlamsız/kırılgan olurdu); aynı koruma `AsgEnvironmentTests.Production_without_AUTH_returns_401_instead_of_fake_admin` ile uçtan uca zaten sınanıyor.

**Yapay zekâ kaydı**

**Model ve sürüm:** Claude Code (Claude Sonnet 5, düşünme seviyesi: Orta-Yüksek).
**Kullanılan adım(lar):** Kodun anlaşılması (iskelet, CRS/GRD desenleri, mevcut arayüzler), gereksinim/tasarım eşleştirmesi, hedef dilde kod üretme, test senaryosu üretme (WBS-2 §4.1).
**Verilen bağlam:** WBS-1 §2, §4.1 (AUTH analizi), WBS-2 §2 (mimari, katman kuralları), §3.1 (Sinem'in AUTH tasarımı), §4 (bu bölümün kendisi — YZ kaydı ve karşılaştırma şablonu), WBS-3 görev tanımı; `Application/Abstractions` arayüzleri ve sahte/`Unavailable` gerçeklemeleri; CRS ve GRD modüllerinin mevcut kodu (desen olarak).
**Yapılan iş:** Mevcut iskelet ve CRS/GRD/ASG kodu incelendikten sonra: saf yetki motoru, ASP.NET Core Identity tabanlı veri modeli ve geçiş, gerçek `ICurrentUser`/`IAccessPolicy`, giriş/çıkış/parola değişikliği/yetki sorgulama API'leri, tarayıcı tabanlı (CRS/GRD ile aynı desen) giriş sayfası, örnek veri tohumlayıcı, birim ve entegrasyon testleri; ayrıca CRS/GRD'nin "AUTH henüz hazır değil" test varsayımlarının güncellenmesi.
**Yapılan düzeltmeler:**
- Yapay zekânın kendi düzelttikleri: ilk derleme denemesinde eksik `using` (`SameSiteMode`, `StatusCodes`) giderildi. İlk tam test koşusunda CRS/GRD'nin üç "auth_not_ready" testinin ve bir birim testinin (varsayılan olarak artık gerçek AUTH kullanıldığı için) beklenen şekilde bozulduğu görüldü ve güncellendi. `AppUser.Id`'nin `ValueGeneratedNever` olması nedeniyle test kullanıcıları oluştururken birincil anahtar çakışması oluştu; test fixture'ına açık, çakışmasız bir kimlik sayacı eklendi. ASP.NET Core Identity'nin kilitleme eşiğinin beşinci (altıncı değil) başarısız denemede devreye girdiği ilk testte yanlış varsayılmıştı; test düzeltildi. `/permissions` ucunun her zaman ders bağlamlı rol sorgusu yapması nedeniyle salt yönetici yetkisini sınayan test CRS'nin (bu izole fixture'da bulunmayan) veritabanına erişmeye çalışıp hata verdi; test, CRS/GRD'nin kendi testlerinde zaten kullanılan desenle (`IEnrolmentQuery`'yi sahte ile değiştirme) düzeltildi.
- İnsan incelemesi: Oturum çalıştırma sırasında Docker Desktop bellek baskısı altında bir kez çöktü (Moodle PHP ortamı ile .NET Testcontainers'ın aynı anda çalışması); yeniden başlatılıp testler tekrar koşturuldu. Kodun satır satır insan incelemesi henüz tamamlanmadı — bu PR'ın kendi incelemesi Seray'a düşüyor (kod inceleme zinciri).

**Harcanan süre:** Araştırma (iskelet/CRS/GRD'yi okuma, arayüzleri çıkarma) ve kod yazımı toplam yaklaşık 35–40 dakika (19.11.2026, 12:30 civarı başlayıp 13:30 civarı bitti); test çalıştırma/düzeltme döngüleri (Testcontainers koşuları dâhil) ayrıca yaklaşık 20 dakika sürdü. Bu süreler Claude Code'un kendi çalışmasını ve insanın yönlendirme/okuma süresini birlikte içerir; ayrıştırılmış ölçüm yapılmadı.

**Elle geliştirme:** Karşılaştırma çifti için ikinci işlev (hesap durumu kontrolü ve başarısız giriş kaydı) de bu oturumda YZ ile üretildi; ayrı, bağımsız bir elle geliştirme yapılmadı (GRD'nin §1.5'teki yaklaşımıyla tutarlı: elle geliştirme yoksa efor Basic COCOMO (raporda toplu hesaplanır, §3) ile tahmin edilir). Hocanın kararıyla elle geliştirme yapılmaz; karşılaştırma, YZ ile harcanan gerçek efor ile aynı kodun Basic COCOMO tahmini arasında yapılır (§3).

**Kanıt:** Bu PR (şube: `irem/auth-wbs3`).

### 1.3 Ders ve kayıt – CRS (Sinem)

**Kapsam:** CRS-FR-01–FR-09, CRS-NFR-01, NFR-02 (WBS-1 §4.2, WBS-2 §3.2). Kategori/ders CRUD, kısa ad ve kimlik numarası benzersizliği, tarih/TAB doğrulaması, kayıt CRUD ve rol atama, gerçek `ICourseDirectory`/`IEnrolmentQuery`, Blazor ders listesi, detay ve katılımcılar sayfaları.

**Yazılanlar** (PR #20, commit `fd3e155`, 17.11.2026)

| Katman | Dosyalar |
| --- | --- |
| Alan ve kurallar | `src/Moodle.Domain/Courses/CourseEntities.cs`, `src/Moodle.Application/Courses/CourseValidation.cs`, `CrsConflictException.cs` |
| Veri ve servis | `src/Moodle.Infrastructure/Courses/`: `CrsDbContext`, `InitialCrs` migration, `CrsService` (`ICourseDirectory`, `IEnrolmentQuery`, `IAssignmentLearnerQuery` gerçeklemesi) |
| API | `src/Moodle.Api/Courses/`: `CrsEndpoints`, `CrsAuthorization`, `CrsEndpointFilter`, `CrsRequests` |
| Sayfalar | `src/Moodle.Web/Crs/`, `Pages/Courses.razor`, `CourseDetails.razor`, `CourseParticipants.razor`, `wwwroot/crs-api.js` |

**Testler:** 17.11.2026 doğrulamasında tüm paket 31 birim ve 99 entegrasyon testiyle geçti (`docs/crs-api.md`). Sonraki PR'larda M gereksinimlerine servis ve veri modeli düzeyinde birim testleri (`CrsMRequirementTests`) ve gerçek AUTH oturumuyla entegrasyon testleri (`AuthCrsIntegrationTests`) eklendi (PR #47, §2.3).

**Tasarım kararları ve belgeden sapmalar**
- **Kayıt yöntemi modeli uygulanmadı (CRS-FR-06, FR-08, NFR-01).** WBS-2 §3.2'deki `EnrolmentMethods` tablosu ve `enrolmentMethodId`/`roleId` API alanları yoktur; her ders tek ve örtük bir manuel kayıt yöntemiyle çalışır. Mükerrer kayıt `(CourseId, UserId)` benzersizliğiyle engellenir (kayıt yöntemi düzeyindeki kısıttan daha sıkı). Rol, `crs_enrolments.Role` alanında tutulur. AUTH (`AuthAccessPolicy`), ASG, GRD ve FILE `IEnrolmentQuery` üzerinden ders başına tek kayıt varsayar (`FindActiveRoleAsync` tek değer döner); çoklu kayıt yöntemi bu sözleşmenin ekip kararıyla değiştirilmesini gerektirir. Bu nedenle tasarım uyumu tam değildir.
- **Ders silme ve diğer modüllerin verileri (CRS-FR-05, NFR-02).** CRS kendi kayıtlarını aynı transaction'da cascade ile siler. ASG, GRD ve FILE verileri bu teslimde silinmedi; olay iletimi PR #47 ile eklendi, ancak bu modüller için `course.deleted` uzantısı henüz yoktur (§2.3, sınırlılıklar).
- **Yetkisiz isteğe 401/403** döner (CRS ve AUTH tasarımıyla uyumlu; ayrıntı §1.2).

**Yapay zekâ kaydı**

**Model ve sürüm:** _Doldurulacak._ Commit (`fd3e155`) ve CRS belgelerinde kullanılan araç/model kaydı yoktur; doğrulanamadı.
**Kullanılan adım(lar):** _Doldurulacak (doğrulanamadı)._
**Verilen bağlam:** _Doldurulacak (doğrulanamadı)._
**Yapılan iş:** Yukarıdaki dosyalar ve testleri (PR #20). Kodun YZ desteğiyle üretildiği kod içi notlarda ve CRS belgelerinde belirtilir (`CrsService` "YZ destekli" yorumları, `docs/crs-veritabani.md`).
**Yapılan düzeltmeler:** _Doldurulacak (doğrulanamadı)._ (`CrsService.EnrolAsync` üzerinde "YZ taslağıdır" notu bulunur.)
**Harcanan süre:** PR bazında ayrılmadı; AUTH/CRS WBS-3 toplamına dahildir (§2.3, "Süre", geçici tahmin).
**Elle geliştirme:** Yapılmadı (18.11.2026 yöntem değişikliği); elle efor §3'te Basic COCOMO ile tahmin edilir.
**Kanıt:** PR #20.

### 1.4 Ödev ve teslim – ASG ve uçtan uca akış testi (Aslı Ceren)

19.11.2026: V1 PR #21 geçmişi korunmuştur. Güncel main 3b9ccd6 üzerinde gerçek AUTH/CRS bağlantısı tamamlandı: ASG yetenekleri, ödev→ders bağlamı, aktif/yükümlü öğrenci ve ortak liste/ayrıntı kapsamı. Ders sayfası gerçek CourseId ile ASG listesine gider. CRS bölüm modeli bulunmadığından oluşturma ve güvenli ortak ASG seed reddedilir; Sinem bağımlılığıdır. Tam Playwright A kabulü bu nedenle engellidir. Ayrıntılar: [v2 teslimi](asg-file-wbs3/TESLIM-RAPORU.md), [test sonuçları](asg-file-wbs3/DOGRULAMA.json).

Manuel kod yazma zorunluluğu kullanıcı tarafından kaldırılmıştır; geçmiş üretim kayıtları değişmez. Bu çalışmada ayrı manuel/YZ üretim veya karşılaştırma çalışması yürütülmedi.

### 1.5 Değerlendirme – GRD (Fatma Zehra)

**Kapsam:** GRD-FR-01, FR-02, FR-03, BR-01, BR-02, NFR-01 (WBS-1 §4.5, WBS-2 §3.5).

**Yazılanlar**

| Katman | Dosyalar |
| --- | --- |
| Alan | `src/Moodle.Domain/Grading/GradingEntities.cs` |
| İş kuralları | `src/Moodle.Application/Grading/` (`GradeValidation`, `GradeVisibility`, `GradebookPaging`, `GradeResults`, `GrdConflictException`) |
| Veri ve servis | `src/Moodle.Infrastructure/Grading/` (`GrdDbContext`, `GrdService`, `InitialGrd` geçişi) |
| API | `src/Moodle.Api/Grading/`: `GET`/`PUT /assignments/{id}/learners/{learnerId}/attempts/{attempt}/grade`, `GET /courses/{id}/my-grades`, `GET /courses/{id}/gradebook` |
| Sayfalar | `src/Moodle.Web/Grd/`, `Pages/GradeEditor.razor`, `MyGrades.razor`, `Gradebook.razor`, `Grades.razor` |

**Testler:** 129 birim testi, 97 entegrasyon testi (Testcontainers ile PostgreSQL 17), 14 Node testi ve tarayıcı duman testi. Test adlarında gereksinim kimlikleri geçer.

**Tasarım kararları ve belgeden sapmalar**
- Düzen CRS ile aynıdır: iş mantığı `Infrastructure` içindeki serviste, saf kurallar `Application` içinde.
- Yetkisiz istek `403`, AUTH sahte gerçeklemedeyken `503 auth_not_ready` döner (CRS ile aynı).
- Kilitli veya elle değiştirilmiş nota puan yazma `409` ile reddedilir. Moodle'da da değerlendirme formu bu durumda puanı değiştirmez (`public/mod/assign/locallib.php:10025-10032`, `grading_disabled`).
- Daha yeni denemesi olan öğrencide eski denemenin puanı defteri ezmez (Moodle'daki gibi).
- Ders ve kategori toplamları 1. sürümde hesaplanmaz; toplam politikası tanımlanınca eklenecek.
- Not defterinde öğrenciler kimlikle görünür; AUTH'ta kullanıcı bilgisi arayüzü gelince ad gösterilecek.

**Yapay zekâ kaydı**

**Model ve sürüm:** Claude Code (Claude Opus 5.5, düşünme seviyesi: Medium). Adım promptları ve çıktıların incelenmesi için ayrıca Claude (claude.ai) sohbet asistanı kullanıldı.
**Kullanılan adım(lar):** Kodun anlaşılması, hedef dilde kod üretme, test senaryosu üretme (WBS-2 §4.1).
**Verilen bağlam:** WBS-1 §4.5, WBS-2 §2 ve §3.5, CRS modülünün kodu (desen olarak), `Application/Abstractions` arayüzleri ve sahte gerçeklemeleri.
**Yapılan iş:** Beş adımda veri modeli ve geçiş, puan okuma ve yazma, öğrenci not raporu, not defteri ve Blazor sayfaları ile bunların testleri üretildi.
**Yapılan düzeltmeler:**
- Yapay zekânın kendi düzelttikleri: Geçiş gereksiz iki indeks nedeniyle bir kez yeniden üretildi. Test verisindeki bir hata mesajı düzeltildi. Bir testte dizilerin karşılaştırma biçimi düzeltildi. Sayfa adımında iki derleme öncesi düzeltme ve bir test verisi tür hatası giderildi. Bir gerçek hata yakalandı: sunucudan `422` geldiğinde puan alanı ekran okuyucuya geçersiz olarak bildirilmiyordu.
- İnsan incelemesi: Her adımın raporu belgeyle karşılaştırıldı. Belgeden sapmalar tek tek değerlendirilip kabul edildi (yukarıdaki kararlar). Koda elle düzeltme gerekmedi.

**Harcanan süre:** Adım süreleri toplamı yaklaşık 1,1 saat (18.11.2026, 20:30–21:52). Bu süre Claude Code'un çalışmasını ve insanın prompt hazırlama, okuma ve karar verme süresini birlikte içerir; modelin kendi çalışma süresi ayrıca ölçülmedi. Doküman adımları bu süreye dahil değildir.

| Adım | İş | Süre |
| --- | --- | --- |
| 1 | Ortamın ve CRS deseninin incelenmesi | ~16 dk |
| 2 | Veri modeli, tablolar, geçiş | ~9 dk |
| 3 | Puan okuma ve yazma | ~12 dk |
| 4 | Öğrenci not raporu ve not defteri | ~8 dk |
| 5 | Blazor sayfaları | ~19 dk |

**Elle geliştirme:** GRD'de elle yazılmış işlev yoktur. Karşılaştırma için elle yazım eforu Basic COCOMO (raporda toplu hesaplanır, §3) ile **tahmin** edilmiştir. Hocanın şartı "seçilmiş bazı alt fonksiyonlar" olduğundan karşılaştırma diğer modüllerin çiftleriyle yapılır.

**Kanıt:** PR #23

## 2. İkinci sürüm: FILE, ödeve dosya teslimi, AUTH ve CRS'nin S/C gereksinimleri

### 2.1 Dosya altyapısı (Seray)

**Yapılan iş:** FILE veri modeli (`file_blob`, `file_entry`, `file_draft`, `file_setting`) ve EF Core geçişi; `IFileStore`, `IFileService`, `IFilePolicy` arayüzleri ve sahte gerçeklemeleri (PR #27). İçerik adresli yerel disk deposu (`LocalDiskFileStore`: SHA-256, `ab/cd/<özet>`, geçici dosyaya yazıp tek adımda taşıma, özet doğrulaması) ve veritabanı–disk tutarlılık denetimi (`FileConsistencyChecker`).
**Tasarım kararı:** Yazma sırası "önce depoya, sonra veritabanına"dır. Çökme olursa en kötü sonuç sahipsiz dosyadır (güvenle silinir); kaydı olup dosyası olmayan içerik oluşmaz.

#### YZ kaydı (Seray, FILE altyapısı)

**Model ve sürüm:** Claude Sonnet 5.5 (Claude Code)
**Kullanılan adım(lar):** Kod üretimi, test yazımı
**Verilen bağlam:** WBS-2 §3.6 (veri ve API tasarımı), WBS-3-kalan-is-bolumu.pdf Seray bölümü, mevcut modül kalıpları (CRS, GRD)
**Yapılan iş:** Varlıklar, `FilesDbContext`, geçiş, arayüzler ve sahteleri, `LocalDiskFileStore`, `FileConsistencyChecker` ve testleri
**Yapılan düzeltmeler:** Seed verisini okuyan ilk test çalışma zamanı modelinde seed bulunmadığı için kırıldı, tasarım zamanı modeliyle düzeltildi. Sahte dosya politikası üretimde de kayıtlı kalacaktı, fail-closed kalıbına (`UnavailableFiles`) çevrildi. İnsan düzeltmesi: yok.
**Harcanan süre:** İnsan: ~1 saat (tahmin) · YZ ile çalışma: ~1 saat (oturum kaydı, alt sınır)
**Kanıt:** PR #27 ve depolama PR'ı

**Karşılaştırma (depolama gerçeklemesi, tutarlılık kontrolü):** Elle yazım eforu COCOMO ile tahmin edilir (Basic, organik, 152 saat/kişi-ay; hoca yönergesi). Küçük kodda tahmin yüksek çıkar, gösterge olarak okunmalıdır.

| İş | Satır | Tahmini elle efor |
| --- | --- | --- |
| `LocalDiskFileStore` | 107 | ~35 saat |
| `FileConsistencyChecker` | 27 | ~8 saat |
| `FileCourseDeletedExtension` | 30 | ~9 saat |

Bu tablo ön tahmindir; COCOMO hesabı raporda herkes için aynı kurallarla toplu yapılır (§3).

#### YZ kaydı (Seray, `course.deleted` dinleyicisi)

**Model ve sürüm:** Claude Sonnet 5.5 (Claude Code)
**Kullanılan adım(lar):** Kod üretimi, test yazımı
**Verilen bağlam:** CRS'nin `ICrsEventExtension` arayüzü ve olay altyapısı (`CrsChangeEvent`, `CrsEventExtensionPipeline`), `CourseFilePolicy`, `FileService.SoftDeleteAsync`
**Yapılan iş:** `FileCourseDeletedExtension` (`src/Moodle.Infrastructure/Files/`): `course.deleted` olayında dersin dosyalarını (`component = course`, bağlam = ders kimliği) çöpe taşır, başka kayıtça kullanılmayan içeriği boşa düşürür. Kalıcı silme temizlik görevinde kalır (FILE-BUG-04 yeniden denetimi korunur). Tekrar iletime dayanıklıdır: yalnızca `DeletedAt = null` kayıtlar işlenir. Ödev teslim dosyaları (`mod_assign`) ASG'nin kapsamındadır, dokunulmadı. Gerçek PostgreSQL ile 4 entegrasyon testi (`FileCourseDeletedTests`).
**Yapılan düzeltmeler:** Düzeltme gerekmedi, testler ilk çalıştırmada geçti. İnsan düzeltmesi: yok.
**Harcanan süre:** İnsan: ~0,3 saat (tahmin) · YZ ile çalışma: ~0,3 saat (tahmin; oturum kaydı alt sınırı ~0,1 saat)
**Kanıt:** PR (`seray/file-course-deleted`)

### 2.2 Dosya kaydetme, indirme, silme ve kota (İrem)

**Kapsam:** FILE-FR-02, 03, 04, 05, 09; FILE-BR-10, 11, 12, 13, 14; FILE-BUG-01 (WBS-1 §4.6, WBS-2 §3.6).

**Yazılanlar**

| Katman | Dosyalar |
| --- | --- |
| İş mantığı ve veri | `src/Moodle.Infrastructure/Files/FileService.cs` — gerçek `IFileService`: taslak→kalıcı taşıma (`CommitAsync`), yetkili okuma/indirme (`OpenAsync`), silme/çöp (`DeleteAsync`), kota (`GetQuotaAsync`); `UserFilePolicy.cs` — `component="user"` için taslak ve özel dosya sahiplik politikası |
| API | `src/Moodle.Api/Files/FileEndpoints.cs`: `POST /file-areas/{contextId}/{component}/{area}/{itemId}/commit`, `GET .../files`, `GET /files/{id}`, `GET /files/{id}/content` (akış, `Content-Disposition`), `DELETE /files/{id}`, `GET /users/me/quota` |
| DI | `DependencyInjection.cs`: `IFileService` artık (fakes kapalıyken) gerçek `FileService`'i bağlıyor; `IFilePolicy` olarak `UserFilePolicy` kaydedildi |

**Taslak/yükleme (FR-01, 07, 08, 10, 11) hakkında not:** `IFileService` tek bir arayüz olduğundan ve commit/indirme kendi testlerimde uçtan uca çalışabilsin diye, `CreateDraftAsync`/`AddToDraftAsync`/`ListDraftAsync`/`DeleteFromDraftAsync` için de çalışan bir **taban** yazıldı (temel ad/boş dosya kontrolü dâhil). Ad benzersizleştirme, boyut/tür sınırı ve antivirüs (FR-07, 08, 10, 11) — Aslı Ceren'in kapsamı — bu tabanın üzerine eklenecek; bu yüzden bu uçların HTTP yüzeyi (`POST /file-drafts` vb.) bilerek açılmadı, yalnızca servis seviyesinde (testlerimde doğrudan `IFileService` çağrılarak) kullanılıyor.

**Testler:** 4 yeni birim testi (`UserFilePolicyTests`, saf sahiplik mantığı), 6 yeni entegrasyon testi (`FileEndpointTests`, gerçek PostgreSQL + gerçek HTTP): kaydetme, zorla indirme, yetkisiz erişimde 404, silme+kota, kayıtsız bileşene commit reddi, paylaşılan içeriğin yalnızca son referans silinince boşa düşmesi (FILE-BR-13/14 — PDF'te "henüz testi yok" diye işaretliydi, bu PR'da eklendi). Tüm paket 270 birim + 283 entegrasyon testiyle yeşil.

**Tasarım kararları ve belgeden sapmalar**

- **Seray'ın gerçek (yerel disk) `IFileStore`'u henüz yok.** Yalnızca tablolar, arayüzler ve sahteler (ilk PR) landı. `FileService` yalnızca `IFileStore` **arayüzüne** bağlıdır; testlerimde Seray'ın `FakeFileStore`'unu (bellek içi) kullandım — tıpkı CRS/GRD'nin AUTH'u beklerken yaptığı gibi. Seray gerçek depoyu bağladığında `FileService` hiç değişmeden gerçek diske yazmaya başlayacak.
- **`GetQuotaAsync`, `IFileService`'e eklendi.** Orijinal arayüzde kota yoktu; FR-09 için gerekli olduğundan eklendi (AUTH'ta `IEnrolmentQuery.FindActiveRoleAsync` eklemesiyle aynı gerekçe: arayüz, gerçek ihtiyaç ortaya çıkınca büyütülür). `FakeFileService` ve `UnavailableFiles` da güncellendi.
- **Yetkisiz erişimde 403 değil 404** (FILE-BR-11, 12): WBS-2 §3.6.4 tasarımıyla birebir — dosyanın var olup olmadığı bile sızdırılmaz. Bu, AUTH'un genel 401/403 yaklaşımından kasıtlı olarak farklıdır (orada "kaynak" kavramı yok); FILE'ın kendi "sahip bileşen" modeli bunu gerektiriyor.
- **İndirme `Results.File(..., enableRangeProcessing: true)` ile akış hâlinde** (NFR-01): içerik belleğe tam yüklenmeden akıtılır ve HTTP Range isteklerini destekler. k6 ile 50 eş zamanlı kullanıcı ölçümü (T-FILE-15) bu oturumda **çalıştırılmadı**; gerçek ölçüm için uygulamanın `docker compose up` ile ayağa kaldırılıp çalıştırılması gerekir (WBS-1 §8'deki Moodle ölçümüyle aynı yöntem). Kod gereksinimi (akış, doğru başlıklar) karşılanmıştır; sayısal p95 sonucu bekliyor.
- **FILE-BR-14'ün testi eklendi** (PDF'te açıkça eksik olduğu belirtilmişti): paylaşılan içeriğin yalnızca gerçekten son etkin kaydı silinince `FileBlob.OrphanedAt` işaretlendiği, hâlâ başka bir kayıt tarafından kullanılıyorsa işaretlenmediği doğrulandı. Gerçek kalıcı silme (temizlik görevi) Fatma Zehra'nın kapsamındadır; bu PR yalnızca "boşa düşme" işaretini doğru koyar.

**Yapay zekâ kaydı**

**Model ve sürüm:** Claude Code (Claude Sonnet 5, düşünme seviyesi: Orta-Yüksek).
**Kullanılan adım(lar):** Kodun anlaşılması (Seray'ın FILE iskeleti: tablolar, arayüzler, sahteler), hedef dilde kod üretme, test senaryosu üretme (WBS-2 §4.1).
**Verilen bağlam:** WBS-1 §4.6, WBS-2 §3.6, Seray'ın FILE iskelet PR'ı (`IFileStore`, `IFileService`, `IFilePolicy`, `FilesDbContext`), AUTH modülünün kendi kodu (desen olarak: `ICurrentUser` kullanımı, test fixture yapısı).
**Yapılan iş:** `FileService` (tam `IFileService` gerçeklemesi), `UserFilePolicy`, FILE API uçları, DI güncellemesi, birim ve entegrasyon testleri tek oturumda üretildi.
**Yapılan düzeltmeler:**
- Yapay zekânın kendi düzelttikleri: İlk derleme denemesinde test fixture'ında `Task.WhenAll`/`AsTask()` tür uyuşmazlığı ve bir dosyada yanlış yazılmış tam nitelikli tür adı giderildi. İkisi de ilk derlemede yakalandı, ikinci denemede derlendi.
- İnsan incelemesi: Henüz tamamlanmadı; bu PR'ın kod incelemesi sırada Sinem'e düşüyor (İrem → Sinem).

**Harcanan süre:** Yaklaşık 20–25 dakika (19.11.2026, 13:50 civarı başlayıp 14:10 civarı bitti) — AUTH'ta kurulan desenlerin (test fixture yapısı, DI genişletme yaklaşımı) doğrudan tekrar kullanılabilmesi nedeniyle AUTH'tan belirgin biçimde daha hızlı tamamlandı.

**Elle geliştirme:** Karşılaştırma çifti için ikinci işlev (kota hesabı) de bu oturumda YZ ile üretildi; ayrı bir elle geliştirme yapılmadı (AUTH'taki §1.2 ile aynı sınırlılık, §3'te not edilmiştir).

**Kanıt:** Bu PR (şube: `irem/file-wbs3-2surum`).

### 2.3 AUTH ve CRS'nin kalan gereksinimleri (Sinem)

#### CRS-FR-11 / CRS-NFR-06: Arka planda ders silme

> Bu geliştirme **YZ destekli** yapılmıştır (Claude Code, Claude Opus 5.5). Kod, testler ve bu bölüm YZ tarafından üretildi; elle geliştirilmiş olarak raporlanmamalıdır.

**Kapsam:** CRS-FR-11 (D/C), CRS-NFR-06 (D/C), CRS-BR-11 (WBS-1 §4.2, WBS-2 §3.2.3 ve §3.2.5). Moodle'daki karşılığı `public/lib/moodlelib.php`, `delete_course()`.

**Davranış**
- `Crs:BackgroundDeletion:Enabled` ayarı varsayılan olarak `false`'tur. Kapalıyken `DELETE /api/v1/courses/{courseId}` önceden olduğu gibi senkron siler ve `204` döner.
- Ayar açıkken talep `crs_course_deletion_jobs` tablosuna kalıcı olarak yazılır. API `202 Accepted`, iş bilgisi ve `Location: /api/v1/courses/deletion-jobs/{jobId}` döner. İşin durumu `GET /api/v1/courses/deletion-jobs/{jobId}` ile sorgulanır.
- İş durumları Pending (1), Running (2), Succeeded (3) ve Failed (4) şeklindedir. Deneme sayısı, başlama ve bitiş zamanı, hata kodu ve güvenli hata mesajı saklanır.
- Yetki her zaman kabulden önce denetlenir (`course.delete`, ders bağlamı). Yetkisiz kullanıcı `403`, kimliksiz kullanıcı `401` alır ve iş kaydı oluşmaz.
- Durum sorgusunun yetkisi dersin var olup olmamasına göre değişir:
  - **Ders hâlâ varsa:** ders bağlamında `course.delete` gerekir.
  - **Ders silinmişse:** ders bağlamı artık yoktur. Durumu yalnızca talebi yapan kullanıcı (`RequestedByUserId`) veya sistem ya da kategori düzeyinde `course.delete` yetkisi olan kullanıcı görebilir. Kategori, silme anında iş kaydına yazılır.
- Ayarlar: `Crs:BackgroundDeletion:PollIntervalMilliseconds` (varsayılan 5000, en az 10; testlerde 50) ve `Crs:BackgroundDeletion:MaxAttempts` (varsayılan 3).

**Yazılanlar**

| Katman | Dosyalar |
| --- | --- |
| Veri | `src/Moodle.Infrastructure/Courses/Migrations/20261121000100_CrsCourseDeletionJobs.cs` (+ `.Designer.cs`): ham SQL ile yazıldı, mevcut CRS migration'larıyla aynı kalıpta. `crs_courses` tablosuna FK yoktur, iş geçmişi silmeden sonra da kalır. Bekleyen veya çalışan işler için `("CourseId") WHERE "State" IN (1,2)` kısmi unique index'i var. `Down`, aktif iş varken tabloyu silmeyi reddeder. |
| Kuyruk | `src/Moodle.Infrastructure/Courses/CrsCourseDeletionJobs.cs`: `CrsBackgroundDeletionOptions`, `CrsCourseDeletionJobState`, `CrsCourseDeletionJob`, `CrsCourseDeletionRules` (hata sınıflandırması), `CrsCourseDeletionJobs` (`EnqueueAsync`, `FindAsync`) |
| İşleyici | `src/Moodle.Infrastructure/Courses/CrsCourseDeletionProcessor.cs`: `CrsCourseDeletionProcessor` (`RecoverAsync`, `ProcessNextAsync`, `CountActiveAsync`), `CrsCourseDeletionWorker` (`BackgroundService`) |
| API | `src/Moodle.Api/Courses/CrsEndpoints.cs`: `DELETE /courses/{courseId}` ayara göre `204` veya `202` döner; yeni uç `GET /courses/deletion-jobs/{jobId:guid}` |
| Kayıt | `src/Moodle.Infrastructure/DependencyInjection.cs` (CRS bölümü, 5 satır) |
| Test altyapısı | `tests/Moodle.IntegrationTests/CrsApiFixture.cs`: `CreateFactory`'ye isteğe bağlı `settings` ve `userId` parametreleri eklendi. Varsayılanlar eski davranışı korur. |

**Testler:** 15 birim test durumu (`CrsBackgroundDeletionTests`) ve 17 PostgreSQL entegrasyon testi (Testcontainers, PostgreSQL 17) yazıldı: `CrsBackgroundDeletionPostgresTests` (9) ve `CrsBackgroundDeletionApiTests` (8). Test adlarında gereksinim kimlikleri geçer. Senaryolar:
- ayar kapalıyken senkron `204`
- ayar açıkken `202`, `Location` ve worker'ın işi tamamlaması
- aynı ders için ikinci talebin mevcut işi döndürmesi
- NFR-03 engeli: kuyruğa almada `409`, kuyruğa aldıktan sonra kayıt yapılırsa işin kalıcı olarak Failed olması
- `course_not_found` hatasının ayrı bir transaction'da kaydedilmesi
- beklenmeyen hatada tekrar deneme ve ardından Failed, exception metni kaydedilmeden
- açılış kurtarması ve SKIP LOCKED
- aynı veritabanında iki ayrı host ile yeniden başlatma
- yetkisiz silme ve durum sorgusu

Bekleme gereken testlerde sabit `Task.Delay` yerine 30 saniye zaman aşımlı yoklama kullanılır. **Test sonuçları (21.11.2026):** Bu geliştirmenin birim testleri ve ilgili entegrasyon testleri başarılı. Genel regresyon (`dotnet test Moodle.slnx`): 852 başarılı, 0 başarısız, 0 atlanan; test süresi 413,1 saniye, toplam komut süresi 430,3 saniye. Build: 0 hata, yeni kodda 0 uyarı.

**Tasarım kararları**
- **Mevcut silme yeniden yazılmadı.** Worker `CrsService.DeleteCourseAsync` metodunu çağırır. Böylece NFR-03'teki ders satırı kilidi (`WithCourseLockAsync`), aktif işlem kontrolü, cascade, `crs_record_change` trigger'ının ürettiği `course.deleted` ve `enrolment.deleted` olayları ve bu olaylara bağlı cache geçersizleştirmesi olduğu gibi korunur. `DeleteCourseAsync` HTTP kullanıcı bağlamına bağlı değildir; talep sahibi yalnızca iz kaydı için `RequestedByUserId` alanında saklanır.
- **Kuyruğa alma.** `WithCourseLockAsync` içinde yapılır. Ders yoksa `404` döner. NFR-03 ön kontrolü engel bulursa `409 course_operation_in_progress` döner. Bekleyen veya çalışan iş varsa yeni iş açılmaz, mevcut iş döndürülür. Aynı ders kilidi eşzamanlı talepleri sıraya koyar; kısmi unique index son güvencedir.
- **Atomik sahiplenme.** `UPDATE … WHERE "Id" = (SELECT … WHERE "State" = 1 … FOR UPDATE SKIP LOCKED LIMIT 1) RETURNING` tek ifadedir ve hemen commit edilir. Birden çok örnek aynı işi alamaz.
- **Silme ve başarı kaydı tek transaction'dadır.** Worker önce iş satırını, sonra ders satırını kilitler, ardından `DeleteCourseAsync` çağrılır (dış transaction'a katılır). İşin Succeeded olarak işaretlenmesi de aynı commit'tedir. Ders silindiği hâlde iş Running ya da Failed görünemez. Kilit sırası her zaman iş satırı → ders satırıdır; kuyruğa alma iş satırını kilitlemediği için kilitlenme (deadlock) oluşmaz.
- **Hata kaydı ayrı bir transaction'dadır.** NFR-03 engeli veya bulunamayan ders durumunda silme transaction'ı geri alınır, change tracker temizlenir ve iş yeni bir transaction'da güncellenir.

  | Hata | Kod | Sonuç |
  | --- | --- | --- |
  | NFR-03 engeli | `course_operation_in_progress` | Hemen Failed, tekrar denenmez. Kullanıcı yedekleme/geri yükleme bitince yeniden talep eder. |
  | Diğer CRS çakışmaları | `course_deletion_conflict` | Hemen Failed |
  | Ders bulunamadı | `course_not_found` | Hemen Failed |
  | Beklenmeyen hata | `deletion_failed` | `Attempts < MaxAttempts` ise Pending'e döner, aksi hâlde Failed |

  Veritabanına yalnızca koda karşılık gelen sabit Türkçe mesaj yazılır. Exception metni (SQL, bağlantı bilgisi) yalnızca `ILogger` ile loglanır.
- **Yeniden başlatma.** İş kalıcı tabloda tutulduğu için kaybolmaz. Worker açılışta Running'de kalmış işleri `FOR UPDATE SKIP LOCKED` ile Pending'e döndürür; başka bir örneğin o anda işlediği iş kilitli olduğundan atlanır. Kurtarma başarısız olursa her turda yeniden denenir. Uygulama düzgün kapanırken yarıda kalan iş Pending'e döner ve deneme hakkı tüketilmez. Çökme durumunda ise yarıda kalan deneme sayılır.
- **HTTP isteğinden bağımsızlık.** İsteğin `CancellationToken` değeri yalnızca kuyruğa almayı kapsar. Worker yalnızca uygulamanın durma token'ını kullanır ve her iş için `CreateAsyncScope` ile ayrı bir scope (`CrsDbContext`, `CrsService`) açar.
- **Ayar kapalıyken bekleyen iş varsa** worker açılışta bir uyarı loglar ve çıkar. İşler, ayar yeniden açılınca işlenir.

**Bilinen sınırlamalar**
- **İş kuyruğa alındıktan sonra yedekleme/geri yükleme kaydı yapılabiliyor.** `ICrsCourseOperationRegistry.RegisterPendingAsync` bekleyen silme işlerini bilmiyor. Bu bir tutarlılık riski oluşturmaz: worker NFR-03 kontrolünü silme kilidi altında yeniden yapar ve iş `course_operation_in_progress` ile Failed olur. NFR-03 koduna dokunmamak için bu davranış değiştirilmedi.
- **Ayar kapalıyken bekleyen işler işlenmez.** Bu sırada ders senkron yoldan silinirse, ayar açıldığında iş `course_not_found` ile Failed olur.
- **Çökme döngüsü sınırlanmıyor.** Deneme sınırı yalnızca beklenmeyen hatalar için geçerlidir. İşlenirken sürekli çöken bir süreç aynı işi her açılışta yeniden dener; çökme ile kurtarma arasında bir deneme sınırı yoktur.
- **Bekleyen işin iptali yok.** Bekleyen bir silme işini iptal eden bir uç yok; WBS-2'de tanımlanmamıştı.
- **Durum sorgusunda varlık bilgisi.** Ders silinmişse yetki kategori düzeyinde denetlenir. Kategori de silinmişse yalnızca talep sahibi ve sistem düzeyi yetkilisi işi görebilir. Bilinmeyen `jobId` için `403` yerine `404` döner; rastgele GUID'ler tahmin edilemediği için bunun bilgi sızıntısı riski ihmal edilebilir.
- **Kullanıcı arayüzü değiştirilmedi.** Blazor ders silme ekranı `202` yanıtını henüz ayrıca göstermiyor. Arayüz, ayar açıkken yalnızca isteğin başarılı olduğunu görür.

**Yapay zekâ kaydı**

**Model ve sürüm:** Claude Code (VS Code eklentisi), model Claude Opus 5.5 (`claude-opus-5-5`).
**Üretim şekli:** YZ. Görev listesinde bu iş için karşılaştırma çifti **"arka planda silme elle"** olarak tanımlanmıştı. 18.11.2026 yöntem değişikliğiyle (belge başı) elle geliştirme yapılmadı; bu işin elle eforu §3'te aynı kod için Basic COCOMO ile tahmin edilir. Kod incelemesinde insan düzeltmesi yapılırsa üretim şekli "YZ + düzeltme" olarak güncellenecektir.
**Kullanılan adım(lar):** Kodun anlaşılması, uygulama planı, hedef dilde kod üretme, test senaryosu üretme, belge taslağı (WBS-2 §4.1).
**Verilen bağlam:** WBS-1 ve WBS-2'deki CRS-FR-11 ve CRS-NFR-06 tanımları; `CrsEndpoints.cs`, `CrsService.cs`, `CrsCourseOperationRegistry.cs` (NFR-03), `Program.cs`, `DependencyInjection.cs`, CRS migration'ları ve testleri; `FileCleanupWorker` (mevcut arka plan görevi deseni).
**Prompt özeti:**
1. **İlk prompt:** Ders silmeyi yapılandırmaya bağlı olarak asenkron arka plan göreviyle yürüt. Kapalıyken senkron davranışı koru, açıkken kalıcı kayıt tut ve `202` dön. `BackgroundService` mevcut `DeleteCourseAsync` metodunu kullansın. Durum takibi, NFR-03 koruması ve yarış durumları, kabulden önce yetkilendirme, HTTP token'ından bağımsızlık ve iş başına scope, yeniden başlatmaya dayanıklılık ve güvenli hata kaydı olsun. Olaylar ve cache korunsun. Birim ve PostgreSQL testleri ile izlenebilirlik ve WBS-3 belgeleri yazılsın. Test çalıştırılmasın, commit/push/PR yapılmasın.
2. **Ek kararlar:** Ayar adı; `202` ile `jobId` ve `Location`; durum ucu; aynı ders için tekil aktif iş (kısmi unique index); kuyruğa almada NFR-03 ön kontrolü ve asıl kontrolün worker kilidi altında yapılması; deneme sayısı ve sonuç durumlarının tanımı; açılışta Running → Pending; `FOR UPDATE SKIP LOCKED`; yeni migration; zaman aşımlı yoklama; iki host ile yeniden başlatma testi. Kodlamadan önce plan sunulup onay beklendi.
3. **Plan onayı ve eklemeler:** NFR-03 engelinde kalıcı Failed. Kuyruk sonrası yedekleme açığının yalnızca belgelenmesi. Ayar kapalıyken açılışta uyarı. Silinmiş ders için durum sorgusu yetkisi (talep sahibi veya sistem/kategori `course.delete`) ve bunun testleri. Hata kaydının ayrı transaction'da yapılması ve testi. Designer dosyasında `[DbContext]` ve `[Migration]` attribute'ları. Ayrı belge yerine sınırlamaların bu bölüme yazılması.

**Yapılan düzeltmeler:**
- **YZ'nin kendi düzelttikleri:** Test dosyasında iki `EF1002` derleme uyarısı (ham SQL ile DDL) giderildi. İki test doğrulaması sadeleştirildi.
- **İnsan katkısı:** Kodlamadan önce plan aşamasında tasarım kararları verildi (yukarıdaki 3. madde). Kod üzerinde insan düzeltmesi henüz yok; inceleme sonrası güncellenecek.

**Harcanan süre:** PR bazında ayrılmadı; AUTH/CRS WBS-3 toplamına dahildir (aşağıda "Süre", geçici tahmin).
**Elle geliştirme:** Yapılmadı (yöntem değişikliği); elle efor §3'te Basic COCOMO ile tahmin edilir.
**Kanıt:** PR #44 (şube: `sinem/crs-background-delete`, commit `24666ed`).

#### CRS-FR-10, NFR-07, NFR-08, NFR-04, NFR-05: Olaylar, uzantı noktaları, önbellek ve gereksiz yazma (PR #31)

**Yapılan iş** (commit `09e9484`, `d1f64c5`, `599d8a5`; 19.11.2026): `crs_record_change` trigger'ı ders ve kayıt değişikliklerini veriyle aynı transaction'da `crs_events` tablosuna yazar (alan değerleri değil alan adları tutulur). `ICrsEventExtension`, `CrsEventExtensionPipeline` ve `CrsEventDispatcher` modüllerin olayları en az bir kez, alındı kaydıyla almasını sağlar. `CrsQueryCache` olay revizyonuna bağlı geçersizleştirme yapar. Değişmeyen kayıt güncellemesi veritabanına yazılmaz. Ayrıntı: `docs/crs-events-extensions.md`, `docs/crs-nfr04-cache.md`, `docs/crs-nfr05.md`.
**Testler:** `CrsEventExtensionTests`, `CrsNfr04CacheTests`, `CrsNfr05NoopTests` (birim); `CrsEventsPostgresTests`, `CrsNfr04CachePostgresTests`, `CrsNfr05NoopPostgresTests` (entegrasyon).
**YZ kaydı:** Model ve sürüm, kullanılan adımlar ve düzeltmeler: _doldurulacak_ (commit'lerde ve belgelerde araç/model kaydı yok; doğrulanamadı). İlgili belgeler çalışmanın YZ desteğiyle üretildiğini belirtir. Süre: PR bazında ayrılmadı, toplam geçici tahmine dahildir. Elle geliştirme yapılmadı; elle efor §3'te COCOMO ile tahmin edilir. **Kanıt:** PR #31.

#### CRS-NFR-03: Yedekleme/geri yükleme sırasında silme koruması (PR #43)

**Yapılan iş** (commit `86d6125`, 20.11.2026): `ICrsCourseOperationRegistry` ve `CrsCourseOperationRegistry` (`RegisterPendingAsync`, `MarkRunningAsync`, `ReleaseAsync`), `crs_course_operations` tablosu; `DeleteCourseAsync` ders satırı kilidi altında bekleyen/devam eden işlem varsa `409 course_operation_in_progress` döner. Ayrıntı: `docs/crs-nfr03-delete-guard.md`.
**Testler:** `CrsNfr03DeleteGuardTests` (birim), `CrsNfr03DeleteGuardPostgresTests` (entegrasyon).
**Sınırlılık:** Depoda gerçek bir yedekleme/geri yükleme yürütücüsü yoktur; koruma kayıt arayüzü üzerinden çalışır.
**YZ kaydı:** Model ve sürüm, kullanılan adımlar ve düzeltmeler: _doldurulacak_ (commit'te ve `docs/crs-nfr03-delete-guard.md`'de araç/model kaydı yok; belge kodun YZ desteğiyle üretildiğini belirtir). Süre: PR bazında ayrılmadı, toplam geçici tahmine dahildir. Elle geliştirme yapılmadı; elle efor §3'te COCOMO ile tahmin edilir. **Kanıt:** PR #43.

#### CRS-FR-07: Kayıt başlangıç ve bitiş zamanının güncellenmesi (PR #45)

**Yapılan iş** (commit `bdf904c`, 21.11.2026): `PATCH /courses/{id}/enrolments/{id}` artık `startsAt` ve `endsAt` alanlarını da kabul eder. Gönderilmeyen alanlar değişmez, açık `null` tarihi temizler. Kayıt oluşturmadaki tarih kuralı `CourseValidation.ValidateEnrolmentDates` olarak ortaklaştırıldı; geçersiz aralık `422` döner ve hiçbir alan yazılmaz. Durum/rol güncellemesi ve mevcut `UpdateEnrolmentAsync(id, suspended, role)` imzası korundu. Blazor katılımcılar ekranında tarih düzenleme yoktur.
**Testler:** `CrsFr07EnrolmentDatesTests` (birim, 7 durum), `CrsFr07EnrolmentDatesPostgresTests` (entegrasyon, 4 test). Kayıtlı sonuç: 21.11.2026 genel koşusunda 863 testten 861'i geçti, 2'si başarısız oldu, atlanan 0. Başarısız iki testin adı ve hata mesajı kaydedilmedi. FR-07 test dosyaları PR #45 commit'inden (`bdf904c`) sonra değişmedi. Son koşu sonucu: _doldurulacak._

#### AUTH-FR-06 ve AUTH-FR-09: Hesap kilitleme ve zorunlu parola değişikliği (PR #46)

- **AUTH-FR-06:** İrem'in AUTH teslimindeki (PR #28) kilitleme kuralları (`Lockout.MaxFailedAccessAttempts=5`, 5 dakika) gereksinimi karşılar ve `AuthEndpointTests.Account_locks_out_after_repeated_failed_attempts` ile sınanır; ek kod gerekmedi.
- **AUTH-FR-09** (commit `ff52c2c`, 21.11.2026): `RequiresPasswordChange` true ise `Login.razor` kullanıcıyı `/change-password?required=true` sayfasına yönlendirir; yeni `ChangePassword.razor` mevcut `POST /api/v1/auth/change-password` ucunu kullanır. `PasswordChangeRequiredMiddleware`, değişiklik tamamlanana kadar `/api/v1/auth/*`, sağlık ve OpenAPI dışındaki API uçlarını `403 password_change_required` ile reddeder; `AuthAccessPolicy` bu kullanıcıya yetki vermez. Bayrak her istekte veritabanından okunduğu için parola değişince erişim aynı oturumda açılır.
- **Sınırlılık:** Kullanıcı parola sayfası dışındaki bir sayfayı adresle açarsa engel API düzeyinde kalır; sayfa düzeyinde yönlendirme yoktur.
- **Testler:** `AuthFr09ForcedPasswordTests` (birim ve entegrasyon). Önceki çalışma sırasında bildirilen test sonucu: odaklı entegrasyon testleri 29/29 başarılı. Birim testlerinin ayrı sonucu bildirilmedi.

#### CRS olay iletimi, gerçek AUTH–CRS testleri ve M gereksinimleri test kapsamı (PR #47)

**Yapılan iş** (commit `20e4804`, 21.11.2026):
- `CrsEventExtensionPipeline` ve `CrsEventDispatcher` DI'a kaydedildi; yeni `CrsEventDispatchWorker` commit edilmiş olayları (`course.deleted` dahil) kayıtlı uzantılara iletir. Kayıtlı uzantı yoksa görev veritabanına dokunmadan çıkar (CRS-FR-05, NFR-02, NFR-08).
- `AuthCrsIntegrationTests`: gerçek Identity çerezi, `AuthCurrentUser` ve `AuthAccessPolicy` ile Manager ders oluşturur (201), Student oluşturamaz (403), ders rolü CRS kaydından okunur, askıya alınan kayıt yetkiyi hemen kaldırır.
- `CrsMRequirementTests`: CRS-FR-01, 02, 04, 05, 06, 08, NFR-01, 02 için veritabanına bağlanmayan birim testleri. `CrsCourseDeletionCleanupTests`: silmede `course.deleted` ve `enrolment.deleted` olaylarının uzantıya ulaşması (senkron ve arka planda silme).

**Testler:** Build başarılı. İlgili birim testleri 11/11 geçti (`CrsMRequirementTests`). İlgili entegrasyon testleri 37/38 geçti; `AuthCrsIntegrationTests` ve `CrsCourseDeletionCleanupTests` (senkron silme) geçti, `CrsCourseDeletionCleanupTests` (arka planda silme) `Assert.Contains() Failure: Filter not matched in collection` ile başarısız oldu. Kök neden testin zamanlamasıydı (olaylar uzantıya tek tek teslim edilirken arada denetleniyordu); bekleme koşulu iki olayı birlikte bekleyecek şekilde düzeltildi ve düzeltme PR #47'ye (`20e4804`) dahil edildi. Önceki çalışma sırasında bildirilen test sonucu: daha önce sorun yaşanan iki senaryo ayrı yeniden çalıştırıldığında 2/2 başarılı. Önceki toplu koşuda görülen Docker.DotNet kaynaklı `TaskCanceledException` Testcontainers/Docker altyapısına aittir; üretim kodu hatası olarak değerlendirilmemiştir.

#### Ders detay sayfasından FILE ve GRD bağlantıları

`Pages/CourseDetails.razor`'a "Ders dosyaları" (`/courses/{id}/files`), "Not defteri" (`/courses/{id}/gradebook`) ve "Notlarım" (`/courses/{id}/my-grades`) bağlantıları eklendi. Bağlantılar yalnızca gezinme sağlar; yetki hedef sayfalarda ve API'de denetlenir (mevcut "Ödevler" bağlantısıyla aynı). `CrsBlazorTests.Details_show_category_visibility_dates_and_editable_information` bağlantıları doğrular. Önceki çalışma sırasında bildirilen test sonucu: `CrsBlazorTests` 14/14 başarılı. Aynı PR'da `docs/izlenebilirlik.md` CRS tablosu, bu bölüm ve `docs/crs-api.md` güncellendi. **Kanıt:** PR #48 (commit `b96f147`).

#### YZ kaydı (PR #45, #46, #47, #48)

**Model ve sürüm:** Claude Code (VS Code eklentisi), model Claude Opus 5.5 (`claude-opus-5-5`).
**Üretim şekli:** YZ.
**Kullanılan adım(lar):** Kodun ve gereksinimlerin karşılaştırılması, hedef dilde kod üretme, test senaryosu üretme, belge güncelleme (WBS-2 §4.1).
**Verilen bağlam:** WBS-1/WBS-2'deki AUTH-FR-06, FR-09, CRS-FR-05–08, NFR-01, NFR-02 tanımları; AUTH, CRS, ASG, GRD ve FILE kodu ve testleri.
**Prompt özeti:** (1) CRS-FR-07 kayıt tarihlerinin PATCH ile güncellenmesi; (2) AUTH-FR-09 zorunlu parola değişikliği yönlendirmesi ve API engeli; (3) gerçek AUTH–CRS entegrasyon testleri, ders silmede diğer modüller için olay iletimi, kayıt yöntemi analizi ve M gereksinimleri test kapsamı; (4) başarısız `CrsCourseDeletionCleanupTests` için kök neden analizi; (5) ders detay bağlantıları ve belge güncellemeleri (PR #48); (6) AUTH/CRS son durum incelemeleri ve bu kayıtların tamamlanması. Her adımda test ve build kullanıcı tarafından çalıştırıldı; commit/PR kullanıcı tarafından yapıldı. YZ yalnızca PR #44'te, kullanıcının izniyle `dotnet build` çalıştırdı; hiçbir adımda test çalıştırmadı.
**Yapılan düzeltmeler:**
- YZ'nin kendi düzelttikleri: `CrsCourseDeletionCleanupTests` olayları teslim sırasının ortasında denetliyordu (zamanlama); bekleme koşulu iki olayı birlikte bekleyecek şekilde düzeltildi, doğrulamalar gevşetilmedi (PR #47).
- CRS-FR-07 sonrası genel koşuda 2 başarısız test bildirildi (863 test, 861 geçti). YZ test çıktısına erişemediği için test adlarını ve hata mesajlarını istedi; bu bilgi kayda geçmedi. İki testin adı, nedeni ve düzeltmesi: _doldurulacak (doğrulanamadı)._
- İnsan düzeltmesi: Kod üzerinde insan düzeltmesi kaydı yoktur (doğrulanamadı). İnsan katkısı görev tanımları, plan aşamasındaki kararlar ve test/build koşturmasıyla sınırlı olarak kayıtlıdır.
**Harcanan süre:** PR bazında ayrılmadı; aşağıdaki geçici toplam tahmine dahildir.
**Elle geliştirme:** Yapılmadı (18.11.2026 yöntem değişikliği); elle efor §3'te Basic COCOMO ile tahmin edilir.
**Kanıt:** PR #45, #46, #47, #48; bu kayıtları tamamlayan belge PR'ı: _doldurulacak._

#### Süre (geçici, 21.11.2026)

Sinem'in WBS-3 AUTH/CRS çalışması için 21.11.2026 itibarıyla geçici tahmin: **yaklaşık 13 saat aktif YZ destekli çalışma + 6 saat test/doğrulama = 19 saat**. Bu süre nihai değildir ve WBS-3 tamamlanınca güncellenecektir. PR bazında dağılım ölçülmediği için verilmemiştir; §3'teki efor sütunları bu tahmin kesinleşince doldurulacaktır.

#### Bilinen sınırlılıklar (§2.3 geneli)
- **ASG/GRD/FILE ders silme temizliği tamamlanmadı.** CRS, `course.deleted` olayını iletir; ancak ASG (Aslı Ceren), GRD (Fatma Zehra) ve FILE (İrem, Seray) için üretim uzantısı yoktur. Bu modüllerin ders verileri silmeden sonra kalır; dersin varlığını denetledikleri için erişilemez.
- **Kayıt yöntemi modeli** WBS-2'den sapmadır (§1.3).

### 2.4 Dosya yükleme, antivirüs, ödeve dosya teslimi ve uçtan uca test (Aslı Ceren)

**21.11.2026 güncel main / nihai ASG entegrasyonu:** Son doğrulama: 995/995 .NET, 54/54 JS, Playwright 6 geçti / 2 bölüm bağımlılığı nedeniyle atlandı. 02017af fast-forward alındı; önceki çalışmalar korundu. Mevcut CRS olay işleyicisine ASG/FILE ve kullanıcı onaylı GRD temizliği bağlandı; migration, tekrar deneme, transaction ve yarış testleri eklendi. Gerçek CRS öğrenci rol/tarih kapsamı doğrulanır. Bölüm modeli ve örnek SiteAdmin hesabı eksikliği gerçek kabul bağımlılığıdır. [Güncel entegrasyon](asg-file-wbs3/NIHAI-ASG-FILE-ENTEGRASYONU.md). YZ süresi: **Ölçülmedi / kullanıcı tarafından doldurulacak**.

**21.11.2026 — son FILE entegrasyonu:** PR #40 ve #42 main a254d4a üzerinde doğrulandı; yeni çalışma dalı codex/asg-file-final-integration. Ortak dosya yöneticisi mevcut draft uçları, CSRF ve sizeBytes sözleşmesiyle gerçek tarayıcıda yükleme→kalıcı kayıt→öğrenci indirme→yetkili silme akışını geçti. Üretim kaynaklarını veya AUTH seed'ini değiştirmek gerekmedi. SiteAdmin pozitif yetki testi gerçek AUTH ile yalnız test ortamında oluşturulan kullanıcıyla yapıldı.

Ayrı dotnet süreçleri ve PostgreSQL 17 kullanılarak yükleme/temizlik ortak içerik kilidi, başarı/hata/iptal/rollback/yeniden deneme ve fiziksel byte bütünlüğü altı regresyonla doğrulandı. MIME için PDF/PNG/JPG/JPEG/TXT, yanlış istemci başlığı, yanlış uzantı/içerik ve ASG/ders dosyasına geçiş testleri eklendi. Mevcut üretim MIME ve kilit düzeltmeleri PR #42'de zaten vardır; ikinci yükleme sistemi yazılmadı.

**Son ölçülen sonuç:** Release başarılı; 468 birim + 471 entegrasyon = **939/939 .NET**; JavaScript **54/54**; Playwright **6 geçti / 2 CRS bölüm bağımlılığı nedeniyle atlandı**. Gerçek ClamAV, Docker Compose/HTTP ve beş DbContext snapshot kontrolü başarılı. Önceki 907 sonucu tarihsel bazdır; main'deki PR #45 testleri ve bu turdaki 21 yeni entegrasyon vakası korunarak çalıştırılmıştır. [Ayrıntılı son rapor](asg-file-wbs3/SON-FILE-ENTEGRASYONU.md), [makine özeti](asg-file-wbs3/DOGRULAMA.json).

**YZ yöntemi ve süre:** Kod/test/belgeler YZ desteğiyle üretildi. Güncel öğretim kararı gereği ayrı manuel geliştirme şartı yoktur; tarihsel kayıtlar güncel eksik görev anlamına gelmez. Doğrulanabilir toplam efor kaydı bulunmadığından süre hesaplanmadı. **Aslı Ceren'in doğrulanmış YZ ile çalışma süresi: doldurulacak.** Test koşu süreleri toplam geliştirme süresi olarak kullanılmadı. COCOMO ve karşılaştırma raporu Fatma Zehra'nın sorumluluğunda bırakıldı.

#### Önceki 19–20.11.2026 kayıtları (tarihsel)

**20.11.2026 güncellemesi:** Seray PR #39 / e2b50d0 main 1424b04 üzerinden fast-forward ile alındı; yerel çalışmalar korundu, yeni commit oluşturulmadı. Eşzamanlılık 10/10, .NET 708/708, JavaScript 23/23 geçti. Playwright 4 geçti / gerçek CRS bölüm modeli nedeniyle 2 skip. Önceki FileStore hatası giderildi; aşağıdaki 19.11 sonuçları tarihseldir. [Son kontrol](asg-file-wbs3/SON-KONTROL.md).

19.11.2026: FILE-FR-01/07/08/10/11, FILE-BR-06/07/08/09 için gerçek taslak HTTP uçları, sahiplik, benzersiz ad, sunucu sınırları, ad/tür/içerik doğrulaması ve ClamAV INSTREAM eklendi. Kullanıcı onaylı varsayılanlar PDF/PNG/JPEG/TXT, 10 dosya ve toplam 50 MiB; ayarlanabilir, WBS sayısal sabiti değildir. İrem'in FileService ve Seray'ın deposu kullanılır. Onaylı yeni sözleşme IVirusScanner; mevcut ortak FILE arayüzleri korunur.

ASG metin/dosya türlerini destekler; dosya taşıması ve sürümlü teslim aynı PostgreSQL transaction/savepoint'inde yapılır. Dosya read policy ortak ASG kapsamına bağlıdır; doğrudan FILE write ile sürüm/pencere kontrolü atlanamaz. Blazor yükleme/tarama/dosya listesi/indirme ve hata gösterimi eklendi. Ortak dosya yöneticisi gelene kadar yalnız ASG adaptörü kullanılır.

Son .NET: 358/358 entegrasyon geçti; birim 337 geçti, değiştirilmemiş LocalDiskFileStore eşzamanlılık testi başarısız. 21 JS geçti. Test başarısızlığı gizlenmedi. Tam A/B E2E gerçek CRS bölüm modeline bağlıdır. [Teslim](asg-file-wbs3/TESLIM-RAPORU.md), [entegrasyon devri](asg-file-wbs3/ENTEGRASYON.md), [çalıştırma ve gerçek sonuçlar](asg-file-wbs3/TESTLER.md). V2 PR henüz yoktur.

### 2.5 Temizlik görevi, ders dosyaları ve rapor (Fatma Zehra)

**Kapsam:** FILE-FR-12, FR-13; FILE-BR-04, BR-05; FILE-BUG-02, BUG-03, BUG-04 (WBS-1 §4.6, WBS-2 §3.6). Yönetici dosya ayarları ve bakım ucu, ortak dosya yöneticisi bileşeni.

**Yazılanlar**

| Katman | Dosyalar |
| --- | --- |
| Uygulama (sözleşmeler ve saf kurallar) | `src/Moodle.Application/Files/`: `IFileMaintenance.cs` (`FileMaintenanceResult`), `FileRetentionRules.cs`, `IFileSettingsService.cs` (`FileSettings` ve doğrulaması, `ISiteAdminQuery`) |
| Veri ve servis | `src/Moodle.Infrastructure/Files/`: `FileMaintenanceService`, `FileCleanupWorker` (`FileCleanupOptions`), `FileSettingsService`, `IdentitySiteAdminQuery`, `CourseFilePolicy`, `CourseFilesSeedContributor`; `FileEntryDeletedAtIndex` geçişi; `FileService.cs`'te 4 satır (FILE-BUG-02, aşağıda) |
| API | `src/Moodle.Api/Files/FileAdminEndpoints.cs`: `GET`/`PUT /api/v1/admin/file-settings`, `POST /api/v1/admin/file-maintenance/run`. Ders dosyaları için mevcut FILE uçları kullanılır: `GET /api/v1/file-areas/{courseId}/course/files/0/files`, `POST .../commit`, `GET /api/v1/files/{id}/content`, `DELETE /api/v1/files/{id}` |
| Sayfalar | `src/Moodle.Web/Files/` (`FileApiClient`, `FileModels`, `FilePageBase`, `FileBrowserHttpHandler`, `FileNotice.razor`, `FileManager.razor`), `wwwroot/file-api.js`; `/courses/{courseId}/files` (`Pages/CourseFiles.razor`), `/admin/files` (`Pages/AdminFileSettings.razor`); `NavMenu.razor`'a "Dosya ayarları" bağlantısı |
| Kayıt | `DependencyInjection.cs` (FILE bölümü), `Program.cs` (`AddFileAdminApi`, `MapFileAdminEndpoints`, `AddFileWeb`) |

**Testler:** Bu çalışmada 93 birim testi (`FileRetentionRulesTests`, `FileCleanupWorkerTests`, `CourseFilePolicyTests`, `FileWebClientTests`, `FileBlazorTests`), 40 entegrasyon testi (Testcontainers ile PostgreSQL 17: `FileMaintenanceTests`, `FileAdminEndpointTests`, `SiteAdminQueryTests`, `CourseFileEndpointTests`, `CourseFilesSeedTests`, `FileBlazorHostingTests`) ve 28 Node testi (`tests/web/file-api.test.mjs`) eklendi. Test adlarında gereksinim kimlikleri geçer. Tüm paket 412 birim, 378 entegrasyon ve 49 Node testiyle yeşil.

**Tasarım kararları ve belgeden sapmalar**
- **Temizlik sırası ve danışma kilidi.** `FileMaintenanceService.RunAsync` sırasıyla şunları yapar: (1) son etkinliği taslak süresinden eski taslakları ve taslak kayıtlarını siler (FILE-BUG-02); (2) çöpe atıldığı andan itibaren bekleme süresi dolan kayıtları kalıcı siler (FILE-BUG-03); (3) boşa düştüğü andan itibaren bekleme süresi dolan içeriğin `file_blob` satırını `FOR UPDATE` ile kilitler, kullanımı ve `OrphanedAt`'i aynı işlemde yeniden denetler, satırı siler ve işlem onaylandıktan sonra dosyayı diskten siler (FILE-BUG-04). Diskten silme başarısız olursa satır geri gelmez; hata günlüğe ve sonuca yazılır, `FileConsistencyChecker` raporunda görünür. Her çalıştırma `pg_try_advisory_lock` ile korunur; kilit başka bir örnekteyse tur atlanır. Arka plan görevi `Files:CleanupIntervalMinutes` (varsayılan 60) aralıkla çalışır, `Files:CleanupEnabled=false` ile kapatılır, testlerde kapalıdır.
- **FILE-BUG-02 için İrem'in koduna 4 satırlık değişiklik.** `FileService.cs`'te `DeleteFromDraftAsync` ve `CommitAsync` artık taslağın `LastActivityAt` alanını günceller (2 satır değişti, 2 satır eklendi). Listeleme etkinlik sayılmaz; kaydetmeden sonra taslak satırı silinmez. Dosyada başka satır değiştirilmedi.
- **Kalıcı silinen kayıtların içeriğine `OrphanedAt` yazılır.** Temizlik bir kaydı kalıcı silince içeriği kullanan etkin kayıt kalmadıysa içerik o andan boşa düşmüş sayılır. Böyle yapılmasaydı süresi dolan taslaktaki içerik hiç silinmezdi.
- **Ayarlar ve bakım ucu yalnızca site yöneticisine (`SiteAdmin`) açık.** `IAccessPolicy` `SiteAdmin` ile `Manager`'ı ayırmadığından AUTH dosyalarına dokunmadan `IdentitySiteAdminQuery` yazıldı (ASP.NET Identity `UserManager`, rol adı `CapabilityRules.SiteAdmin`; askıdaki veya silinmiş kullanıcı yönetici sayılmaz). Bakım ucu WBS-2 §3.6.4'e uygun olarak `202` döner; gövdede `purgedEntries` ve `errors` alanları da vardır. Temizlik başka yerde yürürken dönen `409 maintenance_running` WBS-2'nin hata tablosunda yoktu, eklendi. Geçersiz ayar `422` (ayar değişmez), bilinmeyen alan `400` döner.
- **Ders dosyası yetkisi (`CourseFilePolicy`).** Yönetici (`course.managefiles` yeteneği; şu an `CapabilityRules`'ta tanımlı olmadığından yalnızca yöneticiye açık) veya düzenleyen öğretmen (`EditingTeacher`) yazar; düzenleme yetkisiz öğretmen (`Teacher`) ve öğrenci yazamaz (Moodle'daki `moodle/course:managefiles`, WBS-1 §4.6.7). Öğretmenler ve yönetici okur; öğrenci yalnızca ders görünürse okur. Reddedilen erişim FILE'ın kuralı gereği 404'tür (FILE-BR-11). Rol `IEnrolmentQuery` ile ayrıca denetlenir; politika AUTH'taki değişikliğe bağımlı değildir.
- **Yükleme bileşeni WBS-2 §3.6 sözleşmesine göre yazıldı.** `FileManager` dosyayı taslağa yükler (`POST /file-drafts`, `POST /file-drafts/{id}/files`) ve "Kaydet" ile kalıcı alana kaydeder (`commit`). Yükleme uçları (Aslı Ceren) gelene kadar 404/405 alınır ve "Yükleme şu an kullanılamıyor" uyarısı gösterilir; liste, indirme ve silme çalışmaya devam eder.

**Sınırlılıklar**
- **Yükleme tarafında kapanmayan FILE-BUG-04 yarış durumu.** Yükleme içeriğin diskte olduğunu görüp yazmayı atladıktan hemen sonra temizlik aynı içeriği silerse, yükleme yeni bir `file_blob` satırı ekler ve kaydı olup dosyası olmayan içerik oluşur (tutarlılık raporunda görünür). Tam çözüm için yükleme tarafında (`FileService.EnsureBlobRowAsync` ve depoya yazma; İrem ve Aslı Ceren'in kodu) aynı kilidin alınması gerekir.
- **Örnek veride `SiteAdmin` yok.** `yonetici1` yalnızca `Manager` olduğundan örnek veriyle `/admin/files` herkese "yetkiniz yok" gösterir.
- **Yazma yetkisi baştan bilinmiyor.** WBS-2'de yazma yetkisini bildiren bir alan olmadığından ders dosyaları sayfasında yükleme ve silme denetimleri önce görünür; kaydetme veya silme 403/404 alınca gizlenir. Öğrenci denetimleri bir kez görür.
- **Yüklemede veri iki kez yol alır.** Dosya içeriği tarayıcıdan sunucuya (`InputFile`), sonra yeniden tarayıcıya gider ve oradan `fetch` ile API'ye gönderilir. Küçük ve orta boyutlu dosyalar için yeterlidir.
- **Tüm dosyalar `application/octet-stream` türüyle kaydedilir.** `FileService` türü algılamıyor; tür algılama yükleme kapsamındadır (Aslı Ceren).
- **Ders sayfasından bağlantı yok.** Ders sayfası (CRS, Sinem) değiştirilmediği için `/courses/{courseId}/files` şimdilik yalnızca adresle açılır.

**Yapay zekâ kaydı**

**Model ve sürüm:** Claude Code (Claude Opus 5.5, düşünme seviyesi: Medium). Adım promptları ve çıktıların incelenmesi için ayrıca Claude (claude.ai) sohbet asistanı kullanıldı.
**Kullanılan adım(lar):** Kodun anlaşılması, hedef dilde kod üretme, test senaryosu üretme (WBS-2 §4.1).
**Verilen bağlam:** WBS-1 §4.6, WBS-2 §3.6, birleşmiş FILE kodu (Seray: altyapı ve depo; İrem: kaydetme, indirme, silme, kota), AUTH'un yetki yapısı (`CapabilityRules`, `AuthAccessPolicy`), CRS ve GRD'nin API ve Web desenleri.
**Yapılan iş:** Dört adımda: (1) mevcut FILE ve AUTH kodunun incelenmesi; (2) temizlik görevi, arka plan görevi ve FILE-BUG-02/03/04 düzeltmeleri; (3) yönetici ayarları, bakım ucu, ders dosyası politikası ve örnek veri; (4) dosya yöneticisi bileşeni, ders dosyaları ve yönetici sayfaları; her adımın testleriyle birlikte.
**Yapılan düzeltmeler:**
- Yapay zekânın kendi düzelttikleri (toplam 4): 3. adımda örnek PDF'in `/Length` değeri yanlış hesaplanmıştı ve bir test dosyasında kullanılmayan `using` vardı. 4. adımda bUnit testlerinde eksik `using` build hatası verdi; ayrıca `/admin/files` barındırma testi, Blazor'un prerender'da Türkçe karakterleri HTML kodlaması (`ı` → `&#x131;`) yüzünden başarısız oldu ve test, HTML'i çözerek karşılaştıracak şekilde düzeltildi.
- İnsan incelemesi: 1 davranış düzeltmesi istendi. Gizli derste öğrenci ders dosyalarını okuyabiliyordu; Moodle'a uygun olarak engellendi.

**Harcanan süre:** Adım süreleri toplamı yaklaşık 1,0 saat (20.11.2026). Süreler Claude Code'un çalışmasını ve insanın prompt hazırlama, okuma ve karar verme süresini birlikte içerir; modelin kendi çalışma süresi ayrıca ölçülmedi. Belge adımları bu süreye dahil değildir.

| Adım | İş | Süre |
| --- | --- | --- |
| 1 | İnceleme | 22:30–22:41 (~11 dk) |
| 2 | Temizlik görevi ve FILE-BUG-02/03/04 | 22:43–23:00 (~17 dk) |
| 3 | Yönetici ayarları, bakım ucu, ders dosyaları | 23:02–23:17 (~15 dk) |
| 4 | Dosya yöneticisi bileşeni ve sayfalar | 23:18–23:37 (~19 dk) |

**Elle geliştirme:** Yapılmadı; efor Basic COCOMO ile raporda toplu hesaplanır (§3).

**Kanıt:** PR #40

## 3. Yapay zekâ destekli geliştirme ve COCOMO elle tahmini karşılaştırması

Tasarım: WBS-2 §4.4, kriter K17 (eşik yok, raporlanır). Bütün kod YZ ile yazılır; YZ ile harcanan gerçek efor, aynı kodun elle yazılsaydı gerektireceği eforun Basic COCOMO tahminiyle karşılaştırılır (organik, Efor = 2,4 × KLOC^1,05 kişi-ay, 1 kişi-ay = 152 saat; satır sayısı boş ve yorum satırları hariç). Efor, hata ve inceleme verileri herkesin yapay zekâ kaydından ve PR geçmişinden alınır. Satır sayısı ve COCOMO sütunları en sonda herkes için aynı kurallarla toplu doldurulur.

| Modül | Kapsam | Satır (boş ve yorum hariç) | Gerçek efor, YZ ile (saat) | COCOMO elle tahmini (saat) | Oran (gerçek / tahmin) | İnceleme ve testte bulunan hata | Hata / 1.000 satır | İnceleme turu | Kaynak |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **1. sürüm** | | | | | | | | | |
| Altyapı | Ortak bileşenler (§1.1) | | | | | | | | |
| AUTH | Kimlik ve yetki (§1.2) | | ~1 | | | 6† | | 1 | PR (şube: `irem/auth-wbs3`); `ManualAccountStatusCheck.cs`¶ |
| CRS | Ders ve kayıt (§1.3) | | | | | | | | §1.3, PR #20 |
| ASG | Ödev ve teslim (§1.4) | | | | | | | | |
| GRD | Değerlendirme (§1.5) | | ~1,1 | | | 7◊ | | | §1.5, PR #23 |
| **2. sürüm** | | | | | | | | | |
| FILE | Altyapı (§2.1) | | ~1 | | | 2‡ | | | §2.1 |
| FILE | Kaydetme, indirme, silme ve kota (§2.2) | | ~0,5 | | | 2§ | | 1 | PR (şube: `irem/file-wbs3-2surum`); `ManualQuotaCalculator.cs`¶ |
| FILE | Yükleme ve antivirüs (§2.4) | | | | | | | | |
| FILE | Temizlik görevi ve ders dosyaları (§2.5) | | ~1,0 | | | 4△ | | 1 | §2.5 |
| ASG | Ödeve dosya teslimi (§2.4) | | | | | | | | |
| AUTH ve CRS | Kalan gereksinimler (§2.3) | | | | | | | | §2.3, PR #31, #43, #44, #45, #46, #47 |

† İnceleme turu sayısı, bu oturumdaki kendi kendine derleme/test döngülerinden (insan incelemesi değil) sayılmıştır: ilk derleme hatası (eksik `using`), ardından tam test koşusunda ortaya çıkan 3 CRS/GRD testi + 1 birim testi düzeltmesi, ardından AUTH'un kendi testlerinde 3 ayrı hata (kimlik çakışması, kilitlenme eşiği, `/permissions`'ın CRS'ye bağımlılığı). Bağımsız bir insan kod incelemesi henüz yapılmadı.
§ İlk derlemede iki hata (yanlış tam nitelikli tür adı, `Task`/`ValueTask` uyuşmazlığı); ikinci denemede derlendi ve tüm testler ilk çalıştırmada geçti.
◊ §1.5'teki YZ kaydında YZ'nin kendi düzelttiği maddelerden sayılmıştır: geçişin gereksiz iki indeks nedeniyle yeniden üretilmesi, test verisindeki hata mesajı, bir testte dizi karşılaştırma biçimi, sayfa adımında iki derleme öncesi düzeltme, bir test verisi tür hatası ve bir gerçek hata (`422` yanıtında puan alanının ekran okuyucuya geçersiz bildirilmemesi). İnsan incelemesinde koda düzeltme gerekmedi. İnceleme turu sayısı kayıtta yok. Süre, Claude Code'un çalışmasını ve insan süresini birlikte içerir.
‡ §2.1'deki YZ kaydından: seed verisini okuyan testin çalışma zamanı modelinde kırılması ve sahte dosya politikasının üretimde kayıtlı kalması (fail-closed kalıbına çevrildi). İnsan düzeltmesi yok. İnceleme turu sayısı kayıtta yok. Efor, kayıttaki "YZ ile çalışma: ~1 saat (oturum kaydı, alt sınır)" değeridir. Kayıtta ayrıca "İnsan: ~1 saat (tahmin)" yazılıdır.
△ §2.5'teki YZ kaydında YZ'nin kendi düzelttikleri: örnek PDF'in `/Length` hesabı, kullanılmayan `using`, eksik `using` (build hatası) ve Türkçe karakter kodlaması nedeniyle başarısız olan barındırma testi. İnsan incelemesinde ayrıca 1 davranış düzeltmesi istendi (gizli derste öğrencinin ders dosyalarını okuması); bu sayıya dahil değildir.
¶ Bu dosyalar da aynı oturumda YZ ile üretilmiştir; incelemede ve testte hata bulunmamıştır (0 hata, 1 tur). Modülün YZ kodu olarak satır sayımına dahil edilir.

**Yorum:** Doldurulduktan sonra yazılacak.

**Sınırlılıklar:** Elle efor ölçülmez, aynı kod için Basic COCOMO ile tahmin edilir. COCOMO büyük projeler için geliştirilmiştir; bu kadar küçük kodda tahmin yüksek çıkar, bu yüzden oran kesin ölçüm değil gösterge olarak okunmalıdır. Gerçek efor kayıtlarında insan süresi ile modelin çalışma süresi her zaman ayrıştırılamamıştır (ör. AUTH ve FILE kayıtları). Örneklem küçüktür ve modüller farklı kişilerce geliştirilmiştir; hata ve inceleme turu sayıları kısmen YZ'nin kendi derleme/test döngülerinden gelir, bağımsız insan incelemesi henüz tamamlanmamıştır.
