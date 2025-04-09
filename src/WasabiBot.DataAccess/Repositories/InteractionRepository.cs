using System.Data;
using Dapper;
using WasabiBot.DataAccess.Entities;
using WasabiBot.DataAccess.Interfaces;

namespace WasabiBot.DataAccess.Repositories;

public class InteractionRepository(IDbConnection connection) : IInteractionRepository
{
    public async Task<InteractionEntity?> GetByIdAsync(ulong id)
    {
        const string sql = """
                               select id, channel_id, application_id, user_id, guild_id, 
                                      username, global_name, nickname, data, created_at
                               from interactions
                               where id = @Id
                           """;

        return await connection.QuerySingleOrDefaultAsync<InteractionEntity>(sql, new { Id = id });
    }

    public async Task<bool> CreateAsync(InteractionEntity interaction)
    {
        const string sql = """
                               insert into interactions (
                                   id, channel_id, application_id, user_id, guild_id,
                                   username, global_name, nickname, data, created_at
                               ) 
                               values (
                                   @Id, @ChannelId, @ApplicationId, @UserId, @GuildId,
                                   @Username, @GlobalName, @Nickname, @Data::jsonb, @CreatedAt
                               )
                           """;

        var result = await connection.ExecuteAsync(sql, interaction);
        return result > 0;
    }
}
