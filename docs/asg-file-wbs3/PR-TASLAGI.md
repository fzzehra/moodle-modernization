# PR başlığı
ASG/FILE nihai entegrasyonu: ders silme temizliği ve veri bütünlüğü testleri

# Açıklama
Silinen dersin ASG teslimleri ve dosya referansları artık mevcut CRS olay dağıtıcısıyla temizlenir. Onaylı GRD uzantısı, ASG ödev kimlikleri silinmeden değerlendirmeleri ve not defterini temizler. Transaction, idempotent tekrar işleme ve ortak PostgreSQL kilitleri geç gelen yazmaları ve dosya kaybını önler; paylaşılan blob fiziksel olarak silinmez.

Gerçek CRS yükümlü öğrenci sorgusu, AUTH HTTP silme → worker akışı, ayrı süreç FILE yarışları, MIME ve rollback testleri korunur/eklenir. origin/main 02017af fast-forward alınmış, önceki çalışmalar geri yüklenmiştir.

Doğrulama: Release; 501 birim + 494 entegrasyon = 995/995 .NET; 54/54 JavaScript; PostgreSQL 17; gerçek ClamAV; beş DbContext migration/snapshot; Compose ve HTTP kontrolleri başarılı. Playwright 6 geçti / 2 atlandı: gerçek CRS bölüm modeli hâlâ yoktur. Sahte bölümle uçtan uca kabul gösterilmedi. SiteAdmin pozitif API testi gerçek AUTH ve test kullanıcısıyla geçer; örnek SiteAdmin hesabı olmadığı için pozitif tarayıcı kabulü bağımlı kalır.

Yeni migration: AsgDeletedCourses. ASG/FILE/GRD ortak PostgreSQL yerleşimi gerekir. Olay etkileri modül bazında transaction ile uygulanır; CRS receipt ayrı transaction olduğundan yeniden deneme güvenlidir, modüller arasında tek transaction yoktur.

Ayrıntılar: docs/asg-file-wbs3/NIHAI-ASG-FILE-ENTEGRASYONU.md ve DOGRULAMA.json/finalAsgCleanupIntegration.
Bu dosya yalnız PR taslağıdır; PR açılmadı.
