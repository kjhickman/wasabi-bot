using System.Text.Json;
using Discord;
using WasabiBot.Discord.Api;

public class DiscordInteractionDataBuilder
{
    private string _id = Random.Shared.NextInt64().ToString();
    private string? _name;
    private int _type = (int)ApplicationCommandType.Slash;
    private List<InteractionDataOption>? _options;
    private string? _guildId;

    public DiscordInteractionDataBuilder WithTestValues()
    {
        _name = "foo";
        _guildId = Random.Shared.NextInt64().ToString();

        return this;
    }
    
    public DiscordInteractionDataBuilder WithName(string name)
    {
        _name = name;
        return this;
    }
    
    public DiscordInteractionDataBuilder WithStringDataOption(string name, string value)
    {
        _options ??= [];
        _options.Add(new InteractionDataOption
        {
            Name = name,
            Type = ApplicationCommandOptionType.String,
            Value = JsonDocument.Parse($"\"{value}\"").RootElement
        });
        return this;
    }
    
    public DiscordInteractionData Build()
    {
        return new DiscordInteractionData
        {
            Id = _id,
            Name = _name ?? throw new InvalidOperationException("Name is required"),
            Type = _type,
            Options = _options?.ToArray(),
            GuildId = _guildId,
        };
    }
}