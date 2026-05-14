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
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        if (request.UserId.HasValue)
        {
            conditions.Add("\"UserId\" = @UserId");
            parameters.Add("UserId", request.UserId.Value);
        }

        if (request.ChannelId.HasValue)
        {
            conditions.Add("\"ChannelId\" = @ChannelId");
            parameters.Add("ChannelId", request.ChannelId.Value);
        }

        if (request.ApplicationId.HasValue)
        {
            conditions.Add("\"ApplicationId\" = @ApplicationId");
            parameters.Add("ApplicationId", request.ApplicationId.Value);
        }

        if (request.GuildId.HasValue)
        {
            conditions.Add("\"GuildId\" = @GuildId");
            parameters.Add("GuildId", request.GuildId.Value);
        }

        if (request.Cursor != null)
        {
            parameters.Add("CursorCreatedAt", request.Cursor.CreatedAt);
            parameters.Add("CursorId", request.Cursor.Id);
            if (request.SortDirection == SortDirection.Desc)
            {
                conditions.Add("(\"CreatedAt\" < @CursorCreatedAt OR (\"CreatedAt\" = @CursorCreatedAt AND \"Id\" < @CursorId))");
            }
            else
            {
                conditions.Add("(\"CreatedAt\" > @CursorCreatedAt OR (\"CreatedAt\" = @CursorCreatedAt AND \"Id\" > @CursorId))");
            }
        }

        parameters.Add("Limit", request.Limit + 1);
        var whereClause = conditions.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", conditions);
        var orderClause = request.SortDirection == SortDirection.Desc
            ? "ORDER BY \"CreatedAt\" DESC, \"Id\" DESC"
            : "ORDER BY \"CreatedAt\", \"Id\"";

        var sql = $"""
            SELECT "Id", "ChannelId", "ApplicationId", "UserId", "GuildId", "Username", "GlobalName", "Nickname", "Data", "CreatedAt"
            FROM "Interactions"
            {whereClause}
            {orderClause}
            LIMIT @Limit
            """;

        await using var connection = await dataSource.OpenConnectionAsync();
        var interactions = await connection.QueryAsync<InteractionRow>(sql, parameters);
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
