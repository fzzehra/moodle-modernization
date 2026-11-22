# FILE-NFR-01 / T-FILE-15: yeni sistem performans ölçümü

**Yöntem:** Eski sistem ölçümüyle (WBS-1 §4.6.10) aynı yöntem: 50 sanal kullanıcı **tek bir paylaşılan
oturumu** kullanarak aynı dosyayı indirir (`GET /api/v1/files/{id}/content`). Dosya: `ogretmen1`'in özel
dosyası `performans-ornek.txt` (234.428 bayt, ~229 KiB — WBS-2 §3.6'daki örnek ders notu büyüklüğüne
yakın; eski ölçümdeki tam 1 MB `olcum.bin` değil, bkz. `FilesSeedContributor`). Araç: `grafana/k6`
(konteyner, `host.docker.internal` üzerinden `docker/docker-compose.yml` ile ayağa kaldırılmış uygulamaya).
1 ısınma koşusu (10 sn, atıldı) + 3 ölçülen tekrar (30 sn), her biri 50 sanal kullanıcı.

| Koşu | VU | Süre | İstek sayısı | Ort. ms | Medyan ms | p95 ms | Hata % |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 50 | 30 sn | 1500 | 28.51 | 18.75 | 95.64 | 0.00 |
| 2 | 50 | 30 sn | 1500 | 27.15 | 21.00 | 62.84 | 0.00 |
| 3 | 50 | 30 sn | 1450 | 37.54 | 23.32 | 150.75 | 0.00 |

**Sonuç: FILE-NFR-01 karşılanıyor.** Üç tekrarda da p95 ≤ 1.000 ms hedefinin çok altında (en kötü tekrar:
150,75 ms). Hata oranı üç tekrarda da %0 (toplam 4.450 istek). Eski sistem başlangıç değeriyle (Moodle,
p95 = 20,4 sn, bkz. WBS-1 §4.6.10) karşılaştırıldığında yeni sistem bu yükte iki büyüklük mertebesi daha
hızlı; bu fark büyük ölçüde eski ölçümdeki paylaşılan-oturum kilidi darboğazından ve PHP/Moodle çalışma
zamanının genel yüküdür — ASP.NET Core Kestrel + akış hâlinde `Results.File` böyle bir kilide girmiyor.

**Ortam:** `environment.txt` (tarih, Docker kaynakları, PostgreSQL sürümü, k6 sürümü, commit, dosya bilgisi).

**Nasıl tekrar çalıştırılır:**
```bash
SITEADMIN_USERNAME=... SITEADMIN_PASSWORD=... SEED_SAMPLE_DATA=true \
  docker compose -f docker/docker-compose.yml up -d --build
# file_entry tablosunda "performans-ornek.txt" kaydının Id'sini FILE_ID olarak kullanın (örnek veriyle 1).
docker run --rm -i --add-host=host.docker.internal:host-gateway \
  -e BASE=http://host.docker.internal:8080 -e FILE_ID=1 -e VUS=50 -e SECONDS_PER_RUN=30 \
  grafana/k6 run --quiet - < ortam/testler/file-nfr01/download.js
```

**Not:** k6 betiği bind mount gerektirmeden stdin'den çalıştırılır (`k6 run - < dosya.js`); bazı Docker
Desktop kurulumlarında repo dizini varsayılan paylaşılan yollar listesinde olmayabilir.
