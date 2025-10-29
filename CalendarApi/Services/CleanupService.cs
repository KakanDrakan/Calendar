using CalendarApi.Stores;

namespace CalendarApi.Services
{
    public class CleanupService : BackgroundService
    {
        private readonly SessionStore sessionStore;
        private readonly TimeSpan cleanupInterval = TimeSpan.FromMinutes(30);
        private readonly ILogger<CleanupService> logger;

        public CleanupService(SessionStore sessionStore, ILogger<CleanupService> logger)
        {
            this.sessionStore = sessionStore;
            this.logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("CleanupService is starting.");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    sessionStore.CleanupExpiredAsync();
                    logger.LogInformation("Expired sessions cleaned up at {Time}.", DateTime.UtcNow);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error occurred during cleanup of expired sessions.");
                }
                await Task.Delay(cleanupInterval, stoppingToken);
            }
            logger.LogInformation("CleanupService is stopping.");
        }
    }
}
