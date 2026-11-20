# 20.11.2026 — Seray PR #39 entegrasyonu

Düzeltme `e2b50d0f83a6ccd43fe4b54dec9360035d95ec50`, main birleşmesi `1424b04` / PR #39. ASG geliştirme dalı 3b9ccd6'dan 1424b04'e `git merge --ff-only origin/main` ile ilerletildi; yeni commit/merge commit üretilmedi. Önce bütün yerel değişiklikler coverage/pre-main-1424b04/working-tree altında yedeklendi ve SHA-256 manifest'i doğrulandı. İki ortak belge ayrı kopyalarda üç yönlü birleştirildi; izlenebilirlikte gelen eski-sonuç sütunu ile yerel ASG/FILE geliştirme sütunları birlikte korundu. Diğer yerel dosyalar hash ile değişmeden korundu.

Bu main farkında uygulama kaynağı olarak yalnız LocalDiskFileStore değişmiştir. Migration/snapshot, ortak API ve FILE arayüzleri, DI kayıtları ve Compose dosyaları upstream tarafından değiştirilmemiştir; ASG'nin mevcut sürümlü transaction ve politika bağlantıları korunur. Seray kaynağı main'deki haliyle kullanılır; yerel ek değişiklik yapılmadı, eski test aynen korunur.

Yeni sabit sayıdaki hash-şeritli SemaphoreSlim kilitleri aynı süreçteki depo instance'ları için varlık kontrolü ve File.Move adımını sıraya alır. Bu, mevcut tek uygulama sürecindeki tekrar testini düzeltir. Süreçler arası ortak fiziksel depoda aynı garantinin sınandığı iddia edilmez; static kilit süreçler arasında paylaşılmaz. ASG draft yüklemelerinin PostgreSQL hash kilidi ayrıca korunur. Bu çalışma yeni çok-replikalı depolama mimarisi geliştirmez.

Güncel main'de CRS bölüm modeli/gerçek ICourseSectionQuery yok; fail-closed UnavailableCourseSectionQuery DI kaydı korunur. Ortak dosya yöneticisi bileşeni de yok; ASG'ye özel adaptör korunur. İlgili diğer ekip kodlarında yeni değişiklik yapılmadı. Güncel sonuçlar DOGRULAMA.json içindeki serayFixIntegration alanında ve SON-KONTROL.md üst bölümündedir. Aşağıdaki önceki devir bilgileri, bu güncellemenin değiştirmediği yerlerde geçerlidir.

---
# Ortak entegrasyon sözleşmeleri — 19.11.2026

Kullanıcının açık onayıyla yalnız AUTH ASG uyarlaması ve IVirusScanner eklendi. ICurrentUser, IAccessPolicy, ICourseDirectory, ICourseSectionQuery, IEnrolmentQuery, IFileService, IFileStore ve IFilePolicy imzaları korunur.

## Sinem — CRS

`Task<bool> ICourseSectionQuery.BelongsToCourseAsync(int sectionId, int courseId, CancellationToken ct = default)` gerçek bölüm kaydını bulmalı; bölüm yoksa, başka derse aitse veya ders geçersizse false döndürmelidir. Bilinmeyen durumu true saymamalı, sentetik bölüm kimliğini kabul etmemelidir. DI'daki `AddScoped<ICourseSectionQuery, UnavailableCourseSectionQuery>()` gerçek adaptörle değiştirilecektir. Model/migration Sinem'e aittir. Ardından bölüm sahipliği olumlu/yanlış ders/silinmiş bölüm ve tam Playwright A/B çalıştırılmalıdır.

`Task<IReadOnlyList<int>> IAssignmentLearnerQuery.ListRequiredLearnerIdsAsync(int courseId, CancellationToken ct = default)` gerçek CrsService'e bağlıdır. Aktif, tarih aralığı geçerli ve Role="Student" kayıtlarının kimliklerini verir; öğretmenleri veya teslim yükümlüsü olmayanları içermez. Mevcut gerçekleme korunmuştur; yeni bölüm çalışması bu sözleşmeyi değiştirmemelidir.

## İrem — AUTH

`Task<bool> ISubmissionAccessPolicy.CanAssessLearnerAsync(int evaluatorId, int courseId, int assignmentId, int learnerId, CancellationToken ct = default)` ASG ödev kimliği/ders eşleşmesini, gerçek AUTH grade yeteneğini ve hedef öğrencinin aktif/yükümlü kaydını birlikte kontrol eder. `assignmentId` AUTH bağlam tablosunun anahtarı değildir. Şimdi DI gerçek `AssignmentSubmissionAccessPolicy` kullanır; önceki `UnavailableSubmissionScope` runtime kaydı kaldırılmıştır. Kapsam belirsizliğinde erişim verilmez.

`AuthAccessPolicy` artık IAssignmentQuery alır; Module ve `mod/assign:*` isteklerinde gerçek CourseId çözümlenir. CapabilityRules: view kayıtlı kullanıcı/yönetici; submit aktif Student; grade ilgili ders Teacher/EditingTeacher veya yönetici; addinstance/manageactivities EditingTeacher veya yönetici. Aktif öğrenci yükümlülüğü ASG katmanında ayrıca doğrulanır. Grup modeli yok; gelecekte grup kapsamı gerektiğinde bu politika güçlendirilmelidir, mevcut course-wide öğretmen davranışı gizli grup garantisi değildir.

## FILE / antivirüs

`Task<VirusScanResult> IVirusScanner.ScanAsync(Stream content, CancellationToken ct = default)` geçerli konumdan akışı okur, akışın mülkiyetini almaz. Sonuç Clean/Infected; clamd bağlantı/servis/zaman aşımı hatası AppException(`scanner_unavailable`,503); çağıran iptali normal cancellation olarak yayılır. Clean dışındaki sonuç kabul edilmez. Infected HTTP 422 `virus_detected` olur. DI singleton ClamAvVirusScanner; testler açıkça controllable scanner ile değiştirebilir, production için temiz kabul eden fallback yoktur.

Ayarlar: `ClamAv__Host`, `ClamAv__Port` (3310), `ClamAv__TimeoutSeconds` (30); `FileUpload__MaxFileBytes`, `FileUpload__MaxAreaBytes`, `FileUpload__MaxFiles`, `FileUpload__Extensions__0` vb. Compose clamd portunu host'a yayımlamaz. Config sınırları ClamAV StreamMaxLength/MaxScanSize sınırlarıyla birlikte seçilmelidir; servis reddi yükleme kabulüne dönüşmez.

FILE servisi taslak yüklemede yeni DraftUploadService bağımlılığı alır; kalıcı commit/indirme/silme/kota yöntemleri yeniden yazılmadı. AssignmentStore AttachFilesAsync, mevcut CommitAsync'i aynı transaction'a katılan FilesDbContext ile çağırır. `IFilePolicy` component=`mod_assign`, area=`submission`, ContextId=ASG assignment ID, ItemId=submission ID. Generic FILE yazma yetkisi verilmez; sadece sürüm ve pencere kontrolünden geçen iç ASG write scope yazabilir. Okuma ASG ReadAsync üzerinden aynı kullanıcı/hedef öğrenci kapsamını kullanır.

API: POST `/api/v1/file-drafts/`; POST/GET `/{draftId}/files`; DELETE `/{draftId}/files/{fileId}`; GET `/api/v1/file-drafts/limits`. Multipart tek `file` alanı, isteğe bağlı path (yalnız `/`) ve overwrite (varsayılan false). Normal yükleme aynı adı benzersizleştirir; açık overwrite mevcut sözleşme davranışıdır. Kullanıcı kimliği body'den alınmaz. Cookie yazıları için GET `/api/v1/antiforgery` token'ı `X-CSRF-TOKEN` başlığında gönderilir.

`SaveSubmission` JSON: `text`, zorunlu `version`, isteğe bağlı `fileDraftId`. Draft ID verildiğinde bütün yeni taslak mevcut teslim dosyalarının yerini alır; verilmezse mevcut dosyalar korunur. Stale version 409, kural ihlali ilgili 409/422, scan/DB dağıtım engeli 503. ASG iç IAssignmentStore genişletilmiştir; ekip ortak FILE arayüzlerinin imzası değişmez.

## Arayüz ve seed devri

Ortak dosya yöneticisi hazır olduğunda yalnız ASG'nin `asg-files.js` ve SubmissionForm dosya alanı adaptörü değiştirilir; API, draft sahipliği ve server doğrulaması korunur. Gerçek bölüm adaptörü geldikten sonra `ASG_SEED_SAMPLE=true` yalnız kontrollü Development/Testing fixture ortamında açılabilir. Mevcut hesapların yalnız isim eşleşmesi yeterli sayılmaz; sentetik credential doğrulaması da vardır. Üretimde ASG seed çalışmaz.
