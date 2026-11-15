# Ödev ve değerlendirme testleri

Bu dizin, Moodle 5.3.1 üzerinde ASG ve GRD karakterizasyonunu ve WBS-1 §6.3 başlangıç ölçümünü yeniden çalıştırır. Yalnızca ekibin yerel, sentetik Moodle ortamında kullanılır.

## Ön koşullar

- WSL Ubuntu içinde [ortam kurulumunu](../../README.md) tamamlayın. PostgreSQL ve PHP 8.3 ile resmî moodle-docker kullanın.
- Moodle revizyonu `cfe123f26fc7071981fe949cb16cd5a08a8234f1`. Ölçümde kullanılan moodle-docker revizyonu `f0583ec8`. Yerel Moodle adresi `http://localhost:8000`.
- [Ortak feature dosyasını](../../ornek-veri.feature) yükleyin. Ölçümdeki SHA-256 `e79ec1e26605e0257783193d9fed45b22ec03ff0caf4132b1fdb517340ce8523`. Docker web, PostgreSQL ve Selenium çalışır durumda olmalı. Composer test bağımlılıklarını ortam rehberine göre hazırlayın.
- Ubuntu’da Bash, Python 3.9+, Docker CLI, `flock` ve performans için PATH üzerinde k6 **2.4.0** gerekir.
- Varsayılan lab kökü `$HOME/moodle-lab`. Başka bir konum için `MOODLE_LAB` ayarlayın.

Test hesabı parolalarını repo dışında, yalnızca sahibinin okuyabildiği bir JSON dosyasında hesap adı → parola eşlemesi olarak hazırlayın. Anahtarlar `ogrenci1`–`ogrenci10` ve `ogretmen1`. Aşağıdaki komutlar repo kökünden çalıştırılır.

```bash
export ASG_GRD_CREDENTIALS="$(cat "$HOME/.config/asg-grd/credentials.json")"
```

## Çalıştırma

Testleri sırayla, başka bir benchmark veya Behat koşusu çalışmıyorken yürütün. Karşılaştırılabilir performans ölçümü için aynı makineyi, aynı Docker koşullarını ve aynı fixture başlangıç durumunu kullanın.

```bash
bash ortam/testler/asg-grd/benchmark.sh
bash ortam/testler/asg-grd/functional.sh
unset ASG_GRD_CREDENTIALS
```

Her çalıştırıcı isteğe bağlı olarak repo dışındaki çıktı üst dizininin mutlak yolunu ilk argüman olarak alır. Varsayılan `$MOODLE_LAB/asg-grd-runs` veya lab değişkeni verilmemişse `$HOME/moodle-lab/asg-grd-runs`. Her koşu kendi `run.*` dizinini oluşturur.

- **Performans:** `benchmark.sh`, `pages.js` ile dört sayfada 5 ve 50 VU için 10 sn ısınma ve beşer 15 sn ölçüm yapar. Koşular arasında 5 sn ve hedef istekleri arasında VU başına 1 sn bekler. 50 VU, 10 öğrencinin beşer bağımsız oturumunu kullanır. Oturum açma ölçüm penceresinin dışındadır. Hedef URL ve HTML sayfa kimliği kontrol edilir. Ham noktalar, özetler ve Docker istatistikleri `k6/` altında üretilir. `summarize.py` bunlardan `results.md` ve `per-run.json` üretir.
- **İşlevsel:** `functional.sh`, geçici PostgreSQL kopyasını kullanır ve yerel web uygulamasını kısa süre bu kopyaya yönlendirir. Bu sırada uygulamayı başka işler için kullanmayın. Yetki, kesim ve korumalı not kontrollerinden sonra yapılandırmayı geri yükler. Moodle’ın ayrı Behat tablolarında `characterization.feature` ve sabit revizyondaki gizleme, kilit ve not değiştirme senaryoları çalışır. Deneme bağlantısı SQL ile doğrulanır. Bir test başarısız olursa çıktı dizinindeki ilgili günlüğü inceleyin. İşlem zorla sonlandırılırsa lab kökündeki `.asg-grd-config.*` yedeğinden Moodle `config.php` dosyasını geri yükleyin.

Ham çıktı, çerez, parola dosyası, veritabanı kopyası ve tam ZAP raporu ekip deposu dışında tutulur. Ekip içi paylaşımda ham arşivin erişimini sınırlandırın ve paylaşmadan önce oturum verisi ve kişisel bilgileri denetleyin.

[Ölçüm ve güvenlik özeti](SONUCLAR.md) · [WBS-1 §8](../../../docs/WBS-1-analiz.md#8-eski-sistemin-başlangıç-ölçümleri-aslı-ceren)
