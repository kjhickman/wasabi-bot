using System.Text.Json;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.JsonModels;
using WasabiBot.DataAccess.Entities;
using WasabiBot.DataAccess.Interfaces;

namespace WasabiBot.Api.EventHandlers;

[GatewayEvent(nameof(GatewayClient.InteractionCreate))]
public class InteractionCreatedEventHandler(IServiceProvider provider) : IGatewayEventHandler<Interaction>
{
    public async ValueTask HandleAsync(Interaction interaction)
    {
        IJsonModel<JsonInteraction> jsonInteraction = interaction;
        var interactionData = jsonInteraction.JsonModel.Data;
        var entity = new InteractionEntity
        {
            Id = interaction.Id,
            ChannelId = interaction.Channel.Id,
            ApplicationId = interaction.ApplicationId,
            UserId = interaction.User.Id,
            GuildId = interaction.GuildId,
            Username = interaction.User.Username,
            GlobalName = interaction.User.GlobalName,
            Data = JsonSerializer.Serialize(interactionData, JsonContext.Default.JsonInteractionData),
            CreatedAt = interaction.CreatedAt
        };

        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInteractionRepository>();

        await repository.CreateAsync(entity);
    }
}
