using CalendarApi.Helpers;
using CalendarApi.Stores;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace CalendarApi.Services
{
    public class GraphSubscriptionService
    {
        private readonly GraphServiceClient graphService;
        private readonly IConfiguration config;
        private readonly SubscriptionStore store;

        public GraphSubscriptionService(GraphServiceClient graphService, IConfiguration config, SubscriptionStore store)
        {
            this.graphService = graphService;
            this.config = config;
            this.store = store;
        }

        public async Task<Subscription?> CreateCalendarSubscriptionAsync(string calendarId, string resource)
        {
            if(store.TryGetSubscription(calendarId, out var existing))
            {
                return existing;
            }

            var notificationUrl = config["Devtunnel:Url"] + "/api/webhook"; //CHANGE FOR PRODUCTION
            Console.WriteLine(notificationUrl);
            if (string.IsNullOrEmpty(notificationUrl))
                throw new Exception("NotificationUrl is not configured");

            var subscription = new Subscription
            {
                ChangeType = "created,updated,deleted",
                NotificationUrl = notificationUrl,
                Resource = resource,
                ExpirationDateTime = DateTime.UtcNow.AddDays(2)
            };

            try
            {
                var result = await graphService.Subscriptions.PostAsync(subscription);
                store.SaveSubscription(calendarId, result);
                ConsoleHelper.WriteTimeToConsole();
                Console.WriteLine($"Subscription created: {result.Id}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating subscription: {ex.Message}");
                throw;
            }
        }
        public async Task<int> DeleteSubscriptionsForResourceAsync(string targetResource)
        {
            int deletedCount = 0;

            try
            {
                var subscriptions = await graphService.Subscriptions.GetAsync();

                if (subscriptions?.Value != null)
                {
                    foreach (var sub in subscriptions.Value)
                    {
                        if (sub.Resource?.Equals(targetResource, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            await graphService.Subscriptions[sub.Id].DeleteAsync();
                            Console.WriteLine($"Deleted subscription: {sub.Id}");
                            deletedCount++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting subscriptions: {ex.Message}");
            }

            return deletedCount;
        }
    }
}
