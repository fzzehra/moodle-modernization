# Paylaşım arşivi dosya denetimi

Otomatik eşleşmeler alan adlarını ve sentetik hesapları da kapsar. Eşleşme değerleri bu listede yer almaz.

| Kaynak dosya | İşlem | Açıklama | Eşleşme türleri |
| --- | --- | --- | --- |
| behat-enable.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| behat-init.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| behat-restore-list.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| behat.log | Dahil | Sonuç ve test adımları korundu. E-posta, hesap adı, kişisel yollar ve eşleşen değerler ayıklandı. | email, fixture_identity |
| benchmark.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | private_path |
| benchmark.sh | Hariç | Betik, fixture veya yapılandırma. Sürüm kontrollü test paketi ayrı tutulur. | fixture_identity |
| characterization.feature | Hariç | Betik, fixture veya yapılandırma. Sürüm kontrollü test paketi ayrı tutulur. | email, fixture_identity |
| composer.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| final-check/behat-cleanup.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| final-check/behat-enable.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| final-check/clone.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| final-check/cutoff-first.log | Hariç | İlk deneme veya tanılama koşusu. Nihai sonuçlardan ayrı. | — |
| final-check/cutoff.log | Dahil | Sonuç ve test adımları korundu. E-posta, hesap adı, kişisel yollar ve eşleşen değerler ayıklandı. | — |
| final-check/cutoff.py | Hariç | Betik, fixture veya yapılandırma. Sürüm kontrollü test paketi ayrı tutulur. | fixture_identity |
| final-check/grade-attempt.log | Dahil | Sonuç ve test adımları korundu. E-posta, hesap adı, kişisel yollar ve eşleşen değerler ayıklandı. | fixture_identity |
| final-check/grade-clone.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| final-check/grade-protection.log | Dahil | Sonuç ve test adımları korundu. E-posta, hesap adı, kişisel yollar ve eşleşen değerler ayıklandı. | — |
| final-check/grade-protection.php | Hariç | Betik, fixture veya yapılandırma. Sürüm kontrollü test paketi ayrı tutulur. | fixture_identity |
| final-check/grade_override.log | Dahil | Sonuç ve test adımları korundu. E-posta, hesap adı, kişisel yollar ve eşleşen değerler ayıklandı. | email, fixture_identity |
| final-check/grading.feature | Hariç | Betik, fixture veya yapılandırma. Sürüm kontrollü test paketi ayrı tutulur. | email, fixture_identity |
| final-check/grading.log | Dahil | Sonuç ve test adımları korundu. E-posta, hesap adı, kişisel yollar ve eşleşen değerler ayıklandı. | email, fixture_identity |
| final-check/hidden_grades.log | Dahil | Sonuç ve test adımları korundu. E-posta, hesap adı, kişisel yollar ve eşleşen değerler ayıklandı. | email, fixture_identity |
| final-check/locking.log | Dahil | Sonuç ve test adımları korundu. E-posta, hesap adı, kişisel yollar ve eşleşen değerler ayıklandı. | fixture_identity |
| final-check/production-permissions.log | Dahil | Sonuç ve test adımları korundu. E-posta, hesap adı, kişisel yollar ve eşleşen değerler ayıklandı. | — |
| final-check/verify-pages-production.py | Hariç | Betik, fixture veya yapılandırma. Sürüm kontrollü test paketi ayrı tutulur. | credential_fields, session_or_token_fields, fixture_identity |
| fixture-final-counts.txt | Dahil | Sonuç ve test adımları korundu. E-posta, hesap adı, kişisel yollar ve eşleşen değerler ayıklandı. | — |
| fixture-submission-status.txt | Dahil | Sonuç ve test adımları korundu. E-posta, hesap adı, kişisel yollar ve eşleşen değerler ayıklandı. | — |
| fixture.log | Dahil | Sonuç ve test adımları korundu. E-posta, hesap adı, kişisel yollar ve eşleşen değerler ayıklandı. | email, fixture_identity |
| functional-pages-exit.txt | Dahil | Sonuç ve test adımları korundu. E-posta, hesap adı, kişisel yollar ve eşleşen değerler ayıklandı. | — |
| functional-pages.log | Dahil | Sonuç ve test adımları korundu. E-posta, hesap adı, kişisel yollar ve eşleşen değerler ayıklandı. | — |
| functional.sh | Hariç | Betik, fixture veya yapılandırma. Sürüm kontrollü test paketi ayrı tutulur. | fixture_identity |
| initial-attempt/behat-listed-path.log | Hariç | İlk deneme veya tanılama koşusu. Nihai sonuçlardan ayrı. | — |
| initial-attempt/behat-path.log | Hariç | İlk deneme veya tanılama koşusu. Nihai sonuçlardan ayrı. | — |
| initial-attempt/benchmark.log | Hariç | İlk deneme veya tanılama koşusu. Nihai sonuçlardan ayrı. | — |
| initial-attempt/k6/list-1-smoke.log | Hariç | İlk deneme veya tanılama koşusu. Nihai sonuçlardan ayrı. | — |
| initial-attempt/k6/list-1-smoke.points.json | Hariç | İlk deneme veya tanılama koşusu. Nihai sonuçlardan ayrı. | session_or_token_fields |
| initial-attempt/k6/list-1-smoke.stats.txt | Hariç | İlk deneme veya tanılama koşusu. Nihai sonuçlardan ayrı. | — |
| initial-attempt/k6/list-1-smoke.summary.json | Hariç | İlk deneme veya tanılama koşusu. Nihai sonuçlardan ayrı. | — |
| initial-attempt/redirect/benchmark.log | Hariç | İlk deneme veya tanılama koşusu. Nihai sonuçlardan ayrı. | private_path |
| k6/assignment-5-1.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/assignment-5-1.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/assignment-5-1.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/assignment-5-1.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/assignment-5-2.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/assignment-5-2.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/assignment-5-2.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/assignment-5-2.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/assignment-5-3.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/assignment-5-3.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/assignment-5-3.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/assignment-5-3.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/assignment-5-4.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/assignment-5-4.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/assignment-5-4.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/assignment-5-4.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/assignment-5-5.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/assignment-5-5.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/assignment-5-5.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/assignment-5-5.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/assignment-5-warmup.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/assignment-5-warmup.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/assignment-5-warmup.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/assignment-5-warmup.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/assignment-50-1.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/assignment-50-1.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/assignment-50-1.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/assignment-50-1.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/assignment-50-2.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/assignment-50-2.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/assignment-50-2.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/assignment-50-2.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/assignment-50-3.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/assignment-50-3.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/assignment-50-3.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/assignment-50-3.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/assignment-50-4.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/assignment-50-4.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/assignment-50-4.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/assignment-50-4.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/assignment-50-5.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/assignment-50-5.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/assignment-50-5.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/assignment-50-5.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/assignment-50-warmup.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/assignment-50-warmup.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/assignment-50-warmup.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/assignment-50-warmup.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/course-5-1.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/course-5-1.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/course-5-1.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/course-5-1.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/course-5-2.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/course-5-2.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/course-5-2.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/course-5-2.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/course-5-3.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/course-5-3.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/course-5-3.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/course-5-3.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/course-5-4.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/course-5-4.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/course-5-4.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/course-5-4.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/course-5-5.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/course-5-5.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/course-5-5.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/course-5-5.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/course-5-warmup.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/course-5-warmup.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/course-5-warmup.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/course-5-warmup.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/course-50-1.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/course-50-1.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/course-50-1.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/course-50-1.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/course-50-2.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/course-50-2.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/course-50-2.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/course-50-2.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/course-50-3.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/course-50-3.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/course-50-3.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/course-50-3.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/course-50-4.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/course-50-4.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/course-50-4.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/course-50-4.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/course-50-5.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/course-50-5.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/course-50-5.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/course-50-5.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/course-50-warmup.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/course-50-warmup.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/course-50-warmup.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/course-50-warmup.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/environment.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | ip_address |
| k6/grades-5-1.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/grades-5-1.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/grades-5-1.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/grades-5-1.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/grades-5-2.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/grades-5-2.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/grades-5-2.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/grades-5-2.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/grades-5-3.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/grades-5-3.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/grades-5-3.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/grades-5-3.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/grades-5-4.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/grades-5-4.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/grades-5-4.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/grades-5-4.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/grades-5-5.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/grades-5-5.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/grades-5-5.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/grades-5-5.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/grades-5-warmup.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/grades-5-warmup.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/grades-5-warmup.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/grades-5-warmup.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/grades-50-1.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/grades-50-1.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/grades-50-1.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/grades-50-1.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/grades-50-2.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/grades-50-2.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/grades-50-2.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/grades-50-2.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/grades-50-3.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/grades-50-3.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/grades-50-3.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/grades-50-3.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/grades-50-4.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/grades-50-4.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/grades-50-4.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/grades-50-4.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/grades-50-5.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/grades-50-5.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/grades-50-5.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/grades-50-5.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/grades-50-warmup.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/grades-50-warmup.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/grades-50-warmup.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/grades-50-warmup.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/list-1-smoke.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/list-1-smoke.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/list-1-smoke.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/list-1-smoke.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/list-5-1.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/list-5-1.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/list-5-1.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/list-5-1.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/list-5-2.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/list-5-2.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/list-5-2.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/list-5-2.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/list-5-3.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/list-5-3.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/list-5-3.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/list-5-3.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/list-5-4.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/list-5-4.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/list-5-4.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/list-5-4.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/list-5-5.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/list-5-5.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/list-5-5.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/list-5-5.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/list-5-warmup.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/list-5-warmup.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/list-5-warmup.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/list-5-warmup.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/list-50-1.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/list-50-1.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/list-50-1.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/list-50-1.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/list-50-2.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/list-50-2.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/list-50-2.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/list-50-2.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/list-50-3.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/list-50-3.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/list-50-3.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/list-50-3.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/list-50-4.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/list-50-4.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/list-50-4.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/list-50-4.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/list-50-5.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/list-50-5.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/list-50-5.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/list-50-5.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/list-50-warmup.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| k6/list-50-warmup.points.json | Dahil | Ölçüm adı, zaman ve değer korundu. URL, istek adı ve diğer etiketler çıkarıldı. | session_or_token_fields |
| k6/list-50-warmup.stats.txt | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6/list-50-warmup.summary.json | Dahil | Metrikler korundu. setup_data, root_group, options ve state çıkarıldı. | k6_setup_data |
| k6/per-run.json | Dahil | Tekrar başına hesaplanan ölçüm değerleri korundu. | — |
| k6/results.md | Dahil | Docker örnekleri veya ölçüm özeti korundu. Yerel tanımlayıcılar ayıklandı. | — |
| k6-download-url.txt | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
| pages.js | Hariç | Betik, fixture veya yapılandırma. Sürüm kontrollü test paketi ayrı tutulur. | credential_fields, session_or_token_fields |
| smoke/assignment.json | Hariç | İlk deneme veya tanılama koşusu. Nihai sonuçlardan ayrı. | — |
| smoke/assignment.log | Hariç | İlk deneme veya tanılama koşusu. Nihai sonuçlardan ayrı. | — |
| smoke/course.json | Hariç | İlk deneme veya tanılama koşusu. Nihai sonuçlardan ayrı. | — |
| smoke/course.log | Hariç | İlk deneme veya tanılama koşusu. Nihai sonuçlardan ayrı. | — |
| smoke/grades.json | Hariç | İlk deneme veya tanılama koşusu. Nihai sonuçlardan ayrı. | — |
| smoke/grades.log | Hariç | İlk deneme veya tanılama koşusu. Nihai sonuçlardan ayrı. | — |
| smoke/list.json | Hariç | İlk deneme veya tanılama koşusu. Nihai sonuçlardan ayrı. | — |
| smoke/list.log | Hariç | İlk deneme veya tanılama koşusu. Nihai sonuçlardan ayrı. | — |
| summarize.py | Hariç | Betik, fixture veya yapılandırma. Sürüm kontrollü test paketi ayrı tutulur. | — |
| verify-pages.py | Hariç | Betik, fixture veya yapılandırma. Sürüm kontrollü test paketi ayrı tutulur. | credential_fields, session_or_token_fields, fixture_identity |
| zap/baseline.html | Hariç | Etkin HTML raporu ve ayrıntılı istek kanıtı. Temiz JSON ve konsol özeti kullanıldı. | session_or_token_fields, email |
| zap/baseline.json | Dahil | Uyarı adı, risk, güven, adet, yöntem ve URL yolu korundu. Sorgu, kanıt ve eşleşme metinleri çıkarıldı. | session_or_token_fields, email |
| zap/baseline.log | Dahil | Kural ve konsol özeti korundu. Alt satırlardaki istek örnekleri ve çalışma ayrıntıları çıkarıldı. | private_path |
| zap/baseline.sh | Hariç | Betik, fixture veya yapılandırma. Sürüm kontrollü test paketi ayrı tutulur. | credential_fields |
| zap/exit-code.txt | Dahil | Sonuç ve test adımları korundu. E-posta, hesap adı, kişisel yollar ve eşleşen değerler ayıklandı. | — |
| zap/image.txt | Dahil | Sonuç ve test adımları korundu. E-posta, hesap adı, kişisel yollar ve eşleşen değerler ayıklandı. | — |
| zap/loopback.py | Hariç | Betik, fixture veya yapılandırma. Sürüm kontrollü test paketi ayrı tutulur. | ip_address |
| zap/zap.yaml | Hariç | Betik, fixture veya yapılandırma. Sürüm kontrollü test paketi ayrı tutulur. | private_path |
| zap-image.log | Hariç | Kurulum/tanılama çıktısı veya yeniden üretim kaynağı. Sonuç kanıtı paketi dışında. | — |
