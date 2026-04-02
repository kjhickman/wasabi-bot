namespace WasabiBot.Api.Infrastructure.Lavalink;

internal static class DependencyInjection
{
    public static void AddLavalinkServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<LavalinkOptions>()
            .Bind(builder.Configuration.GetSection(LavalinkOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
