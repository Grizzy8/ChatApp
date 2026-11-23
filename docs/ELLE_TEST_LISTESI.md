# Elle Test Listesi

Teslimden önce iki ya da üç istemciyle (A, B, C) uçtan uca denenecek senaryolar.
Her maddenin yanındaki kutu işaretlenerek ilerlenir.

## Hazırlık
- [ ] `ChatServer` başlatıldı, port 5000 dinleniyor (durum çubuğunda yazıyor).
- [ ] `ChatClient` üç kez açıldı (A, B, C). Sunucu adresi `127.0.0.1`, port `5000`.
- [ ] Başka bir bilgisayardan denenecekse güvenlik duvarı 5000 portuna izin veriyor.

## 1. Hesap ve giriş
- [ ] A "Kayıt Ol" ile yeni hesap açar; parola tekrarı uyuşmazsa uyarı çıkar.
- [ ] Kısa/geçersiz kullanıcı adı ve kısa parola reddedilir, neden gösterilir.
- [ ] Aynı kullanıcı adıyla ikinci kayıt reddedilir.
- [ ] Yanlış parolayla giriş reddedilir; art arda çok deneme hız sınırına takılır.
- [ ] Aynı hesapla ikinci kez giriş reddedilir (zaten bağlı).
- [ ] Giriş sonrası alt sol profil şeridinde kullanıcı adı ve ID görünür; "Kopyala" ID'yi panoya alır.
- [ ] "Çıkış" giriş ekranına döner.

## 2. Arama ve kişiler
- [ ] Arama kutusuna tam kullanıcı adı ya da ID yazınca kişi dışı kullanıcı da bulunur.
- [ ] Kısmi ad ("kir") yazınca kişi dışı kimse bulunmaz.
- [ ] Bulunan kullanıcıya tıklayınca "kişilere ekle" onayı çıkar; onaylayınca "Kişiler" kartına eklenir.
- [ ] Kendini arayınca sonuç çıkmaz.
- [ ] Kişi kartı açılıp kapanır; kişiye tıklayınca sohbet açılır; sağ tıkla kaldırılır.
- [ ] Kişinin çevrimiçi/çevrimdışı durumu noktayla görünür; yalnızca kişiler ve ortak grup üyeleri birbirini görür.

## 3. Özel mesaj
- [ ] A → B mesajı B'de solda, A'da sağda görünür; saat ve gün ayracı doğru.
- [ ] B sohbeti açık değilken sohbet listesinde okunmamış olarak belirgin görünür; açınca okunmuş olur.
- [ ] Sol listede sohbetler son mesaja göre sıralıdır; önizleme/sayaç yoktur.
- [ ] B çıkış yapar, A mesaj yollar; B girince mesaj teslim edilir.
- [ ] 500 karakterden uzun mesaj yazılamaz; boş mesaj gönderilmez.
- [ ] Çok hızlı mesaj gönderince hız sınırı uyarısı çıkar, uygulama kilitlenmez.

## 4. Gruplar
- [ ] A "+ Yeni grup" ile grup kurar; kurucu rolüyle listede görünür.
- [ ] A, B ve C'yi davet eder; B ve C'de "Davetler" sayacı artar; kabul edince grup listelerine eklenir.
- [ ] Daveti reddetmek grubu eklemez.
- [ ] Grup mesajı herkese ulaşır; kendi mesajım sağda, diğerleri solda.
- [ ] Üyeler paneli açılıp kapanır; çevrimiçi ve çevrimdışı üyeler ayrı listelenir.
- [ ] C çıkış yapar, A grup mesajı yollar; C girince grup mesajı teslim edilir.
- [ ] Grup adına sağ tık ya da "Ayarlar" ile ayarlar penceresi açılır.
- [ ] Kurucu: ad değiştirme, yönetici yapma, üye çıkarma, davet, grubu silme çalışır.
- [ ] Yönetici: üye çıkarma/davet çalışır; grubu silemez.
- [ ] Üye: yönetim düğmeleri pasiftir; yalnızca gruptan ayrılabilir.
- [ ] Çıkarılan ya da ayrılan kullanıcı grup mesajı almaz.

## 5. Geçmiş
- [ ] İstemci kapatılıp açılınca sohbetler ve mesajlar yerel geçmişten gelir.
- [ ] Geçmiş `Belgelerim\ChatApp\History\<kullanıcı>\sohbetler\` altında konuşma başına bir dosyadır.
- [ ] Okunmamış durumu yeniden girişte korunur.

## 6. Sunucu izleme ve gizlilik
- [ ] "Bağlı istemciler" listesinde kullanıcı adı değil ID görünür.
- [ ] Günlüklerde kullanıcı adı, grup adı, mesaj metni ve parola yoktur.
- [ ] Sunucu durdurulunca istemcilerde bağlantı koptu bildirimi çıkar, uygulama çökmez.
- [ ] Sunucu yeniden başlayınca istemciler tekrar giriş yapabilir.
- [ ] Sunucu kapalıyken giriş denemesi anlaşılır bir hata verir.

## 7. Ekran uyumu
- [ ] %100, %125, %150 ve %200 ölçekte hiçbir metin/düğme kırpılmaz.
- [ ] 1366×768 ve 1280×720 çözünürlükte pencere ekrana sığar.
- [ ] Pencere daraltılınca üyeler paneli kendiliğinden gizlenir, genişleyince geri gelir.
- [ ] Başlık çubuğu ve listeler koyu temayla uyumlu.
- [ ] Farklı DPI'lı iki ekran arasında pencere taşınınca arayüz bozulmaz.

## Sonuç
Tarih: ____ / Test eden: ____ / Bulunan sorunlar: ____
