using Lavalink4NET.Tracks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WasabiBot.Api.Features.Music;
using WasabiBot.Api.Features.Radio;
using WasabiBot.Api.Persistence;

namespace WasabiBot.UnitTests.Features.Music;

public class MusicPlaybackStatsRecorderTests
{
    [Test]
    public async Task RecordTrackStartedAsync_PersistsAndIncrementsGuildPlayCount()
    {
        var options = new DbContextOptionsBuilder<WasabiBotContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var services = new ServiceCollection()
            .AddScoped(_ => new WasabiBotContext(options))
            .BuildServiceProvider();

        var recorder = new MusicPlaybackStatsRecorder(
            services.GetRequiredService<IServiceScopeFactory>(),
            new RadioTrackMetadataStore(),
            NullLogger<MusicPlaybackStatsRecorder>.Instance);

        var track = new LavalinkTrack
        {
            Identifier = "creep",
            Title = "Creep",
            Author = "Radiohead",
            SourceName = "scsearch",
            Uri = new Uri("https://soundcloud.com/radiohead/creep"),
            IsSeekable = true,
            Duration = TimeSpan.FromMinutes(4),
        };

        await recorder.RecordTrackStartedAsync(42, track);
        await recorder.RecordTrackStartedAsync(42, track);

        await using var context = new WasabiBotContext(options);
        var play = await context.GuildTrackPlays.SingleAsync();
        await Assert.That(play.PlayCount).IsEqualTo(2);
        await Assert.That(play.Title).IsEqualTo("Creep");
    }
}
