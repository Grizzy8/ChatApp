# Mimari

Bu belge sunucunun üç temel akışını [Mermaid](https://mermaid.js.org/) sıra diyagramlarıyla gösterir. İstemci ile sunucu arasındaki her mesaj, TCP üzerinde satır başına bir JSON nesnesi olarak (`ChatMessage`) iletilir; mesaj türü `MessageType`, hata nedeni `ErrorCode` alanındadır. Sunucu hazır hata metni göndermez, Türkçe açıklamayı istemci üretir.

İlgili kodlar: `ChatServer/ChatServerHost*.cs`, `ChatServer/Storage/*Repository.cs`, `ChatShared/PasswordHasher.cs`.

## 1. Kayıt / giriş el sıkışması

Bağlantıdan sonra ilk mesaj, süre sınırı (`JoinTimeout`) içinde gelen geçerli bir `Register` ya da `Login` olmalıdır; aksi halde bağlantı kapatılır. Kayıt ve giriş denemeleri bağlantı adresi başına hız sınırına (`AuthAttemptLimiter`) tabidir.

Zamanlama saldırısı koruması iki parçadır:

- Kullanıcı adı kayıtlı olmasa bile sahte bir hash (`DummyPasswordHash`) ile gerçek bir PBKDF2 doğrulaması yapılır; yanıt süresine bakarak hangi kullanıcı adlarının var olduğu anlaşılamaz.
- Hash karşılaştırması `CryptographicOperations.FixedTimeEquals` ile sabit sürede yapılır.

Ayrıca kullanıcı adı yanlış da olsa parola yanlış da olsa istemciye aynı hata (`InvalidCredentials`) döner. Parola hiçbir zaman günlüğe yazılmaz.

```mermaid
sequenceDiagram
    autonumber
    actor C as İstemci
    participant S as ChatServerHost
    participant L as AuthAttemptLimiter
    participant U as UserRepository
    participant P as PasswordHasher

    C->>S: TCP bağlantısı
    alt JoinTimeout içinde mesaj gelmez
        S--)C: bağlantıyı kapatır
    end

    C->>S: Register ya da Login (kullanıcı adı, parola)

    alt Mesaj çözümlenemez
        S-->>C: Error (InvalidMessage), bağlantı kapanır
    else İlk mesaj Register/Login değil
        S-->>C: Error (NotJoined), bağlantı kapanır
    end

    S->>L: TryAcquire(uç nokta)
    alt Deneme sınırı aşıldı
        S-->>C: JoinRejected (TooManyAttempts), bağlantı kapanır
    end

    alt RegisterRequest
        S->>S: UsernameValidator + PasswordValidator
        alt Kural ihlali
            Note over S: UsernameInvalid ya da PasswordInvalid
        else Kurallara uygun
            S->>P: Hash(parola) [thread havuzunda]
            P-->>S: pbkdf2-sha256$600000$tuz$hash
            S->>U: CreateAsync(kullanıcı adı, hash)
            alt Kullanıcı adı alınmış
                U-->>S: null
                Note over S: UsernameTaken
            else Hesap oluştu
                U-->>S: UserRecord (yeni UserId)
            end
        end
    else LoginRequest
        S->>U: FindByUsernameAsync(kullanıcı adı)
        U-->>S: UserRecord ya da null
        Note over S: Kullanıcı yoksa saklı hash yerine<br/>DummyPasswordHash kullanılır
        S->>P: Verify(parola, saklı ya da sahte hash) [thread havuzunda]
        Note over P: PBKDF2 yeniden türetilir,<br/>FixedTimeEquals ile karşılaştırılır
        P-->>S: doğru / yanlış
        Note over S: Kullanıcı yok ya da parola yanlış ise<br/>her ikisinde de InvalidCredentials
    end

    alt Başarısız (hata kodu != None)
        S-->>C: JoinRejected (ErrorCode)
        Note over S,C: UsernameTaken, UsernameInvalid, PasswordInvalid,<br/>InvalidCredentials, ServerError ...
        S--)C: bağlantı kapanır
    else Başarılı
        S->>S: _membershipLock altında oturumu kaydeder
        alt Hesap zaten çevrimiçi
            S-->>C: JoinRejected (AccountAlreadyOnline), bağlantı kapanır
        else Oturum kabul edildi
            S-->>C: JoinAccepted (kullanıcı adı, UserId, çevrimiçi liste)
            S--)C: ContactList, GroupList, InviteList
            S--)C: Bekleyen mesajlar (bkz. Akış 2)
        end
    end
```

## 2. Özel mesaj teslimatı

Alıcı kullanıcı adı ya da ID ile verilebilir. Çevrimiçi alıcıya mesaj doğrudan iletilir ve veritabanına yazılmaz. Alıcı çevrimdışıysa (ya da gönderim sırasında düştüyse) mesaj `PendingMessages` tablosuna eklenir ve alıcı bir sonraki girişinde teslim edilir. Mesaj içeriği sunucu günlüğüne yazılmaz.

```mermaid
sequenceDiagram
    autonumber
    actor A as Gönderen (A)
    participant S as ChatServerHost
    participant U as UserRepository
    participant R as MessageRouter
    participant M as MessageRepository
    participant DB as PendingMessages
    actor B as Alıcı (B)

    A->>S: Private (alıcı, metin)
    S->>S: Hız sınırı, metin ve alıcı doğrulaması
    S->>U: FindByUsernameOrIdAsync(alıcı)
    alt Alıcı yok
        S-->>A: Error (RecipientNotFound)
    else Alıcı kendisi
        S-->>A: Error (InvalidMessage)
    end

    alt B çevrimiçi
        S->>R: SendToAsync(B, Private)
        R-->>B: Private (gönderen, metin, UTC zaman damgası)
        S->>R: SendToAsync(A, Private)
        R-->>A: Private (aynı kopya: iletildi onayı ve sunucu saati)
    else B çevrimdışı (ya da gönderim sırasında düştü)
        S->>M: EnqueueAsync(A, B, metin, UTC)
        M->>DB: INSERT (RecipientId, SenderId, Payload, SentAt)
        alt B'nin kutusu dolu (MaxPendingMessagesPerRecipient)
            M-->>S: false
            S-->>A: Error (RecipientInboxFull)
        else Kuyruğa alındı
            M-->>S: true
            S->>R: SendToAsync(A, PrivateQueued)
            R-->>A: PrivateQueued ("çevrimdışı, giriş yapınca iletilecek")
        end
    end

    Note over B,S: --- B daha sonra bağlanır ---

    B->>S: Login (bkz. Akış 1)
    S-->>B: JoinAccepted
    S->>M: ListPendingAsync(B)
    M->>DB: SELECT (Id sırasıyla)
    DB-->>M: bekleyen kayıtlar
    M-->>S: PendingMessage listesi
    loop Her bekleyen mesaj
        S->>R: SendToAsync(B, Private, SentAtUtc)
        R-->>B: Private (özgün gönderim zamanıyla)
    end
    S->>M: DeleteUpToAsync(B, son teslim edilen Id)
    M->>DB: DELETE (yalnızca teslim edilenler)
    Note over S,DB: Bağlantı yarıda koparsa kalan mesajlar silinmez,<br/>bir sonraki girişe kalır
```

## 3. Grup mesaj yayını

Üye olmayan kullanıcının mesajı `GroupNotFound` ile reddedilir (grupların varlığı sızmasın diye "yok" ile "üye değil" ayırt edilmez). Mesaj her üyeye ayrı ayrı denenir; çevrimiçi üyelere anında iletilir, teslim edilemeyen çevrimdışı üyeler için tek seferde üye başına bir satır kuyruğa yazılır. Gönderenin kendisi kuyruğa alınmaz; çevrimiçi olduğu için kendi kopyasını da yayından alır.

```mermaid
sequenceDiagram
    autonumber
    actor A as Gönderen üye (A)
    participant S as ChatServerHost
    participant G as GroupRepository
    participant R as MessageRouter
    participant M as MessageRepository
    participant DB as PendingMessages
    actor B as Çevrimiçi üye (B)
    actor C as Çevrimdışı üye (C)

    A->>S: GroupMessage (grup ID, metin)
    S->>S: Hız sınırı, metin doğrulaması
    S->>G: GetMembersIfMemberAsync(grup, A)
    alt A üye değil ya da grup yok
        G-->>S: null
        S-->>A: Error (GroupNotFound)
    else A üye
        G-->>S: Üye listesi (A, B, C ...)
    end

    loop Her üye için
        alt Üye çevrimiçi
            S->>R: SendToAsync(üye, GroupMessage)
            R-->>B: GroupMessage (gönderen, grup ID, metin, UTC)
            R-->>A: GroupMessage (A kendi kopyasını da alır)
        else Üye çevrimdışı ya da gönderim başarısız
            Note over S: Gönderen değilse offlineIds listesine eklenir
        end
    end

    opt offlineIds boş değil
        S->>M: EnqueueGroupAsync(A, grup, offlineIds, metin, UTC)
        loop Her çevrimdışı üye
            alt Üyenin kutusu dolu
                Note over M: Bu üye atlanır, diğerleri etkilenmez
            else Yer var
                M->>DB: INSERT (RecipientId = C, SenderId = A, GroupId, Payload, SentAt)
            end
        end
        M-->>S: kuyruğa alınan sayısı
    end

    Note over C,S: --- C daha sonra bağlanır ---

    C->>S: Login (bkz. Akış 1)
    S-->>C: JoinAccepted
    S->>M: ListPendingAsync(C)
    M-->>S: bekleyen kayıtlar (GroupId dolu olanlar grup mesajıdır)
    loop Her bekleyen mesaj
        S->>R: SendToAsync(C, GroupMessage, SentAtUtc)
        R-->>C: GroupMessage (Recipient = grup ID)
    end
    S->>M: DeleteUpToAsync(C, son teslim edilen Id)
    M->>DB: DELETE
```
