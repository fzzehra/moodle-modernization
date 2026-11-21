> **22.11.2026 güncel teslim:** Gerçek bölüm entegrasyonu tamamlandı. Release, .NET **1008/1008**, JS **54/54**, Playwright **9/9**; 0 skip. Aşağıdaki eski bölüm engeli/açık PR kayıtları tarihseldir. [Son mimari ve kanıt](SON-BOLUM-ENTEGRASYONU.md).

# ASG–CRS WBS kapsam kararı — 21.11.2026

## Sonuç: Durum B — bölüm ilişkisi mevcut kabul kriterinde zorunlu
Adı CourseSection olan bir C# sınıfı veya belirli bir CRS tablo tasarımı zorunlu tutulmamıştır. Ancak **ödevin belirtilen ders ve bölümde bulunması** açık kabul kriteridir. Bu iki ifade farklıdır. Yalnız CourseId doğrulaması mevcut kabul kriterini karşılamaz. Yeni bölüm modeli oluşturulmadı, mevcut güvenli ret ve testler korunmuştur.

| Soru | Belge, bölüm ve gereksinim | Sonuç |
|---|---|---|
| Bölüm zorunlu mu? | WBS-1-analiz.md §4.4.9, ASG-FR-01, satır 1009: “Ödev belirtilen derste ve bölümde görünür.”; §4.4.10 T-ASG-02, satır 1021 | Bölüm ilişkisi açıkça zorunlu; yalnızca testlerden doğmuyor |
| Doğrudan ders yeterli mi? | WBS-2-tasarim.md §3.4.1, satır 1122: courseId, sectionId ve “Bölüm aynı derse ait olmalıdır.” | Mevcut tasarımda hayır; bölüm sahipliği de doğrulanmalı |
| Bölüm bazlı oluşturma açık FR mı? | WBS-1 ASG-FR-01 / T-ASG-02; WBS-2 §3.4.2 POST /courses/{courseId}/assignments, satır 1134 | Evet, ASG-FR-01'in kabul kriteridir; ayrı bir CRS-FR CourseSection gereksinimi çıkarılamaz |
| ICourseSectionQuery WBS-2'de var mı? | WBS-2 §3.4.1/2 ilişkiyi tarif eder; belgede ICourseSectionQuery adı yoktur | Arayüz, önceki kullanıcı onayıyla eklenen tamamlayıcı uygulama sözleşmesidir; WBS-2'de isimle tanımlı ortak sözleşme değildir |
| Genel ASG update/delete zorunlu mu? | WBS-1 §4.4.4/9/10: ASG-FR-01/02/03, BR-01/02, NFR-01; WBS-2 §3.4.2; WBS-3 §1.4 ve §2.4 | Hayır. Ödev oluşturma/okuma ve teslim kaydetme/gönderme/listeme kapsamı var. PUT my-submission teslim güncellemesidir; genel ödev PATCH/DELETE gereksinimi değildir. Endpoint eklenmedi |
| A/B hangi kabulü kapsar? | A: ASG-FR-01/02, ASG-BR-01, ASG-FR-03/NFR-01 ve WBS-2 §3.5 GRD akışı; B: bunlara ek WBS-3 §2.4 FILE-FR-01/07/08/10/11, FILE-BR-06/07/08/09 dosya kabulü | A metin, B dosyalı gerçek oluşturma→teslim→değerlendirme zinciridir. Mevcut iki test bütün güvenlik/sınır kriterlerini tek başına kapsamaz; bunlar ayrıca birim/entegrasyon/negatif tarayıcı testlerindedir |

## Gerçek kod
- Assignment.SectionId nullable olmayan int; EF alanı zorunlu ve CourseId/SectionId indeksi var. Fiziksel CRS bölüm FK'sı yoktur; bu durum sahiplik doğrulandığı anlamına gelmez.
- CreateAssignment ve doğrulama pozitif SectionId ister. AssignmentService.CreateAsync gerçek dersi bulur, öğretmen/yönetici yetkisini denetler, BelongsToCourseAsync ile ders-bölüm eşleşmesini arar.
- DI gerçek ICourseDirectory/IEnrolmentQuery/IAssignmentLearnerQuery → CrsService kullanır. Bunlar ders, aktif kayıt ve yükümlü öğrenci bilgisi sağlar; bölüm kimliği/aidiyeti sağlamaz.
- ICourseSectionQuery → UnavailableCourseSectionQuery false döner; yetkili gerçek kullanıcıda oluşturma 422 invalid_section ile reddedilir.
- Bölüm bağımlılığı üretim kodundadır. E2E_SECTION_ID yalnız test girdisidir; gerçek sorgu yokken rastgele sayı vermek çözüm değildir.
- Gerçek CourseId ile mevcut ödevlerin okunması, teslim/yetki ve temizliği çalışır. Test-only mevcut ödev fixture'ları, gerçek oluşturma kabulü yerine gösterilmez.

## En az değişiklik gerektiren yol
Yeni CRS modeli bu görevde istenmediği için eklenmedi. Mevcut WBS korunacaksa ekipten, gerçek bir bölüm kaynağı ve **BelongsToCourseAsync(int sectionId, int courseId, CancellationToken ct) → Task<bool>** sağlayıcısı istenir: yalnız aynı derse ait mevcut bölüm için true; başka ders/bilinmeyen/silinmiş bölüm için false; hata/iptal yayılır, erişim belirsizliğinde kabul edilmez. Scoped DI mevcut Unavailable kaydının yerini alır. Gerçek bölüm kimliğini elde etmek için API veya kontrollü gerçek veri hazırlama yolu ve PostgreSQL sahiplik testleri gerekir.
Bu, Sinem'in mevcut CRS WBS görevinde açıkça atanmış bir CourseSection modeli bulunduğu anlamına gelmez. Sorumluluk ve yeni kapsam ekip kararıdır; önceki belgelerdeki doğrudan “Sinem yapmalı” ifadeleri bu kapsam ayrımını içermiyordu.
Alternatif ders düzeyinde ASG tasarımı ancak ASG-FR-01 kabul kriteri, T-ASG-02 ve WBS-2 §3.4.1/2 değişikliği onaylanırsa uygulanabilir. Onay olmadan section kontrolünü kaldırmak minimum düzeltme değil, gereksinim değişikliğidir.

## GitHub / dal ayrımı
Fetch sonrası origin/main 4d53353 oldu: PR #50 merged; b0c7cdd ve 96e5eaf geldi. Açık PR listesi boş. Yeni FileCourseDeletedExtension genel course alanını temizler; mod_assign ASG kapsamına dokunmaz. Model/migration veya bölüm sorgusu eklemez.
Mevcut dal HEAD a21e18a, önceki bütün commit edilmemiş test/belgeler korunur. Kullanıcının merge yasağı nedeniyle yeni main dalımıza alınmadı. Bu rapordaki yerel test sayıları **a21e18a tabanlı çalışma dalına** aittir; PR #50 testlerinin burada çalıştırıldığı iddia edilmez.

## Değişiklik ve kabul durumu
Bu görevde üretim kaynakları, migration, ortak sözleşmeler ve Playwright beklentileri değiştirilmedi. Önceki gerçek SiteAdmin fixture/test geliştirmesi korundu. Yeni gereksinim değerlendirme raporu ve doğrulama kayıtları eklendi.
Bölüm şartı mevcut belgelerde açık olduğu için iki A/B atlaması kaldırılmadı. ASG-FR-01'in gerçek tam oluşturma kabulü ve bu iki E2E hâlâ tamamlanmış değildir; genel ödev update/delete mevcut WBS kapsamında eksik teknik görev sayılmaz.
Yeni PR, önceki test/fixture ve bu belge değişikliklerini taşıyabilir; tam ASG kabulünün tamamlandığı iddiasıyla açılmamalıdır. PR açılmadı; commit/push/merge yapılmadı.


Son yeniden koşu (a21e18a tabanlı dal): Release başarılı; .NET 995/995 (501 birim + 494 entegrasyon), JS 54/54; Playwright 7 geçti / 2 bölüm bağımlılığı nedeniyle atlandı, 0 başarısız. PostgreSQL 17, gerçek ClamAV ve migration/snapshot regresyonları geçti. SiteAdmin fixture silindi. Whitespace/conflict kontrolleri temiz. Yeni main 4d53353 bu dala merge edilmedi; yeni PR #50 testleri bu toplamda yoktur.
