using System.Threading.Channels;
using OpenTelemetry.Trace;
using WasabiBot.DataAccess.Entities;
using WasabiBot.DataAccess.Services;

namespace WasabiBot.Web.Services;

public class BackgroundInteractionService : BackgroundService
{
    private readonly ILogger<BackgroundInteractionService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly Channel<InteractionRecord> Channel = System.Threading.Channels.Channel.CreateUnbounded<InteractionRecord>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public BackgroundInteractionService(ILogger<BackgroundInteractionService> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }
    
    public static async Task QueueInteractionAsync(string interaction)
    {
        var record = InteractionRecord.FromInteractionJson(interaction);
        await Channel.Writer.WriteAsync(record);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var interaction = await Channel.Reader.ReadAsync(stoppingToken);
                await ProcessInteractionAsync(interaction);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to process interaction");
                throw;
            }
        }
    }

    private async Task ProcessInteractionAsync(InteractionRecord interaction)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var tracer = scope.ServiceProvider.GetRequiredService<Tracer>();
        var interactionRecordService = scope.ServiceProvider.GetRequiredService<InteractionRecordService>();
        using var span = tracer.StartActiveSpan($"{nameof(BackgroundInteractionService)}.{nameof(ProcessInteractionAsync)}");
        await interactionRecordService.CreateAsync(interaction);
    }
}