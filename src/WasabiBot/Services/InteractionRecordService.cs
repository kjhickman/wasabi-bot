using System.Data;
using Dapper;
using WasabiBot.Database.Entities;

namespace WasabiBot.Services;

public class InteractionRecordService
{
    private readonly IDbConnection _connection;

    public InteractionRecordService(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task CreateAsync(InteractionRecord record)
    {
        const string sql = """
                           INSERT INTO interaction_record (
                               type, data, guild_id, channel_id, member_nickname, member_avatar_hash, 
                               member_role_ids, member_joined_at, member_premium_since, member_deafened, member_muted,
                               member_permissions, user_id, username, user_global_name, version, received_at
                           )
                           VALUES (
                               @Type, @Data::jsonb, @GuildId, @ChannelId, @MemberNickname, @MemberAvatarHash, 
                               @MemberRoleIds, @MemberJoinedAt, @MemberPremiumSince, @MemberDeafened, @MemberMuted,
                               @MemberPermissions, @UserId, @Username, @UserGlobalName, @Version, @ReceivedAt
                           );
                           """;
        
        await _connection.ExecuteAsync(sql, record);
    }
}
