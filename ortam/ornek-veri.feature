Feature: Moodle Lab ornek veri seti
  Tum ekibin ayni verilerle calismasi icin ortak test verisi.
  Calistirma: ortam/README.md

  Scenario: Ornek veriyi olustur
    Given the following config values are set as admin:
      | sendcoursewelcomemessage | 0 | enrol_manual |

    And the following "categories" exist:
      | name                    | category | idnumber |
      | Mühendislik Fakültesi   | 0        | MUH      |
      | Yazılım Mühendisliği    | MUH      | SENG     |
      | Bilgisayar Mühendisliği | MUH      | CENG     |

    And the following "users" exist:
      | username  | firstname | lastname  | email                 |
      | yonetici1 | Yönetici  | Bir       | yonetici1@example.com |
      | ogretmen1 | Ayşe      | Yılmaz    | ogretmen1@example.com |
      | ogretmen2 | Mehmet    | Demir     | ogretmen2@example.com |
      | asistan1  | Elif      | Kaya      | asistan1@example.com  |
      | danisman1 | Can       | Öztürk    | danisman1@example.com |

    And "10" "users" exist with the following data:
      | username  | ogrenci[count]             |
      | firstname | Öğrenci                    |
      | lastname  | [count]                    |
      | email     | ogrenci[count]@example.com |

    And the following "system role assigns" exist:
      | user      | role    |
      | yonetici1 | manager |

    And the following "courses" exist:
      | fullname              | shortname | category | numsections | initsections |
      | Programlamaya Giriş   | SENG101   | SENG     | 4           | 1            |
      | Veri Yapıları         | SENG201   | SENG     | 4           | 1            |
      | Veritabanı Sistemleri | CENG301   | CENG     | 4           | 1            |

    And the following "course enrolments" exist:
      | user      | course  | role           |
      | ogretmen1 | SENG101 | editingteacher |
      | ogretmen1 | SENG201 | editingteacher |
      | ogretmen2 | CENG301 | editingteacher |
      | asistan1  | SENG101 | teacher        |

    And "10" "course enrolments" exist with the following data:
      | user   | ogrenci[count] |
      | course | SENG101        |
      | role   | student        |

    And "5" "course enrolments" exist with the following data:
      | user   | ogrenci[count] |
      | course | SENG201        |
      | role   | student        |

    And "5" "course enrolments" exist with the following data:
      | user   | ogrenci[count] |
      | course | CENG301        |
      | role   | student        |

    And the following "activities" exist:
      | activity | name           | intro                      | course  | idnumber  | section | assignsubmission_onlinetext_enabled | submissiondrafts |
      | assign   | SENG101 Ödev 1 | Değişkenler ve döngüler    | SENG101 | seng101a1 | 1       | 1                                   | 0                |
      | assign   | SENG101 Ödev 2 | Fonksiyonlar               | SENG101 | seng101a2 | 2       | 1                                   | 1                |
      | assign   | SENG201 Ödev 1 | Bağlı listeler             | SENG201 | seng201a1 | 1       | 1                                   | 0                |
      | assign   | CENG301 Ödev 1 | ER diyagramı               | CENG301 | ceng301a1 | 1       | 1                                   | 0                |

    And the following "activities" exist:
      | activity | name                 | intro              | course  | idnumber  | section |
      | resource | SENG101 Ders Notları | Hafta 1 ders notu  | SENG101 | seng101r1 | 1       |

    And the following "mod_assign > submissions" exist:
      | assign         | user      | onlinetext              |
      | SENG101 Ödev 1 | ogrenci1  | Öğrenci 1 teslim metni  |
      | SENG101 Ödev 1 | ogrenci2  | Öğrenci 2 teslim metni  |
      | SENG101 Ödev 1 | ogrenci3  | Öğrenci 3 teslim metni  |
      | SENG101 Ödev 1 | ogrenci4  | Öğrenci 4 teslim metni  |
      | SENG101 Ödev 1 | ogrenci5  | Öğrenci 5 teslim metni  |
      | SENG101 Ödev 1 | ogrenci6  | Öğrenci 6 teslim metni  |
      | SENG101 Ödev 2 | ogrenci6  | Yarım kalmış taslak     |
      | SENG201 Ödev 1 | ogrenci1  | Bağlı liste çözümü      |
      | SENG201 Ödev 1 | ogrenci2  | Bağlı liste çözümü      |
      | CENG301 Ödev 1 | ogrenci1  | ER diyagramı açıklaması |

    And the following "grade grades" exist:
      | gradeitem      | user     | grade |
      | SENG101 Ödev 1 | ogrenci1 | 85    |
      | SENG101 Ödev 1 | ogrenci2 | 72    |
      | SENG101 Ödev 1 | ogrenci3 | 90    |
      | SENG101 Ödev 1 | ogrenci4 | 55    |
      | SENG201 Ödev 1 | ogrenci1 | 78    |