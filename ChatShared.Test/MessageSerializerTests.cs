using ChatShared;

namespace ChatShared.Test;

public class MessageSerializerTests
{
    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var original = new ChatMessage(
            MessageType.Private,
            Sender: "ali",
            Recipient: "ayse",
            Text: "selam",
            Timestamp: new DateTime(2026, 11, 23, 14, 2, 11, DateTimeKind.Utc),
            Users: new[] { "ali", "ayse" },
            ErrorCode: ErrorCode.RecipientNotFound);

        var result = MessageSerializer.Deserialize(MessageSerializer.Serialize(original));

        Assert.NotNull(result);
        Assert.Equal(original.Type, result.Type);
        Assert.Equal(original.Sender, result.Sender);
        Assert.Equal(original.Recipient, result.Recipient);
        Assert.Equal(original.Text, result.Text);
        Assert.Equal(original.Timestamp, result.Timestamp);
        Assert.Equal(original.Users, result.Users);
        Assert.Equal(original.ErrorCode, result.ErrorCode);
    }

    [Fact]
    public void RoundTrip_UtcTimestamp_StaysUtcAndKeepsInstant()
    {
        var utc = new DateTime(2026, 11, 23, 23, 30, 0, DateTimeKind.Utc);
        var original = new ChatMessage(MessageType.Private, Sender: "ali", Timestamp: utc);

        string json = MessageSerializer.Serialize(original);
        var result = MessageSerializer.Deserialize(json);

        Assert.Contains("Z", json); // UTC işareti hat üzerinde korunur
        Assert.NotNull(result?.Timestamp);
        Assert.Equal(DateTimeKind.Utc, result.Timestamp.Value.Kind);
        Assert.Equal(utc, result.Timestamp.Value);
        Assert.Equal(utc.ToLocalTime(), result.Timestamp.Value.ToLocalTime()); // yerel saate çeviri aynı ana işaret eder
    }

    [Fact]
    public void RoundTrip_PrivateQueuedAndInboxFullError()
    {
        var queued = new ChatMessage(MessageType.PrivateQueued, Sender: "Ali", Recipient: "Bea", Text: "selam");
        var error = new ChatMessage(MessageType.Error, ErrorCode: ErrorCode.RecipientInboxFull);

        Assert.Equal(queued, MessageSerializer.Deserialize(MessageSerializer.Serialize(queued)));
        Assert.Equal(error, MessageSerializer.Deserialize(MessageSerializer.Serialize(error)));
    }

    [Fact]
    public void RoundTrip_PreservesGroupLists()
    {
        var message = new ChatMessage(
            MessageType.GroupList,
            Groups: [new GroupInfo("AB23-CD45", "Arkadaşlar", GroupRole.Owner)],
            Invites: [new GroupInviteInfo("EF67-GH89", "Ders", "Ali")],
            Members: [new GroupMemberInfo("JK23-MN45", "Bea", GroupRole.Admin)]);

        ChatMessage? copy = MessageSerializer.Deserialize(MessageSerializer.Serialize(message));

        Assert.NotNull(copy);
        Assert.Equal(GroupRole.Owner, copy.Groups!.Single().Role);
        Assert.Equal("Ders", copy.Invites!.Single().GroupName);
        Assert.Equal(GroupRole.Admin, copy.Members!.Single().Role);
    }

    [Fact]
    public void Serialize_WritesGroupRoleAsString()
    {
        string json = MessageSerializer.Serialize(new ChatMessage(MessageType.GroupList, Groups: [new GroupInfo("AB23-CD45", "G", GroupRole.Member)]));
        Assert.Contains("\"Member\"", json);
    }

    [Fact]
    public void RoundTrip_PreservesTurkishCharacters()
    {
        var original = new ChatMessage(MessageType.Chat, Sender: "Ayşe", Text: "Günaydın, çay İster misin? ğüşıöç");

        var result = MessageSerializer.Deserialize(MessageSerializer.Serialize(original));

        Assert.Equal("Günaydın, çay İster misin? ğüşıöç", result!.Text);
        Assert.Equal("Ayşe", result.Sender);
    }

    [Fact]
    public void Serialize_ProducesSingleLine_EvenWhenTextContainsNewlines()
    {
        var message = new ChatMessage(MessageType.Chat, Sender: "ali", Text: "ilk satır\r\nikinci satır\n\"tırnak\"");

        var line = MessageSerializer.Serialize(message);

        Assert.DoesNotContain('\n', line);
        Assert.DoesNotContain('\r', line);
        Assert.Equal(message.Text, MessageSerializer.Deserialize(line)!.Text);
    }

    [Fact]
    public void Serialize_OmitsNullFields()
    {
        var line = MessageSerializer.Serialize(new ChatMessage(MessageType.Leave));

        Assert.DoesNotContain("null", line);
        Assert.DoesNotContain("sender", line);
    }

    [Fact]
    public void Deserialize_ReadsHandWrittenJson()
    {
        var result = MessageSerializer.Deserialize("{\"type\":\"Login\",\"sender\":\"ali\",\"password\":\"parola-123\"}");

        Assert.NotNull(result);
        Assert.Equal(MessageType.Login, result.Type);
        Assert.Equal("ali", result.Sender);
        Assert.Equal("parola-123", result.Password);
    }

    [Fact]
    public void RoundTrip_PreservesPasswordAndUserId()
    {
        var original = new ChatMessage(MessageType.Register, Sender: "ali", Password: "parola-123", UserId: "K7M2-9QXD");

        var result = MessageSerializer.Deserialize(MessageSerializer.Serialize(original));

        Assert.NotNull(result);
        Assert.Equal("parola-123", result.Password);
        Assert.Equal("K7M2-9QXD", result.UserId);
    }

    [Fact]
    public void RoundTrip_PreservesContactList()
    {
        var original = new ChatMessage(
            MessageType.ContactList,
            Contacts: new[] { new ContactInfo("K7M2-9QXD", "Ayşe"), new ContactInfo("AAAA-2222", "veli") });

        var result = MessageSerializer.Deserialize(MessageSerializer.Serialize(original));

        Assert.NotNull(result);
        Assert.Equal(original.Contacts, result.Contacts);
    }

    [Fact]
    public void ToString_NeverRevealsThePassword()
    {
        var message = new ChatMessage(MessageType.Login, Sender: "ali", Password: "çok-gizli-parola");

        Assert.DoesNotContain("çok-gizli-parola", message.ToString());
        Assert.Contains("ali", message.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("merhaba bu json degil")]
    [InlineData("{\"type\":\"Login\"")]
    [InlineData("[1,2,3]")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"sender\":\"ali\"}")]
    [InlineData("{\"type\":\"BilinmeyenTur\"}")]
    [InlineData("{\"type\":42}")]
    [InlineData("{\"type\":\"Chat\",\"timestamp\":\"dün\"}")]
    public void Deserialize_ReturnsNull_ForInvalidInput(string? line)
    {
        Assert.Null(MessageSerializer.Deserialize(line));
    }

    [Fact]
    public void Deserialize_ReturnsNull_WhenLineExceedsMaxLength()
    {
        var huge = "{\"type\":\"Chat\",\"text\":\"" + new string('a', ProtocolConstants.MaxLineLength) + "\"}";

        Assert.Null(MessageSerializer.Deserialize(huge));
    }
}
