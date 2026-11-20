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

### 1.4 Ödev ve teslim – ASG ve uçtan uca akış testi (Aslı Ceren)

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

### 2.4 Dosya yükleme, antivirüs, ödeve dosya teslimi ve uçtan uca test (Aslı Ceren)

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

**Kanıt:** PR #__

## 3. Yapay zekâ destekli geliştirme ve COCOMO elle tahmini karşılaştırması

Tasarım: WBS-2 §4.4, kriter K17 (eşik yok, raporlanır). Bütün kod YZ ile yazılır; YZ ile harcanan gerçek efor, aynı kodun elle yazılsaydı gerektireceği eforun Basic COCOMO tahminiyle karşılaştırılır (organik, Efor = 2,4 × KLOC^1,05 kişi-ay, 1 kişi-ay = 152 saat; satır sayısı boş ve yorum satırları hariç). Efor, hata ve inceleme verileri herkesin yapay zekâ kaydından ve PR geçmişinden alınır. Satır sayısı ve COCOMO sütunları en sonda herkes için aynı kurallarla toplu doldurulur.

| Modül | Kapsam | Satır (boş ve yorum hariç) | Gerçek efor, YZ ile (saat) | COCOMO elle tahmini (saat) | Oran (gerçek / tahmin) | İnceleme ve testte bulunan hata | Hata / 1.000 satır | İnceleme turu | Kaynak |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **1. sürüm** | | | | | | | | | |
| Altyapı | Ortak bileşenler (§1.1) | | | | | | | | |
| AUTH | Kimlik ve yetki (§1.2) | | ~1 | | | 6† | | 1 | PR (şube: `irem/auth-wbs3`); `ManualAccountStatusCheck.cs`¶ |
| CRS | Ders ve kayıt (§1.3) | | | | | | | | |
| ASG | Ödev ve teslim (§1.4) | | | | | | | | |
| GRD | Değerlendirme (§1.5) | | ~1,1 | | | 7◊ | | | §1.5, PR #23 |
| **2. sürüm** | | | | | | | | | |
| FILE | Altyapı (§2.1) | | ~1 | | | 2‡ | | | §2.1 |
| FILE | Kaydetme, indirme, silme ve kota (§2.2) | | ~0,5 | | | 2§ | | 1 | PR (şube: `irem/file-wbs3-2surum`); `ManualQuotaCalculator.cs`¶ |
| FILE | Yükleme ve antivirüs (§2.4) | | | | | | | | |
| FILE | Temizlik görevi ve ders dosyaları (§2.5) | | ~1,0 | | | 4△ | | 1 | §2.5 |
| ASG | Ödeve dosya teslimi (§2.4) | | | | | | | | |
| AUTH ve CRS | Kalan gereksinimler (§2.3) | | | | | | | | |

† İnceleme turu sayısı, bu oturumdaki kendi kendine derleme/test döngülerinden (insan incelemesi değil) sayılmıştır: ilk derleme hatası (eksik `using`), ardından tam test koşusunda ortaya çıkan 3 CRS/GRD testi + 1 birim testi düzeltmesi, ardından AUTH'un kendi testlerinde 3 ayrı hata (kimlik çakışması, kilitlenme eşiği, `/permissions`'ın CRS'ye bağımlılığı). Bağımsız bir insan kod incelemesi henüz yapılmadı.
§ İlk derlemede iki hata (yanlış tam nitelikli tür adı, `Task`/`ValueTask` uyuşmazlığı); ikinci denemede derlendi ve tüm testler ilk çalıştırmada geçti.
◊ §1.5'teki YZ kaydında YZ'nin kendi düzelttiği maddelerden sayılmıştır: geçişin gereksiz iki indeks nedeniyle yeniden üretilmesi, test verisindeki hata mesajı, bir testte dizi karşılaştırma biçimi, sayfa adımında iki derleme öncesi düzeltme, bir test verisi tür hatası ve bir gerçek hata (`422` yanıtında puan alanının ekran okuyucuya geçersiz bildirilmemesi). İnsan incelemesinde koda düzeltme gerekmedi. İnceleme turu sayısı kayıtta yok. Süre, Claude Code'un çalışmasını ve insan süresini birlikte içerir.
‡ §2.1'deki YZ kaydından: seed verisini okuyan testin çalışma zamanı modelinde kırılması ve sahte dosya politikasının üretimde kayıtlı kalması (fail-closed kalıbına çevrildi). İnsan düzeltmesi yok. İnceleme turu sayısı kayıtta yok. Efor, kayıttaki "YZ ile çalışma: ~1 saat (oturum kaydı, alt sınır)" değeridir. Kayıtta ayrıca "İnsan: ~1 saat (tahmin)" yazılıdır.
△ §2.5'teki YZ kaydında YZ'nin kendi düzelttikleri: örnek PDF'in `/Length` hesabı, kullanılmayan `using`, eksik `using` (build hatası) ve Türkçe karakter kodlaması nedeniyle başarısız olan barındırma testi. İnsan incelemesinde ayrıca 1 davranış düzeltmesi istendi (gizli derste öğrencinin ders dosyalarını okuması); bu sayıya dahil değildir.
¶ Bu dosyalar da aynı oturumda YZ ile üretilmiştir; incelemede ve testte hata bulunmamıştır (0 hata, 1 tur). Modülün YZ kodu olarak satır sayımına dahil edilir.

**Yorum:** Doldurulduktan sonra yazılacak.

**Sınırlılıklar:** Elle efor ölçülmez, aynı kod için Basic COCOMO ile tahmin edilir. COCOMO büyük projeler için geliştirilmiştir; bu kadar küçük kodda tahmin yüksek çıkar, bu yüzden oran kesin ölçüm değil gösterge olarak okunmalıdır. Gerçek efor kayıtlarında insan süresi ile modelin çalışma süresi her zaman ayrıştırılamamıştır (ör. AUTH ve FILE kayıtları). Örneklem küçüktür ve modüller farklı kişilerce geliştirilmiştir; hata ve inceleme turu sayıları kısmen YZ'nin kendi derleme/test döngülerinden gelir, bağımsız insan incelemesi henüz tamamlanmamıştır.
