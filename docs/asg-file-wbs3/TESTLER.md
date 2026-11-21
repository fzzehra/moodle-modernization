> **21.11.2026 — güncel son FILE entegrasyonu:** PR #40/#42 main'de; codex/asg-file-final-integration dalında 939/939 .NET, 54/54 JS, Playwright 6 geçti / gerçek CRS bölüm bağımlılığı nedeniyle 2 atlandı. Ortak dosya yöneticisi, süreçler arası PostgreSQL kilidi ve MIME davranışı doğrulandı. Yeni üretim kaynağı değişikliği gerekmedi; testler ve kayıtlar tamamlandı. Commit/push yapılmadı. [Son rapor](SON-FILE-ENTEGRASYONU.md); DOGRULAMA.json → finalFileIntegration. Aşağıdaki önceki durumlar tarihseldir.

> **21.11.2026 — son durum:** Kullanıcı onayıyla temizlik/yükleme veri kaybı düzeltildi. 845/845 .NET ve 54/54 JavaScript geçti; Playwright 4 geçti / CRS bölüm bağımlılığı nedeniyle 2 atlandı. Üç metinsel çakışma çözülmüş durumda; commit/push yapılmadı, merge commit onayı bekleniyor. Ayrıntılar [BIRLESME-65cd11d.md](BIRLESME-65cd11d.md), makine özeti DOGRULAMA.json → main65cd11dLockFix. Aşağıdaki önceki sonuçlar tarihsel kayıttır.

> **20.11.2026 — PR #39 sonrası güncel durum:** Seray e2b50d0 düzeltmesi main 1424b04'ten yeni commit oluşturmadan fast-forward ile alındı; bütün yerel çalışmalar yedeklenip korundu. Eşzamanlılık 10/10, .NET 708/708, JS 23/23 geçti. Playwright 4 geçti / bölüm modeli nedeniyle 2 skip. Önceki kırmızı FileStore sonucu giderilmiştir; aşağıdaki önceki sonuçlar tarihsel kayıttır. [Güncel ayrıntılı sonuç](SON-KONTROL.md), [makine kanıtı](DOGRULAMA.json), [tekrarlar](ESZAMANLILIK-TEKRARLARI.json).
> **20.11.2026 son kontrol:** [Güncel kabul raporu](SON-KONTROL.md) ve [izole tekrar kanıtı](ESZAMANLILIK-TEKRARLARI.json) önceki sonuçları tamamlar. Yeni regresyon: 707/708 .NET geçti; tek hata güncel main'de de bulunan fiziksel depo eşzamanlılığı. 23 JS geçti. Dosyalı SSR form/list/detail ve çakışmada taslağı gösterme doğrulandı. Aşağıdaki önceki toplamlar tarihsel kayıttır.
# Testleri çalıştırma

.NET 10 SDK, Docker/Testcontainers, PostgreSQL 17 gerekir. Normal geliştirme ortamında:

```sh
dotnet test Moodle.slnx -c Release --logger trx --results-directory coverage/v2 --collect 'XPlat Code Coverage'
node --test tests/web/crs-api.test.mjs tests/web/grd-api.test.mjs
```

Gerçek ClamAV testine `CLAMAV_TEST_HOST` ile erişilebilir clamd host'u sağlanmalıdır; port 3310'dur. Bu değişken yoksa tek gerçek-daemon testi açıkça skip olur; diğer scanner hata/zaman aşımı testleri çalışır. Bu teslimin son Linux çalıştırması gerçek Compose clamd ile aynı ağdadır.

## Gerçek tarayıcı

```sh
docker compose -p asg-v2 -f docker/docker-compose.yml -f docker/docker-compose.v2-test.yml up -d --build
cd tests/e2e
npm ci
E2E_BASE_URL=http://modern:8080 npx playwright test
```

Bu test override'ı Playwright konteyneri `asg-v2_default` ağı üzerinden erişsin diye AsgApi BaseUrl'i `http://modern:8080` seçer. Test çalıştırıcısı aynı ağa bağlanmalıdır. Host'tan HTTP sağlık adresi localhost:8180'dir; ASG SSR sayfalarına başka Host ile erişim bilinçli olarak reddedilir. İnsan tarayıcısı localhost:8180 kullanacaksa AsgApi BaseUrl de aynı origin yapılmalıdır; çerez forward güvenliği gevşetilmemelidir.

Playwright paketi ve resmî browser imajı 1.56.1'e sabittir. Kaynak: `tests/e2e/specs/asg-file.spec.mjs`. Sadece ortak sentetik Development fixture (ogrenci1/ogrenci2/ogretmen1) kullanılır. Kalıcı gerçek kullanıcı hesabıyla çalıştırılmaz. Taslaklar ve yeni ödev başlıkları her çalıştırmada ayrı oluşturulur; dış kullanıcı verisi silinmez. İsteğe bağlı `E2E_SECTION_ID` gerçek SENG101 bölüm kimliği olmalıdır. Sahte bölüm adaptörü veya SQL ile oluşturulmuş ödevi gerçek oluşturma kabulü diye kullanmayın.

Dört bağımsız tarayıcı testi: gerçek giriş/ders ödev listesi; bölüm bulunmadığında güvenli ret; gerçek taramayla temiz dosya/benzersiz ad/yanlış tür/EICAR; başka öğrencinin taslağına ret. Tam A/B scriptleri metin/dosya UI kaydı, gönderme, öğretmen ayrıntısı/indirmesi, gerçek GRD not yazma ve öğrenci not okumasını içerir. Gerçek bölüm adaptörü yokken bu ikisi `skip` olur; başarılı sayılmaz. Öğrenci kaydı ortak gerçek CRS seed'indendir, sahte enrolment servisi kullanılmaz.

Kapalı/kilitli dosyalı teslim, başka öğrencinin teslim dosyası, yetkisini kaybeden öğretmen, boyut/alan sınırı ve rollback PostgreSQL API entegrasyon testlerinde doğrulanır; bunların tamamı tarayıcıda çalıştırılmış diye raporlanmaz. Tam E2E bölüm engeli açılınca GRD UI/not verisi eşleşmesi de gerçek koşullarda tamamlanmalıdır.

Başarısızlıklarda screenshot/trace ve HTML raporu `coverage/playwright` altında yereldir, Git'ten dışlanır. Trace oturum çerezi ve sentetik giriş bilgisi içerebilir: ham trace paylaşmayın; paylaşım öncesi gizli alanları temizleyin. CI'ya bağımlılıklı tam A/B'nin zorunlu green kontrolü eklenmedi; bölüm adaptörü ve izole fixture sağlandıktan sonra ekip CI kararına bağlanmalıdır.

## Gerçek sonuçların kaydı

Makinece okunur özet: DOGRULAMA.json. Tarihsel 266 .NET + 7 JS ve 117 ASG kaydı değiştirilmez. Güncel başlangıç main: 319 birim + 338 entegrasyon = 657 geçti. İlk v2 çalıştırmasında iki yeni entegrasyon/fixture uyarlaması düzeltildi; önceki LocalDiskFileStore eşzamanlılık testi zaman zaman başarısız oldu ve korunarak raporlandı. Son .NET testindeki bu başarısızlık gizlenmez.

Windows doğrudan SDK denemesi UNC DLL Uygulama Denetimi hatasıyla çalışamadı; sıfır keşfedilen test başarılı test sayılmaz. Docker soketi için otomatik onay reddi sonrası kullanıcı sınırlı çalıştırmayı açıkça onayladı: kaynak salt okunur, derleme konteyner /tmp dizininde, yalnız coverage/v2-final-linux sonuç dizini yazılabilir. Hiçbir testin atlanması bu ortam hatasını gizlemek için kullanılmadı.

Son ölçülen sonuç: Release derleme başarılı; 338 birim testte 337 geçti / 1 başarısız; 358 entegrasyon testinin tamamı geçti, atlanan yok. Toplam 696 .NET testte 695 geçti / 1 başarısız. Mimari testler bu birim grubundadır ve geçti. JavaScript 21/21. Playwright son çalıştırma 4 geçti / 2 bölüm bağımlılığı nedeniyle skip / 0 başarısız. İlk tarayıcı denemesi AUTH formuna InteractiveServer hazır olmadan tıkladı ve 1 hata verdi; script sabit bekleme yerine Blazor OnRenderCompleted websocket onayını bekleyecek şekilde düzeltildi. AUTH formu kaynak kodu değiştirilmedi; kullanıcı çok erken tıklarsa prerender davranışı AUTH sahibinin konusudur.

Coverlet son entegrasyon raporu: toplam satır %85,08 / dal %55,83; API satır %77,70 / dal %57,64. Birim raporu toplam satır %20,88 / dal %38,28. Bunlar ayrı çalıştırma raporlarıdır, birleştirilmiş oran değildir. Yalnız test kapsamı ölçümüdür; kod büyüklüğü veya efor ölçümü üretilmedi. Ham raporlar coverage/v2-final-linux altında Git dışındadır.

Teslim hijyeni: güncel Git adaylarında token/özel anahtar biçimi taramasında eşleşme bulunmadı. Yeni/değişen teslim dosyalarında bin/obj/node_modules/coverage/trace/test çıktısı yok; ignore kuralları bu çıktıları dışlar. Repoda önceden izlenen ortam/testler/asg-grd/evidence/*.log kanıt dosyaları vardır; bu çalışmada değiştirilmedi veya silinmedi. Sentetik fixture parolaları ve Compose'un mevcut local_dev_only değeri geliştirme örneğidir; gerçek kimlik bilgisi eklenmedi. Bu örüntü taraması bütün olası sır biçimlerini matematiksel olarak kanıtlamaz.

Başlangıç 657 testinin yöntem adları son TRX'lerle karşılaştırıldı: kaybolan test yöntemi yok. File artık desteklendiği için eski “desteklenmeyen tür” negatif girdisi `unsupported` olarak güncellendi; ret beklentisi korunur. ASG OpenAPI hata listesine yeni 503 eklendi, önceki kontrol zayıflatılmadı. Mimari ve migration/snapshot testleri geçti. Git whitespace ve çakışma işareti kontrolleri temizdir.


20.11.2026 güncel origin/main a986a0a izolasyonu: 10 tekrarda 4 geçiş / 6 hata; önceki 6a4c371 ve mevcut dal/tmpfs sonuçları korunur. Toplam 50 izole tekrar, 40 başarısız / 10 başarılı. Bu tekrarlar tekil .NET regresyon sayısına eklenmez.

Güncel JavaScript komutu: `node --test tests/web/asg-files.test.mjs tests/web/crs-api.test.mjs tests/web/grd-api.test.mjs` (23/23 geçti).


## 21.11.2026 — 02017af sonrası nihai ASG entegrasyonu
Release başarılı; **501 birim + 494 entegrasyon = 995/995 .NET**, 0 başarısız/atlanan. Önceki 939 test adı yeni TRX içinde korundu. JavaScript **54/54**; Playwright **6 geçti / 2 gerçek CRS bölüm bağımlılığı nedeniyle atlandı**. PostgreSQL 17, gerçek ClamAV temiz/EICAR, beş context migration/snapshot ve HTTP 200/401 kontrolleri geçti.
Yeni ASG/GRD temizleme testleri: 8/8; gerçek CRS öğrenci rol/tarih/suspension: 6/6; gerçek AUTH HTTP ders silme + DI worker: 1/1. İlk hedefli ASG koşusu 7/7; sonraki test eklenirken namespace derleme hatası düzeltildi, final çözüm build/test geçti.
91 benzersiz gereksinim, 9 sütun; çakışma işareti/hassas anahtar kalıbı/gereksiz build çıktısı yok. Ham TRX ve loglar ignored coverage/asg-cleanup-regression altındadır.
[Güncel entegrasyon ve kalan bağımlılıklar](NIHAI-ASG-FILE-ENTEGRASYONU.md), [makine özeti](DOGRULAMA.json) / finalAsgCleanupIntegration. Önceki sonuçlar tarihsel kayıttır.
