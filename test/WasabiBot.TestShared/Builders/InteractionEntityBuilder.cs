using WasabiBot.DataAccess.Entities;

namespace WasabiBot.TestShared.Builders;

/// <summary>Builder for creating InteractionEntity test data.</summary>
public static class InteractionEntityBuilder
{
    /// <summary>Creates an InteractionEntity with sensible defaults for testing.</summary>
    public static InteractionEntity Create(
        long id = 1,
        long userId = 111,
        long channelId = 222,
        long applicationId = 333,
        long? guildId = 444,
        string username = "TestUser",
        string? data = null,
        DateTimeOffset? createdAt = null)
    {
        return new InteractionEntity
        {
            Id = id,
            UserId = userId,
            ChannelId = channelId,
            ApplicationId = applicationId,
            GuildId = guildId,
            Username = username,
            GlobalName = "TestGlobalName",
            Nickname = "TestNickname",
            Data = data,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
    }

    /// <summary>Creates multiple InteractionEntity records for batch testing.</summary>
    public static InteractionEntity[] CreateMultiple(
        int count = 5,
        long? baseUserId = null,
        long? baseChannelId = null,
        long? baseApplicationId = null)
    {
        var entities = new InteractionEntity[count];
        var baseTime = DateTimeOffset.UtcNow.AddDays(-count);

        for (int i = 0; i < count; i++)
        {
            entities[i] = new InteractionEntity
            {
                Id = 1000 + i,
                UserId = baseUserId ?? (100 + i),
                ChannelId = baseChannelId ?? (200 + i),
                ApplicationId = baseApplicationId ?? 333,
                GuildId = 444,
                Username = $"User{i}",
                GlobalName = $"Global{i}",
                Nickname = $"Nick{i}",
                Data = null,
                CreatedAt = baseTime.AddDays(i)
            };
        }

        return entities;
    }
}
