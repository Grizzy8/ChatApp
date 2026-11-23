# ChatApp – Yeni Vizyon Geliştirme Planı

Durum: **taslak, onay bekliyor** (kod yazılmadı). Hazırlanma tarihi: 23 Kasım 2026. Ödev teslimi: 30 Kasım 2026 23:59.

## 1. Vizyon (konuşmada netleşenler)

Numara gerektirmeyen, WhatsApp'ın kişi mantığı ve Discord'un rol mantığıyla çalışan bir sohbet uygulaması:

- Kayıt/giriş: **kullanıcı adı + şifre**. Her hesaba sunucunun ürettiği **benzersiz ID** verilir (ör. `ali#4821`).
- Kişiler: başka kullanıcılar **kullanıcı adı veya ID** ile kişilere eklenir. QR yok.
- İki ayrı bölüm: **Özel sohbetler** (kişiyle birebir, başkası görmez) ve **Gruplar**.
- Gruplar: kurucu grubu yönetir; üyeler kullanıcı adı/ID ile davet edilir; rol ve yetkiler vardır (Discord benzeri).
- **Çevrimdışı** kullanıcıya mesaj ve davet gönderilebilir; kullanıcı girince teslim edilir (sunucu saklar).
- Gizlilik: sunucu arayüzü içerik/isim göstermez, yalnızca ID'leri görür; içerik log'a yazılmaz. Uçtan uca şifreleme **özel sohbetler için** eklenecek, gruplara uyumlu hazırlanacak.
- Dağıtım: sunucu şimdilik kendi bilgisayarda; **arayüzsüz servis olarak da çalışabilecek** (ileride buluta taşınabilir). İstemci tek dosya **kurulabilir .exe** olarak yayınlanacak.
- Mevcut sunucu formu **kalır** (izleme için), yanına arayüzsüz servis eklenir.

## 2. Açık maddeler (başlamadan netleşmeli)

1. **Ödev PDF'i (ChatApp.pdf) isterleri.** Bu oturumda okunamadı. Yeni vizyonun PDF'teki zorunlu isterleri (özellikle genel/herkese açık sohbet var mı, hangi özellikler istenmiş) karşıladığı doğrulanmalı. Gerekirse herkesin otomatik üye olduğu bir **"Genel" grubu** tutulur.
2. Rol sistemi derinliği: teslimde sabit roller yeterli mi (bkz. §6)?

## 3. Aşamalar ve kapsam

Vizyonun tamamı 7 günlük sürenin çok üstünde. Bu yüzden üç aşama:

### Aşama A – Teslim (30 Kasım'a kadar)
Çalışan, sağlam ve gösterilebilir çekirdek:
1. Protokol v2 + SQLite depolama altyapısı (mesaj içeriği "opak yük" alanında: E2E'ye hazırlık)
2. Hesaplar: kayıt, giriş, şifre hash'i (PBKDF2), benzersiz ID, giriş denemesi sınırı
3. Kişiler: ekleme (ad/ID), listeleme, silme
4. Özel sohbetler + çevrimdışı teslim (okunmamış sayacı, gelen kutusu)
5. Gruplar: kurma, davet (çevrimdışı dahil), kabul/red, üye listesi, gruptan ayrılma, **3 sabit rol** (Kurucu / Yönetici / Üye)
6. İstemci arayüzü: sol panelde "Özel sohbetler" ve "Gruplar", seçilen konuşmaya göre sağda sohbet
7. Geçmiş kaydı konuşma bazlı; sunucu izleme formu ID'lerle
8. Sağlamlaştırma, elle test listesi, README, istemci için tek dosya publish

### Aşama B – Teslim sonrası, öncelikli
- **TLS** (şifre ağda korunsun), sertifika parmak izi doğrulaması
- **Arayüzsüz sunucu servisi** (`ChatServer.Service`), ayar dosyası
- **Özel sohbetlerde uçtan uca şifreleme** (ECDH + AES-GCM), güvenlik kodu karşılaştırma
- Yükleyici (Inno Setup)

### Aşama C – Sonraki
- Özel rol/yetki sistemi (isim + serbest yetki seçimi)
- Grup uçtan uca şifreleme (grup anahtarı dağıtımı ve yenileme)
- Buluta taşıma (VPS, TLS sertifikası, yedekleme)

> Aşama A'ya sığmazsa önce **§5'teki sıranın sonundan** kesilir (roller → grup daveti çevrimdışı), çekirdek (hesap, kişi, özel sohbet) korunur.

## 4. Mimari

```
ChatShared        protokol, mesaj türleri, doğrulayıcılar, hız sınırlayıcı (mevcut) + yeni mesaj türleri
ChatServer.Core   (yeni) sunucu çekirdeği: oturumlar, yönlendirme, hesap/grup/kişi servisleri, SQLite katmanı
ChatServer        WinForms izleme formu (çekirdeği kullanır)
ChatServer.Service (Aşama B) arayüzsüz çalıştırıcı
ChatClient        WinForms istemci
ChatShared.Test   saf mantık testleri (+ şifre hash'i, ID üretimi, yetki kuralları)
```

Mevcut `ChatServerHost` sunucu çekirdeğine dönüşür (zaten formdan bağımsız). `MessageChannel`, `BoundedLineReader`, `SlidingWindowRateLimiter`, `CrashLog`, `ChatHistoryStore` yeniden kullanılır.

## 5. Veri modeli (SQLite)

| Tablo | Alanlar |
|---|---|
| Users | Id (benzersiz kod), Username (benzersiz, büyük/küçük harf duyarsız), PasswordHash, Salt, CreatedAt |
| Contacts | OwnerId, ContactId, AddedAt |
| Conversations (özel) | iki kullanıcı arası sohbet kimliği |
| Messages | Id, ConversationId/GroupId, SenderId, Payload (opak), SentAt, DeliveredAt |
| Groups | Id, Name, OwnerId, CreatedAt |
| GroupMembers | GroupId, UserId, Role, JoinedAt |
| Invites | Id, GroupId, FromUserId, ToUserId, Status, CreatedAt |

Teslim edilmemiş mesaj/davet = `DeliveredAt` boş kayıtlar; kullanıcı girince gönderilir. Kayıt sırası §3 Aşama A'daki sıra ile aynıdır.

## 6. Roller (Aşama A: sabit)

| Yetki | Kurucu | Yönetici | Üye |
|---|---|---|---|
| Mesaj yazma | ✓ | ✓ | ✓ |
| Üye davet etme | ✓ | ✓ | ✓/✗ (kurucu belirler) |
| Üye çıkarma | ✓ | ✓ | ✗ |
| Rol atama (yönetici yap) | ✓ | ✗ | ✗ |
| Grup adı/silme | ✓ | ✗ | ✗ |

Yetkiler bir `Permission` bayrağı kümesi olarak tutulur; böylece Aşama C'de serbest rol sistemine geçmek şema değişikliği gerektirmez.

## 7. Protokol v2 (özet)

Satır tabanlı JSON korunur. Yeni mesaj türleri (taslak): `Register`, `Login`, `AuthOk`, `AuthFail`, `AddContact`, `ContactList`, `DirectMessage`, `CreateGroup`, `GroupInvite`, `InviteResponse`, `GroupMessage`, `GroupMembers`, `LeaveGroup`, `SetRole`, `Pending` (çevrimdışı teslimat). Mesaj içeriği `Payload` alanında opak taşınır; sunucu içeriği yorumlamaz, E2E eklenince protokol değişmez.

## 8. Güvenlik notları

- Şifreler yalnızca hash'li saklanır (PBKDF2, rastgele salt); asla log'a yazılmaz.
- **Aşama A'da trafik şifresizdir**: şifre ağda düz gider. Yalnızca güvenilen yerel ağda kullanılmalı; README'de açıkça belirtilecek. TLS Aşama B'dedir.
- Giriş denemesi sınırı, mevcut hız sınırlayıcı ve bağlantı limiti korunur.
- Sunucu arayüzü yalnızca ID gösterir; mesaj içeriği hiçbir yerde log'a yazılmaz.
- Çevrimdışı mesajlar sunucuda saklanır; E2E gelene kadar sunucu teknik olarak okuyabilir (README'de belirtilecek).

## 9. Test stratejisi

- Birim testler (`ChatShared.Test`): şifre hash'i, ID üretimi, kullanıcı adı doğrulama, yetki kuralları.
- Gerçek soketle senaryo testleri (geçici projelerle): kayıt/giriş, çevrimdışı teslim, davet akışı, rol yetkileri, bozuk veri.
- Elle test kontrol listesi (README) ve ekran görüntüleri.

## 10. Riskler

| Risk | Önlem |
|---|---|
| Süre (7 gün, geniş kapsam) | Aşamalama; kesme sırası belirli (§3) |
| Protokol değişikliği mevcut istemciyi kırar | İstemci ve sunucu birlikte güncellenir; sürüm numarası protokolde |
| Şifre ağda açık (Aşama A) | README'de belirtilir; TLS Aşama B'nin ilk işi |
| SQLite eşzamanlılık | Tek yazıcı kuyruğu ya da kısa işlemler; testle doğrulanır |
| PDF isterleriyle çelişki | §2/1: başlamadan doğrulama |
