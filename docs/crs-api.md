# CRS API

CRS uç noktaları `CrsService` ve mevcut `CrsDbContext` üzerinden çalışır.
`Program.cs` içindeki middleware sırası, OpenAPI, sağlık kontrolü, Blazor ve diğer
modül kayıtları korunmuştur. API isteklerinde entity yerine ayrı istek modelleri
kullanılır; kimlikler URL'den gelir, istek gövdesiyle değiştirilemez.

## AUTH durumu ve erişim sınırı

**Gerçek AUTH henüz bağlı değildir.** Depodaki `FakeCurrentUser` herkesi yönetici
gösterdiğinden CRS bu servisle veya `FakeAccessPolicy` ile gelen bütün istekleri
`503 / auth_not_ready` ile kapatır. Bu kısıt Production dahil tüm ortamlarda
geçerlidir; geliştirme ortamı için yetki atlama anahtarı yoktur. Sağlık kontrolü,
OpenAPI ve mevcut Blazor sayfaları bu CRS kısıtından etkilenmez.

Gerçek AUTH hazır olduğunda kendi modülü `ICurrentUser` ve `IAccessPolicy`
gerçeklemelerini DI'a bağlamalıdır. CRS kimliği gövdeden veya özel HTTP
başlığından okumaz; yalnızca `ICurrentUser` kullanır. Oturum yoksa `401`,
istenen kapsamda yetki yoksa `403` döner. Başarılı API entegrasyon testlerinde
yalnızca test hostuna bağlanan denetimli AUTH test servisleri kullanılır;
bunlar uygulamanın gerçek AUTH entegrasyonunun tamamlandığını göstermez.

## Uç noktalar

Tüm yollar `/api/v1` ile başlar. PATCH gönderilmeyen alanları korur; boş PATCH
`422` döner. Oluşturmalar `201` ve `Location`, güncellemeler `200`, silmeler
`204` döner. GET listeleri yalnızca erişim izni bulunan öğeleri içerir.

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
| DELETE | `/courses/{courseId}` | Ders ve CRS kayıtlarını silme | Derste `course.delete` |
| GET | `/courses/{courseId}/enrolments` | Kayıt listesi | Derste `enrolment.view` |
| GET | `/courses/{courseId}/enrolments/{enrolmentId}` | Kayıt detayı | Derste `enrolment.view` |
| POST | `/courses/{courseId}/enrolments` | Kullanıcı kaydetme | Derste `enrolment.create` ve rol atama yetkileri |
| PATCH | `/courses/{courseId}/enrolments/{enrolmentId}` | `suspended` ve/veya `role` | Derste `enrolment.update`; rol gönderilirse ayrıca rol atama yetkileri |
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
  "role": "Teacher"
}
```

## Hatalar ve mevcut sınırlar

Mevcut `{ "error": { "code": "...", "message": "..." } }` biçimi korunur:

- `400`: bozuk JSON, tür uyuşmazlığı, bilinmeyen veya değiştirilemez istek alanı.
- `401 / 403`: oturum veya kaynak yetkisi eksik.
- `404`: kaynak yok veya kayıt belirtilen derse ait değil.
- `409`: kısa ad/kimlik numarası/kayıt çakışması, dolu kategori veya eşzamanlı veri bütünlüğü çakışması.
- `422`: boş/geçersiz/uzun alan, tarih aralığı, kategori döngüsü veya boş PATCH.
- `503`: gerçek AUTH servisleri henüz bağlı değil.
- `500`: beklenmeyen hata; veritabanı ayrıntıları istemciye verilmez.

AUTH, ASG ve GRD gerçeklemeleri değiştirilmemiştir. Mevcut arayüzlerde kullanıcı
ve rol kataloğu sorgulaması olmadığından `userId` pozitifliği kontrol edilir;
gerçek kullanıcının/rolün varlığı henüz doğrulanamaz. Kayıttaki `role` CRS
verisidir; gerçek AUTH rol ataması veya oturum yetkisi oluşturmaz. Birden çok
kayıt yöntemi, kayıt tarihlerini sonradan değiştirme, olay/denetim kayıtları ve
ASG/GRD verilerini silme bu API teslimine dahil değildir. `EnrolAsync` üzerindeki
YZ taslağı açıklaması korunmuştur.

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
