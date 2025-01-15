using System.Text.Json;
using System.Text.Json.Serialization;
using Discord;
using WasabiBot.Discord.Api;

namespace WasabiBot.Tests.Integration.Builders;

public class InteractionBuilder
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private string _id = Random.Shared.NextInt64().ToString();
    private InteractionType _type;
    private string _token = "test-token";
    private string _applicationId = Random.Shared.NextInt64().ToString();
    private int _version;
    private string? _guildId;
    private string? _channelId;
    private Entitlement[] _entitlements = [];
    private GuildMember? _guildMember;
    private DiscordInteractionData? _data;

    public InteractionBuilder WithTestValues()
    {
        _type = InteractionType.Ping;
        _version = 1;
        _guildId = Random.Shared.NextInt64().ToString();
        _channelId = Random.Shared.NextInt64().ToString();
        
        _entitlements =
        [
            new Entitlement
            {
                Id = Random.Shared.NextInt64().ToString(),
                Type = EntitlementType.Purchase,
                SkuId = Random.Shared.NextInt64().ToString(),
                ApplicationId = Random.Shared.NextInt64().ToString(),
                UserId = Random.Shared.NextInt64().ToString(),
                GuildId = Random.Shared.NextInt64().ToString()
            }
        ];

        _guildMember = new GuildMember
        {
            User = new User
            {
                Id = Random.Shared.NextInt64().ToString(),
                Username = "testuser",
                GlobalName = "User",
                Discriminator = "1234"
            },
            RoleIds = []
        };

        return this;
    }
  
    public InteractionBuilder WithType(InteractionType type)
    {
        _type = type;
        return this;
    }
    
    public InteractionBuilder WithData(DiscordInteractionData data)
    {
        _data = data;
        return this;
    }
  
    public Interaction Build()
    {
        return new Interaction
        {
            Id = _id,
            Type = _type,
            Token = _token,
            Version = _version,
            ApplicationId = _applicationId,
            GuildId = _guildId,
            ChannelId = _channelId,
            Entitlements = _entitlements,
            GuildMember = _guildMember,
            Data = _data,
        };
    }
  
    public string BuildJson()
    {
        return JsonSerializer.Serialize(Build(), JsonSerializerOptions);
    }
}