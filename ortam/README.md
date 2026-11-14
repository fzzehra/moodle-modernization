# Geliştirme ve Test Ortamı (WBS-1 §1)

Herkes Moodle'ı kendi bilgisayarında, **aynı sürüm, aynı kod ve aynı örnek veriyle** çalıştırır.

| | |
| --- | --- |
| Moodle sürümü | 5.3.1 (LTS) |
| Kaynak kod revizyonu | `cfe123f26fc7071981fe949cb16cd5a08a8234f1` (Moodle release 5.3.1, 12.11.2026) |
| Çalıştırma aracı | [moodlehq/moodle-docker](https://github.com/moodlehq/moodle-docker) |
| PHP | 8.3 |
| Veritabanı | PostgreSQL 17 |
| Adres | http://localhost:8000 |
| Yönetici | `admin` / `test` |

## 0. Ön koşul

- Docker Desktop kurulu ve **Engine running** durumunda olmalı.
- **Windows:** Bütün komutlar **WSL (Ubuntu) içinde** çalıştırılır ve Moodle kodu Ubuntu'nun kendi diskinde (`~/...`) durur. Kodu `C:\...` altına koyup oradan çalıştırmak **çok yavaş**: veritabanı kurulumu WSL'de 3 dakika, `C:\` üzerinden yaklaşık 2 saat sürüyor ve her sayfa dakikalarca açılıyor.
- **Mac:** Komutlar doğrudan Terminal'de çalıştırılır; WSL adımları (1. bölüm) atlanır.

## 1. Windows: Ubuntu (WSL) kurulumu (bir kerelik)

PowerShell'de:

```powershell
wsl --install -d Ubuntu
```

Kullanıcı adı (küçük harf, boşluksuz) ve şifre belirleyin.

Docker Desktop → **Settings → Resources → WSL integration** → **Ubuntu** anahtarını açın → **Apply & restart**.

Ubuntu'da Docker yetkisi verin:

```bash
sudo groupadd -f docker
sudo usermod -aG docker $USER
exit
```

Ardından PowerShell'de `wsl --shutdown` çalıştırın. Docker Desktop'ı tamamen kapatın (balina simgesi → **Quit Docker Desktop**), yeniden açın ve **Engine running** yazmasını bekleyin. Sonra `wsl -d Ubuntu` ile Ubuntu'yu açın. `docker ps` hata vermeden boş bir tablo gösteriyorsa hazırsınız.

> **Not:** Bu sırada "WSL integration with distro 'Ubuntu' unexpectedly stopped" penceresi çıkabilir. Docker Desktop yeniden açılınca kendiliğinden kaybolur.

## 2. Kaynak kodu indirme (bir kerelik)

```bash
mkdir -p ~/moodle-lab && cd ~/moodle-lab
git clone https://github.com/moodlehq/moodle-docker.git
git clone -b MOODLE_503_STABLE --depth 1 https://github.com/moodle/moodle.git
cd moodle
git fetch --depth 1 origin cfe123f26fc7071981fe949cb16cd5a08a8234f1
git checkout cfe123f26fc7071981fe949cb16cd5a08a8234f1
git log -1 --format="%H %cd"   # cfe123f26fc7... olmalı
```

## 3. Ayarlar (bir kerelik)

```bash
echo 'export MOODLE_DOCKER_WWWROOT=~/moodle-lab/moodle' >> ~/.bashrc
echo 'export MOODLE_DOCKER_DB=pgsql' >> ~/.bashrc
echo 'export MOODLE_DOCKER_PHP_VERSION=8.3' >> ~/.bashrc
source ~/.bashrc
cp ~/moodle-lab/moodle-docker/config.docker-template.php ~/moodle-lab/moodle/config.php
```

> **Mac:** Varsayılan kabuk zsh ise `~/.bashrc` yerine `~/.zshrc` kullanın.

## 4. Moodle'ı başlatma ve kurma

```bash
cd ~/moodle-lab/moodle-docker
bin/moodle-docker-compose up -d
bin/moodle-docker-wait-for-db
bin/moodle-docker-compose exec webserver php admin/cli/install_database.php --agree-license --fullname=MoodleLab --shortname=moodlelab --summary=Test --adminpass=test --adminemail=admin@example.com
```

İlk `up -d` Docker imajlarını indirdiği için birkaç dakika sürer. Kurulum yaklaşık 3 dakika sürer ve **"Installation completed successfully."** ile biter.

## 5. Örnek veri için geliştirici kütüphaneleri (bir kerelik)

```bash
bin/moodle-docker-compose exec webserver bash -c "curl -sS https://getcomposer.org/installer -o /tmp/composer-setup.php && php /tmp/composer-setup.php --install-dir=/tmp --quiet && php /tmp/composer.phar install --no-interaction"
```

Baştaki "dubious ownership" uyarısı zararsızdır.

## 6. Örnek veriyi yükleme

Takım reposunun bulunduğu yere göre (Windows'ta repo genelde `C:\dev\moodle-modernization`, Ubuntu'dan `/mnt/c/dev/moodle-modernization` olarak görünür):

```bash
cp /mnt/c/dev/moodle-modernization/ortam/ornek-veri.feature ~/moodle-lab/moodle/ornek-veri.feature
bin/moodle-docker-compose exec webserver php public/admin/tool/generator/cli/runtestscenario.php --feature=/var/www/html/ornek-veri.feature --disable-composer
```

Sonuç **"14/14 steps executed successfully."** olmalı.

### Örnek verinin içeriği

| Tür | İçerik |
| --- | --- |
| Kategoriler | Mühendislik Fakültesi › Yazılım Mühendisliği (SENG), Bilgisayar Mühendisliği (CENG) |
| Kullanıcılar | `yonetici1` (site yöneticisi), `ogretmen1`, `ogretmen2`, `asistan1`, `danisman1`, `ogrenci1`–`ogrenci10` |
| Şifreler | Kullanıcı adıyla aynı (örn. `ogrenci1` / `ogrenci1`) |
| Dersler | SENG101 Programlamaya Giriş, SENG201 Veri Yapıları, CENG301 Veritabanı Sistemleri |
| Kayıtlar | SENG101: ogretmen1 (öğretmen), asistan1 (düzenleme yetkisiz öğretmen), 10 öğrenci · SENG201: ogretmen1, 5 öğrenci · CENG301: ogretmen2, 5 öğrenci |
| Ödevler | SENG101 Ödev 1 ve 2, SENG201 Ödev 1, CENG301 Ödev 1 (çevrimiçi metin teslimi) |
| Dosya | SENG101 Ders Notları (kaynak dosya) |
| Teslimler | SENG101 Ödev 1: 6 öğrenci teslim etti · SENG101 Ödev 2: ogrenci6 taslakta bıraktı · SENG201 Ödev 1: 2 öğrenci · CENG301 Ödev 1: 1 öğrenci |
| Notlar | SENG101 Ödev 1: ogrenci1–4 (85, 72, 90, 55) · SENG201 Ödev 1: ogrenci1 (78) |

Böylece "teslim edilmiş ve notlandırılmış", "teslim edilmiş ama notlandırılmamış", "taslakta kalmış" ve "hiç teslim edilmemiş" durumlarının hepsi test edilebilir.

## 7. Günlük kullanım

```bash
cd ~/moodle-lab/moodle-docker
bin/moodle-docker-compose up -d     # başlat
bin/moodle-docker-compose stop      # durdur (veri korunur)
```

## 8. Sıfırdan kurulum (temiz veri)

Veriyi bozduysanız veya örnek veriyi baştan yüklemek istiyorsanız:

```bash
cd ~/moodle-lab/moodle-docker
bin/moodle-docker-compose down      # container'ları ve veritabanını siler
```

Sonra 4. ve 6. adımları tekrarlayın. 5. adım tekrar gerekmez.

## Sık karşılaşılan sorunlar

| Sorun | Çözüm |
| --- | --- |
| `permission denied ... docker.sock` | 1. bölümdeki Docker yetkisi adımları yapılmamış veya Ubuntu yeniden başlatılmamış. |
| `The command 'docker' could not be found in this WSL 2 distro` | Docker Desktop'ı tamamen kapatıp açın, **Engine running** olduktan sonra Ubuntu'yu açın. |
| `MOODLE_DOCKER_WWWROOT is not set` | 3. bölümdeki ayarlar yüklenmemiş: `source ~/.bashrc`. |
| Sayfalar dakikalarca açılmıyor (Windows) | Moodle kodu `C:\` altında. WSL içine (`~/moodle-lab`) taşıyın. |
| Windows PowerShell'de `bin\moodle-docker-wait-for-db.cmd` bulunamıyor | Bu betiğin Windows sürümü yok; komutları WSL'de çalıştırın. |