namespace ChatShared;

/// <summary>Sunucu ile istemci arasındaki mesaj türleri.</summary>
public enum MessageType
{
    /// <summary>İstemci → Sunucu: yeni hesap oluşturma isteği (Sender = kullanıcı adı, Password). Başarılıysa oturum da açılır.</summary>
    Register,

    /// <summary>İstemci → Sunucu: mevcut hesapla giriş isteği (Sender = kullanıcı adı, Password).</summary>
    Login,

    /// <summary>
    /// Sunucu → İstemci: kayıt/giriş kabul edildi ve sohbete katılındı. Sender = hesabın kayıtlı kullanıcı adı,
    /// UserId = hesabın benzersiz kimliği, Users = çevrimiçi kullanıcı listesi.
    /// </summary>
    JoinAccepted,

    /// <summary>İstemci → Sunucu: kişi ekleme isteği. Text = kullanıcı adı ya da kimlik (ID).</summary>
    AddContact,

    /// <summary>İstemci → Sunucu: kişiyi silme isteği. Text = kişinin kimliği (ID).</summary>
    RemoveContact,

    /// <summary>Sunucu → İstemci: kişi listesinin tamamı (Contacts). Girişte ve her değişiklikten sonra gönderilir.</summary>
    ContactList,

    /// <summary>Sunucu → İstemci: kayıt/giriş reddedildi, ErrorCode ekli.</summary>
    JoinRejected,

    /// <summary>Genel sohbet mesajı (istemci → sunucu → herkes).</summary>
    Chat,

    /// <summary>Özel mesaj (istemci → sunucu → alıcı ve gönderen).</summary>
    Private,

    /// <summary>
    /// Sunucu → gönderen: alıcı çevrimdışıydı, özel mesaj sunucuda bekletiliyor (alanlar Private ile aynı).
    /// Alıcı bir sonraki girişinde mesajı normal Private olarak alır.
    /// </summary>
    PrivateQueued,

    /// <summary>Sunucu → herkes: yeni bir kullanıcı katıldı.</summary>
    UserJoined,

    /// <summary>Sunucu → herkes: bir kullanıcı ayrıldı.</summary>
    UserLeft,

    /// <summary>İstemci → Sunucu: kullanıcı bağlantıyı kendi isteğiyle kesiyor.</summary>
    Leave,

    /// <summary>İstemci → Sunucu: grup kur. Text = grup adı. Başarıda GroupList gelir.</summary>
    CreateGroup,

    /// <summary>Sunucu → İstemci: üyesi olunan grupların tamamı (Groups). Girişte ve her değişiklikten sonra gönderilir.</summary>
    GroupList,

    /// <summary>Sunucu → İstemci: bekleyen davetlerin tamamı (Invites). Girişte ve her değişiklikten sonra gönderilir.</summary>
    InviteList,

    /// <summary>İstemci → Sunucu: gruba davet. Recipient = grup ID, Text = davet edilenin kullanıcı adı ya da ID'si.</summary>
    InviteToGroup,

    /// <summary>Sunucu → davet eden: davet bırakıldı. Recipient = grup ID, Text = davet edilenin kullanıcı adı.</summary>
    InviteSent,

    /// <summary>İstemci → Sunucu: daveti kabul et. Recipient = grup ID.</summary>
    AcceptInvite,

    /// <summary>İstemci → Sunucu: daveti reddet. Recipient = grup ID.</summary>
    DeclineInvite,

    /// <summary>İstemci → Sunucu: grup üyelerini iste. Recipient = grup ID. Yanıt GroupMembers.</summary>
    GetGroupMembers,

    /// <summary>Sunucu → İstemci: bir grubun üyeleri (Recipient = grup ID, Members). Üyelik değişince tüm çevrimiçi üyelere gönderilir.</summary>
    GroupMembers,

    /// <summary>Grup mesajı. İstemci → Sunucu: Recipient = grup ID, Text. Sunucu → çevrimiçi üyeler: Sender, Recipient (grup ID), Text, Timestamp.</summary>
    GroupMessage,

    /// <summary>İstemci → Sunucu: gruptan ayrıl. Recipient = grup ID.</summary>
    LeaveGroup,

    /// <summary>İstemci → Sunucu: üyeyi gruptan çıkar. Recipient = grup ID, Text = üyenin ID'si.</summary>
    RemoveMember,

    /// <summary>İstemci → Sunucu: üyeyi yönetici yap (yalnızca kurucu). Recipient = grup ID, Text = üyenin ID'si.</summary>
    PromoteMember,

    /// <summary>İstemci → Sunucu: yöneticiliği al (yalnızca kurucu). Recipient = grup ID, Text = üyenin ID'si.</summary>
    DemoteMember,

    /// <summary>İstemci → Sunucu: grubun adını değiştir (yalnızca kurucu). Recipient = grup ID, Text = yeni ad.</summary>
    RenameGroup,

    /// <summary>İstemci → Sunucu: grubu sil (yalnızca kurucu). Recipient = grup ID.</summary>
    DeleteGroup,

    /// <summary>Sunucu → İstemci: bir isteğin işlenemediğini bildirir, ErrorCode ekli.</summary>
    Error
}
