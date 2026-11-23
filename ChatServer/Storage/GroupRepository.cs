using System.Globalization;
using ChatShared;
using Microsoft.Data.Sqlite;

namespace ChatServer.Storage;

/// <summary>
/// Grupların, üyeliklerin ve davetlerin kalıcı kaydı. Yetki denetimi (<see cref="GroupPermissions"/>) ve değişiklik
/// aynı BEGIN IMMEDIATE işleminde yapılır; böylece rol/üyelik arada değişse bile yetkisiz işlem uygulanamaz ve
/// sınırlar eşzamanlı isteklerle aşılamaz. Tüm sorgular parametrelidir.
/// </summary>
public sealed class GroupRepository
{
    private const int PrimaryKeyViolation = 1555;
    private const int MaxIdAttempts = 5;

    private readonly ChatDatabase _database;

    public GroupRepository(ChatDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    /// <summary>Grubu kurar; kurucu otomatik olarak Owner üyedir. Kullanıcı en fazla <see cref="ProtocolConstants.MaxGroupsPerUser"/> gruba üye olabilir.</summary>
    public async Task<(GroupResult Result, GroupRecord? Group)> CreateAsync(string ownerId, string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(ownerId);
        ArgumentException.ThrowIfNullOrEmpty(name);

        GroupRecord? created = null;
        GroupResult result = await WriteAsync(async (connection, tx) =>
        {
            if (await CountAsync(connection, tx, "SELECT COUNT(*) FROM GroupMembers WHERE UserId = $u;", ct: ct, ("$u", ownerId)).ConfigureAwait(false)
                >= ProtocolConstants.MaxGroupsPerUser)
            {
                return GroupResult.GroupLimitReached;
            }

            string now = Now();
            for (int attempt = 0; attempt < MaxIdAttempts; attempt++)
            {
                string id = UserId.Generate();
                try
                {
                    await ExecuteAsync(connection, tx, "INSERT INTO Groups (Id, Name, OwnerId, CreatedAt) VALUES ($id, $name, $owner, $now);",
                        ct, ("$id", id), ("$name", name), ("$owner", ownerId), ("$now", now)).ConfigureAwait(false);
                }
                catch (SqliteException ex) when (ex.SqliteExtendedErrorCode == PrimaryKeyViolation)
                {
                    continue; // kimlik çakışması pratikte imkânsız; yeni kimlik dene
                }

                await ExecuteAsync(connection, tx, "INSERT INTO GroupMembers (GroupId, UserId, Role, JoinedAt) VALUES ($g, $u, 'Owner', $now);",
                    ct, ("$g", id), ("$u", ownerId), ("$now", now)).ConfigureAwait(false);
                created = new GroupRecord(id, name, GroupRole.Owner);
                return GroupResult.Ok;
            }

            throw new InvalidOperationException("Benzersiz grup kimliği üretilemedi.");
        }, ct).ConfigureAwait(false);

        return (result, created);
    }

    /// <summary>Kullanıcının üyesi olduğu gruplar (ada göre sıralı).</summary>
    public async Task<IReadOnlyList<GroupRecord>> ListAsync(string userId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        await using SqliteConnection connection = await _database.OpenAsync(ct).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT g.Id, g.Name, m.Role FROM GroupMembers m JOIN Groups g ON g.Id = m.GroupId " +
            "WHERE m.UserId = $u ORDER BY g.Name COLLATE NOCASE, g.Id;";
        command.Parameters.AddWithValue("$u", userId);

        var groups = new List<GroupRecord>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            groups.Add(new GroupRecord(reader.GetString(0), reader.GetString(1), ParseRole(reader.GetString(2))));
        }

        return groups;
    }

    /// <summary>Grubun üyeleri (kurucu, yöneticiler, üyeler; kendi içinde ada göre). İstek sahibi üye değilse null.</summary>
    public async Task<IReadOnlyList<GroupMemberRecord>?> GetMembersIfMemberAsync(string groupId, string requesterId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(groupId);
        ArgumentException.ThrowIfNullOrEmpty(requesterId);

        IReadOnlyList<GroupMemberRecord> members = await ReadMembersAsync(groupId, ct).ConfigureAwait(false);
        return members.Any(m => m.UserId == requesterId) ? members : null;
    }

    /// <summary>Grubun üyeleri, yetki denetimi olmadan. Yalnızca sunucu içi bildirimler için; istemciye doğrudan verilmez.</summary>
    public async Task<IReadOnlyList<GroupMemberRecord>> ListMembersAsync(string groupId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(groupId);
        return await ReadMembersAsync(groupId, ct).ConfigureAwait(false);
    }

    /// <summary>Gruba davet edilmiş, henüz yanıtlamamış kullanıcıların adları (grup silinirken bildirim için).</summary>
    public async Task<IReadOnlyList<string>> ListInviteeUsernamesAsync(string groupId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(groupId);

        await using SqliteConnection connection = await _database.OpenAsync(ct).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT u.Username FROM GroupInvites i JOIN Users u ON u.Id = i.InviteeId WHERE i.GroupId = $g;";
        command.Parameters.AddWithValue("$g", groupId);

        var names = new List<string>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    /// <summary>Kullanıcıya gelen bekleyen davetler (eskiden yeniye).</summary>
    public async Task<IReadOnlyList<GroupInviteRecord>> ListInvitesAsync(string userId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        await using SqliteConnection connection = await _database.OpenAsync(ct).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT i.GroupId, g.Name, u.Username FROM GroupInvites i " +
            "JOIN Groups g ON g.Id = i.GroupId JOIN Users u ON u.Id = i.InviterId " +
            "WHERE i.InviteeId = $u ORDER BY i.CreatedAt, i.GroupId;";
        command.Parameters.AddWithValue("$u", userId);

        var invites = new List<GroupInviteRecord>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            invites.Add(new GroupInviteRecord(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }

        return invites;
    }

    /// <summary>Davet bırakır. Davet eden kurucu/yönetici olmalı; hedef üye olmamalı ve zaten davetli olmamalı.</summary>
    public Task<GroupResult> InviteAsync(string groupId, string actorId, string inviteeId, CancellationToken ct = default) =>
        WriteAsync(async (connection, tx) =>
        {
            GroupRole? role = await RoleOfAsync(connection, tx, groupId, actorId, ct).ConfigureAwait(false);
            if (role is null)
            {
                return GroupResult.GroupNotFound;
            }

            if (!GroupPermissions.CanInvite(role.Value))
            {
                return GroupResult.NotAuthorized;
            }

            if (await RoleOfAsync(connection, tx, groupId, inviteeId, ct).ConfigureAwait(false) is not null)
            {
                return GroupResult.AlreadyMember;
            }

            if (await CountAsync(connection, tx, "SELECT COUNT(*) FROM GroupInvites WHERE GroupId = $g AND InviteeId = $u;",
                    ct: ct, ("$g", groupId), ("$u", inviteeId)).ConfigureAwait(false) > 0)
            {
                return GroupResult.AlreadyInvited;
            }

            long members = await CountAsync(connection, tx, "SELECT COUNT(*) FROM GroupMembers WHERE GroupId = $g;", ct: ct, ("$g", groupId)).ConfigureAwait(false);
            long invites = await CountAsync(connection, tx, "SELECT COUNT(*) FROM GroupInvites WHERE GroupId = $g;", ct: ct, ("$g", groupId)).ConfigureAwait(false);
            if (members + invites >= ProtocolConstants.MaxGroupMembers)
            {
                return GroupResult.GroupFull;
            }

            if (await CountAsync(connection, tx, "SELECT COUNT(*) FROM GroupInvites WHERE InviteeId = $u;", ct: ct, ("$u", inviteeId)).ConfigureAwait(false)
                >= ProtocolConstants.MaxPendingInvitesPerUser)
            {
                return GroupResult.InviteLimitReached;
            }

            await ExecuteAsync(connection, tx,
                "INSERT INTO GroupInvites (GroupId, InviteeId, InviterId, CreatedAt) VALUES ($g, $invitee, $inviter, $now);",
                ct, ("$g", groupId), ("$invitee", inviteeId), ("$inviter", actorId), ("$now", Now())).ConfigureAwait(false);
            return GroupResult.Ok;
        }, ct);

    /// <summary>Daveti kabul eder: kullanıcı Member olarak gruba eklenir ve davet silinir.</summary>
    public Task<GroupResult> AcceptInviteAsync(string groupId, string userId, CancellationToken ct = default) =>
        WriteAsync(async (connection, tx) =>
        {
            if (await CountAsync(connection, tx, "SELECT COUNT(*) FROM GroupInvites WHERE GroupId = $g AND InviteeId = $u;",
                    ct: ct, ("$g", groupId), ("$u", userId)).ConfigureAwait(false) == 0)
            {
                return GroupResult.InviteNotFound;
            }

            if (await CountAsync(connection, tx, "SELECT COUNT(*) FROM GroupMembers WHERE UserId = $u;", ct: ct, ("$u", userId)).ConfigureAwait(false)
                >= ProtocolConstants.MaxGroupsPerUser)
            {
                return GroupResult.GroupLimitReached;
            }

            // Davetler üye sayısına dahil sayıldığı için normalde sınır aşılmaz; yine de savunmacı kontrol.
            if (await CountAsync(connection, tx, "SELECT COUNT(*) FROM GroupMembers WHERE GroupId = $g;", ct: ct, ("$g", groupId)).ConfigureAwait(false)
                >= ProtocolConstants.MaxGroupMembers)
            {
                return GroupResult.GroupFull;
            }

            await ExecuteAsync(connection, tx, "INSERT INTO GroupMembers (GroupId, UserId, Role, JoinedAt) VALUES ($g, $u, 'Member', $now);",
                ct, ("$g", groupId), ("$u", userId), ("$now", Now())).ConfigureAwait(false);
            await ExecuteAsync(connection, tx, "DELETE FROM GroupInvites WHERE GroupId = $g AND InviteeId = $u;",
                ct, ("$g", groupId), ("$u", userId)).ConfigureAwait(false);
            return GroupResult.Ok;
        }, ct);

    public Task<GroupResult> DeclineInviteAsync(string groupId, string userId, CancellationToken ct = default) =>
        WriteAsync(async (connection, tx) =>
            await ExecuteAsync(connection, tx, "DELETE FROM GroupInvites WHERE GroupId = $g AND InviteeId = $u;",
                ct, ("$g", groupId), ("$u", userId)).ConfigureAwait(false) > 0
                ? GroupResult.Ok
                : GroupResult.InviteNotFound, ct);

    /// <summary>Üyeyi gruptan çıkarır (bkz. <see cref="GroupPermissions.CanRemove"/>).</summary>
    public Task<GroupResult> RemoveMemberAsync(string groupId, string actorId, string targetId, CancellationToken ct = default) =>
        WriteAsync(async (connection, tx) =>
        {
            GroupRole? actor = await RoleOfAsync(connection, tx, groupId, actorId, ct).ConfigureAwait(false);
            if (actor is null)
            {
                return GroupResult.GroupNotFound;
            }

            GroupRole? target = await RoleOfAsync(connection, tx, groupId, targetId, ct).ConfigureAwait(false);
            if (target is null)
            {
                return GroupResult.MemberNotFound;
            }

            if (!GroupPermissions.CanRemove(actor.Value, target.Value))
            {
                return GroupResult.NotAuthorized;
            }

            await DeleteMembershipAsync(connection, tx, groupId, targetId, ct).ConfigureAwait(false);
            return GroupResult.Ok;
        }, ct);

    /// <summary>Kullanıcı kendi isteğiyle ayrılır. Kurucu ayrılamaz.</summary>
    public Task<GroupResult> LeaveAsync(string groupId, string userId, CancellationToken ct = default) =>
        WriteAsync(async (connection, tx) =>
        {
            GroupRole? role = await RoleOfAsync(connection, tx, groupId, userId, ct).ConfigureAwait(false);
            if (role is null)
            {
                return GroupResult.GroupNotFound;
            }

            if (!GroupPermissions.CanLeave(role.Value))
            {
                return GroupResult.OwnerCannotLeave;
            }

            await DeleteMembershipAsync(connection, tx, groupId, userId, ct).ConfigureAwait(false);
            return GroupResult.Ok;
        }, ct);

    /// <summary>Üyeyi yönetici yapar ya da yöneticiliğini alır. Yalnızca kurucu; kurucunun rolü değiştirilemez.</summary>
    public Task<GroupResult> SetRoleAsync(string groupId, string actorId, string targetId, GroupRole newRole, CancellationToken ct = default)
    {
        if (newRole == GroupRole.Owner)
        {
            throw new ArgumentException("Kurucu rolü atanamaz.", nameof(newRole));
        }

        return WriteAsync(async (connection, tx) =>
        {
            GroupRole? actor = await RoleOfAsync(connection, tx, groupId, actorId, ct).ConfigureAwait(false);
            if (actor is null)
            {
                return GroupResult.GroupNotFound;
            }

            if (!GroupPermissions.CanChangeRoles(actor.Value))
            {
                return GroupResult.NotAuthorized;
            }

            GroupRole? target = await RoleOfAsync(connection, tx, groupId, targetId, ct).ConfigureAwait(false);
            if (target is null)
            {
                return GroupResult.MemberNotFound;
            }

            if (target == GroupRole.Owner)
            {
                return GroupResult.NotAuthorized;
            }

            await ExecuteAsync(connection, tx, "UPDATE GroupMembers SET Role = $role WHERE GroupId = $g AND UserId = $u;",
                ct, ("$role", newRole.ToString()), ("$g", groupId), ("$u", targetId)).ConfigureAwait(false);
            return GroupResult.Ok;
        }, ct);
    }

    /// <summary>Grubun adını değiştirir. Yalnızca kurucu.</summary>
    public Task<GroupResult> RenameAsync(string groupId, string actorId, string newName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(newName);

        return WriteAsync(async (connection, tx) =>
        {
            GroupRole? role = await RoleOfAsync(connection, tx, groupId, actorId, ct).ConfigureAwait(false);
            if (role is null)
            {
                return GroupResult.GroupNotFound;
            }

            if (!GroupPermissions.CanRename(role.Value))
            {
                return GroupResult.NotAuthorized;
            }

            await ExecuteAsync(connection, tx, "UPDATE Groups SET Name = $name WHERE Id = $g;", ct, ("$name", newName), ("$g", groupId)).ConfigureAwait(false);
            return GroupResult.Ok;
        }, ct);
    }

    /// <summary>Grubu siler; üyelikler ve davetler birlikte silinir. Yalnızca kurucu.</summary>
    public Task<GroupResult> DeleteAsync(string groupId, string actorId, CancellationToken ct = default) =>
        WriteAsync(async (connection, tx) =>
        {
            GroupRole? role = await RoleOfAsync(connection, tx, groupId, actorId, ct).ConfigureAwait(false);
            if (role is null)
            {
                return GroupResult.GroupNotFound;
            }

            if (!GroupPermissions.CanDeleteGroup(role.Value))
            {
                return GroupResult.NotAuthorized;
            }

            await ExecuteAsync(connection, tx, "DELETE FROM Groups WHERE Id = $g;", ct, ("$g", groupId)).ConfigureAwait(false);
            return GroupResult.Ok;
        }, ct);

    // ---- yardımcılar ----

    /// <summary>Üyeliği siler ve o üyeye bekleyen grup mesajlarını da siler: çıkarılan kişi sonradan mesaj okuyamaz.</summary>
    private static async Task DeleteMembershipAsync(SqliteConnection connection, SqliteTransaction tx, string groupId, string userId, CancellationToken ct)
    {
        await ExecuteAsync(connection, tx, "DELETE FROM GroupMembers WHERE GroupId = $g AND UserId = $u;",
            ct, ("$g", groupId), ("$u", userId)).ConfigureAwait(false);
        await ExecuteAsync(connection, tx, "DELETE FROM PendingMessages WHERE GroupId = $g AND RecipientId = $u;",
            ct, ("$g", groupId), ("$u", userId)).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<GroupMemberRecord>> ReadMembersAsync(string groupId, CancellationToken ct)
    {
        await using SqliteConnection connection = await _database.OpenAsync(ct).ConfigureAwait(false);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT m.UserId, u.Username, m.Role FROM GroupMembers m JOIN Users u ON u.Id = m.UserId " +
            "WHERE m.GroupId = $g ORDER BY CASE m.Role WHEN 'Owner' THEN 0 WHEN 'Admin' THEN 1 ELSE 2 END, u.UsernameKey;";
        command.Parameters.AddWithValue("$g", groupId);

        var members = new List<GroupMemberRecord>();
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                members.Add(new GroupMemberRecord(reader.GetString(0), reader.GetString(1), ParseRole(reader.GetString(2))));
            }
        }

        return members;
    }


    /// <summary>İşlemi BEGIN IMMEDIATE altında çalıştırır; yalnızca Ok dönerse kaydeder, aksi halde geri alır.</summary>
    private async Task<GroupResult> WriteAsync(
        Func<SqliteConnection, SqliteTransaction, Task<GroupResult>> body, CancellationToken ct)
    {
        await using SqliteConnection connection = await _database.OpenAsync(ct).ConfigureAwait(false);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);

        GroupResult result = await body(connection, transaction).ConfigureAwait(false);
        if (result == GroupResult.Ok)
        {
            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }

        return result;
    }

    private static async Task<GroupRole?> RoleOfAsync(
        SqliteConnection connection, SqliteTransaction tx, string groupId, string userId, CancellationToken ct)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = "SELECT Role FROM GroupMembers WHERE GroupId = $g AND UserId = $u;";
        command.Parameters.AddWithValue("$g", groupId);
        command.Parameters.AddWithValue("$u", userId);
        object? value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return value is string text ? ParseRole(text) : null;
    }

    private static async Task<long> CountAsync(
        SqliteConnection connection, SqliteTransaction tx, string sql, CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return Convert.ToInt64(await command.ExecuteScalarAsync(ct).ConfigureAwait(false));
    }

    private static async Task<int> ExecuteAsync(
        SqliteConnection connection, SqliteTransaction tx, string sql, CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static GroupRole ParseRole(string text) =>
        Enum.TryParse(text, ignoreCase: false, out GroupRole role) ? role : throw new InvalidOperationException($"Bilinmeyen rol: {text}");

    private static string Now() => DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
}
