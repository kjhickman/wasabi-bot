using System.Text.Json;
using System.Text.Json.Serialization;
using WasabiBot.Api.Core.Extensions;
using WasabiBot.DataAccess.Entities;

namespace WasabiBot.Api.Features.Interactions;

public class InteractionDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }
    
    [JsonPropertyName("channel_id")]
    public long ChannelId { get; set; }
    
    [JsonPropertyName("application_id")]
    public long ApplicationId { get; set; }
    
    [JsonPropertyName("user_id")]
    public long UserId { get; set; }
    
    [JsonPropertyName("guild_id")]
    public long? GuildId { get; set; }
    
    [JsonPropertyName("username")]
    public required string Username { get; set; }
    
    [JsonPropertyName("global_name")]
    public string? GlobalName { get; set; }
    
    [JsonPropertyName("nickname")]
    public string? Nickname { get; set; }
    
    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }
    
    [JsonPropertyName("created_at")]
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
