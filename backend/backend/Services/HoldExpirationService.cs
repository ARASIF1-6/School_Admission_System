using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Services;

public class HoldExpirationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HoldExpirationService> _logger;

    public HoldExpirationService(IServiceProvider serviceProvider, ILogger<HoldExpirationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var now = DateTime.UtcNow;
                var expiredHolds = await db.Holds
                    .Where(h => h.Status == HoldStatus.Held && h.ExpiresAt <= now)
                    .ToListAsync(stoppingToken);

                if (expiredHolds.Count > 0)
                {
                    foreach (var h in expiredHolds)
                    {
                        h.Status = HoldStatus.Expired;
                    }
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Expired {Count} seat holds automatically.", expiredHolds.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in hold expiration service.");
            }

            // Check every 5 seconds
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
