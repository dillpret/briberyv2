using Bribery.Domain;
using Microsoft.Extensions.Hosting;

namespace Bribery.Api;

public sealed class GameTickHostedService : BackgroundService
{
    private readonly GameService _gameService;
    private readonly ILogger<GameTickHostedService> _logger;

    public GameTickHostedService(GameService gameService, ILogger<GameTickHostedService> logger)
    {
        _gameService = gameService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _gameService.TickAll();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to advance game timers");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
