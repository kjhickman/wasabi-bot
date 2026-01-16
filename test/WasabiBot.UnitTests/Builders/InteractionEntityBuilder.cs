using WasabiBot.DataAccess.Entities;

namespace WasabiBot.UnitTests.Builders;

public sealed class InteractionEntityBuilder
{
    private long _id = 1;
    private long _userId = 123456789;
    private long _channelId = 999;
    private long _applicationId = 888;
    private long? _guildId;
    private string _username = "testuser";
    private string? _globalName;
    private string? _nickname;
    private string? _data;
    private DateTimeOffset _createdAt = DateTimeOffset.UtcNow;

    public static InteractionEntityBuilder Create() => new();

    public InteractionEntityBuilder WithId(long id)
    {
        _id = id;
        return this;
    }

    public InteractionEntityBuilder WithUserId(long userId)
    {
        _userId = userId;
        return this;
    }

    public InteractionEntityBuilder WithChannelId(long channelId)
    {
        _channelId = channelId;
        return this;
    }

    public InteractionEntityBuilder WithApplicationId(long applicationId)
    {
        _applicationId = applicationId;
        return this;
    }

    public InteractionEntityBuilder WithGuildId(long? guildId)
    {
        _guildId = guildId;
        return this;
    }

    public InteractionEntityBuilder WithUsername(string username)
    {
        _username = username;
        return this;
    }

    public InteractionEntityBuilder WithGlobalName(string? globalName)
    {
        _globalName = globalName;
        return this;
    }

    public InteractionEntityBuilder WithNickname(string? nickname)
    {
        _nickname = nickname;
        return this;
    }

    public InteractionEntityBuilder WithData(string? data)
    {
        _data = data;
        return this;
    }

    public InteractionEntityBuilder WithCreatedAt(DateTimeOffset createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public InteractionEntity Build()
    {
        return new InteractionEntity
        {
            Id = _id,
            UserId = _userId,
            ChannelId = _channelId,
            ApplicationId = _applicationId,
            GuildId = _guildId,
            Username = _username,
            GlobalName = _globalName,
            Nickname = _nickname,
            Data = _data,
            CreatedAt = _createdAt
        };
    }
}
