# WBS-3 CRS Blazor arayüzü

## Ekranlar

| Yol | Bileşen | İşlevler |
| --- | --- | --- |
| `/courses` | `Pages/Courses.razor` | Yetkili dersleri listeleme, kategori filtresi, ders oluşturma/düzenleme ve onaylı silme; detay/katılımcı bağlantıları |
| `/courses/{CourseId}` | `Pages/CourseDetails.razor` | Ders/kategori bilgileri, görünürlük, kimlik numarası, başlangıç/bitiş ve düzenleme |
| `/courses/{CourseId}/participants` | `Pages/CourseParticipants.razor` | Kullanıcı kimliği, rol, aktif/askıda/zaman durumu, kayıt tarihleri; kayıt oluşturma, durum/rol düzenleme ve onaylı kayıttan çıkarma |

Mevcut `MainLayout` ve `Routes` korunmuştur; ana menüye yalnızca **Dersler**
bağlantısı eklenmiştir. Etkileşimli sunucu render modu CRS sayfalarına uygulanır;
diğer modüllerin sayfaları değiştirilmemiştir.

## Veri ve oturum akışı

Web katmanı yalnızca mevcut `/api/v1/categories`, `/api/v1/courses` ve
`/api/v1/courses/{id}/enrolments` HTTP sözleşmelerini kullanır. Domain,
Application veya Infrastructure proje referansı eklenmemiştir. API endpoint'leri
ve AUTH kontrolleri değiştirilmemiştir.

`CrsApiClient` aynı sözleşmeleri HTTP üzerinden okur/yazar. Sunucu Blazor
bileşenlerinden localhost'a ayrı bir kullanıcı gibi istek atmak yerine,
`CrsBrowserHttpHandler` tarayıcıdaki `crs-api.js` modülüne çağrı yapar. Modül:

- Aynı kaynaktaki CRS yollarıyla ve GET/POST/PATCH/DELETE ile sınırlıdır.
- `credentials: same-origin` kullanır; kullanıcının gerçek tarayıcı oturumu API'ye gider.
- Kimlik, Authorization başlığı veya sahte çerez üretmez; sunucudan çerez kopyalamaz.
- Yönlendirmeyi hata sayar; AUTH yanıtını sahte başarıyla değiştirmez.
- HTTP durum kodu ve gövdeyi C# istemcisine olduğu gibi iletir.

İlk SSR yanıtı yükleme metni ve kapalı işlem düğmeleri içerir. API yüklemesi
Blazor etkileşimli render tamamlandıktan sonra başlar. JavaScript ve SignalR
bağlantısı gerekir. API hostuna etkileşimli sunucu desteği, statik varlık
eşlemesi ve Blazor başlangıç betiği eklenmiştir.

Hostta `.razor` dosyası bulunmadığı için .NET 10'un
`RequiresAspNetWebAssets=true` ayarı kullanılır; bileşenler RCL'de kalır.
Statik varlık manifesti API assembly'sine göre seçilir; özel test hostu
ApplicationName değiştirse de doğru manifest kullanılır. Yerelde Production
ortamında çalıştırıldığında da RCL varlıklarının bulunması için
`UseStaticWebAssets` çağrılır. Bu düzenin dayanağı:
[Microsoft Blazor proje yapısı](https://learn.microsoft.com/aspnet/core/blazor/project-structure?view=aspnetcore-10.0).

## Ortak dosyalar

`src/Moodle.Web/Crs/` altında:

- `CrsModels.cs`: HTTP veri modelleri, form doğrulaması ve tarih/durum gösterimi.
- `CrsApiClient.cs`: HTTP sözleşmeleri ve kullanıcıya uygun hata mesajları.
- `CrsBrowserHttpHandler.cs`: tarayıcı HTTP köprüsü ve DI kaydı.
- `CrsPageBase.cs`: yükleme, meşgul/hata durumu, yeniden deneme ve yaşam süresi iptali.
- `CourseEditor.razor`, `EnrolmentEditor.razor`: doğrulamalı, ortak formlar.
- `CrsNotice.razor`, `CrsConfirm.razor`: erişilebilir bildirim ve silme onayı.

`wwwroot/crs-api.js` tarayıcı taşımasını sağlar. Mevcut `app.css` renkleri ve
yerleşimi kullanılarak küçük ekranlara uyumlu form/tablo stilleri eklenmiştir.

## Form davranışları

- Başlangıç/bitiş tarihleri açıkça **UTC** olarak gösterilir ve düzenlenir;
  sunucunun yerel saat dilimine bağlı gizli dönüşüm yapılmaz.
- Boş kimlik numarası ve temizlenen isteğe bağlı tarihler API'ye `null` gider.
- Katılımcı durum güncellemesinde değişmeyen rol gönderilmez. Böylece yalnızca
  askıya alma işlemi gereksiz rol atama yetkisi istemez.
- Rol değişikliği mevcut API'nin genel ve hedef role özel yetkilerinden geçer.
- Kaydetme sırasında düğmeler/form pasifleştirilir; aynı işlemin tekrar gönderilmesi önlenir.
- Çakışma veya doğrulama hatasında düzenlenen bilgiler korunur.
- Ders silme ve kayıttan çıkarma açık kullanıcı onayı ister; vazgeçme yazma isteği oluşturmaz.

## Gerçek AUTH tamamlanana kadar

**Üretim dahil mevcut AUTH engeli korunur.** API'deki `503 auth_not_ready`
yanıtı atlatılmaz. Üç ekran yüklenir; API yanıtı geldiğinde kullanıcıya kimlik ve
yetkilendirme sisteminin hazır olmadığı açıklanır. Veri ve işlem formları
açılmaz, oluşturma/düzenleme/silme/kayıt işlemleri kullanılamaz. Yeniden deneme
yalnızca aynı korunan API'yi tekrar çağırır.

Gerçek `ICurrentUser`/`IAccessPolicy` bağlanana kadar gerçek kullanıcıyla ders
listeleme, filtreleme, detay/kayıt okuma ve tüm yazma işlemleri doğrulanamaz.
Kullanıcı adları ile kullanıcı/rol seçicileri için mevcut API'de katalog yoktur;
formlar kullanıcı kimliği ve rol adı kullanır. Kayıttaki rol, AUTH oturumuna
yetki eklemez. AUTH, ASG ve GRD gerçeklemeleri değiştirilmemiştir.

## Testler

Eklenen C# test dosyaları:

- `CrsBlazorTests.cs`: bUnit ile kullanıcı etkileşimleri, filtre, formlar,
  onaylar, durum/rol farkı, 503 uyarısı, hata sonrası veriyi koruma ve meşgul durum.
- `CrsWebClientTests.cs`: HTTP hata çevirisi, bozuk hata gövdeleri ve ağ hataları.
- `CrsWebTestSupport.cs`: yalnızca testlerde kullanılan HTTP yanıtları.
- `CrsBlazorHostingTests.cs`: gerçek hostta sayfalar, CSS/Blazor/JS varlıkları ve
  değişmeyen gerçek API `503 auth_not_ready` yanıtı.
- `CrsWebApiContractTests.cs`: UI HTTP modelleriyle gerçek PostgreSQL üzerinde
  ders/katılımcı akışı ve rol atama yetkisi olmadan yalnızca durum güncellemesi.

Test hostundaki denetimli AUTH test servisleri yalnızca testlerde kullanılır;
uygulamanın güvenlik kayıtlarını değiştirmez. Mevcut 130 testin dosyaları korunur.

```powershell
dotnet restore Moodle.slnx
dotnet build Moodle.slnx --no-restore
dotnet test Moodle.slnx --no-build --no-restore
node --test tests/web/crs-api.test.mjs
```

Node testleri tarayıcı taşımasının aynı kaynak/oturum sınırını, izin verilen
yolları ve ağ hatalarında sahte yanıt üretilmediğini doğrular. PostgreSQL testleri
için Docker Desktop gerekir. Bileşen testleri gerçek bir kullanıcının AUTH
entegrasyonunun veya yetkili tarayıcı akışının tamamlandığı anlamına gelmez.

## Doğrulama kaydı — 17 Kasım 2026

- Derleme: başarılı, **0 uyarı, 0 hata**.
- .NET testleri: **56 birim + 105 entegrasyon = 161 başarılı**; başarısız ve
  atlanan yok. Önceki 130 test değiştirilmeden geçmiştir.
- `node --test tests/web/crs-api.test.mjs`: **7 başarılı** taşıma testi.
- Gerçek Chrome kontrolü: üç sayfa hem 1440×1000 masaüstü hem 390×844 mobil
  boyutta açıldı; AUTH uyarısı göründü, yükleme tamamlandı, işlem düğmeleri
  kapalı ve formlar gizli kaldı; yatay taşma görülmedi. Tarayıcıdan yapılan
  12 gerçek API isteğinin tamamında HTTP 503 korundu.

Gerçek tarayıcı kontrolü `tests/web/crs-browser-smoke.mjs` ile Node'un yerleşik
WebSocket desteği üzerinden yapılır; ek otomasyon paketi gerektirmez. Çalışan
yerel API (varsayılan `http://127.0.0.1:5087`) ve izole profille localhost
9227 portunda başlatılmış başsız Chrome gerektirir. Script yalnızca localhost
adreslerine bağlanır, oturum üretmez ve kontrol sonunda kendi Chrome örneğini
kapatır. Komut: `node tests/web/crs-browser-smoke.mjs`.

Ekran görüntüleri Git tarafından dışlanan `TestResults/crs-preview/` altında
saklanır (`courses-desktop.png`, `details-desktop.png`,
`participants-desktop.png` ve mobil karşılıkları). Yetkili CRUD etkileşimleri
bUnit ve gerçek API/PostgreSQL test hostunda sınanmıştır; gerçek AUTH ile
tarayıcıdan uçtan uca CRUD henüz doğrulanamaz.
