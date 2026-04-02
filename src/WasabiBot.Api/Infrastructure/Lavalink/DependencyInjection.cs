using Lavalink4NET;
using Lavalink4NET.NetCord;
using Microsoft.Extensions.Options;

namespace WasabiBot.Api.Infrastructure.Lavalink;

internal static class DependencyInjection
{
    public static void AddLavalinkServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddLavalink();

        builder.Services.AddOptions<LavalinkOptions>()
            .Bind(builder.Configuration.GetSection(LavalinkOptions.SectionName))
            .ValidateDataAnnotations()
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
