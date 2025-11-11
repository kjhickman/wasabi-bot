using System.Text.Json;
using WasabiBot.Api.Core.Extensions;
using WasabiBot.DataAccess.Entities;

namespace WasabiBot.Api.Features.Interactions;

public class InteractionDto
{
    public long Id { get; set; }
    public long ChannelId { get; set; }
    public long ApplicationId { get; set; }
    public long UserId { get; set; }
    public long? GuildId { get; set; }
    public required string Username { get; set; }
    public string? GlobalName { get; set; }
    public string? Nickname { get; set; }
    public JsonElement? Data { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public static InteractionDto FromEntity(InteractionEntity entity)
    {
        return new InteractionDto
        {
            Id = entity.Id,
            ChannelId = entity.ChannelId,
            ApplicationId = entity.ApplicationId,
            UserId = entity.UserId,
            GuildId = entity.GuildId,
            Username = entity.Username,
            GlobalName = entity.GlobalName,
            Nickname = entity.Nickname,
            Data = entity.Data.IsNullOrWhiteSpace() ? null : JsonSerializer.Deserialize<JsonElement>(entity.Data),
            CreatedAt = entity.CreatedAt
        };
    }
}
