# Nihai ASG + FILE entegrasyonu — 21.11.2026

## Git ve kapsam
Çalışma dalı: codex/asg-file-final-integration. a254d4a → origin/main 02017af fast-forward alındı; yeni commit oluşturulmadı. PR #46 (ff52c2c: zorunlu parola değişikliği), PR #47 (20e4804: CRS olay dağıtımı / AUTH-CRS testleri), PR #48 (b96f147: ders bağlantıları ve belgeler) entegre edildi.
Önceki 17 dosya coverage/pre-main-02017af altında SHA256 doğrulamalı yedeklendi, stash ile korundu ve geri uygulandı. Stash ek yedek olarak tutuldu. Paylaşılan WBS/izlenebilirlik kayıtları iki taraf korunarak birleştirildi; 91 benzersiz gereksinim var.

## Silinen ders
Mevcut ICrsEventExtension / CrsEventDispatcher / EventDispatchWorker kullanılır. GRD uzantısı sıra 50, ASG uzantısı sıra 100'dür. GRD ödev kimlikleri kaybolmadan değerlendirmeleri, kalemleri, öğrenci notlarını ve kategori ağacını temizler. Her uzantı kendi yerel transaction'ını kullanır; CRS receipt ayrı transaction'dadır. Bu nedenle tekrar çağırma güvenlidir; tüm modüller için tek atomik transaction iddiası yoktur. GRD başarılı / ASG başarısız olursa ASG retry edilir; GRD tekrar çağrısı zararsızdır.
ASG uzantısı teslim kurallarını, teslimleri, ödevleri siler; mod_assign FILE referanslarını çöp kutusuna taşır. Ortak blob aktif başka referans taşıyorsa yetim sayılmaz; fiziksel dosya yalnız mevcut FILE saklama/temizlik servisi tarafından kaldırılır.
AsgDeletedCourses migration'ı kalıcı ders silinme kaydı tutar. ASG yazma ve temizlik aynı PostgreSQL ders advisory kilidini alır; geç gelen oluşturma reddedilir. GRD not yazımı aynı kilit altında gerçek ders varlığını yeniden doğrular. FILE içerik kilitleri mevcut ortak bigint anahtarıyla korunur. Üretimde ASG/FILE/GRD aynı PostgreSQL veritabanını kullanmalıdır.

## Gerçek CRS ve AUTH
IAssignmentLearnerQuery zaten gerçek CrsService'e bağlıdır; Student, askıya alınmama ve başlangıç/bitiş tarihleri yeni parametrik testlerde doğrulanır. Öğretmenler yükümlü öğrenci listesine girmez. Varsayılan AUTH gerçektir; geliştirici fake bayrağı üretim kabul testi değildir.
ICourseSectionQuery hâlâ UnavailableCourseSectionQuery'dir; gerçek CRS bölüm modeli yoktur. Ödev oluşturma invalid_section ile güvenli reddedilir. Sahte bölüm eklenmedi.
SiteAdmin rolü mevcut, örnek hesabı yoktur. Pozitif SiteAdmin testi gerçek AUTH üzerinde yalnız test kullanıcısıyla yapılır; üretim seed'i değiştirilmedi.

## Doğrulama
Son makine sonuçları DOGRULAMA.json / finalAsgCleanupIntegration alanında ve TESTLER.md ek kaydındadır.
Playwright: 6 geçti, 2 atlandı. A/B tam ödev oluşturma → metin/dosyalı teslim → GRD akışı gerçek CRS bölüm kimliğine bağlıdır. Bunlar gerçek uçtan uca kabul edilmiş sayılmadı.
Ortak FILE yöneticisinin gerçek öğretmen yükleme/kaydetme, öğrenci indirme ve yetkili silme akışı geçti.
Önceki 939/939 .NET ve 54/54 JavaScript sonuçları tarihsel olarak korunur.

## Kalan işler
Sinem: gerçek bölüm modeli ve ICourseSectionQuery; ardından iki A/B tarayıcı senaryosu ve oluşturulan gerçek ASG verisinin tarayıcıdan ders silme sonrası temizliği.
Pozitif SiteAdmin tarayıcı kabulü gerçek yönetici hesabı sağlanınca çalıştırılabilir; ayrıcalıklı örnek hesap kendiliğinden eklenmedi.
Course.deleted işleyişi sonunda tutarlıdır; background worker gecikmesi boyunca okuma temizlenmeyi bekleyebilir.
Commit/push/PR açma ve main'e birleştirme yapılmadı. PR taslağı hazırlanır; gerçek A/B kabulü için bölüm bağımlılığı devam eder.
YZ çalışma süresi: Ölçülmedi / kullanıcı tarafından doldurulacak.


## 21.11.2026 — 02017af sonrası nihai ASG entegrasyonu
Release başarılı; **501 birim + 494 entegrasyon = 995/995 .NET**, 0 başarısız/atlanan. Önceki 939 test adı yeni TRX içinde korundu. JavaScript **54/54**; Playwright **6 geçti / 2 gerçek CRS bölüm bağımlılığı nedeniyle atlandı**. PostgreSQL 17, gerçek ClamAV temiz/EICAR, beş context migration/snapshot ve HTTP 200/401 kontrolleri geçti.
Yeni ASG/GRD temizleme testleri: 8/8; gerçek CRS öğrenci rol/tarih/suspension: 6/6; gerçek AUTH HTTP ders silme + DI worker: 1/1. İlk hedefli ASG koşusu 7/7; sonraki test eklenirken namespace derleme hatası düzeltildi, final çözüm build/test geçti.
91 benzersiz gereksinim, 9 sütun; çakışma işareti/hassas anahtar kalıbı/gereksiz build çıktısı yok. Ham TRX ve loglar ignored coverage/asg-cleanup-regression altındadır.
[Güncel entegrasyon ve kalan bağımlılıklar](NIHAI-ASG-FILE-ENTEGRASYONU.md), [makine özeti](DOGRULAMA.json) / finalAsgCleanupIntegration. Önceki sonuçlar tarihsel kayıttır.
