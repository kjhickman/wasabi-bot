namespace WasabiBot.Api.Features.Music;

internal interface IMusicDashboardSearchService
{
    Task<MusicDashboardSearchResults> SearchAsync(string query, CancellationToken cancellationToken = default);
}
