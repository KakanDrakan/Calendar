using Microsoft.Extensions.Caching.Memory;
using Microsoft.Graph.Models;

namespace CalendarApi.Stores
{
    public class SubscriptionStore
    {
        private readonly IMemoryCache cache;
        private readonly TimeSpan subscriptionLifetime = TimeSpan.FromHours(1); //CHANGE FOR PRODUCTION

        public SubscriptionStore(IMemoryCache cache)
        {
            this.cache = cache;
        }

        public bool TryGetSubscription(string calendarId, out Subscription? subscription)
        {
            return cache.TryGetValue(calendarId, out subscription);
        }

        public void SaveSubscription(string calendarId, Subscription subscription)
        {
            cache.Set(calendarId, subscription, subscriptionLifetime);
        }
    }
}
