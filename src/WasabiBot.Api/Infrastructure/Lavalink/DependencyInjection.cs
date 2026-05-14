using Lavalink4NET;
using Lavalink4NET.Clients;
using Lavalink4NET.NetCord;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NetCord.Gateway;

namespace WasabiBot.Api.Infrastructure.Lavalink;

internal static class DependencyInjection
{
    public static void AddLavalinkServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddLavalink();
        builder.Services.Replace(ServiceDescriptor.Singleton<IDiscordClientWrapper>(serviceProvider =>
            new DiscordClientWrapper(serviceProvider.GetRequiredService<GatewayClient>())));

        builder.Services.AddOptions<LavalinkOptions>()
            .Bind(builder.Configuration.GetSection(LavalinkOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.BaseUrl), "Lavalink:BaseUrl is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Password), "Lavalink:Password is required.")
            .Validate(options => options.ResumeTimeoutSeconds is >= 1 and <= 3600, "Lavalink:ResumeTimeoutSeconds must be between 1 and 3600.")
            .ValidateOnStart();

        builder.Services.AddOptions<AudioServiceOptions>()
            .Configure<IOptions<LavalinkOptions>>((audioOptions, lavalinkOptions) =>
            {
                var options = lavalinkOptions.Value;

                audioOptions.BaseAddress = new Uri(options.BaseUrl!, UriKind.Absolute);
                audioOptions.Passphrase = options.Password!;
                audioOptions.ResumptionOptions = new LavalinkSessionResumptionOptions(
                    TimeSpan.FromSeconds(options.ResumeTimeoutSeconds));
            });
    }
}
