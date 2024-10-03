using dotenv.net;
using Microsoft.AspNetCore.Http.HttpResults;
using VinceBot;
using VinceBot.Discord;
using VinceBot.Discord.Enums;
using VinceBot.Filters;
using VinceBot.Services;

var builder = WebApplication.CreateSlimBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, JsonContext.Default);
});
builder.Services.AddScoped<IInteractionService, InteractionService>();

DotEnv.Load();

var app = builder.Build();

app.MapPost("/", async Task<Results<Ok<InteractionResponse>, ProblemHttpResult>> (HttpContext ctx, IInteractionService interactionService) =>
{
    var interaction = await ctx.Request.ReadFromJsonAsync(JsonContext.Default.Interaction);
    if (interaction == null)
    {
        Console.WriteLine("Interaction could not be deserialized.");
        return TypedResults.Problem();
    }

    if (interaction.Type == InteractionType.Ping)
    {
        // ACK ping
        return TypedResults.Ok(InteractionResponse.Pong());
    }
    
    var response = await interactionService.HandleInteraction(interaction);

    return TypedResults.Ok(response);
}).AddEndpointFilter<DiscordValidationFilter>();

app.Run();