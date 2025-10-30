using CalendarApi.Stores;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace CalendarApi.Services
{
    public class CleanupService : BackgroundService
    {
        private readonly SessionStore sessionStore;
        private readonly SubscriptionStore subscriptionStore;
        private readonly GraphSubscriptionService graphSubscriptionService;
        private readonly TimeSpan cleanupInterval = TimeSpan.FromMinutes(15);
        private readonly ILogger<CleanupService> logger;

        public CleanupService(SessionStore sessionStore, ILogger<CleanupService> logger, SubscriptionStore subscription, GraphSubscriptionService graphSubscriptionService)
        {
            this.sessionStore = sessionStore;
            this.logger = logger;
            this.subscriptionStore = subscription;
            this.graphSubscriptionService = graphSubscriptionService;
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
                logger.LogInformation("Expired sessions cleaned up at {Time}.", DateTime.UtcNow);
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

            var expiring = await subscriptionStore.GetExpiringSubscriptionsAsync(soon);

            foreach (var sub in expiring)
            {
                if (CalendarHub.IsCalendarActive(sub.CalendarId))
                {
                    try
                    {
                        await graphSubscriptionService.RenewSubscriptionAsync(sub);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error occurred during subscription renewal");
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