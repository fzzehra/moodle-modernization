> **21.11.2026 — güncel son FILE entegrasyonu:** PR #40/#42 main'de; codex/asg-file-final-integration dalında 939/939 .NET, 54/54 JS, Playwright 6 geçti / gerçek CRS bölüm bağımlılığı nedeniyle 2 atlandı. Ortak dosya yöneticisi, süreçler arası PostgreSQL kilidi ve MIME davranışı doğrulandı. Yeni üretim kaynağı değişikliği gerekmedi; testler ve kayıtlar tamamlandı. Commit/push yapılmadı. [Son rapor](SON-FILE-ENTEGRASYONU.md); DOGRULAMA.json → finalFileIntegration. Aşağıdaki önceki durumlar tarihseldir.

> **21.11.2026 — son durum:** Kullanıcı onayıyla temizlik/yükleme veri kaybı düzeltildi. 845/845 .NET ve 54/54 JavaScript geçti; Playwright 4 geçti / CRS bölüm bağımlılığı nedeniyle 2 atlandı. Üç metinsel çakışma çözülmüş durumda; commit/push yapılmadı, merge commit onayı bekleniyor. Ayrıntılar [BIRLESME-65cd11d.md](BIRLESME-65cd11d.md), makine özeti DOGRULAMA.json → main65cd11dLockFix. Aşağıdaki önceki sonuçlar tarihsel kayıttır.

# Güncel sonuç — Seray PR #39 sonrası, 20.11.2026

**Fiziksel depo eşzamanlılık hatasının önceki regresyon engeli giderildi.** Seray'ın `e2b50d0f83a6ccd43fe4b54dec9360035d95ec50` düzeltmesi PR #39 ile main `1424b04` içine alınmıştır. Geliştirme dalı 3b9ccd6'dan bu main commit'ine **fast-forward** edildi; yeni commit veya merge commit oluşturulmadı. Yerel ASG/FILE çalışmalarının tamamı hash doğrulamalı yedeklendi (`coverage/pre-main-1424b04`), ortak WBS-3 ve izlenebilirlik belgeleri gelen ekip kayıtlarıyla birleştirildi. Diğer yerel kaynak/test dosyaları yedekle birebir korundu. Seray'ın kodu ve mevcut testi değiştirilmedi.

| Eşzamanlılık tekrarı | Sonuç |
| --- | --- |
| 1 | Geçti |
| 2 | Geçti |
| 3 | Geçti |
| 4 | Geçti |
| 5 | Geçti |
| 6 | Geçti |
| 7 | Geçti |
| 8 | Geçti |
| 9 | Geçti |
| 10 | Geçti |

**Güncel tam regresyon: 343/343 birim + 365/365 entegrasyon = 708/708 .NET; 0 başarısız, 0 atlanan. JavaScript 23/23.** Release build, Docker Compose config/build ve HTTP kontrolleri geçti. Sağlık/giriş/ders/OpenAPI 200; oturumsuz draft-limit isteği 401. PostgreSQL 17, migration/model snapshot ve mimari testleri geçti. ASG/FILE gerçek-cookie SSR form/liste/ayrıntı ve yetkilendirme grubu 24/24, LocalDiskFileStore grubu 10/10, gerçek ClamAV temiz/EICAR dahil scanner grubu 3/3 başarılı. Tekilleştirme, hash/byte içerik bütünlüğü, yarım yazım temizliği ve sürümlü dosya taşıma testleri korunarak çalıştırıldı. Ham kanıt: `coverage/v2-seray-fix/`; makine özeti: DOGRULAMA.json → serayFixIntegration. Önceki 707/708 ve kırmızı tekrarlar tarihsel kayıttır; silinmedi.

**Playwright: 4 geçti, 2 skip, 0 hata.** Güncel main'de Sinem'in bölüm modeli ve gerçek ICourseSectionQuery hâlâ yoktur. Bu yüzden A/B'nin gerçek öğretmen ödev oluşturma adımı çalışamaz; sahte bölümle başarı üretilmedi. Fail-closed DI korunur. Ortak dosya yöneticisi de henüz yok; çalışan ASG adaptörü korunur. Gerçek AUTH/CRS öğrenci/FILE/GRD bağlantıları korunmuştur, API/DI/migration/Compose sözleşmelerinde yeni uyumsuzluk tespit edilmedi.

Onaylı kapsamda yeni bağımsız ASG/FILE geliştirme eksiği bırakılmadı. Kalan işler: Sinem'in bölüm modeli sonrası gerçek oluşturma/ortak seed ve tam A/B kabulünü çalıştırmak; ortak dosya yöneticisi geldiğinde adaptör devri. Önceki kırmızı FileStore testi artık PR öncesi engel değildir. Tam işlevsel kabul için CRS bölüm bağımlılığı ve iki E2E hâlâ açıktır; tamamlanmış gibi sunulamaz. Seray'ın static kilidi süreç içi kapsamlıdır; çok süreçli ortak depo garantisi bu testlerle doğrulanmış sayılmaz.

**Son Git kontrolleri:** whitespace temiz; değişen teslim dosyalarında çakışma işareti, taranan sır örüntüsü veya build çıktısı yok; staging boş. Seray kaynağı ve mevcut test HEAD ile birebir aynı.

**Git:** yeni commit, merge commit, push, PR, rebase, force push veya main dalına birleştirme yapılmadı. Dalda commit edilmemiş geliştirmeler korunuyor. COCOMO, karşılaştırma, manuel geliştirme ve ekip kod incelemesi yapılmadı.

---

## Önceki doğrulama kaydı (tarihsel)
# Commit öncesi son kabul doğrulaması — 20.11.2026

Çalışma dalı `codex/asg-file-wbs3-v2`, korunmuş başlangıç HEAD `3b9ccd6`. 19.11.2026 fetch sonrası `origin/main` `6a4c371` idi. 20.11.2026 yeniden fetch ile `a986a0a` doğrulandı; aradaki değişiklikler yalnız WBS belgelerindedir, ilgili kabul kriteri satırları ve fiziksel depo/test kaynakları değişmemiştir. Yeni main için ek 10 izole tekrar ayrıca kaydedildi. Dal değiştirilmedi; stash/reset/merge/rebase yapılmadı. Main `git archive` ile alındı ve resmî SDK konteynerinin ayrı `/tmp/main` dizininde derlendi; mevcut çalışma ağacı `/tmp/current` altında bağımsız kopyayla test edildi. Kaynak bağlaması salt okunurdur. Yalnız Git dışı coverage sonuçları ve bu çalışmanın düzeltmeleri yazıldı.

## Başarısız testin kesinleşen veya olası nedeni

Değiştirilmemiş `LocalDiskFileStoreTests.Eszamanli_ayni_icerik_yazimi_tek_kopya_birakir` her ortamda ayrı süreçlerle 10 kez çalıştırıldı. Her çağrı 20 eşzamanlı yazım yapar. Test kaldırılmadı, filtre yalnız izolasyon amacıyla kullanıldı, assertion değiştirilmedi.

| Kaynak | Fiziksel test dizini | Geçti | Başarısız |
| --- | --- | ---: | ---: |
| Mevcut çalışma | Konteyner `/tmp` | 0 | 10 |
| origin/main 6a4c371 | Konteyner `/tmp` | 0 | 10 |
| Güncel origin/main a986a0a (20.11) | Konteyner `/tmp` | 4 | 6 |
| Mevcut çalışma | Ayrı tmpfs `/ramtmp` | 1 | 9 |
| origin/main 6a4c371 | Ayrı tmpfs `/ramtmp` | 5 | 5 |

Ham her tekrarın sonucu ve hata mesajı: [ESZAMANLILIK-TEKRARLARI.json](ESZAMANLILIK-TEKRARLARI.json); TRX/log: `coverage/v2-final-check/`. İşletim sistemi Ubuntu 24.04 linux-x64, SDK 10.0.404, runtime 10.0.15. `LocalDiskFileStore.cs` ve bu test dosyası güncel main ile birebir aynı; `git diff origin/main -- <iki dosya>` boş.

Kesinleşenler: ASG değişikliği gerekli değildir; hata güncel main'de vardır. Test gerçek FileStore'u doğrudan kurar, AUTH/ASG/DI/yükleme servisini çağırmaz. Aynı hata tmpfs üzerinde de vardır; yalnız WSL paylaşımlı çalışma klasörüne özgü değildir. Hatalı beklenti değil, tek yazanın `AlreadyExisted=false` bildirmesi sözleşmesinin ihlalidir.

Konumlar: `src/Moodle.Infrastructure/Files/LocalDiskFileStore.cs:53` hedef varlık kontrolü; `:62` File.Move; `:64` yalnız IOException olursa kaybeden sayma; `:70` koşulsuz yeni-yazım sonucu. Test assertion'ı `tests/Moodle.UnitTests/LocalDiskFileStoreTests.cs:82`.

Moodle bağımlılığı olmayan ayrı tanı programı yalnız `File.Move(source,target)` (overwrite varsayılan false) kullanır. 10 turdaki başarılı çağrı sayıları: **14, 1, 1, 1, 6, 6, 1, 2, 3, 1**; beklenen tek kazanan 5/10 turda ihlal edildi. Sistem çağrısı izinde `rename` ve `link` yolları gözlendi. İlaveten yalnız tanı için `strace rename:delay_enter=100ms` ile yarış penceresi büyütüldüğünde 10 turun her birinde 20 çağrı başarılı döndü. Bu zorlanmış interleaving, yukarıdaki 50 gerçek test sonucuna dahil değildir.

Sonuç: Seray'ın mevcut depolama yaklaşımı ile bu Linux/.NET File.Move davranışının etkileşiminden kaynaklanan **varlık kontrolü–taşıma yarışı**. Tek başına `File.Exists` ardından `File.Move` bu ortamda tek kazanan garantisi sağlamaz. .NET dışındaki sürüm/işletim sistemlerinde aynı sıklıkta olacağı iddia edilmez. Bulguyu yalnız “ortamda rastgele test bozuk” diye kapatmak doğru değildir; ortam runtime davranışını görünür kılar. Aynı hash için son içerik aynı olabilir, fakat AlreadyExisted bilgisinin güvenilirliği ve ona dayanan geri alma kararları önemlidir. Seray'ın kaynak koduna onaysız düzeltme uygulanmadı. Çözümün farklı instance/süreçler arasında atomik oluşturma/yeniden kullanım garantisini koruması gerekir; yalnız test assertion'ını gevşetmek çözüm değildir.

## Yapılan düzeltmeler

1. `src/Moodle.Web/wwwroot/asg-files.js:17`: tarama/yükleme sürerken formun Kaydet düğmeleri de devre dışı; tamamlanınca önceki disabled durumları geri gelir. Başarılı/hatalı tarama için `tests/web/asg-files.test.mjs` iki regresyon ekler.
2. `src/Moodle.Web/Pages/SubmissionForm.razor:27` ve `:75`: 409 sonrası dosya taslağı kimliğinin yanında ad/boyut listesi de yeniden yüklenir. Başarılı kayıtta geçici liste sıfırlanır; teslimde dosya kalmamışsa eski indirme listesi tutulmaz. `FileUploadV2Tests.Real_cookie_SSR_file_form_conflict_list_detail_and_download` gerçek cookie ve SSR form POST ile doğrular.
3. Eksik kabul kanıtları eklendi: 1 MiB PDF ve içerik bütünlüğü; 255/256 ad sınırı HTTP; klasör/bozuk UTF-8; daha düşük FileSetting sınırı; bütün izinli uzantıların imza/MIME eşleşmesi. Mevcut negatif testler veya fiziksel depo değiştirilmedi.

## Gereksinim bazında tamamlanma durumu

WBS-1 sınıf/öncelikleri korunur: FR-01 K/M, FR-07 K/S, FR-08 K/M, FR-10 K/M, FR-11 K/C. BR maddelerine yeni öncelik uydurulmadı.

| Gereksinim | Kabul kriteri ve ayrı doğrulama | Durum |
| --- | --- | --- |
| FILE-FR-01 | 1 MiB PDF kendi taslağında listelenir, metadata doğru, indirilen baytlar aynı; başka kullanıcı liste/indirme/yükleme 404. `FileUploadV2Tests:273`, `:108` | Geçti (son TRX) |
| FILE-FR-07 | Aynı ad/aynı içerik yeni adla saklanır, orijinal korunur. Unit T_FILE_09 + entegrasyon `:108`, gerçek Playwright temiz yükleme | Uygulandı |
| FILE-FR-08 | En düşük mevcut sunucu/FileSetting sınırı; sınırda kabul, üstünde 413, metadata yok. `:130`, `:304` | Uygulandı |
| FILE-FR-10 | Geçersiz ad, 256 karakter, boş dosya, klasör, yanlış imza/tür ve bozuk UTF-8 anlamlı 400/415; 5xx yok. `:122`, `:287`, `:296` + UploadValidationTests | Uygulandı |
| FILE-FR-11 | Her kabul edilen yükleme taranır; infected 422, unavailable/timeout 503; kalıcı kayıt yok. `:144`, VirusScannerTests; gerçek ClamAV temiz/EICAR | Uygulandı |
| FILE-BR-06 | Düz ad, 255 sınırı, geçersiz ad/path reddi; fiziksel yol kullanıcı adından üretilmez. T_FILE_12 unit/HTTP | Uygulandı |
| FILE-BR-07 | Varsayılan overwrite=false: `ad (1).uzantı`, çoklu çakışma ve eşzamanlı ad tekilliği. Draft satır kilidi; `:108` + unit | Uygulandı; alttaki bağımsız FileStore yarışı ayrıca açık |
| FILE-BR-08 | Dosya boyutu, toplam alan, öğe sayısı, boş/klasör; sınır ve üst sınır. `:130`, `:253`, `:304`, `:296` | Mevcut onaylı yapılandırma kapsamında uygulandı |
| FILE-BR-09 | Uzantı + içerik imzası, TXT UTF-8, gerçek AV; client MIME beyanından bağımsız metadata. Unit pozitif/negatif + T_FILE_14 | Uygulandı |

WBS'nin eski PHP/site/ders/modül minimumu, bu uygulamada mevcut FileSetting ve FileUpload sınırları üzerinden uygulanır; CRS'de farklı ders başına veya ASG'de farklı ödev başına ayrı sayısal boyut alanı bulunmaz. Kullanıcının onayladığı sunucu ayarlanabilir başlangıç kapsamı esas alınır (PDF/PNG/JPEG/TXT, 10 dosya, toplam 50 MiB). Bu onay, mevcut olmayan ders/ödev bazlı ayar ekranının geliştirildiği iddiasına dönüştürülmez. Böyle ayrı bir politika talep edilirse ekip model/sözleşme kararı gerekir.

Ayrı test kanıtı grupları: benzersiz ad — unit + 3 eşzamanlı HTTP yükleme; eşzamanlı teslim — iki aynı version yazımında tek commit; dosya boyutu — sınır/üstü ve düşük DB sınırı; geçersiz isim — traversal/kontrol/255–256; boş/klasör — 400 ve kayıt yok; tür — beş izinli uzantı + yanlış imza/UTF-8 415; AV — gerçek temiz/EICAR + timeout/unavailable + kayıt yok. Kesin sayılar ve geçen test adları DOGRULAMA.json son kontrol bölümünde yer alır.

ASG formu: tür/sınır, ad/boyut, tarama durumu, hata, kaydet/gönder, kilit/pencere, çakışmada koruma. Liste: dosya sayısı ve ayrıntı bağlantısı. Ayrıntı: ad/boyut ve yetkili indirme; başka öğrenciye metadata sızmaz. SSR entegrasyon testi mevcut ödev fixture'ını doğrudan kurar; gerçek CRS bölüm kontrolünden geçen oluşturma veya tam A/B testi olarak sunulmaz.

## Güncel test sonuçları

Son kontrol: **343 birim testte 342 geçti / 1 başarısız; 365 entegrasyon testinin tamamı geçti. Toplam 708 .NET testte 707 geçti / 1 başarısız / 0 skip.** Başarısızlık aynı değiştirilmemiş LocalDiskFileStore testidir. **23 JavaScript testi geçti. 20.11.2026 güncel Docker imajıyla Playwright: 4 geçti / 2 bağımlılık nedeniyle skip / 0 başarısız.** Yeni gerçek-cookie SSR form/list/detail/indirme ve çakışma testi başarılı; Release/Docker publish başarılı. Sonuçlar [DOGRULAMA.json](DOGRULAMA.json) içindeki sonKontrol bölümündedir. Önceki 696 test/695 geçiş ve 23 öncesi 21 JS kaydı tarihsel olarak korunur. 50 tekrar sonucu hiçbir şekilde tek bir başarılı tekrar ile değiştirilmez.

## Bana ait kalan bağımsız işler

Bu son kontrolde bulunan iki bağımsız arayüz eksiği düzeltildi ve kabul kanıtları genişletildi. Onaylı mevcut sayısal sınır kapsamındaki ASG/FILE bağımsız geliştirme işi bırakılmadı. Tam A/B kabulünün koşul kalktıktan sonra gerçekten çalıştırılması hâlâ Aslı Ceren'in doğrulama sorumluluğudur; scriptin mevcut olması bu görevi başarılı yapmaz. Bölüm modeli geldikten sonra gerçek örnek seed ve tam not akışı da tekrar doğrulanacaktır.

Gerçek AUTH DI: AuthCurrentUser/AuthAccessPolicy varsayılan; AssignmentSubmissionAccessPolicy gerçek hedef öğrenci kapsamı. UnavailableIdentity/UnavailableSubmissionScope/FakeSubmissionAccessPolicy runtime kaydı değildir. FakeCurrentUser/FakeAccessPolicy/FakeFileService yalnız açık Development/Testing opt-in ile seçilebilir; Production opt-in DevelopmentConfiguration tarafından reddedilir. ClamAvVirusScanner runtime, kontrollü scanner yalnız test override'ıdır. UnavailableCourseSectionQuery hâlâ fail-closed runtime bağımlılığıdır.

## Diğer ekip üyelerine bağımlı kalan işler

- Sinem: gerçek CRS bölüm modeli + `ICourseSectionQuery.BelongsToCourseAsync(sectionId,courseId)` adaptörü/DI. İki Playwright tam senaryosu `tests/e2e/specs/asg-file.spec.mjs:56` üzerinde E2E_SECTION_ID olmadığı için skip edilir; geçerli gerçek SENG101 bölüm ID'si ve gerçek adaptör birlikte gerekir. Yalnız ortam değişkeni uydurmak yeterli olmaz: oluşturma API'si invalid_section döndürür. AUTH/FILE/GRD eksik olduğu için skip edilmiyor; ortak senaryonun **öğretmen ödev oluşturma adımı** engelli olduğu için metin A ve dosya B zincirleri başlayamıyor.
- Seray: yukarıdaki fiziksel depo tek-kazanan yarışı için düzeltme/onay ve çok süreçli regresyon.
- Fatma Zehra: ortak dosya yöneticisi geldiğinde ASG adaptörünün devri; mevcut özel adaptör çalışır, bu bağımlılık bağımsız yüklemeyi engellemez. Temizlik/onarım politikası dosya sistemi–DB belirsiz commit sınırı için korunur.
- Ekip: modüller arası FK/silme ve gerekirse ayrı ders/ödev boyut politikaları. İrem'in mevcut gerçek AUTH ve FILE kalıcı hizmetleri kullanılmaktadır; yeni AUTH sağlayıcısı beklenmiyor.

## PR açılmasına engel olan durumlar

Tüm testler yeşil değildir: mevcut main'deki fiziksel depo testi tekrarlanabilir biçimde kırmızıdır. Onaysız Seray düzeltmesi yapılmadı. Bu nedenle “regresyon tamamen geçti” denilerek teslim edilmemelidir. Tam A/B E2E ve gerçek oluşturma/seed kabulü CRS bölüm bağımlılığı yüzünden engellidir; bunlar ekipçe açık takip edilmeden tam kabul iddiası yapılamaz. Güncel main a986a0a doküman commitleri bu çalışma dalına birleştirilmedi; ileride onaylı birleşmede ortak doküman uyumu kontrol edilmelidir.

COCOMO, karşılaştırma raporu, manuel geliştirme ve ekip kod incelemesi yürütülmedi. Commit/push/PR/merge yapılmadı.

Son teslim hijyeni: Git diff --check temiz; yeni/değişen teslim dosyalarında çakışma işareti, taranan token/özel anahtar örüntüsü veya build/test çıktısı bulunmadı. Tanı programı, archive ve syscall logları yalnız Git dışı coverage dizinindedir.


## 21.11.2026 — 02017af sonrası nihai ASG entegrasyonu
Release başarılı; **501 birim + 494 entegrasyon = 995/995 .NET**, 0 başarısız/atlanan. Önceki 939 test adı yeni TRX içinde korundu. JavaScript **54/54**; Playwright **6 geçti / 2 gerçek CRS bölüm bağımlılığı nedeniyle atlandı**. PostgreSQL 17, gerçek ClamAV temiz/EICAR, beş context migration/snapshot ve HTTP 200/401 kontrolleri geçti.
Yeni ASG/GRD temizleme testleri: 8/8; gerçek CRS öğrenci rol/tarih/suspension: 6/6; gerçek AUTH HTTP ders silme + DI worker: 1/1. İlk hedefli ASG koşusu 7/7; sonraki test eklenirken namespace derleme hatası düzeltildi, final çözüm build/test geçti.
91 benzersiz gereksinim, 9 sütun; çakışma işareti/hassas anahtar kalıbı/gereksiz build çıktısı yok. Ham TRX ve loglar ignored coverage/asg-cleanup-regression altındadır.
[Güncel entegrasyon ve kalan bağımlılıklar](NIHAI-ASG-FILE-ENTEGRASYONU.md), [makine özeti](DOGRULAMA.json) / finalAsgCleanupIntegration. Önceki sonuçlar tarihsel kayıttır.
