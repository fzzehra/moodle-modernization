# PR başlığı
feat(asg-file): kalıcı ders bölümü entegrasyonu ve tam gerçek E2E kabulü

# Açıklama
ASG-FR-01 oluşturma akışı artık PostgreSQL'de kalıcı ders bölümünü doğrular. Minimal CourseSection ve mevcut ICourseSectionQuery gerçeklemesi, ders yetkisi/CSRF kontrollü GET/POST uçları ve CRS migration/snapshot eklendi. Sahte bölüm veya sabit bölüm kimliği kullanılmaz; örnek ASG seed'i gerçek bölüm kimliğine eşler.

Gerçek A/B kullanıcı akışları metin/dosyalı teslim, öğretmen erişimi ve GRD puan aktarımını tamamlar. Dokuz Playwright senaryosunda skip kalmaz. Geçici gerçek Identity kullanıcıları ve API ile oluşturulan ders/kategoriler koşudan sonra temizlenir; çalıştırma yönergeleri tests/e2e/README.md içindedir.

PR #50 main 4d53353 üzerinden alınmıştır. Genel ders dosyası temizliğine içerik kilidi/yerel transaction, gerçek ders dosyası commit'ine CRS satır kilidi eklenmiştir; silme yarışı kaybeden commit taslağı korur. Paylaşılan blob fiziksel olarak silinmez. Mevcut ASG/GRD transaction, retry ve dosya erişim politikaları korunur.

course.managefiles açık yetki kuralı ve yalnız Development/Testing ortamında dış yapılandırmalı isteğe bağlı SiteAdmin seed'i eklenmiştir. Varsayılan yönetici parolası yoktur; mevcut sıradan kullanıcı terfi ettirilmez; yeni hesapta parola değişimi zorunludur.

Migration: 20261121210130_CourseSections. Up yalnız yeni crs_sections tablosu/FK/indeks ekler. CRS/FILE/ASG/GRD aynı PostgreSQL veritabanında çalışmalıdır. Olay temizliği sonunda tutarlıdır; dağıtık tek transaction iddiası yoktur.
Son test kanıtları docs/asg-file-wbs3/DOGRULAMA.json/finalSectionDelivery ve SON-BOLUM-ENTEGRASYONU.md içindedir.
Bu belge taslaktır; PR açılmadı.

Doğrulama: Release başarılı; 505 birim + 503 entegrasyon = **1008/1008 .NET**; JS **54/54**; Playwright **9/9**, 0 skip. PostgreSQL 17, gerçek ClamAV, beş model snapshot/migration ve Compose/HTTP geçti. Önceki 995 test korunur; ilk fixture/exception beklenti hataları düzeltildikten sonra son tam koşu başarılıdır.
