# WBS-3 Mevcut Alt Fonksiyonların Yapay Zekâ Desteğiyle Yeniden Geliştirilmesi

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

**Elle geliştirme:** Karşılaştırma çifti için ikinci işlev (hesap durumu kontrolü ve başarısız giriş kaydı) de bu oturumda YZ ile üretildi; ayrı, bağımsız bir elle geliştirme yapılmadı (GRD'nin §1.5'teki yaklaşımıyla tutarlı: elle geliştirme yoksa efor COCOMO II ile tahmin edilir, §3). WBS-2 §4.4'teki "adil karşılaştırma" ilkesi (aynı kişi, aynı güncel gereksinim, kör inceleme) bu nedenle bu çift için henüz uygulanamadı; gerçek bir karşılaştırma isteniyorsa ikinci işlevin bağımsız olarak elle yazılması gerekir.

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

**Elle geliştirme:** GRD'de elle yazılmış işlev yoktur. Karşılaştırma için elle yazım eforu COCOMO II ile **tahmin** edilmiştir (§3). Hocanın şartı "seçilmiş bazı alt fonksiyonlar" olduğundan karşılaştırma diğer modüllerin çiftleriyle yapılır.

**Kanıt:** PR #23

## 2. İkinci sürüm: FILE, ödeve dosya teslimi, AUTH ve CRS'nin S/C gereksinimleri

### 2.1 Dosya altyapısı (Seray)

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

## 3. Yapay zekâ destekli ve elle geliştirme karşılaştırması

Tasarım: WBS-2 §4.4, kriter K17 (eşik yok, raporlanır). Veriler herkesin yapay zekâ kaydından ve PR geçmişinden alınır. Bu tablo herkesin kaydı tamamlanınca doldurulur.

| Modül | İşlev | Üretim | İnsan eforu (saat) | Satır | İnceleme ve testte bulunan hata | Hata / 1.000 satır | İnceleme turu | Değişen satır oranı | Kaynak |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Altyapı | Tohumlama | YZ | | | | | | | |
| Altyapı | Hata gövdesi ve mimari testi | Elle | | | | | | | |
| AUTH | Giriş ve çıkış | YZ | ~1 | ~1086* | 6† | — | 1 | — | PR (bu şube) |
| AUTH | Hesap durumu ve başarısız giriş kaydı | Elle yapılmadı; aynı oturumda YZ ile üretildi | | | — | — | — | — | |
| CRS | Ders oluşturma | YZ | | | | | | | |
| CRS | Derse kayıt | Elle | | | | | | | |
| ASG | Teslim kaydetme | YZ | | | | | | | |
| ASG | Teslim listesi | Elle | | | | | | | |
| GRD | Puan yazma | YZ | | | | | | | |
| GRD | Kişisel not raporu | Elle yapılmadı; efor COCOMO II tahmini | | | — | — | — | — | |
| FILE | İndirme ve yetki | YZ | ~0,5 | ~540‡ | 2§ | — | 1 | — | PR (bu şube: `irem/file-wbs3-2surum`) |
| FILE | Kota hesabı | Elle yapılmadı; aynı oturumda YZ ile üretildi | | | — | — | — | — | |

2. sürümün diğer çiftleri (Seray, Sinem, Aslı Ceren, Fatma Zehra) kendi PR'ları bitince bu tabloya eklenir.

\* AUTH satırındaki satır sayısı, giriş/çıkış işlevine özgü değil; tüm AUTH modülünün (üretilmiş geçiş dosyaları hariç) el yazısı satır toplamıdır. Giriş/çıkış ve hesap durumu/başarısız giriş kaydı aynı sınıfta (`AuthService.LoginAsync`) iç içe olduğundan anlamlı bir ayrım yapılamadı; bu, AUTH çiftinin bu tablo için en büyük sınırlılığıdır.
† İnceleme turu sayısı, bu oturumdaki kendi kendine derleme/test döngülerinden (insan incelemesi değil) sayılmıştır: ilk derleme hatası (eksik `using`), ardından tam test koşusunda ortaya çıkan 3 CRS/GRD testi + 1 birim testi düzeltmesi, ardından AUTH'un kendi testlerinde 3 ayrı hata (kimlik çakışması, kilitlenme eşiği, `/permissions`'ın CRS'ye bağımlılığı). Bağımsız bir insan kod incelemesi henüz yapılmadı.
‡ FILE satırındaki satır sayısı da (AUTH'taki gibi) tüm FILE katkımın (İrem) toplamıdır; indirme/yetki ile kota hesabı aynı `FileService` sınıfında iç içedir, ayrı sayılamaz.
§ İlk derlemede iki hata (yanlış tam nitelikli tür adı, `Task`/`ValueTask` uyuşmazlığı); ikinci denemede derlendi ve tüm testler ilk çalıştırmada geçti.

**Yorum:** Doldurulduktan sonra yazılacak. Sınırlılıklar da burada belirtilecek: örneklem küçük, geliştiriciler farklı, GRD'de ve AUTH'ta elle veri yok (AUTH'ta ve FILE'da ayrıca iki işlev aynı oturumda ve aynı kod birimlerinde üretildiği için anlamlı biçimde ayrılamadı).
