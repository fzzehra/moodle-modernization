# WBS-2 Hedef Mimari ve Modernizasyon Sürecinin Tasarlanması

## 1. Hedef dil ve teknoloji seçimi (Seray)

## 2. Hedef mimari ve deployment diyagramı (Seray)

## 3. Veri ve arayüz tasarımı

API tabloları: Metot · Yol · İstek · Yanıt · Hangi rol çağırabilir

### 3.1 Kimlik ve yetkilendirme (Sinem)

### 3.2 Ders ve kayıt (Sinem)

#### 3.2.1 Tasarım yaklaşımı

Ders ve kayıt (CRS) modülünün modernizasyonunda, Moodle 5.3.1 kaynak kodundan elde edilen mevcut veri modeli ve iş kuralları temel alınacaktır. Amaç, mevcut ders yönetimi ve kullanıcı kayıt işlevlerini koruyarak veri doğrulama, yetkilendirme, arayüz erişimi ve bakım kolaylığı açısından daha sürdürülebilir bir hedef tasarım oluşturmaktır.

Hedef tasarımda ders oluşturma, güncelleme ve silme işlemleri ile kullanıcıyı derse kaydetme, kayıt bilgilerini güncelleme ve dersten kayıt silme işlemleri ayrı servis operasyonları olarak ele alınacaktır. Bu operasyonlara erişim için REST tabanlı API arayüzleri önerilecektir.

Mevcut Moodle veri yapısındaki course, course_categories, enrol ve user_enrolments tabloları ile user ilişkisi korunacaktır. Veri bütünlüğü kontrolleri, iş kuralları ve yetkilendirme doğrulamaları servis katmanında uygulanacaktır.

Kaynak sistem üzerinde gerçekleştirilen CRS-TEST-01–07 testleri, temel ders ve kayıt işlemlerinin beklenen sonuçlarını doğrulamıştır. Test sırasında ders kısa adının başında bulunan TAB karakterinin ayrı bir değer olarak kabul edildiği gözlemlenmiştir. Bu nedenle hedef tasarımda kısa adların kaydedilmeden önce görünmeyen karakterler açısından doğrulanması önerilmektedir.

Bu bölümde tanımlanan REST API uç noktaları ve servis katmanı, mevcut Moodle 5.3.1 uygulamasında bulunduğu doğrulanan bileşenler değil, modernizasyon kapsamında önerilen hedef tasarım öğeleridir.

#### 3.2.2 Veri modeli ve bütünlük kuralları

Hedef CRS tasarımında Moodle 5.3.1 veri modeliyle uyumluluk korunacaktır. Dersler, kategoriler, kayıt yöntemleri ve kullanıcı kayıtları mevcut tablolar üzerinden yönetilecektir. Yeni bir veritabanı şeması oluşturulması bu aşamada önerilmemektedir.

| Tablo | Temel alanlar | Hedef tasarımdaki görevi |
|---|---|---|
| `course` | `id`, `category`, `fullname`, `shortname`, `idnumber`, `visible` | Ders bilgilerinin yönetilmesi |
| `course_categories` | `id`, `name`, `parent`, `visible` | Ders kategorilerinin ve hiyerarşinin yönetilmesi |
| `enrol` | `id`, `courseid`, `enrol`, `status` | Ders kayıt yöntemlerinin tanımlanması |
| `user_enrolments` | `id`, `enrolid`, `userid`, `status`, `timestart`, `timeend` | Kullanıcı ders kayıtlarının yönetilmesi |
| `user` | `id` | Ders kayıtlarının kullanıcılarla ilişkilendirilmesi |

**Temel ilişkiler**

1. `course.category` → `course_categories.id`
2. `course_categories.parent` → `course_categories.id`
3. `enrol.courseid` → `course.id`
4. `user_enrolments.enrolid` → `enrol.id`
5. `user_enrolments.userid` → `user.id`

**Hedef veri bütünlüğü kuralları**

- Ders oluşturma ve güncelleme işlemlerinde kategori geçerliliği kontrol edilecektir.
- Ders kısa adı (`shortname`) ve boş olmayan ders kimlik numarası (`idnumber`) benzersizlik açısından doğrulanacaktır.
- Kısa ad alanındaki baştaki ve sondaki boşluklar ile TAB gibi görünmeyen karakterler için açık bir doğrulama politikası uygulanacaktır. Bu politika mevcut kayıtlarla uyumluluk değerlendirilerek belirlenecektir.
- Aynı kullanıcı, aynı kayıt yöntemi örneğine birden fazla kez kaydedilemeyecektir.
- Kayıt işlemlerinde kullanıcı ve kayıt yöntemi varlığı doğrulanacaktır.
- Ders silme işlemlerinde ilişkili kayıtların ve içeriklerin tutarlı biçimde temizlenmesi sağlanacaktır.
- Kayıt durumu ve başlangıç/bitiş zamanları servis katmanında doğrulanacaktır.

**Veritabanı ve servis katmanı sorumlulukları**

Mevcut `user_enrolments` tablosundaki `(enrolid, userid)` benzersizliği korunacaktır. `course.shortname` alanında ise mevcut PostgreSQL şemasında benzersiz olmayan indeks bulunduğu gözlemlenmiştir. Bu nedenle kısa ad benzersizliği servis katmanında kontrol edilecektir.

Eşzamanlı işlemler nedeniyle oluşabilecek mükerrer kayıt riskleri ayrıca değerlendirilecektir. Veritabanı düzeyinde ek benzersizlik kısıtı uygulanması, mevcut Moodle uyumluluğu ve veri temizliği analizinden sonra kararlaştırılacaktır.

Bu tasarım, WBS-1 kapsamındaki CRS-BR-01, CRS-BR-02, CRS-BR-03, CRS-BR-07 ve CRS-FR-01, CRS-FR-02, CRS-FR-06 gereksinimleriyle ilişkilidir.
#### 3.2.3 REST API tasarımı

Hedef CRS modülünde ders yönetimi ve kullanıcı kayıt işlemlerinin REST tabanlı API uç noktaları üzerinden sunulması önerilmektedir. API tasarımı mevcut Moodle 5.3.1 iş kurallarını koruyacak, ancak istemci ile iş mantığı arasındaki bağımlılığı azaltacaktır.

Aşağıdaki uç noktalar modernizasyon kapsamında önerilen hedef arayüzlerdir; mevcut Moodle API uç noktaları oldukları iddia edilmemektedir.

**Ders yönetimi API'leri**

| Metot | Yol | İstek | Başarılı yanıt | Yetkili rol |
|---|---|---|---|---|
| POST | `/api/v1/courses` | `categoryId`, `fullName`, `shortName`, `idNumber`, `startDate`, `endDate` | `201 Created`, ders kimliği | Yetkili yönetici / ders oluşturma yetkisine sahip kullanıcı |
| PATCH | `/api/v1/courses/{courseId}` | Güncellenecek ders alanları | `200 OK`, güncel ders bilgileri | Yetkili yönetici / ders düzenleme yetkisine sahip kullanıcı |
| DELETE | `/api/v1/courses/{courseId}` | Ders kimliği | `204 No Content` veya asenkron işlem için `202 Accepted` | Yetkili yönetici / ders silme yetkisine sahip kullanıcı |

**Kullanıcı kayıt API'leri**

| Metot | Yol | İstek | Başarılı yanıt | Yetkili rol |
|---|---|---|---|---|
| POST | `/api/v1/courses/{courseId}/enrolments` | `userId`, `enrolId`, `roleId`, `startTime`, `endTime` | `201 Created`, kayıt kimliği | Kullanıcı kaydetme yetkisine sahip kullanıcı |
| PATCH | `/api/v1/courses/{courseId}/enrolments/{enrolmentId}` | `status`, `startTime`, `endTime` | `200 OK`, güncel kayıt bilgileri | Kayıt düzenleme yetkisine sahip kullanıcı |
| DELETE | `/api/v1/courses/{courseId}/enrolments/{enrolmentId}` | Kayıt kimliği | `204 No Content` | Kayıt silme yetkisine sahip kullanıcı |

**API tasarım ilkeleri**

- API sürümlemesi `/api/v1` yolu üzerinden yapılacaktır.
- İstek verileri servis katmanında doğrulanacaktır.
- Ders kısa adı ve kimlik numarası benzersizlik kontrolleri uygulanacaktır.
- Kayıt yöntemi, kullanıcı ve ders ilişkileri doğrulanacaktır.
- Yetkilendirme yalnızca kullanıcı rol adına göre değil, ilgili ders veya kategori bağlamındaki yetkilere göre yapılacaktır.
- Başarılı işlemlerde uygun HTTP durum kodları döndürülecektir.
- Hatalı istekler için tutarlı hata yanıtları kullanılacaktır.
- Ders ve kayıt değişiklikleri için sistem olayları oluşturulacaktır.
- Ders silme işlemi asenkron yürütülürse istemciye işlem durumu takip edilebilir şekilde sunulacaktır.

**Önerilen hata yanıtları**

| HTTP kodu | Durum |
|---|---|
| `400 Bad Request` | Geçersiz veya eksik istek alanları |
| `401 Unauthorized` | Kimlik doğrulaması yapılmamış istek |
| `403 Forbidden` | İşlem için gerekli yetki bulunmaması |
| `404 Not Found` | Ders, kullanıcı kaydı veya ilgili kaynağın bulunamaması |
| `409 Conflict` | Mükerrer kısa ad, kimlik numarası veya kayıt çakışması |
| `422 Unprocessable Content` | Tarih aralığı veya alan doğrulama kurallarının ihlali |

**Gereksinim ilişkisi:** CRS-FR-01–CRS-FR-11, CRS-BR-01–CRS-BR-13.
#### 3.2.4 Rol bazlı yetkilendirme

Hedef CRS tasarımında yetkilendirme, Moodle'ın rol ve bağlam (context) tabanlı erişim kontrolü yaklaşımıyla uyumlu olacaktır. Kullanıcının yalnızca rol adına bakılarak işlem izni verilmeyecek; ilgili ders veya kategori bağlamında gerekli yetkiye sahip olup olmadığı kontrol edilecektir.

**Yetkilendirme matrisi**

| İşlem | Gerekli Moodle yetkisi | Kontrol bağlamı |
|---|---|---|
| Ders oluşturma | `moodle/course:create` | Kategori |
| Ders bilgilerini güncelleme | `moodle/course:update` | Ders |
| Ders silme | `moodle/course:delete` | Ders |
| Kullanıcıyı derse kaydetme | `enrol/manual:enrol` | Ders |
| Kullanıcının kayıt durumunu güncelleme | `enrol/manual:manage` | Ders |
| Kullanıcıyı dersten çıkarma | `enrol/manual:unenrol` | Ders |
| Kullanıcıya rol atama | `moodle/role:assign` | Ders |

Bu matris, manuel kayıt yöntemi ve temel ders yönetimi işlemleri için önerilen yetkilendirme eşlemesini göstermektedir. Farklı kayıt eklentileri kendi yetki kurallarını uygulayabilir. Gerçek yetki kontrolleri, ilgili Moodle işlemlerinin kaynak kodu ve bağlam kurallarıyla uyumlu şekilde uygulanacaktır.

**Yetkilendirme ilkeleri**

1. Her API isteğinde kimlik doğrulaması yapılacaktır.
2. İlgili ders veya kategori bağlamı belirlenecektir.
3. İşlem için gerekli capability kontrol edilecektir.
4. Yetkisiz işlemler `403 Forbidden` yanıtıyla reddedilecektir.
5. Kullanıcı kayıt işlemlerinde ilgili kayıt yönteminin etkinliği ve işlem yetkisi doğrulanacaktır.
6. Rol atama işlemlerinde kullanıcının hedef rolü atamaya yetkili olup olmadığı ayrıca kontrol edilecektir.
7. Sistem ana sayfası dersi (`SITEID`) için mevcut Moodle kısıtları korunacaktır.
8. Yetki kontrolleri yalnızca istemci arayüzünde değil, sunucu tarafında gerçekleştirilecektir.

**Gereksinim ilişkisi:** CRS-FR-01, CRS-FR-04, CRS-FR-05, CRS-FR-06, CRS-FR-07, CRS-FR-08, CRS-FR-09 ve CRS-BR-05, CRS-BR-12.

#### 3.2.5 İşlem akışları ve hata yönetimi

Hedef CRS modülünde ders yönetimi ve kullanıcı kayıt işlemleri API, servis ve veri erişim katmanları üzerinden yürütülecektir. İş kuralları servis katmanında merkezi olarak uygulanacak; veri doğrulama, yetkilendirme ve veri bütünlüğü kontrolleri ilgili işlemler kapsamında gerçekleştirilecektir.

**Genel işlem akışı**

1. İstemci REST API üzerinden isteği gönderir.
2. API katmanında kimlik doğrulaması yapılır.
3. İlgili ders veya kategori bağlamında yetkilendirme kontrolü gerçekleştirilir.
4. Servis katmanında istek alanları ve iş kuralları doğrulanır.
5. Veri erişim katmanı gerekli veritabanı işlemlerini gerçekleştirir.
6. İlgili sistem olayları tetiklenir ve gerekli önbellekler geçersiz kılınır.
7. İstemciye uygun HTTP yanıtı döndürülür.

**Ders yönetimi**

| İşlem | Hedef servis operasyonu | Temel kontroller |
|---|---|---|
| Ders oluşturma | `createCourse()` | Kategori geçerliliği, kısa ad ve kimlik numarası benzersizliği, tarih doğrulama |
| Ders güncelleme | `updateCourse()` | Ders varlığı, yetkilendirme, alan doğrulama ve benzersizlik kontrolleri |
| Ders silme | `deleteCourse()` | Silme yetkisi, SITEID kısıtı, yedekleme/geri yükleme çakışmaları, ilişkili veri temizliği |

**Kullanıcı kayıt yönetimi**

| İşlem | Hedef servis operasyonu | Temel kontroller |
|---|---|---|
| Kullanıcı kaydetme | `enrolUser()` | Kullanıcı ve kayıt yöntemi varlığı, yetkilendirme, mükerrer kayıt kontrolü |
| Kayıt güncelleme | `updateUserEnrolment()` | Kayıt varlığı, durum ve zaman alanları, değişiklik kontrolü |
| Kayıt silme | `unenrolUser()` | Kayıt varlığı, yetkilendirme, diğer kayıt yöntemlerinin korunması |

Belirtilen servis operasyonları modernizasyon kapsamında önerilen soyutlamalardır; Moodle 5.3.1 kaynak kodundaki PHP fonksiyonlarıyla birebir aynı bileşenler değildir.

**İşlem tutarlılığı**

- Birden fazla ilişkili veri değişikliği içeren işlemlerde uygun veritabanı transaction mekanizmaları kullanılacaktır.
- Hata durumunda tutarsız veya kısmen tamamlanmış veri değişikliklerinin oluşması önlenecektir.
- Ders silme sırasında ilişkili kayıtlar ve içerikler mevcut Moodle iş kurallarıyla uyumlu şekilde temizlenecektir.
- Asenkron ders silme kullanıldığında işlem durumu izlenecek; işlem tamamlanmadan dersin tamamen silindiği bildirilmeyecektir.
- Kullanıcının bir kayıt yöntemi üzerinden kaydı kaldırıldığında diğer kayıt yöntemlerinden gelen kayıtları korunacaktır.
- Kayıt bilgileri değişmemişse gereksiz veritabanı güncellemelerinden kaçınılacaktır.

**Hata yönetimi**

API katmanında doğrulama, yetkilendirme ve veri bütünlüğü hataları standart bir yanıt yapısıyla sunulacaktır.

Önerilen hata yanıtı:

```json
{
  "error": {
    "code": "COURSE_SHORTNAME_CONFLICT",
    "message": "Ders kısa adı zaten kullanılıyor.",
    "field": "shortName"
  }
}
```

Hata mesajlarında hassas sistem bilgileri veya veritabanı ayrıntıları istemciye açıklanmayacaktır. Sunucu tarafında ise hata türü ve ilgili işlem izlenebilir biçimde kaydedilecektir.

**Gereksinim ilişkisi:** CRS-FR-01–CRS-FR-11, CRS-NFR-01–CRS-NFR-08.

#### 3.2.6 Gereksinim eşlemesi

Hedef CRS tasarımında önerilen veri modeli, REST API uç noktaları, yetkilendirme kontrolleri ve servis operasyonları WBS-1 kapsamında belirlenen gereksinimlerle ilişkilendirilmiştir. Bu eşleme, analiz ve tasarım aşamaları arasında izlenebilirliği sağlamayı amaçlamaktadır.

**Fonksiyonel gereksinim eşlemesi**

| Gereksinim ID | Hedef tasarım karşılığı | İlgili tasarım bölümü |
|---|---|---|
| CRS-FR-01 | Kategori doğrulamasıyla ders oluşturma API'si | 3.2.2, 3.2.3, 3.2.5 |
| CRS-FR-02 | Kısa ad ve kimlik numarası benzersizlik kontrolü | 3.2.2, 3.2.5 |
| CRS-FR-03 | Ders başlangıç ve bitiş tarihi doğrulaması | 3.2.3, 3.2.5 |
| CRS-FR-04 | Ders bilgilerini güncelleme API'si | 3.2.3, 3.2.5 |
| CRS-FR-05 | İlişkili verileri temizleyen ders silme operasyonu | 3.2.3, 3.2.5 |
| CRS-FR-06 | Kayıt yöntemi üzerinden kullanıcıyı derse kaydetme API'si | 3.2.2, 3.2.3, 3.2.5 |
| CRS-FR-07 | Kayıt durumu ve zaman bilgilerini güncelleme API'si | 3.2.3, 3.2.5 |
| CRS-FR-08 | Belirli kayıt yöntemi üzerinden kaydı kaldırma API'si | 3.2.3, 3.2.5 |
| CRS-FR-09 | Ders bağlamında rol atama ve yetki kontrolleri | 3.2.4, 3.2.5 |
| CRS-FR-10 | Ders ve kayıt değişikliklerinde sistem olaylarının tetiklenmesi | 3.2.3, 3.2.5 |
| CRS-FR-11 | Asenkron ders silme ve işlem durumu yönetimi | 3.2.3, 3.2.5 |

**Fonksiyonel olmayan gereksinim eşlemesi**

| Gereksinim ID | Hedef tasarım karşılığı | İlgili tasarım bölümü |
|---|---|---|
| CRS-NFR-01 | Mükerrer kullanıcı kaydının engellenmesi ve benzersizlik kontrolü | 3.2.2, 3.2.5 |
| CRS-NFR-02 | Ders silme sırasında ilişkili verilerin tutarlı temizlenmesi | 3.2.2, 3.2.5 |
| CRS-NFR-03 | Yedekleme/geri yükleme işlemleriyle çakışmanın engellenmesi | 3.2.5 |
| CRS-NFR-04 | Veri değişikliklerinden sonra ilgili önbelleklerin geçersiz kılınması | 3.2.5 |
| CRS-NFR-05 | Değişmeyen kayıt bilgilerinde gereksiz güncellemelerin önlenmesi | 3.2.5 |
| CRS-NFR-06 | Yapılandırmaya bağlı asenkron ders silme desteği | 3.2.3, 3.2.5 |
| CRS-NFR-07 | Ders ve kayıt değişikliklerinin sistem olaylarıyla izlenmesi | 3.2.3, 3.2.5 |
| CRS-NFR-08 | Eklenti genişletme mekanizmalarıyla uyumluluğun korunması | 3.2.1, 3.2.5 |

**İzlenebilirlik yaklaşımı**

- Her CRS fonksiyonel gereksinimi en az bir hedef tasarım bileşeniyle eşleştirilmiştir.
- Fonksiyonel olmayan gereksinimler veri bütünlüğü, güvenilirlik, performans, verimlilik, ölçeklenebilirlik, izlenebilirlik ve genişletilebilirlik açısından değerlendirilmiştir.
- WBS-1'de tanımlanan CRS-BR-01–CRS-BR-13 iş kuralları hedef tasarımda korunacaktır.
- Bu eşleme tasarım kapsamını göstermektedir; hedef sistemin uygulanmış veya bütün gereksinimlerin test edilmiş olduğu anlamına gelmemektedir.
- Ayrıntılı gereksinim izlenebilirliği proje kapsamındaki `docs/izlenebilirlik.md` dosyasıyla tutarlı tutulacaktır.
#### 3.2.7 Test bulguları ve tasarım kararları

Moodle 5.3.1 üzerinde ders yönetimi ve kullanıcı kayıt işlemlerinin mevcut davranışını değerlendirmek amacıyla manuel arayüz testleri gerçekleştirilmiştir. İlgili işlemler PostgreSQL veritabanı üzerinden de kontrol edilmiştir.

**Gerçekleştirilen testler**

| Test ID | Test senaryosu | Gerçekleşen sonuç | Durum |
|---|---|---|---|
| CRS-TEST-01 | Yeni ders oluşturma | Yeni ders oluşturuldu ve Moodle arayüzünde görüntülendi. | PASS |
| CRS-TEST-02 | Ders bilgilerini güncelleme | Dersin tam adı başarıyla güncellendi. | PASS |
| CRS-TEST-03 | Mükerrer ders kısa adı kontrolü | Aynı kısa adla yeni ders oluşturma girişimi hata mesajıyla engellendi. | PASS |
| CRS-TEST-04 | Ders silme | Ders silindi; veritabanında ilgili ders kaydının bulunmadığı doğrulandı. | PASS |
| CRS-TEST-05 | Kullanıcıyı derse kaydetme | Kullanıcı Student rolüyle kaydedildi; veritabanında aktif kayıt doğrulandı. | PASS |
| CRS-TEST-06 | Kullanıcı kayıt durumunu güncelleme | Kullanıcı kaydı Suspended durumuna getirildi; veritabanında `status = 1` doğrulandı. | PASS |
| CRS-TEST-07 | Kullanıcıyı dersten çıkarma | Kullanıcı katılımcı listesinden kaldırıldı; ilgili kayıt veritabanında bulunamadı. | PASS |

**Veritabanı doğrulamaları**

Testlerde Moodle'ın PostgreSQL veritabanındaki `m_course` ve `m_user_enrolments` tabloları kullanılmıştır.

- CRS-TEST-04: Silinen dersin `id = 6` kaydı için sorgu sonucu `0 rows` olarak gözlemlenmiştir.
- CRS-TEST-05: `ogrenci5` kullanıcısının `courseid = 5` için kayıt durumunun `status = 0` olduğu doğrulanmıştır.
- CRS-TEST-06: Aynı kaydın durumunun `status = 1` olarak güncellendiği doğrulanmıştır.
- CRS-TEST-07: Kullanıcının ilgili ders kaydının kaldırıldığı ve sorgunun `0 rows` döndürdüğü doğrulanmıştır.

**Ek bulgu: Ders kısa adında görünmeyen karakter**

Test sırasında iki dersin kısa adının arayüzde aynı göründüğü gözlemlenmiştir. Veritabanında yapılan karakter düzeyindeki inceleme sonucunda ilk dersin kısa adının başında TAB karakteri (`0x09`) bulunduğu belirlenmiştir.

| Ders ID | Kısa adın gerçek içeriği | Uzunluk |
|---|---|---|
| 5 | TAB + `CRS_TEST_01` | 12 |
| 6 | `CRS_TEST_01` | 11 |

Bu nedenle iki kısa ad veritabanında farklı değerler olarak değerlendirilmiştir. Başında TAB bulunmayan `CRS_TEST_01` kısa adıyla yeniden ders oluşturulmaya çalışıldığında Moodle, kısa adın başka bir ders tarafından kullanıldığını belirten hata mesajı göstermiştir.

Bu bulgu, mevcut kısa ad benzersizlik kontrolünün çalıştığını; ancak görünmeyen karakterlerin kullanıcı açısından yanıltıcı ders adlarına neden olabileceğini göstermektedir.

**Hedef tasarım kararları**

1. Ders kısa adları kaydedilmeden önce görünmeyen karakterler açısından doğrulanacaktır.
2. Kısa ad normalizasyonu uygulanmadan önce mevcut kayıtlarla geriye dönük uyumluluk değerlendirilecektir.
3. Mükerrer kısa ad kontrolü servis katmanında korunacaktır.
4. Veri bütünlüğü açısından ek veritabanı kısıtları, mevcut Moodle şemasıyla uyumluluk değerlendirmesinden sonra kararlaştırılacaktır.
5. Ders ve kayıt işlemlerinin doğrulanması için arayüz testleri ile veritabanı kontrolleri birlikte kullanılacaktır.

**Değerlendirme sınırları**

Gerçekleştirilen testler seçilen temel işlevsel senaryolarla sınırlıdır. Eşzamanlı işlem davranışı, yüksek kullanıcı yükü, performans, farklı kayıt eklentileri ve tüm yetkilendirme senaryoları test edilmemiştir. Bu nedenle sonuçlar, CRS modülünün bütün gereksinimlerinin doğrulandığı anlamına gelmemektedir.

**İlgili gereksinimler:** CRS-FR-01, CRS-FR-02, CRS-FR-04–CRS-FR-08, CRS-NFR-01, CRS-NFR-02.
### 3.3 Danışmanlık (İrem)

Moodle'da ADV'nin karşılığı yoktur (WBS-1 §4.3.1); aşağıdaki veri ve API tasarımı sıfırdan tanımlanmıştır ve WBS-1 §4.3.9'daki ADV-FR gereksinimlerini karşılamak üzere kurulmuştur.

#### 3.3.1 Veri tasarımı

| Varlık | Alanlar | İlişki ve kısıt |
| --- | --- | --- |
| Ön şart kuralı | `id, courseId, requiredCourseId, kind (hard\|soft), note, version` | Ders kendi kendinin ön şartı olamaz (`courseId ≠ requiredCourseId`). `(courseId, requiredCourseId)` benzersiz. `kind=hard` kayıt talebini engeller, `kind=soft` yalnızca uyarı üretir (ADV-FR-01, 03). |
| Danışman ataması | `advisorUserId, studentUserId, assignedAt, active` | Bir öğrencinin aktif danışmanı tektir; geçmiş atamalar `active=false` ile saklanır, silinmez. |
| Kayıt talebi | `id, courseId, studentUserId, status (pending\|approved\|rejected\|autoApproved), requiresAdvisorApproval, decidedBy, decidedAt, reason, createdAt, version` | Öğrenci ve ders çifti için en çok bir `pending` talep olabilir. `requiresAdvisorApproval=false` ve tüm zorunlu ön şartlar sağlanıyorsa talep oluşturulduğu anda `autoApproved` olur. |

Zorunlu ön şart sağlanmıyorsa kayıt talebi oluşturulmaz, `422` ile reddedilir (ADV-FR-02). Yumuşak kısıt sağlanmıyorsa talep oluşur ama yanıt gövdesinde uyarı listesi döner (ADV-FR-03). Danışman onayı gerekiyorsa talep `pending` durumunda beklemeye alınır; onay/red kararı `decidedBy`, `decidedAt` ve `reason` ile kayıt altına alınır (ADV-FR-05). Onaylanan talep CRS'nin kayıt uç noktasını (WBS-2 §3.2, Sinem) tetikler; ADV kendi `user_enrolments` yazmaz, yalnızca kararı üretir. Kontenjan kontrolü CRS tarafında yapılır; ADV kontenjan dolu yanıtını (`409`) öğrenciye alternatif ders önerisiyle birlikte iletir (ADV-FR-06, 07).

#### 3.3.2 REST API

Yollar `/api/v1` önekini ve §3.4.2'deki hata biçimini kullanır (`401/403/404/409/422`).

| Metot | Yol | İstek | Yanıt | Hangi rol çağırabilir |
| --- | --- | --- | --- | --- |
| POST | `/courses/{courseId}/prerequisites` | `requiredCourseId, kind, note` | `201` ön şart kuralı | Dersi yönetebilen öğretmen veya yönetici |
| GET | `/courses/{courseId}/prerequisites` | — | `200` kural listesi | Dersi görebilen herkes |
| POST | `/students/{studentId}/advisor` | `advisorUserId` | `200` güncel atama | Yönetici |
| GET | `/students/{studentId}/eligible-courses` | — | `200` `{ eligible: [...], blockedByHardPrerequisite: [...], softWarnings: [...] }` | Öğrencinin kendisi veya danışmanı |
| POST | `/courses/{courseId}/enrollment-requests` | — (öğrenci kimliği oturumdan alınır) | `201` talep (`pending`/`autoApproved`) veya `422` (sağlanmayan zorunlu ön şart) | Öğrenci |
| GET | `/enrollment-requests?status=pending` | Sorgu: `advisorId` | `200` danışmanın bekleyen talepleri | İlgili danışman veya yönetici |
| POST | `/enrollment-requests/{id}/decision` | `decision (approve\|reject), reason, version` | `200` güncellenmiş talep; onaylandıysa CRS kaydını tetikler | Talebin atandığı danışman veya yönetici |

#### 3.3.3 Gereksinim eşlemesi

| Uç | Gereksinim |
| --- | --- |
| `POST`/`GET /courses/{id}/prerequisites` | ADV-FR-01 |
| `POST /enrollment-requests` | ADV-FR-02, 03, 06 |
| `GET /eligible-courses` | ADV-FR-07 |
| `POST /enrollment-requests/{id}/decision` | ADV-FR-04, 05 |
| `POST /students/{id}/advisor` | ADV-FR-04 (danışman ataması önkoşulu) |

#### 3.3.4 Açık noktalar

- CRS'nin kayıt API'si (WBS-2 §3.2) henüz yazılmadı; onay kararının CRS'yi nasıl tetikleyeceği (senkron çağrı mı, olay mı) Sinem'in tasarımıyla birlikte kesinleşecek.
- Kontenjan bilgisinin ADV'ye CRS'den nasıl geleceği (sorgu mu, olay mı) aynı şekilde açık.
- Danışmana "yeni talep var" bildirimi ilk sürüm kapsamı dışıdır (bkz. WBS-1 §4.3.8).

### 3.4 Ödev ve teslim (Aslı Ceren)

#### 3.4.1 Veri tasarımı

| Varlık | Alanlar | İlişki ve kısıt |
| --- | --- | --- |
| Ödev | `id, courseId, sectionId, title, instructions, opensAt, dueAt, cutoffAt, requiresConfirmation, enabledSubmissionTypes, version` | Ders 1:N ödev. Bölüm aynı derse ait olmalıdır. Kimlikler sabit, tarihler isteğe bağlı UTC. |
| Teslim | `id, assignmentId, learnerId, attempt, status, text, submittedAt, updatedAt, version` | Ödev 1:N teslim. `(assignmentId, learnerId, attempt)` benzersiz. İlk deneme 0’dır. `draft` ve `submitted` kalıcı durumlardır. Liste, teslim kaydı yoksa `notSubmitted` gösterir. |
| Teslim kuralı | `assignmentId, learnerId, extendedUntil, locked` | Kullanıcı ve ödev çifti benzersiz. Varsayılan davranış ödev ayarından gelir. |

Açılış, son teslim ve kesim tarihleri verildiklerinde kronolojik sıradadır. Son teslim tarihi gecikme göstergesini etkiler, kesim tarihi yeni kayıt veya gönderme işlemini sınırlar. Yetki, etkin teslim penceresi, durum ve sürüm tek yazma işlemi içinde yeniden kontrol edilir. Sürüm uyuşmazlığı eşzamanlı düzenlemelerin birbirini ezmesini önler. Öğrenci kimliği oturumdan belirlenir. Gönderme onayı kapalıyken kaydetme doğrudan gönderir. Açıkken kayıt taslak oluşturur, ikinci işlem taslağı gönderir. İlk sürüm önerisi bireysel çevrimiçi metinle sınırlıdır. Dosya desteği eklenirse §3.6 dosya alanında `itemId` teslimi gösterir. Sahibi ve ilgili teslimi değerlendirme izni olan görevli okuyabilir. Sahip yalnızca açık ve düzenlenebilir durumdaki teslimine dosya yazabilir.

#### 3.4.2 REST API

Yollar `/api/v1` önekini kullanır. JSON hata gövdesi §3.6.4 ile uyumludur. `401` oturumsuz, `403` kapsamda işlem yetkisi yok, `404` görünmeyen veya bulunamayan nesne, `409` durum ya da sürüm çakışması, `422` alan doğrulama hatasıdır. Başka bir öğrenciye ait görünmeyen teslim için `404` döner.

| Metot | Yol | İstek | Yanıt | Hangi rol çağırabilir |
| --- | --- | --- | --- | --- |
| POST | `/courses/{courseId}/assignments` | `sectionId, title, instructions, opensAt, dueAt, cutoffAt, requiresConfirmation, enabledSubmissionTypes` | `201` ödev ve `Location` başlığı | Derste etkinlik oluşturabilen öğretmen veya yönetici |
| GET | `/assignments/{id}` | — | `200` ödev ve çağıranın izinleri | Etkinliği görebilen ders üyesi |
| GET | `/assignments/{id}/my-submission` | — | `200` `{ status, submission }`. Kayıt yoksa `notSubmitted` ve `null`, aksi hâlde teslim, metin ve sürüm | Kendi teslimini görebilen öğrenci |
| PUT | `/assignments/{id}/my-submission` | `text, version` (ilk kayıtta sürüm 0) | `201` yeni veya `200` güncellenmiş teslim | Uygun kayıt ve teslim yetkisi olan öğrenci |
| POST | `/assignments/{id}/my-submission/submit` | `version` | `200` gönderilen teslim | Taslak sahibi |
| GET | `/assignments/{id}/submissions` | `status, cursor, limit` | `200` izinli öğrenci ve durum sayfası | Ödev değerlendiricisi |
| GET | `/assignments/{id}/submissions/{submissionId}` | — | `200` yetkili teslim görünümü | Sahip veya ilgili öğrenciyi görebilen değerlendirici |

Oluşturma ASG-FR-01’i, teslim yazma ASG-FR-02 ve ASG-BR-01/02’yi, liste ASG-FR-03’ü, teslim okuma ASG-NFR-01’i karşılar.

### 3.5 Değerlendirme (Aslı Ceren)

#### 3.5.1 Veri tasarımı

| Varlık | Alanlar | İlişki ve kısıt |
| --- | --- | --- |
| Ödev değerlendirmesi | `assignmentId, learnerId, attempt, graderId, score, feedback, gradedAt, version` | Ödev ve öğrenci için deneme benzersiz. Değerlendirici oturumdan alınır. |
| Not kategorisi | `id, courseId, parentId, name, aggregation` | Ebeveyn aynı derstedir ve kategori ağacı döngüsüzdür. |
| Not kalemi | `id, courseId, categoryId, assignmentId, type, name, minScore, maxScore, hidden, hiddenUntil, locked, lockAt` | Alt sınır üst sınırı aşamaz. Bir ödev için tek ana kalem. İlişkiler aynı ders kapsamındadır. |
| Öğrenci notu | `itemId, learnerId, rawScore, finalScore, hidden, hiddenUntil, locked, lockAt, overridden, updatedAt, version` | Kalem ve öğrenci benzersiz. Boş puan ile sıfır ayrıdır. |

Sayısal puanlar sabit hassasiyetli ondalık değerlerdir. Puan kaydında değerlendirici yetkisi, öğrenci kapsamı, deneme ve sınırlar doğrulanır. Alt ve üst sınır dahildir. Sınır dışı değer `422` döndürür ve önceki puanı değiştirmez. Puan ilgili ödev, öğrenci ve deneme üçlüsüyle saklanır. Öğretmen yazma isteği kilitli veya elle değiştirilmiş bir notu düzenlemeye çalışırsa `409` ile reddedilir. Korumasız notta kaynak değerlendirme ve defter kaydı tutarlı biçimde güncellenir. Aktarım sırasında koruma oluşmuşsa mevcut defter değeri saklanır ve yanıt aktarım durumunu bildirir. Kalem ve not düzeyindeki gizleme ile kilit durumları sunucu saatine göre hesaplanır. `hidden=true` süresiz gizler. `hiddenUntil` süreli gizlemeyi, `lockAt` otomatik kilitlemeyi belirtir. Gizli puan öğrenci yanıtında yer almaz. Dersin görünürlük politikası satıra izin veriyorsa kalem adı ve boş puan gösterilir. Politika kalemi gizliyorsa satır da yanıttan çıkarılır. Toplam gösterilecekse dahil edilen kalemler, boş değer davranışı ve yuvarlama politikası açıkça tanımlanır.

#### 3.5.2 REST API

Yollar `/api/v1` önekini ve §3.4.2 hata biçimini kullanır. Ders ve grup kapsamı her istekte sunucuda denetlenir. Kişisel not raporunun öğrenci kimliği oturumdan alınır. İstekle başka bir öğrenci kimliği seçilemez.

| Metot | Yol | İstek | Yanıt | Hangi rol çağırabilir |
| --- | --- | --- | --- | --- |
| GET | `/assignments/{id}/learners/{learnerId}/attempts/{attempt}/grade` | — | `200` `{ assessment, minScore, maxScore, version }`. Puan kaydı yoksa `assessment: null`, `version: 0` | Öğrenciyi değerlendirme yetkisi olan öğretmen, asistan veya yönetici |
| PUT | `/assignments/{id}/learners/{learnerId}/attempts/{attempt}/grade` | `score, feedback, version` (ilk kayıtta sürüm 0) | `200` değerlendirme ve defter aktarım durumu | Öğrenciyi değerlendirme yetkisi olan öğretmen, asistan veya yönetici |
| GET | `/courses/{courseId}/my-grades` | — | `200` oturum sahibinin görünür notları, kalem sınırları ve varsa görünür toplam | Kendi raporuna erişebilen öğrenci |
| GET | `/courses/{courseId}/gradebook` | `groupId, cursor, limit` | `200` izinli öğrenci ve kalem sayfası | Dersin değerlendirici raporu yetkisine sahip öğretmen, asistan veya yönetici |

Puan yazma GRD-FR-01 ve GRD-BR-01/02’yi, kişisel rapor GRD-FR-02 ve GRD-NFR-01’i, ders defteri GRD-FR-03’ü karşılar.

### 3.6 Dosya yönetimi (Seray)

Tasarım dilden bağımsızdır (REST + JSON). Gereksinim kimlikleri WBS-1 §4.6.9'dadır. Eski sistemden **korunan** davranışlar K, **düzeltilenler** D, **yeni eklenenler** Y ile işaretlenmiştir.

#### 3.6.1 Tasarım ilkeleri

| İlke | Eski sistemden | Gerekçe |
| --- | --- | --- |
| İçerik adresli depolama: içerik bir kez saklanır, kayıtlar özetle ona bağlanır | K (FILE-BR-01, 03) | Aynı dosyanın çok kopyası disk israfıdır; Moodle'ın en değerli kararlarından biri. |
| Yükleme önce **taslak alanına** gider, kaydedilince kalıcı alana geçer | K (FILE-BR-05) | Yarım kalan form dosya sahipsizliği yaratmaz. |
| Dosya sunumu sahip bileşenin **yetki politikasından** geçer; yetkisiz isteğe "bulunamadı" döner | K (FILE-BR-11, 12) | Dosyanın varlığı bile sızdırılmaz. |
| Özet algoritması **SHA-256** | D (FILE-NFR-02) | Moodle SHA-1 kullanır; yeni sistemde çakışma direnci yüksek bir algoritma seçilir. Dış görünümü değiştirmez. |
| Taslak süresi **son etkinliğe** göre dolar | D (FILE-BUG-02) | Eski sistem oluşturulma tarihine bakar; düzenlenen taslak silinebilir. |
| Kota **her dosya kaydını** sayar | D (FILE-BUG-01) | Eski sistem aynı ad ve içerikli kayıtları tekilleştirerek eksik sayar. |
| Silinen dosya **bekleme süresi** sonunda kalıcı silinir; içerik silinmeden önce kullanım yeniden denetlenir | D (FILE-BUG-03, 04) | Çöp kutusu yaşa göre çalışır, yarış durumu kilitle kapatılır. |

#### 3.6.2 Veri tasarımı

Moodle'ın tek `files` tablosu, "içerik" ve "kayıt" olmak üzere ikiye ayrılır; böylece içerik tekilliği veritabanı kısıtıyla garanti edilir.

| Tablo | Sütunlar | Anahtar / kısıt |
| --- | --- | --- |
| `file_blob` (içerik) | `content_hash` (SHA-256), `size_bytes`, `storage_key`, `created_at`, `orphaned_at` (boşa düştüğü an, boşsa null) | PK `content_hash` |
| `file_entry` (dosya kaydı) | `id`, `content_hash`, `context_id`, `component`, `area`, `item_id`, `path`, `name`, `owner_user_id`, `size_bytes`, `mime_type`, `status`, `source`, `author`, `license`, `sort_order`, `reference_id`, `created_at`, `updated_at`, `deleted_at` | PK `id`; FK `content_hash` → `file_blob`; **benzersiz** (`context_id`, `component`, `area`, `item_id`, `path`, `name`) (FILE-BR-02); dizin (`component`, `area`, `context_id`, `item_id`) |
| `file_draft` (taslak) | `draft_id`, `owner_user_id`, `created_at`, `last_activity_at` | PK `draft_id`; taslak dosyalar `file_entry` içinde `component='user'`, `area='draft'`, `item_id=draft_id` olarak durur |
| `file_setting` | `max_file_bytes`, `user_quota_bytes`, `draft_ttl_hours`, `trash_retention_hours` | Tek satır; yönetici değiştirir |

- Klasörler `file_entry` içinde `name='.'` kaydıdır (Moodle ile aynı).
- `deleted_at` dolu kayıtlar "çöpte"dir; `orphaned_at`, hiçbir kaydın kullanmadığı içeriği işaretler ve kalıcı silme işi bu işaretten **bekleme süresi** sonra çalışır.
- **Kota:** `SELECT SUM(size_bytes) FROM file_entry WHERE owner_user_id = ? AND component='user' AND area <> 'draft' AND deleted_at IS NULL` (her kayıt sayılır).
- **Fiziksel yol:** `storage_key = ab/cd/<özet>`; depo yerel disk veya nesne deposu olabilir (WBS-2 §2).
- Dış depo referansları (Moodle `files_reference`) ilk sürüm kapsamı dışındadır; `reference_id` sütunu yer tutucudur.

#### 3.6.3 Yetki politikası

Her sahip bileşen (ödev, kaynak, profil vb.) iki işlev kaydeder; dosya servisi karar için yalnızca bunları çağırır (Moodle'daki `*_pluginfile` işlevlerinin karşılığı):

- `canRead(kullanıcı, dosya) → bool`
- `canWrite(kullanıcı, alan) → bool`

Hazır politikalar: **taslak** (yalnızca sahibi), **özel dosyalar** (yalnızca sahibi, `manageownfiles`), **ders dosyaları** (düzenleyen öğretmen ve yönetici, `managefiles`). Ödev teslim dosyaları politikası §3.4'te (Aslı Ceren) tanımlanır.

#### 3.6.4 API

Ön ek `/api/v1`. Tüm uçlar oturum gerektirir; misafir yalnızca bileşen politikası izin verirse okuyabilir.

| Metot | Yol | İstek | Yanıt | Hangi rol çağırabilir |
| --- | --- | --- | --- | --- |
| POST | `/file-drafts` | Gövde yok | `201` `{ "draftId", "expiresAt" }` | Giriş yapmış her kullanıcı (misafir hariç) |
| POST | `/file-drafts/{draftId}/files` | `multipart/form-data`: `file`, isteğe bağlı `path`, `overwrite` (varsayılan `false`) | `201` `{ "fileId", "name", "size", "mimeType", "contentHash" }`. Ad çakışırsa ad benzersizleştirilir ve dönen `name` gerçek addır | Taslağın sahibi |
| GET | `/file-drafts/{draftId}/files` | Sorgu: `path` | `200` `{ "files": [ {fileId, name, size, mimeType} ] }` | Taslağın sahibi |
| DELETE | `/file-drafts/{draftId}/files/{fileId}` | — | `204` | Taslağın sahibi |
| POST | `/file-areas/{contextId}/{component}/{area}/{itemId}/commit` | `{ "draftId", "replace": true }` | `200` `{ "files": [...] }`; taslak temizlenir | `canWrite` politikasının izin verdiği kullanıcı |
| GET | `/file-areas/{contextId}/{component}/{area}/{itemId}/files` | Sorgu: `path` | `200` `{ "files": [...] }` | `canRead` politikası |
| GET | `/files/{fileId}` | — | `200` meta veri: ad, boyut, tür, yazar, lisans, tarih | `canRead` politikası |
| GET | `/files/{fileId}/content` | Sorgu: `download=true\|false` | `200` dosya gövdesi, `Content-Disposition`, doğru `Content-Type`. Taslak dosyalar her zaman indirmeye zorlanır | `canRead` politikası; yetkisizse `404` |
| DELETE | `/files/{fileId}` | — | `204`; dosya çöpe taşınır | `canWrite` politikası |
| POST | `/files/{fileId}/restore` | — | `200` meta veri (bekleme süresi dolmadıysa), `410` süresi dolduysa | Dosya sahibi, düzenleyen öğretmen, yönetici |
| GET | `/users/me/quota` | — | `200` `{ "limitBytes", "usedBytes", "unlimited" }` | Giriş yapmış her kullanıcı |
| GET | `/admin/file-settings` | — | `200` `file_setting` | Yönetici |
| PUT | `/admin/file-settings` | `file_setting` alanları | `200` | Yönetici |
| POST | `/admin/file-maintenance/run` | — | `202` `{ "expiredDrafts", "purgedBlobs" }` (temizlik görevini tetikler; test ve bakım için) | Yönetici |

**Hata gövdesi (hepsi için):** `{ "error": { "code": "...", "message": "..." } }`. Hiçbir durumda yığın izi dönmez (K3).

| Durum | Kod | Ne zaman |
| --- | --- | --- |
| 400 | `invalid_name`, `empty_file` | Geçersiz veya 255 karakteri aşan ad; boş dosya veya klasör (FILE-BR-06, 08) |
| 401 | `not_authenticated` | Oturum yok |
| 404 | `not_found` | Dosya yok **veya yetki yok** (FILE-BR-11) |
| 413 | `file_too_large` | Dosya başına sınır aşıldı (FILE-BR-08) |
| 415 | `type_not_allowed` | Alanın kabul etmediği tür (FILE-BR-09) |
| 422 | `quota_exceeded`, `area_limit_exceeded`, `virus_detected` | Kota, alan sınırı veya antivirüs (FILE-BR-08, 09, 10) |
| 410 | `restore_expired` | Geri alma süresi dolmuş |

#### 3.6.5 Gereksinim eşlemesi

| Uç | Gereksinim |
| --- | --- |
| `POST /file-drafts`, `POST .../files` | FILE-FR-01, 07, 08, 09, 10, 11 |
| `POST .../commit` | FILE-FR-02 |
| `GET /files/{id}/content`, `GET /files/{id}` | FILE-FR-03, 04 |
| `DELETE /files/{id}`, `POST .../restore` | FILE-FR-05, 06; FILE-BUG-03, 04 |
| `GET /users/me/quota` | FILE-FR-09; FILE-BUG-01 |
| `POST /admin/file-maintenance/run` | FILE-FR-12; FILE-BUG-02 |
| Ders dosyaları (`component=course`) | FILE-FR-13 |

#### 3.6.6 Açık noktalar

- Ödev teslim dosyalarının yetki politikası §3.4 ile birlikte kesinleştirilecek.
- `restore` ucu eski sistemde yoktur (**Y**, öncelik **C**); ilk sürümde isteğe bağlıdır.
- Yedekleme ve geri yükleme paketleyicileri kapsam dışıdır.

## 4. Yapay zekâ kullanım yaklaşımı (İrem)

Bu bölüm, projenin her adımında yapay zekânın nerede, hangi araçla ve hangi insan kontrolüyle kullanılacağını tanımlar. Amaç; YZ'den hız kazanmak, ama kabul edilen her çıktının insan incelemesinden ve testten geçmesini garanti etmektir (bkz. WBS-1 §2.6). Bölüm 2.6'da belirtilen kayıt zorunluluğu burada somut bir şablona bağlanır; §8.5'teki (Aslı Ceren) kayıt örneği bu şablonun ilk uygulamasıdır.

### 4.1 Hangi adımda, hangi model

| Adım | YZ'nin işi | Önerilen araç/model | Girdi (bağlam) | Çıktı |
| --- | --- | --- | --- | --- |
| Kodun anlaşılması (WBS-1 §4) | Moodle kaynak kodunda ilgili iş akışını, veri modelini ve yetki kontrolünü bulup özetleme | Claude Code / Codex (kod arama + okuma) | Sabit revizyon `cfe123f2`, modül adı, ilgili dosya yolları | İş akışı özeti, dosya/satır referansları (insan doğrulamadan gereksinime dönüşmez) |
| Gereksinim taslağı (WBS-1 §4.9, §5) | Kod özetinden ve anket sonuçlarından K/D/Y gereksinim taslağı üretme | Aynı araç | Kod özeti, §3 anket sonuçları, §2.5 gereksinim şablonu | Taslak FR/BR/BUG listesi (kimlik, sınıf, kabul kriteri) |
| Hedef dilde kod üretme (WBS-3) | Onaylanmış gereksinim ve API tasarımından (WBS-2 §3) hedef dilde kod üretme | Hedef dile göre seçilecek YZ kod asistanı (Seray'ın WBS-2 §1 seçimine bağlı) | Gereksinim kimliği, ilgili API tablosu, eski kodun davranışı | Derlenen/çalışan kod + birim test taslağı |
| Test senaryosu üretme (WBS-1 §10, WBS-5) | Gereksinimden normal/sınır/hatalı girdi/yetkisiz işlem senaryoları üretme | Claude Code / Codex | Gereksinim kimliği, kabul kriteri, örnek veri (`ortam/ornek-veri.feature`) | Test adımları taslağı (insan eski sistemde çalıştırmadan "sonuç" sütununa yazılmaz) |
| Dokümantasyon (WBS-1, WBS-2, WBS-7) | Tablo, ER diyagramı ve API tablosu taslağı yazma | Claude Code / Codex | Kod özeti, gereksinim listesi, veri modeli notları | Markdown taslağı (modül sahibi düzeltip yayımlar) |

Model seçimi kişiye bağlıdır; herkes kullandığı modeli ve sürümünü §4.3'teki şablonla kaydeder. Ekipte şu ana kadar Claude Code (Claude Sonnet ailesi) ve OpenAI Codex kullanıldığı gözlemlenmiştir (bkz. §8.5); yaklaşım belirli bir modele bağlı değildir.

### 4.2 Uzman kontrol noktaları

| Aşama | Kontrol noktası | Kim onaylar | Neye bakarak |
| --- | --- | --- | --- |
| Kod özeti / iş akışı | YZ'nin okuduğu dosya ve satırların gerçekten o davranışı yaptığı doğrulanır | Modül sahibi | Kaynak kodu kendisi açıp karşılaştırarak |
| Gereksinim taslağı | Her gereksinimin tek ve test edilebilir olduğu, kimlik/sınıf/kaynak alanlarının doğru olduğu kontrol edilir | Modül sahibi | WBS-1 §2.5 şablonu ve eski sistemdeki gözlemlenen davranış |
| Üretilen kod | Davranışın korunduğu, iş kuralının bozulmadığı ve kabul kriterinin karşılandığı kontrol edilir | Modül sahibi (PR incelemesi) + CI | Kod inceleme + GitHub Actions zorunlu kontrolleri: derleme/birim test, CodeQL, E2E (WBS-1 §6, K1-K6) |
| Test senaryosu | Senaryonun eski sistemde gerçekten o sonucu verdiği doğrulanır | Modül sahibi | Eski sistemde elle çalıştırma (WBS-1 §10); "Bekliyor" durumundaki senaryolar sonuç sütununa yazılmadan kabul edilmez |
| Dokümantasyon | Üslup, referans doğruluğu ve başka bölümlerle tutarlılık kontrol edilir | Modül sahibi; son birleştirmede Fatma Zehra (WBS-1 §9) | Kaynak koda ve diğer bölümlere çapraz okuma |

Hiçbir YZ çıktısı, yukarıdaki ilgili kontrol noktasından geçmeden `main`'e eklenmez (bkz. ortak kurallar: PR zorunluluğu).

### 4.3 Kayıt şablonu

Her modül sahibi, WBS-1 §8.5'teki örnekle aynı alanları doldurur. Şablon, PR açıklamasına veya modülün "Yapay zekâ ve efor kaydı" alt başlığına eklenir:

```markdown
**Model ve sürüm:** (ör. Claude Sonnet 5, düşünme seviyesi: Orta)
**Kullanılan adım(lar):** (§4.1 tablosundaki adımlardan hangileri)
**Verilen bağlam:** (hangi dosyalar, hangi gereksinim/anket/WBS bölümü)
**Yapılan iş:** (üretilen taslak, kod veya test — kısaca)
**Yapılan düzeltmeler:** (YZ çıktısının neyi yanlış/eksik yaptığı ve insan tarafından nasıl düzeltildiği; "düzeltme yok" da geçerli bir kayıttır)
**Harcanan süre:** İnsan: _ saat · YZ ile çalışma: _ saat (ayrı kaydedilir, WBS-1 §8.5'teki gibi)
**Kanıt:** (log, ekran görüntüsü veya commit/PR bağlantısı)
```

İnsan ve YZ süreleri ayrı tutulur ve katkı yüzdesi olarak yorumlanmaz (WBS-1 §8.5'teki uygulamayla tutarlı).

### 4.4 Yapay zekâ destekli ile manuel geliştirmenin karşılaştırma tasarımı

**Amaç:** WBS-3'te aynı karmaşıklıktaki işlevlerin YZ destekli ve tamamen manuel geliştirilmesi arasındaki efor, hata ve yeniden çalışma farkını ölçmek (WBS-1 §6, K17).

**Hangi fonksiyonlar:** Her modülden birer çift, karşılaştırılabilir karmaşıklıkta ve bağımsız çalışabilecek işlev seçilir — örneğin:
- ASG: teslim kaydetme uç noktası (YZ) ↔ teslim listesi uç noktası (manuel)
- GRD: not yazma uç noktası (YZ) ↔ kişisel not raporu uç noktası (manuel)
- FILE: taslak alanına yükleme (YZ) ↔ dosya silme/çöp (manuel)
- ADV / PLAN: WBS-3/4 kapsamına girerse aynı eşleştirme uygulanır

Son seçim, §9'da ilk sürüme alınan modüller netleşince kesinleşir; aday çiftler yukarıdaki gibi modül başına birer örnekle sınırlı tutulur ki karşılaştırma büyük örneklemden değil, kontrollü eşleştirmeden gelsin.

**Ölçütler:**

| Ölçüt | Tanım | Kaynak |
| --- | --- | --- |
| Efor (adam-ay/adam-saat) | Aynı işlev için harcanan insan süresi; YZ'ye "yönlendirme + inceleme" süresi de insan eforuna dahildir, YZ'nin kendi çalışma süresi ayrı kaydedilir | §4.3 kayıt şablonu |
| Hata oranı | İnceleme ve testte bulunan hata sayısı / 1.000 satır | Kod inceleme notları + CI bulguları (WBS-1 §6, K6) |
| Yeniden çalışma (re-work) | İlk PR'dan birleşmeye kadar geçen inceleme turu sayısı ve bu turlarda değişen satır oranı | GitHub PR geçmişi |

**Adil karşılaştırma nasıl sağlanır:**
- İki işlev de aynı güncel gereksinim ve API tasarımından başlar (bayat/eski taslaktan değil).
- Aynı kabul kriteri ve aynı test seti ile doğrulanır.
- Mümkünse aynı kişi her iki işlevi de geliştirir (beceri farkını ortadan kaldırmak için); değilse geliştiricilerin deneyim seviyesi not düşülür.
- Kod inceleyen kişi hangi işlevin YZ destekli olduğunu bilmeden (mümkün olduğunca) inceler; PR açıklamasında bu bilgi inceleme bitene kadar gizlenebilir.
- Süre ölçümü aynı granülerite ile yapılır (§4.3 şablonu); "YZ'yi yönlendirme" süresi de insan eforuna sayılır, yalnızca modelin kendi çalışma süresi ayrı sütunda tutulur.
- Sonuçlar WBS-6'da raporlanır; eşik tanımlanmaz, amaç ölçüp raporlamaktır (WBS-1 §6, K17).

## 5. İzlenebilirlik yöntemi (Fatma Zehra)

Her gereksinim; eski sistemdeki kaynağına, yeni sistemdeki karşılığına ve onu doğrulayan teste bağlanır. Böylece (1) hiçbir gereksinimin unutulmadığı, (2) yazılan her kodun bir gereksinime dayandığı ve (3) eski ile yeni davranışın aynı testle karşılaştırıldığı gösterilebilir. Matris ayrı bir dosyada tutulur: [izlenebilirlik.md](izlenebilirlik.md).

### 5.1 Matrisin sütunları

| Sütun | İçerik | Örnek |
| --- | --- | --- |
| Gereksinim | Kimlik (WBS-1 §2.5) | `ASG-BR-01` |
| Sınıf | K korunacak · D düzeltilecek · Y yeni | K |
| Eski kod | Sabit revizyondaki dosya ve satırlar (`cfe123f2`) | `public/mod/assign/locallib.php:8701-8705` |
| Yeni kod | Yeni sistemdeki dosya veya fonksiyon | Geliştirmede doldurulur |
| Test | Test kimliği; aynı test iki sistemde de çalışır | `T-ASG-01` |
| Eski sonuç | Testin eski sistemdeki sonucu | Geçti / Kaldı (D sınıfında beklenen) |
| Yeni sonuç | Testin yeni sistemdeki sonucu | Geliştirmede doldurulur |
| Üretim | Kod nasıl yazıldı | YZ / Manuel / YZ + düzeltme |
| PR | Kodun eklendiği pull request | `#12` |

### 5.2 Kurallar

1. **Her gereksinimin en az bir testi vardır.** Testi olmayan gereksinim "doğrulanmamış" sayılır.
2. **Her test bir gereksinime bağlıdır.** Test adında gereksinim kimliği geçer, ör. `T-ASG-01 ASG-BR-01 taslak ayarı açıkken teslim taslak kalır`.
3. **Eski kod referansı sabit revizyona göre verilir.** Dosya yolu ve satır aralığı `cfe123f2` revizyonundaki hâline aittir; Moodle güncellense de referans değişmez.
4. **Yeni kod, gereksinim kimliğiyle işaretlenir.** Commit mesajında ve pull request açıklamasında ilgili kimlikler yazılır, ör. `ASG-FR-03: teslim kaydetme`.
5. **D sınıfı gereksinimler** için test eski sistemde **kalır** (hata orada vardır), yeni sistemde **geçer**. Eşleme eShop pilotundaki "beklenen başarısızlık" yöntemiyle yapılır.
6. **Y sınıfı gereksinimlerin** eski kod referansı yoktur; bu sütuna kaynağı yazılır (ör. kullanıcı ihtiyaçları anketi, §3).
7. **Üretim sütunu** YZ / manuel karşılaştırması (WBS-1 §6, K17) için kullanılır ve WBS-2 §4'teki YZ kayıt şablonuyla eşleşir.
8. **Herkes sadece kendi modülünün satırlarını** düzenler; matris dosyasında her modülün ayrı başlığı vardır.

### 5.3 Kontroller

| Kontrol | Ne zaman | Sonuç |
| --- | --- | --- |
| İleri izlenebilirlik: gereksinim → test | Her modül analizi bitince | Testi olmayan gereksinim kalmamalı |
| Geri izlenebilirlik: kod → gereksinim | Her pull request'te | Gereksinim kimliği olmayan değişiklik birleştirilmez (işlevsel olmayan düzenlemeler hariç) |
| Sonuç karşılaştırması | Her geliştirme döngüsü sonunda | K: iki sistemde geçer · D: eski kalır, yeni geçer · Y: yeni geçer |
| Kapsam özeti | Ara ve son raporda | Modül bazında K / D / Y sayıları ve geçme oranları |

## 6. Modernizasyon süreç taslağı (Sinem)



