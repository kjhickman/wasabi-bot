using System.Text.Json;
using NetCord;
using NetCord.JsonModels;
using WasabiBot.Api.Core.Serialization;
using WasabiBot.DataAccess.Entities;

namespace WasabiBot.Api.Core.Extensions;

internal static class InteractionExtensions
{
    public static InteractionEntity ToEntity(this Interaction interaction)
    {
        IJsonModel<JsonInteraction> jsonInteraction = interaction;
        var interactionData = jsonInteraction.JsonModel.Data;

        return new InteractionEntity
        {
            Id = (long)interaction.Id,
            ChannelId = (long)interaction.Channel.Id,
            ApplicationId = (long)interaction.ApplicationId,
            UserId = (long)interaction.User.Id,
            GuildId = interaction.GuildId.HasValue ? (long?)interaction.GuildId.Value : null,
            Username = interaction.User.Username,
            GlobalName = interaction.User.GlobalName,
            Nickname = (interaction.User as GuildUser)?.Nickname,
            Data = JsonSerializer.Serialize(interactionData, JsonContext.Default.JsonInteractionData),
            CreatedAt = interaction.CreatedAt
        };
    }
}
