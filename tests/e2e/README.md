# Gerçek Playwright kabulü

Önce izole asg-v2 Compose ortamını güncel uygulamayla kurun. PostgreSQL 17 ve gerçek
ClamAV sağlıklı olmalıdır. Canlı/ortak veritabanında fixture veya maintenance çalıştırmayın.

## Gerçek kimlik fixture'ı
tests/Moodle.E2eFixtures yalnız test yürütücüsüdür; uygulama endpoint'i değildir.
E2E_AUTH_CONNECTION ile izole veritabanı bağlantısını verin; bağlantıyı konsola yazmayın.
Rastgele kullanıcı adı, pozitif kullanıcı kimliği ve parola ile gerçek Identity üzerinden
SiteAdmin, öğretmen ve öğrenci kullanıcıları oluşturulur. Öğretmen/öğrenci rollerini
testler gerçek CRS kayıt API'siyle ilgili derse atar. Üretim seed/şifresi değişmez.

    dotnet build tests/Moodle.E2eFixtures -c Release -o coverage/e2e-fixture-bin
    dotnet coverage/e2e-fixture-bin/Moodle.E2eFixtures.dll coverage/e2e-admin.json
    E2E_ADMIN_FIXTURE=/absolute/path/coverage/e2e-admin.json npx playwright test

Konteynerde asg-v2_default ağı, E2E_BASE_URL=http://modern:8080 ve fixture dosyası için
salt okunur mount kullanılır. Kaynak ağacı salt okunur tutulabilir; çıktılar coverage altındadır.

## Temizleme (başarıda da hatada da)
Shell finally/trap içinde AYNI yürütücüyü AYNI fixture dosyasıyla yeniden çağırın.
Dosya varsa fixture'daki üç kullanıcı Identity ile silinir; ardından dosya kaldırılır.
Dosyayı bu çağrıdan önce silmeyin. Yeni bir kullanıcı/parola oluşturmak için önce
önceki koşunun temizliği tamamlanmalıdır. Parola ve bağlantı dosyaları Git'e eklenmez.

## Dokuz gerçek senaryo
Mevcut AUTH, dosya/ClamAV, sahiplik, ortak FILE yöneticisi ve negatif yönetici testleri korunur.
Eski bölüm yokluğu testi, gerçek yanlış ders-bölüm ilişkisi ve yetkisiz oluşturma testi oldu.
A/B testleri gerçek kategori, ders, kullanıcı kayıtları ve bölüm oluşturur; ASG oluşturma,
öğrenci liste/gönderme, öğretmen okuma ve GRD puan aktarımını doğrular.
Ders ve kategori API ile silinir; ASG silme olayı tamamlanana kadar poll yapılır.
Kullanıcılar shell cleanup adımında silinir. E2E_SECTION_ID ve skip gerekmez.
Pozitif SiteAdmin testi mevcut ayar değerlerini kaydeder ve gerçek maintenance çalıştırır.

Tekrarlanabilir Docker çalıştırıcısı: izole bağlantıyı E2E_AUTH_CONNECTION ortam değişkeninde
verip depo kökünde sh tests/e2e/run-isolated.sh çağırın. Betik kaynakları salt okunur bağlar,
derlemeyi geçici konteyner dizininde yapar, gerçek Identity fixture'ını oluşturur ve EXIT
trap ile siler. Docker ağı E2E_DOCKER_NETWORK ile değiştirilebilir; varsayılan asg-v2_default.
Uygulama Compose'da önceden güncel sürümle çalışmalıdır. JavaScript bağımlılıkları
tests/e2e altında kurulu olmalıdır. Bağlantı dizesini komut geçmişine yazmayın.
