# CRS API

CRS uç noktaları `CrsService` ve mevcut `CrsDbContext` üzerinden çalışır.
`Program.cs` içindeki middleware sırası, OpenAPI, sağlık kontrolü, Blazor ve diğer
modül kayıtları korunmuştur. API isteklerinde entity yerine ayrı istek modelleri
kullanılır; kimlikler URL'den gelir, istek gövdesiyle değiştirilemez.

## AUTH durumu ve erişim sınırı

**Gerçek AUTH bağlıdır (PR #28).** CRS kimliği yalnızca `ICurrentUser`
(`AuthCurrentUser`, oturum çerezi) üzerinden okur; gövdeden veya özel HTTP
başlığından okumaz. Yetki `IAccessPolicy` (`AuthAccessPolicy` → `CapabilityRules`)
ile denetlenir. Ders bağlamındaki rol (`EditingTeacher`, `Teacher`, `Student`)
CRS kaydından (`IEnrolmentQuery.FindActiveRoleAsync`) okunur; askıya alınmış veya
süresi dışındaki kayıt rol vermez. Oturum yoksa `401`, istenen kapsamda yetki yoksa
`403` döner.

Geliştirme sahteleri (`FakeCurrentUser`, `FakeAccessPolicy`) yalnızca
`Development:UseFakeIdentity` ile açıkça istendiğinde kullanılır; CRS bu sahtelerle
gelen istekleri her ortamda `503 / auth_not_ready` ile kapatmaya devam eder.
Zorunlu parola değişikliği bekleyen kullanıcı, değişikliği tamamlayana kadar CRS
uçlarına `403 / password_change_required` alır (AUTH-FR-09, PR #46).

Gerçek oturumla uçtan uca davranış `AuthCrsIntegrationTests` ile doğrulanır
(Manager ders oluşturur, Student oluşturamaz, ders rolü CRS kaydından gelir; PR #47).
Diğer CRS API testleri test hostunda denetimli `ICurrentUser`/`IAccessPolicy`
yerine geçenleriyle çalışır.

## Uç noktalar

Tüm yollar `/api/v1` ile başlar. PATCH gönderilmeyen alanları korur; boş PATCH
`422` döner. Oluşturmalar `201` ve `Location`, güncellemeler `200`, silmeler
`204` döner; arka planda ders silme açıkken ders silme `202` döner (aşağıda).
GET listeleri yalnızca erişim izni bulunan öğeleri içerir.

| Metot | Yol | İstek / işlem | Yetki ve kapsam |
| --- | --- | --- | --- |
| GET | `/categories` | Kategori listesi | Her kategori için `category.view` |
| GET | `/categories/{categoryId}` | Kategori detayı | Kategoride `category.view` |
| POST | `/categories` | `name`, isteğe bağlı `parentId` | Üst kategoride veya kökte sistemde `category.manage` |
| PATCH | `/categories/{categoryId}` | `name` ve/veya `parentId` | Kategoride ve değişiyorsa yeni üst kategoride/sistemde `category.manage` |
| DELETE | `/categories/{categoryId}` | Boş kategori silme | Kategoride `category.manage` |
| GET | `/courses` | Ders listesi | Her derste `course.view`; gizli ders için ayrıca `course.viewhidden` |
| GET | `/courses/{courseId}` | Ders detayı | Derste `course.view`; gizli ders için ayrıca `course.viewhidden` |
| POST | `/courses` | Ders oluşturma | Hedef kategoride `course.create` |
| PATCH | `/courses/{courseId}` | Ders alanlarını güncelleme | Derste `course.update`; kategori değişirse hedefte `course.create` |
| DELETE | `/courses/{courseId}` | Ders ve CRS kayıtlarını silme; `Crs:BackgroundDeletion:Enabled=true` ise iş kuyruğa alınır (`202`, `Location`) | Derste `course.delete` (kabulden önce) |
| GET | `/courses/deletion-jobs/{jobId}` | Arka plan silme işinin durumu (`Pending`, `Running`, `Succeeded`, `Failed`) | Ders varsa derste `course.delete`; ders silinmişse talep sahibi veya sistem/kategori düzeyinde `course.delete` |
| GET | `/courses/{courseId}/enrolments` | Kayıt listesi | Derste `enrolment.view` |
| GET | `/courses/{courseId}/enrolments/{enrolmentId}` | Kayıt detayı | Derste `enrolment.view` |
| POST | `/courses/{courseId}/enrolments` | Kullanıcı kaydetme | Derste `enrolment.create` ve rol atama yetkileri |
| PATCH | `/courses/{courseId}/enrolments/{enrolmentId}` | `suspended`, `role`, `startsAt`, `endsAt` (en az biri) | Derste `enrolment.update`; rol gönderilirse ayrıca rol atama yetkileri |
| DELETE | `/courses/{courseId}/enrolments/{enrolmentId}` | Kayıttan çıkarma | Derste `enrolment.delete` |

Yetki adları WBS-2 §3.2.4'teki uygulama yetkileridir; `category.view`,
`category.manage`, `course.view`, `course.viewhidden` ve `enrolment.view`
okuma/kategori işlemleri için eklenmiştir. AUTH bunları `ContextRef` üzerinden
değerlendirir. Liste uç noktaları her öğenin kapsamını ayrı kontrol eder.

Rol atama için hem `course.role.assign` hem de hedef role özel
`course.role.assign:<role>` gerekir (ör. `course.role.assign:Teacher`). Mevcut
`IAccessPolicy` hedef rol parametresi taşımadığından bu ek yetki, genel kayıt
yetkisiyle keyfî rol atanmasını önler. Varsayılan `Student` rolüyle kayıt
oluşturma da aynı kontrolden geçer. Yalnızca durum güncelleme rol yetkisi istemez.
Bu adların gerçek AUTH politikasında da desteklenmesi gerekir.

Bir kayıt kimliği farklı dersin URL'si altında okunamaz, güncellenemez veya
silinemez (`404`). Yetki reddedilen birleşik güncellemelerde hiçbir alan yazılmaz.

## İstek örnekleri

`POST /api/v1/courses`:

```json
{
  "categoryId": 1,
  "fullName": "Veritabanı Sistemleri",
  "shortName": "VT101",
  "idNumber": "CRS-101",
  "visible": true,
  "startDate": "2026-11-17T09:00:00+03:00",
  "endDate": "2027-01-17T09:00:00+03:00"
}
```

`PATCH /api/v1/courses/1` yalnızca gönderilen alanları değiştirir. `idNumber`,
`startDate`, `endDate` açıkça `null` gönderilerek temizlenebilir. Kategori için
`parentId: null` kategoriyi köke taşır. Zorunlu alanlara `null` gönderilemez.
Tarih alanları PostgreSQL'e yazılmadan önce UTC'ye dönüştürülür.

`POST /api/v1/courses/1/enrolments`:

```json
{
  "userId": 42,
  "role": "Student",
  "suspended": false,
  "startsAt": null,
  "endsAt": null
}
```

`PATCH /api/v1/courses/1/enrolments/1`:

```json
{
  "suspended": true,
  "role": "Teacher",
  "endsAt": "2027-01-31T17:00:00+03:00"
}
```

Gönderilmeyen alanlar değişmez; `startsAt`/`endsAt` açıkça `null` gönderilerek
temizlenebilir. Bitiş başlangıçtan sonra olmalıdır; yalnızca bir tarih gönderilirse
mevcut diğer tarihle birlikte doğrulanır, geçersiz aralık `422` döner ve hiçbir alan
yazılmaz (CRS-FR-07, PR #45).

## Hatalar ve mevcut sınırlar

Mevcut `{ "error": { "code": "...", "message": "..." } }` biçimi korunur:

- `400`: bozuk JSON, tür uyuşmazlığı, bilinmeyen veya değiştirilemez istek alanı.
- `401 / 403`: oturum veya kaynak yetkisi eksik; `403 / password_change_required`: zorunlu parola değişikliği bekliyor.
- `404`: kaynak yok veya kayıt belirtilen derse ait değil.
- `409`: kısa ad/kimlik numarası/kayıt çakışması, dolu kategori, eşzamanlı veri bütünlüğü çakışması veya bekleyen/devam eden yedekleme/geri yükleme işlemi (`course_operation_in_progress`, CRS-NFR-03).
- `422`: boş/geçersiz/uzun alan, tarih aralığı, kategori döngüsü veya boş PATCH.
- `503 / auth_not_ready`: istek AUTH geliştirme sahteleriyle geldi.
- `500`: beklenmeyen hata; veritabanı ayrıntıları istemciye verilmez.

Mevcut arayüzlerde kullanıcı kataloğu sorgulaması olmadığından kayıtta `userId`
yalnızca pozitiflik açısından kontrol edilir; AUTH'ta kullanıcının varlığı
doğrulanmaz. Kayıttaki `role`, gerçek AUTH'ta o dersteki yetkileri belirler.

**Bilinen sapmalar ve sınırlar:**
- **Kayıt yöntemi (CRS-FR-06, FR-08, NFR-01):** WBS-2 §3.2'deki `EnrolmentMethods`
  tablosu ve `enrolmentMethodId`/`roleId` alanları uygulanmamıştır. Her ders tek ve
  örtük bir manuel kayıt yöntemiyle çalışır; mükerrer kayıt `(CourseId, UserId)`
  benzersizliğiyle engellenir. AUTH, ASG, GRD ve FILE `IEnrolmentQuery` üzerinden ders
  başına tek kayıt varsayar (`FindActiveRoleAsync` tek değer döner); çoklu yöntem bu
  sözleşmenin ekip kararıyla değiştirilmesini gerektirir.
- **Ders silme ve diğer modüller (CRS-FR-05, NFR-02):** CRS kendi kayıtlarını aynı
  transaction'da siler ve `course.deleted` olayını kayıtlı `ICrsEventExtension`
  uzantılarına iletir (`CrsEventDispatchWorker`, PR #47). ASG, GRD ve FILE için henüz
  üretim uzantısı yoktur; bu modüllerin ders verileri silmeden sonra kalır (dersin
  varlığını denetledikleri için erişilemez). Temizlik ilgili modüllerin işidir.
- `EnrolAsync` üzerindeki YZ taslağı açıklaması korunmuştur.

## Doğrulama

```powershell
dotnet build Moodle.slnx --no-restore
dotnet test Moodle.slnx --no-restore
```

Testler için Docker Desktop gerekir. PostgreSQL entegrasyon testleri geçici
konteynerlerde gerçek migration'ları uygular; geliştirme veritabanını kullanmaz.
Testler CRUD yanında oturum/yetki reddini, Production'da sahte AUTH erişiminin
kapanmasını, dersler arası kayıt erişimini, rol yetkilerini, gizli dersleri,
PATCH/null davranışını ve standart hata gövdelerini doğrular.

17 Kasım 2026 doğrulaması:

- `dotnet build Moodle.slnx --no-restore`: başarılı, 0 uyarı, 0 hata.
- `dotnet test Moodle.slnx --no-build --no-restore`: 31 birim ve 99 entegrasyon
  testi başarılı; toplam 130, başarısız 0, atlanan 0.
- Önceden çalışan 23 testin dosyaları değiştirilmeden tamamı geçti.
