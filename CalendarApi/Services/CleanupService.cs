using CalendarApi.Stores;
using Microsoft.AspNetCore.Razor.TagHelpers;
using StackExchange.Redis;

namespace CalendarApi.Services
{
    public class CleanupService : BackgroundService
    {
        private readonly IConnectionMultiplexer redis;
        private readonly SessionStore sessionStore;
        private readonly SubscriptionStore subscriptionStore;
        private readonly GraphSubscriptionService graphSubscriptionService;
        private readonly TimeSpan cleanupInterval = TimeSpan.FromMinutes(15);
        private readonly ILogger<CleanupService> logger;

        public CleanupService(SessionStore sessionStore, ILogger<CleanupService> logger, SubscriptionStore subscription, GraphSubscriptionService graphSubscriptionService, IConnectionMultiplexer redis)
        {
            this.sessionStore = sessionStore;
            this.logger = logger;
            this.subscriptionStore = subscription;
            this.graphSubscriptionService = graphSubscriptionService;
            this.redis = redis;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("CleanupService is starting.");
            while (!stoppingToken.IsCancellationRequested)
            {
                await CleanupSessions();

                await CleanupSubscriptions();

                await Task.Delay(cleanupInterval, stoppingToken);
            }
            logger.LogInformation("CleanupService is stopping.");
        }

        private async Task CleanupSessions()
        {
            try
            {
                await sessionStore.CleanupExpiredAsync();
                logger.LogInformation("Expired sessions cleaned up at {Time}.", DateTime.Now);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred during cleanup of expired sessions.");
            }
        }

        private async Task CleanupSubscriptions()
        {
            var now = DateTime.UtcNow;
            var soon = now.Add(cleanupInterval);
            var db = redis.GetDatabase();

            var expiring = await subscriptionStore.GetExpiringSubscriptionsAsync(soon);

            foreach (var sub in expiring)
            {
                var activeCount = await db.SetLengthAsync($"active:calendar:{sub.SubscriptionId}");
                var isActive = activeCount > 0;

                if (isActive)
                {
                    try
                    {
                        await graphSubscriptionService.RenewSubscriptionAsync(sub);
                        logger.LogInformation("Renewed active subscription {SubscriptionId}", sub.SubscriptionId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error renewing {SubscriptionId}", sub.SubscriptionId);
                    }
                }
                else
                {
                    logger.LogInformation("Deleting subscription {SubscriptionId} for inactive calendar {CalendarId}.", sub.SubscriptionId, sub.CalendarId);
                    try
                    {
                        await graphSubscriptionService.DeleteSubscriptionAsync(sub.SubscriptionId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error occurred during subscription deletion");
                    }
                }
            }
        }
    }
}