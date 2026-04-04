using WasabiBot.Api.Features.Music;
using WasabiBot.Api.Infrastructure.Discord.Abstractions;

namespace WasabiBot.Api.Features.Radio;

internal interface IRadioService
{
    Task<MusicCommandResult> PlayAsync(ICommandContext ctx, string query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RadioBrowserStation>> SearchStationsAsync(string query, CancellationToken cancellationToken = default);
}
