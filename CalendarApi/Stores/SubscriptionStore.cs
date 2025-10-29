using CalendarApi.Data;
using CalendarApi.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Graph.Models;
using MongoDB.Driver;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace CalendarApi.Stores
{
    public class SubscriptionStore
    {
        private readonly IMongoCollection<CalendarSubscription> _subscriptions;

        public SubscriptionStore(MongoDbContext context)
        {
            _subscriptions = context.GetCollection<CalendarSubscription>("Subscriptions");
        }

        public async Task<(bool, CalendarSubscription)> TryGetSubscription(string calendarId)
        {
            try
            {
                var subscription = await _subscriptions
                    .Find(s => s.CalendarId == calendarId)
                    .FirstOrDefaultAsync();
                return (subscription != null, subscription);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving subscription for calendar {calendarId}: {ex.Message}");
                return (false, null);
            }
        }

        public async Task SaveSubscriptionAsync(string calendarId, Subscription subscription, string userId)
        {
            var dto = new CalendarSubscription
            {
                CalendarId = calendarId,
                SubscriptionId = subscription.Id!,
                ExpiresAt = subscription.ExpirationDateTime,
                UserId = userId,
                Resource = subscription.Resource

            };

            await _subscriptions.ReplaceOneAsync(
                filter: s => s.CalendarId == calendarId,
                replacement: dto,
                options: new ReplaceOptions { IsUpsert = true }
            );
        }

        public async Task<CalendarSubscription?> GetBySubscriptionIdAsync(string subscriptionId)
        {
            return await _subscriptions
                .Find(s => s.SubscriptionId == subscriptionId)
                .FirstOrDefaultAsync();
        }

        public async Task RemoveAsync(string calendarId)
        {
            try
            {
                await _subscriptions.DeleteOneAsync(s => s.CalendarId == calendarId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting subscription for calendar {calendarId}: {ex.Message}");
            }

        }
    }
}
