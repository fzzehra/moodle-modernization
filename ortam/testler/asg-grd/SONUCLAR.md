# Eski Moodle performans ve güvenlik özeti

16 Kasım 2026, Europe/Istanbul. Moodle 5.3.1 (`cfe123f2`), WSL Ubuntu ve resmî moodle-docker. Aşağıdaki değerler saklanan 40 ölçüm koşusundan hesaplandı. 9.770 hedef yanıt doğrulandı. Hata oranı tüm koşularda %0. Medyan ve p95, beş tekrarın ilgili istatistiğinin medyanıdır. CPU için %100 bir mantıksal işlemciyi temsil eder. Her 50 VU p95 değeri hedef sistem için tanımlanan 1.000 ms eşiğinin üzerindedir.

## k6 ve Docker kaynak kullanımı

| Sayfa | VU | Ort. ms | Medyan ms | p95 ms | İstek/sn | Hata % |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Ders kataloğu | 5 | 129.61 | 127.85 | 143.16 | 4.67 | 0.00 |
| Ders kataloğu | 50 | 678.80 | 653.01 | 1249.16 | 31.64 | 0.00 |
| SENG101 ders sayfası | 5 | 204.99 | 213.82 | 237.54 | 4.33 | 0.00 |
| SENG101 ders sayfası | 50 | 974.79 | 938.56 | 1523.29 | 27.05 | 0.00 |
| SENG101 Ödev 1 | 5 | 276.58 | 287.14 | 341.27 | 4.00 | 0.00 |
| SENG101 Ödev 1 | 50 | 1079.36 | 880.97 | 1615.23 | 25.57 | 0.00 |
| SENG101 öğrenci not raporu | 5 | 213.00 | 212.73 | 252.95 | 4.33 | 0.00 |
| SENG101 öğrenci not raporu | 50 | 865.81 | 848.88 | 1469.89 | 28.67 | 0.00 |

| Sayfa | VU | Container | CPU ort. % | CPU tepe % | RAM ort. MiB | RAM tepe MiB | Örnek |
| --- | ---: | --- | ---: | ---: | ---: | ---: | ---: |
| SENG101 Ödev 1 | 5 | PostgreSQL | 20.95 | 34.83 | 152.48 | 167.60 | 25 |
| SENG101 Ödev 1 | 5 | Moodle web | 60.14 | 86.78 | 400.60 | 603.60 | 25 |
| SENG101 Ödev 1 | 50 | PostgreSQL | 173.21 | 195.50 | 191.60 | 225.40 | 25 |
| SENG101 Ödev 1 | 50 | Moodle web | 551.39 | 626.75 | 750.58 | 793.80 | 25 |
| SENG101 ders sayfası | 5 | PostgreSQL | 12.73 | 17.08 | 149.09 | 164.70 | 25 |
| SENG101 ders sayfası | 5 | Moodle web | 64.16 | 80.83 | 389.96 | 549.40 | 25 |
| SENG101 ders sayfası | 50 | PostgreSQL | 104.58 | 124.32 | 180.35 | 211.60 | 24 |
| SENG101 ders sayfası | 50 | Moodle web | 589.44 | 679.27 | 757.33 | 798.10 | 24 |
| SENG101 öğrenci not raporu | 5 | PostgreSQL | 17.73 | 21.27 | 152.10 | 152.80 | 25 |
| SENG101 öğrenci not raporu | 5 | Moodle web | 58.56 | 74.62 | 399.13 | 597.20 | 25 |
| SENG101 öğrenci not raporu | 50 | PostgreSQL | 141.07 | 200.82 | 182.12 | 205.60 | 25 |
| SENG101 öğrenci not raporu | 50 | Moodle web | 573.70 | 697.29 | 743.26 | 779.80 | 25 |
| Ders kataloğu | 5 | PostgreSQL | 7.91 | 11.23 | 146.90 | 152.90 | 25 |
| Ders kataloğu | 5 | Moodle web | 46.19 | 53.76 | 347.65 | 349.10 | 25 |
| Ders kataloğu | 50 | PostgreSQL | 99.23 | 148.77 | 169.60 | 205.80 | 22 |
| Ders kataloğu | 50 | Moodle web | 560.41 | 726.62 | 726.42 | 742.10 | 22 |

## OWASP ZAP baseline

ZAP 2.17.0, imaj digest’i `sha256:9624669db72f54d86c4b46a7064518bd9bf79d8c61a4c31befa9923489f328af`. Tarama yalnızca yerel `http://localhost:8000` üzerinde kimlik doğrulamasız tamamlandı. Konsol 47 URL, 17 uyarı, 0 FAIL, 51 PASS ve çıkış kodu `2` gösterdi. Apache ve PHP sürüm eşleştirmeleri yüksek risk, düşük güven etiketi aldı. CSP ve CSRF belirteci uyarıları orta risk etiketi aldı. Bunlar pasif tarama bulgularıdır. Oturum gerektiren sayfalar kapsam dışındaydı.

Temizlenmiş kaynaklar [k6 koşuları ve Docker örnekleri](evidence/k6/), [ZAP JSON özeti](evidence/zap/baseline.json), [Behat günlüğü](evidence/behat.log) ve [son doğrulama günlükleri](evidence/final-check/) altında yer alır. [Paylaşım denetimi](evidence/PAYLASIM-DENETIMI.md) ayıklama ve hariç bırakma kararlarını listeler. Orijinal ham çıktılar repo dışında `~/moodle-lab/evidence/` altında saklanır.

Ayrıntılı yöntem ve ortam [WBS-1 §8](../../../docs/WBS-1-analiz.md#8-eski-sistemin-başlangıç-ölçümleri-aslı-ceren) bölümündedir.
