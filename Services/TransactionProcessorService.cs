using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;

namespace SACCOBlockChainSystem.Services
{
    public class TransactionProcessorService : BackgroundService
    {
        private readonly ILogger<TransactionProcessorService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(60); // Increased to 60 seconds
        private bool _isProcessing = false;

        public TransactionProcessorService(ILogger<TransactionProcessorService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Transaction Processor Service started.");

            // Wait 2 minutes before first execution to let app stabilize
            await Task.Delay(TimeSpan.FromSeconds(120), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                // Skip if already processing
                if (_isProcessing)
                {
                    await Task.Delay(_interval, stoppingToken);
                    continue;
                }

                _isProcessing = true;

                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var blockchainService = scope.ServiceProvider.GetRequiredService<IBlockchainService>();

                    // FIRST: Check if there are ANY pending transactions (cheap count query)
                    var pendingTransactionsCount = await context.Transactions2
                        .Where(t => string.IsNullOrEmpty(t.BlockchainTxId) && t.Status == "COMPLETED")
                        .CountAsync(stoppingToken);

                    var pendingContribsCount = await context.Contribs
                        .Where(c => string.IsNullOrEmpty(c.BlockchainTxId) && c.Amount.HasValue)
                        .CountAsync(stoppingToken);

                    // ONLY process if there are pending items
                    if (pendingTransactionsCount == 0 && pendingContribsCount == 0)
                    {
                        // No work - log at Debug level (won't show in production logs)
                        _logger.LogDebug("No pending transactions to process");
                        _isProcessing = false;
                        await Task.Delay(_interval, stoppingToken);
                        continue;
                    }

                    _logger.LogInformation($"Found {pendingTransactionsCount} pending transactions, {pendingContribsCount} pending contributions");

                    // Process ONLY 5 at a time
                    var pendingTransactions = await context.Transactions2
                        .Where(t => string.IsNullOrEmpty(t.BlockchainTxId) && t.Status == "COMPLETED")
                        .Take(5)
                        .ToListAsync(stoppingToken);

                    foreach (var transaction in pendingTransactions)
                    {
                        try
                        {
                            var blockchainData = new
                            {
                                TransactionNo = transaction.TransactionNo,
                                MemberNo = transaction.MemberNo,
                                Amount = transaction.Amount,
                                TransactionType = transaction.TransactionType,
                                PaymentMode = transaction.PaymentMode,
                                Timestamp = transaction.ContributionDate
                            };

                            var blockchainTx = await blockchainService.CreateTransaction(
                                transaction.TransactionType,
                                transaction.MemberNo,
                                transaction.Companycode ?? "DEFAULT",
                                transaction.Amount,
                                transaction.Id.ToString(),
                                blockchainData
                            );

                            transaction.BlockchainTxId = blockchainTx.TransactionId;
                            await context.SaveChangesAsync(stoppingToken);

                            await blockchainService.AddToBlockchain(blockchainTx);
                            _logger.LogInformation("Processed transaction {TransactionId}", transaction.TransactionNo);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing transaction {TransactionId}", transaction.TransactionNo);
                        }
                    }

                    // Process ONLY 5 Contrib records
                    var pendingContribs = await context.Contribs
                        .Where(c => string.IsNullOrEmpty(c.BlockchainTxId) && c.Amount.HasValue)
                        .Take(5)
                        .ToListAsync(stoppingToken);

                    foreach (var contrib in pendingContribs)
                    {
                        try
                        {
                            var blockchainData = new
                            {
                                Table = "Contrib",
                                TransactionNo = contrib.TransactionNo,
                                MemberNo = contrib.MemberNo,
                                Amount = contrib.Amount,
                                ReceiptNo = contrib.ReceiptNo,
                                Purpose = contrib.Remarks,
                                Timestamp = contrib.ContrDate ?? DateTime.Now
                            };

                            var blockchainTx = await blockchainService.CreateTransaction(
                                contrib.Amount > 0 ? "DEPOSIT_CONTRIB" : "WITHDRAWAL_CONTRIB",
                                contrib.MemberNo,
                                contrib.CompanyCode ?? "DEFAULT",
                                contrib.Amount ?? 0,
                                contrib.Id.ToString(),
                                blockchainData
                            );

                            contrib.BlockchainTxId = blockchainTx.TransactionId;
                            await context.SaveChangesAsync(stoppingToken);

                            await blockchainService.AddToBlockchain(blockchainTx);
                            _logger.LogInformation("Processed Contrib record {Id}", contrib.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing Contrib record {Id}", contrib.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in transaction processor");
                }
                finally
                {
                    _isProcessing = false;
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
    }
}