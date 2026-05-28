using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;

namespace SACCOBlockChainSystem.Services
{
    public class BlockchainSyncService : BackgroundService
    {
        private readonly ILogger<BlockchainSyncService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(10); // Increased to 10 minutes

        public BlockchainSyncService(ILogger<BlockchainSyncService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Blockchain Sync Service started.");

            // Wait 5 minutes before first execution
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var blockchainService = scope.ServiceProvider.GetRequiredService<IBlockchainService>();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    // Check if there are pending transactions (cheap count query)
                    var pendingCount = await context.BlockchainTransactions
                        .CountAsync(t => t.Status == "PENDING", stoppingToken);

                    if (pendingCount > 0)
                    {
                        _logger.LogInformation($"Found {pendingCount} pending transactions to sync");
                        await blockchainService.ProcessPendingTransactionsAsync();
                        _logger.LogInformation("Blockchain sync completed at {Time}", DateTime.UtcNow);
                    }
                    else
                    {
                        _logger.LogDebug("No pending transactions to sync");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during blockchain sync");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
    }
}