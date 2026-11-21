# PR başlığı
test(file): gerçek SiteAdmin Playwright kabulünü izole Identity fixture ile tamamla

# Açıklama
Main a21e18a üzerindeki AUTH/CRS/ASG/FILE/GRD bağlantıları doğrulandı. Pozitif /admin/files tarayıcı kabulü, üretim seed'ine sabit yönetici parolası eklemeden tamamlanır: test-only yürütücü rastgele hesap oluşturur, gerçek Identity girişi kullanılır ve koşudan sonra hesap silinir. Test ayarları mevcut değerlerle kaydeder ve gerçek maintenance çağırır. Üretim modül kodları değiştirilmedi.

Güncel modül/eksik görev raporu docs/asg-file-wbs3/GUNCEL-GITHUB-DOGRULAMASI.md içindedir. Açık PR #50 main'de sayılmadı; gerçek CRS bölüm modeli olmadığı için iki A/B senaryosu güvenli biçimde bağımlı kalır. Test çalıştırma ve temizleme yönergeleri tests/e2e/README.md içindedir. İlk iki fixture koşusunda claim üretimi hata verdi; asıl neden ValueGeneratedNever modelinde Id=0 bırakılmasıydı. Test verisi pozitif benzersiz kimlik ve sentetik e-postayla düzeltildi.

Doğrulama sonuçları DOGRULAMA.json/currentGithubVerification alanında yer alır. Bu belge PR taslağıdır; PR açılmadı.


Güncel kapsam kararı: [ASG–CRS WBS raporu](ASG-CRS-WBS-KARARI.md). Bölüm kabul kriteri korunur; iki A/B atlaması kaldırılmadı. PR #50 artık origin/main 4d53353 içinde merged, bu dalda henüz alınmadı. Bu tur üretim kodu değiştirilmedi.
