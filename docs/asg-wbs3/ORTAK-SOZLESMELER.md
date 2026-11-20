> **19.11.2026 v2 güncellemesi:** Güncel durum [ASG/FILE teslim raporundadır](../asg-file-wbs3/TESLIM-RAPORU.md). Gerçek AUTH ve aktif CRS öğrenci kapsamı bağlıdır; yalnız gerçek bölüm modeli/ICourseSectionQuery hâlâ engeldir. Manuel kod yazma zorunluluğu kaldırılmıştır. Aşağıdaki önceki rapor tarihsel kayıttır; eski AUTH/manuel bekleme ifadeleri güncel durum sayılmamalıdır. V2 sonuçları ve devri [doğrulama](../asg-file-wbs3/DOGRULAMA.json), [sözleşmeler](../asg-file-wbs3/ENTEGRASYON.md) belgelerindedir. V1 PR #21 korunur, v2 PR açılmadı.
# PR #21 birleşmesinde güncel bağlantılar

18.11.2026 kullanıcı onayıyla CrsService artık IAssignmentLearnerQuery gerçekleştiriyor: aktif, tarih aralığı geçerli, Role="Student" kayıtları. DI gerçek CrsService'e bağlı; ICourseDirectory/IEnrolmentQuery de aynı gerçek servisi kullanır. Sinem için öğrenci sözleşmesi artık öneri değildir.

ICourseSectionQuery, CRS bölüm modeli olmadığı için UnavailableCourseSectionQuery ile false döner; gerçek bölüm sorgusu Sinem'e kalır. ISubmissionAccessPolicy, gerçek AUTH yokken UnavailableSubmissionScope ile false döner; İrem'in gerçek hedef öğrenci politikası beklenir. Geçici ASG sahteleri yalnız açık test fixture'larında kullanılır; runtime gerçek CRS ile sentetik değerlendirici izinleri karıştırılmaz. Compose/launch varsayılan sahte kimlik bayrağı kapalıdır.

[Birleşme kararları ve riskler](BIRLESME-RAPORU.md) güncel kaynaktır. Aşağıdaki sözleşme imzaları/parametreleri geçerlidir; eski “DI geçişi” anlatımı merge öncesi uygulama durumunu açıklar.

---
# AUTH/CRS teslim kapsamı sözleşmeleri — 18.11.2026

Kullanıcı iki yeni ortak sözleşmeyi açıkça onayladı. Mevcut IAccessPolicy, IEnrolmentQuery, ICourseDirectory ve ICourseSectionQuery imzaları değiştirilmedi. Bu belgede gerçek servis adları öneridir; gerçek AUTH/CRS kodu geliştirilmedi.

## Sinem — IAssignmentLearnerQuery (CRS)

Kaynak: src/Moodle.Application/Abstractions/IAssignmentLearnerQuery.cs

```csharp
Task<IReadOnlyList<int>> ListRequiredLearnerIdsAsync(
    int courseId, CancellationToken ct = default);
```

- courseId: gerçek CRS dersinin pozitif kimliği; ASG ödevindeki CourseId ile aynı kimlik uzayı.
- ct: istek iptalini veri sorgusuna iletir.
- Dönüş: bu derste aktif ve bireysel ödev teslim yükümlülüğü bulunan öğrencilerin benzersiz pozitif kullanıcı kimlikleri. Sıralama garantisi gerekmez. Öğretmen, gözlemci, misafir, pasif/askıya alınmış kayıt ve teslim yükümlülüğü olmayan kullanıcılar dahil edilmemeli.
- Ders/kapsam bilinmiyorsa boş liste. null sözleşme çıktısı değildir; ASG savunma amacıyla null'u da boş kabul eder. Sağlayıcı/veritabanı arızasında exception/cancellation yayılmalı; tüm kayıtlı kullanıcılara geri dönüş yapılmamalı.
- Mevcut IEnrolmentQuery.ListEnrolledUserIdsAsync tüm kayıtlıları döndürmeye devam eder; ASG teslim listesi artık bunu kullanmaz. ASG ayrıca IsActivelyEnrolledAsync ile aktifliği yeniden kontrol eder.
- Bu imza ders düzeyinde yükümlülük içindir. Gelecekte ödeve özgü muafiyet/katılımcı seçimi eklenecekse ders listesini tahminle genişletmeyin; ayrı sözleşme tasarımını birlikte değerlendirin.

Geçici FakeAssignmentLearnerQuery yalnız courseId=1 için [3,4,5], diğer dersler için boş liste döndürür. AUTH/CRS tabloları veya kullanıcı kayıtları oluşturmaz.

Mevcut ICourseSectionQuery.BelongsToCourseAsync(sectionId, courseId, ct) için Sinem gerçek bölüm tablosunda bölümün varlığını ve CourseId eşitliğini doğrulamalı; bulunamayan veya başka derse bağlı bölüm false dönmeli. FakeCourseDirectory/FakeEnrolmentQuery/FakeCourseSectionQuery aynı birleşmede kaldırılmalı.

## İrem — ISubmissionAccessPolicy (AUTH)

Kaynak: src/Moodle.Application/Abstractions/ISubmissionAccessPolicy.cs

```csharp
Task<bool> CanAssessLearnerAsync(
    int evaluatorId, int courseId, int assignmentId,
    int learnerId, CancellationToken ct = default);
```

| Parametre | Anlam / doğrulama |
| --- | --- |
| evaluatorId | Güvenilir ICurrentUser'dan alınan değerlendirici kullanıcı kimliği; istemci gövdesinden alınmaz |
| courseId | ASG ödevinin kalıcı CourseId'si; gerçek CRS dersiyle doğrulanır |
| assignmentId | ASG Assignment.Id; AUTH context tablosunun PK'si değildir |
| learnerId | Okunacak teslimin sahibi veya CRS öğrenci listesindeki aday |
| ct | Yetki/veri sorgularına taşınacak istek iptali |

true yalnız değerlendiricinin belirtilen ders/ödev bağlamında bu hedef öğrenciyi değerlendirme kapsamına sahip olduğu kesin olarak doğrulanınca dönmeli. Grup kapsamı, rol ataması, ders üyeliği ve gerçek AUTH bağlam eşlemesi İrem'in modelinde çözümlenmeli. Ders ve ödev ilişkisi doğrulanmalı; başka ders veya modüldeki yetki yeterli değildir. Bilinmeyen kullanıcı/bağlam/kapsam false; teknik hata exception olarak yayılır. Genel yönetici/grade yetkisinden hedef öğrenci izni varsayılmamalı.

IAccessPolicy.HasCapabilityAsync(..., "mod/assign:grade", ContextRef.ForModule(assignmentId)) kontrolü kaldırılmadı. Yeni sözleşme buna EK kapsam denetimidir. ContextRef.ForModule içindeki ASG kimliğinin gerçek AUTH context kaydına eşlenmesi birleşme kabul testinde doğrulanmalı.

Geçici FakeSubmissionAccessPolicy yalnız evaluatorId=2, courseId=1 ve learnerId=3..5 için; ayrıca IAssignmentStore.FindAsync ile ödev gerçekten mevcut ve aynı derse bağlıysa true döndürür. Diğer durumlar false. Gerçek yetki modeli veya genel yönetici bypass'ı değildir.

## ASG ortak uygulama davranışı

AssignmentService.CanAssessAsync hem ListAsync hem ReadAsync tarafından kullanılır: yükümlü öğrenci kümesi + modül grade yetkisi + aktif kayıt + yeni hedef öğrenci politikası birlikte aranır. Liste kapsamı SQL filtre/sayfalamasından önce daraltılır; yetkisiz öğrencinin satırı, teslim ID'si veya cursor bilgisi listeye girmez. Liste tam kapsam verilmezse boş olabilir (HTTP 200, sıfır veri); ayrıntı erişimi HTTP 404 ile gizlenir. Görünmeyen ödev 404, görünür ödevde grade yetkisi yoksa liste 403. Sağlayıcı hatası standart ayrıntısız 500 olur, daha geniş kapsama fallback yapılmaz.

Öğrencinin kendi teslimini okuması ayrı sahiplik yoludur: aktif ve yükümlü öğrenci + submit yetkisi gerekir. Yeni yükümlülük denetimi Mine/Save/Submit ve CanSave/CanSend hesaplarına da uygulanır; öğretmen ya da gözlemcinin genel capability üzerinden öğrenci gibi teslim yazması engellenir.

## DI geçişi

Şimdi Infrastructure/DependencyInjection.cs iki yeni sözleşmeyi scoped kaydeder:
- Development veya Testing + açık Development:UseFakeIdentity=true: iki sınırlı fake.
- Varsayılan/diğer yapılandırma: UnavailableSubmissionScope; öğrenci listesi boş, her hedef erişimi false.
- Program son host yapılandırmasını denetler; Production/Staging'de fake bayrağı uygulamayı başlatmaz.

Birleşmede ilgili kayıtları tek noktadan değiştirin (örnek gerçek tip adları henüz mevcut değildir):

```csharp
services.AddScoped<IAssignmentLearnerQuery, CrsAssignmentLearnerQuery>();
services.AddScoped<ISubmissionAccessPolicy, AuthSubmissionAccessPolicy>();
```

Bunları mevcut fake/fallback kayıtlarına gelişigüzel eklemek yerine eski iki kaydı kaldırıp değiştirin; IServiceCollection aynı sözleşmenin çoklu kaydını tutabilir ve son kayıt çözümlemesi sessiz hataya neden olabilir. İrem aynı zamanda ICurrentUser/IAccessPolicy kayıtlarını, Sinem ICourseDirectory/IEnrolmentQuery/ICourseSectionQuery kayıtlarını gerçek sağlayıcılarla değiştirmeli. Servis ömrü scoped kalmalı; scoped DbContext singleton'a taşınmamalı.

Kullanıcı/rol değişikliğinde stale izin üretmemek için varsa cache doğru değerlendirici/ders/ödev/öğrenci anahtarını ve iptal stratejisini içermeli. ASG şu anda aday başına politika/aktiflik sorgular; büyük derslerde gerçek sağlayıcıların istek içi toplu yükleme/cache uygulaması değerlendirilmeli. Bu performans ihtiyacı güvenli daraltmayı kaldırma gerekçesi değildir.

## Birleşme kabul kontrolleri

Gerçek oturum/cookie, CSRF, modül bağlam eşlemesi, iki farklı değerlendirici grubu, grup dışı ayrıntı URL'si, karma öğretmen/öğrenci listesi, pasif kayıt, yanlış ders/modül ve sağlayıcı kesintisiyle regresyon çalıştırılmalı. Test başlığı/header kimliği yalnız test hostundadır; uygulama bunu kabul etmez.

Gerçek User/Course/Section FK'leri, migration snapshot sırası, tam ortak seed ve sabit ders1 menü bağlantısı ekip birleşmesinde ele alınmalı. Sentetik 2/3/4/5 kullanıcı kimliklerini gerçek verilerle eşleştirerek seed çalıştırmayın. AUTH cookie forwarding/CSRF kabulü ve reverse proxy origin yapılandırması ayrıca doğrulanmalı.
