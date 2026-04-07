using Microsoft.EntityFrameworkCore;
using RekovaBE_CSharp.Data;

namespace RekovaBE_CSharp.Services
{
    public class TransactionExpirationService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TransactionExpirationService> _logger;

        public TransactionExpirationService(
            IServiceScopeFactory scopeFactory,
            ILogger<TransactionExpirationService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Transaction Expiration Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        
                        var thirtySecondsAgo = DateTime.UtcNow.AddSeconds(-30);
                        
                        var expiredTransactions = await context.Transactions
                            .Where(t => t.Status == "PENDING" && 
                                       (t.StkPushSentAt ?? t.CreatedAt) < thirtySecondsAgo)
                            .ToListAsync(stoppingToken);

                        foreach (var transaction in expiredTransactions)
                        {
                            transaction.Status = "EXPIRED";
                            transaction.UpdatedAt = DateTime.UtcNow;
                            _logger.LogInformation($"Transaction {transaction.TransactionId} expired automatically");
                        }

                        if (expiredTransactions.Any())
                        {
                            await context.SaveChangesAsync(stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error in transaction expiration service: {ex.Message}");
                }

                await Task.Delay(10000, stoppingToken);
            }
        }
    }
}
