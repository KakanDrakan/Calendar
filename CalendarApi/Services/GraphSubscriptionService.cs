using Azure.Core;
using Azure.Identity;
using CalendarApi.Helpers;
using CalendarApi.Models;
using CalendarApi.Stores;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System.Globalization;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

namespace CalendarApi.Services
{
    public class GraphSubscriptionService
    {
        private GraphServiceClient graphService;
        private readonly IConfiguration config;
        private readonly SubscriptionStore subscriptionStore;
        private readonly SessionStore sessionStore;

        public GraphSubscriptionService(IConfiguration config, SubscriptionStore store, SessionStore sessionStore)
        {
            this.config = config;
            this.subscriptionStore = store;
            this.sessionStore = sessionStore;
        }

        public async Task<CalendarSubscription?> CreateCalendarSubscriptionAsync(string calendarId, string sessionId)
        {
            (var subscriptionExists, var existing) = await subscriptionStore.TryGetSubscription(calendarId);

            if (subscriptionExists) return existing;

            (var sessionExists, var session) = await sessionStore.TryGetSessionAsync(sessionId);
            if (!sessionExists) throw new ArgumentException("Session not found", nameof(sessionId));

            var accessToken = session.AccessToken;

            var credential = new DelegateCredential((_, _) =>
                new ValueTask<AccessToken>(
                new AccessToken(accessToken, session.TokenExpiresAt ?? DateTimeOffset.UtcNow.AddMinutes(50))
             ));

            graphService = new GraphServiceClient(credential);

            var userId = (await graphService.Me.GetAsync()).Id;

            var notificationUrl = config["Urls:Backend"] + "/api/webhook"; //CHANGE FOR PRODUCTION
            Console.WriteLine(notificationUrl);
            if (string.IsNullOrEmpty(notificationUrl))
                throw new Exception("NotificationUrl is not configured");

            var subscription = new Subscription
            {
                ChangeType = "created,updated,deleted",
                NotificationUrl = notificationUrl,
                Resource = $"/users/{userId}/calendars/{calendarId}/events",
                ExpirationDateTime = DateTimeOffset.UtcNow.AddMinutes(int.Parse(config["ExpirationTime:Subscriptions"]))
            };

            try
            {
                await DeleteSubscriptionsForResourceAsync($"/users/{userId}/calendars/{calendarId}/events");
                var result = await graphService.Subscriptions.PostAsync(subscription);
                await subscriptionStore.SaveSubscriptionAsync(calendarId, result, userId);
                ConsoleHelper.WriteTimeToConsole();
                Console.WriteLine($"Subscription created: {result.Id} for resource {result.Resource}");
                var dto = new CalendarSubscription
                {
                    CalendarId = calendarId,
                    SubscriptionId = result.Id!,
                    ExpiresAt = result.ExpirationDateTime!.Value,
                };
                return dto;
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
            var credential = new ClientSecretCredential(
                tenantId: config["AzureAd:TenantId"],
                clientId: config["AzureAd:ClientId"],
                clientSecret: config["AzureAd:ClientSecret"]);

            var graphService = new GraphServiceClient(credential);


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
        public async Task RenewSubscriptionAsync(CalendarSubscription sub)
        {
            var credential = new ClientSecretCredential(
                tenantId: config["AzureAd:TenantId"],
                clientId: config["AzureAd:ClientId"],
                clientSecret: config["AzureAd:ClientSecret"]);
            var graphService = new GraphServiceClient(credential);
            var updatedSubscription = new Subscription
            {
                ExpirationDateTime = DateTimeOffset.UtcNow.AddMinutes(int.Parse(config["ExpirationTime:Subscriptions"]))
            };
            try
            {
                var result = await graphService.Subscriptions[sub.SubscriptionId].PatchAsync(updatedSubscription);
                sub.ExpiresAt = result.ExpirationDateTime!.Value;
                await subscriptionStore.SaveSubscriptionAsync(sub.CalendarId, result, sub.UserId);
                ConsoleHelper.WriteTimeToConsole();
                Console.WriteLine($"Subscription renewed: {result.Id} new expiration {result.ExpirationDateTime}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error renewing subscription: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteSubscriptionAsync(string subscriptionId)
        {
            var credential = new ClientSecretCredential(
                tenantId: config["AzureAd:TenantId"],
                clientId: config["AzureAd:ClientId"],
                clientSecret: config["AzureAd:ClientSecret"]);
            var graphService = new GraphServiceClient(credential);
            try
            {
                await graphService.Subscriptions[subscriptionId].DeleteAsync();
                ConsoleHelper.WriteTimeToConsole();
                Console.WriteLine($"Subscription deleted: {subscriptionId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting subscription: {ex.Message}");
            }
            await subscriptionStore.RemoveBySubscriptionIdAsync(subscriptionId);
        }
    }
}
