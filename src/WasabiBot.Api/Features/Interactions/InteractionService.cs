using System.ComponentModel;
using Dapper;
using System.Text.Json.Serialization;
using Npgsql;
using OpenTelemetry.Trace;
using WasabiBot.Api.Persistence.Entities;

namespace WasabiBot.Api.Features.Interactions;

public sealed class InteractionService(NpgsqlDataSource dataSource, Tracer tracer) : IInteractionService
{
    public async Task<InteractionEntity?> GetByIdAsync(long id)
    {
        using var span = tracer.StartActiveSpan("interaction.get");
        const string sql = """
            SELECT "Id", "ChannelId", "ApplicationId", "UserId", "GuildId", "Username", "GlobalName", "Nickname", "Data", "CreatedAt"
            FROM "Interactions"
            WHERE "Id" = @Id
            """;

        await using var connection = await dataSource.OpenConnectionAsync();
        return (await connection.QueryFirstOrDefaultAsync<InteractionRow>(sql, new { Id = id }))?.ToEntity();
    }

    public async Task<InteractionEntity[]> GetAllAsync(GetAllInteractionsRequest request)
    {
        using var span = tracer.StartActiveSpan("interaction.getAll");
        const string descSql = """
            SELECT "Id", "ChannelId", "ApplicationId", "UserId", "GuildId", "Username", "GlobalName", "Nickname", "Data", "CreatedAt"
            FROM "Interactions"
            WHERE (@UserId IS NULL OR "UserId" = @UserId)
              AND (@ChannelId IS NULL OR "ChannelId" = @ChannelId)
              AND (@ApplicationId IS NULL OR "ApplicationId" = @ApplicationId)
              AND (@GuildId IS NULL OR "GuildId" = @GuildId)
              AND (@CursorCreatedAt IS NULL OR "CreatedAt" < @CursorCreatedAt OR ("CreatedAt" = @CursorCreatedAt AND "Id" < @CursorId))
            ORDER BY "CreatedAt" DESC, "Id" DESC
            LIMIT @Limit
            """;

        const string ascSql = """
            SELECT "Id", "ChannelId", "ApplicationId", "UserId", "GuildId", "Username", "GlobalName", "Nickname", "Data", "CreatedAt"
            FROM "Interactions"
            WHERE (@UserId IS NULL OR "UserId" = @UserId)
              AND (@ChannelId IS NULL OR "ChannelId" = @ChannelId)
              AND (@ApplicationId IS NULL OR "ApplicationId" = @ApplicationId)
              AND (@GuildId IS NULL OR "GuildId" = @GuildId)
              AND (@CursorCreatedAt IS NULL OR "CreatedAt" > @CursorCreatedAt OR ("CreatedAt" = @CursorCreatedAt AND "Id" > @CursorId))
            ORDER BY "CreatedAt", "Id"
            LIMIT @Limit
            """;

        var parameters = new
        {
            request.UserId,
            request.ChannelId,
            request.ApplicationId,
            request.GuildId,
            CursorCreatedAt = request.Cursor?.CreatedAt,
            CursorId = request.Cursor?.Id,
            Limit = request.Limit + 1,
        };

        await using var connection = await dataSource.OpenConnectionAsync();
        var interactions = request.SortDirection == SortDirection.Desc
            ? await connection.QueryAsync<InteractionRow>(descSql, parameters)
            : await connection.QueryAsync<InteractionRow>(ascSql, parameters);
        return interactions.Select(row => row.ToEntity()).ToArray();
    }

    public async Task<bool> CreateAsync(InteractionEntity interaction)
    {
        using var span = tracer.StartActiveSpan("interaction.create");
        const string sql = """
            INSERT INTO "Interactions" ("Id", "ChannelId", "ApplicationId", "UserId", "GuildId", "Username", "GlobalName", "Nickname", "Data", "CreatedAt")
            VALUES (@Id, @ChannelId, @ApplicationId, @UserId, @GuildId, @Username, @GlobalName, @Nickname, @Data::jsonb, @CreatedAt)
            """;

        await using var connection = await dataSource.OpenConnectionAsync();
        return await connection.ExecuteAsync(sql, interaction) > 0;
    }
}

internal sealed class InteractionRow
{
    public long Id { get; set; }
    public long ChannelId { get; set; }
    public long ApplicationId { get; set; }
    public long UserId { get; set; }
    public long? GuildId { get; set; }
    public required string Username { get; set; }
    public string? GlobalName { get; set; }
    public string? Nickname { get; set; }
    public string? Data { get; set; }
    public DateTime CreatedAt { get; set; }

    public InteractionEntity ToEntity() => new()
    {
        Id = Id,
        ChannelId = ChannelId,
        ApplicationId = ApplicationId,
        UserId = UserId,
        GuildId = GuildId,
        Username = Username,
        GlobalName = GlobalName,
        Nickname = Nickname,
        Data = Data,
        CreatedAt = new DateTimeOffset(CreatedAt.ToUniversalTime()),
    };
}

public class GetAllInteractionsRequest
{
    public long? UserId { get; set; }
    public long? ChannelId { get; set; }
    public long? ApplicationId { get; set; }
    public long? GuildId { get; set; }
    public int Limit { get; set; }
    public SortDirection SortDirection { get; set; } = SortDirection.Desc;
    public InteractionCursor? Cursor { get; set; }
}

public class InteractionCursor
{
    public DateTimeOffset CreatedAt { get; set; }
    public long Id { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter<SortDirection>))]
public enum SortDirection
{
    [Description("Ascending")]
    [JsonStringEnumMemberName("asc")]
    Asc,

    [Description("Descending")]
    [JsonStringEnumMemberName("desc")]
    Desc
}
