using CalendarApi.Dtos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Graph.Models;
using System.Collections.Concurrent;

namespace CalendarApi.Stores
{
    public class SubscriptionStore
    {
        private readonly IMemoryCache cache;
        private readonly TimeSpan subscriptionLifetime = TimeSpan.FromMinutes(10); //CHANGE FOR PRODUCTION
        private readonly ConcurrentDictionary<string, string> subscriptionIdToCalendar = new();

        public SubscriptionStore(IMemoryCache cache)
        {
            this.cache = cache;
        }

        public bool TryGetSubscription(string calendarId, out CalendarSubscriptionDto? subscription)
        {
            return cache.TryGetValue(calendarId, out subscription);
        }

        public void SaveSubscription(string calendarId, Subscription subscription, string userId)
        {
            var dto = new CalendarSubscriptionDto
            {
                CalendarId = calendarId,
                SubscriptionId = subscription.Id!,
                ExpiresAt = subscription.ExpirationDateTime!.Value,
                UserId = userId

            };

            cache.Set(calendarId, dto, subscriptionLifetime);
            subscriptionIdToCalendar[dto.SubscriptionId] = calendarId;
        }

        public CalendarSubscriptionDto? GetBySubscriptionId(string subscriptionId)
        {
            if (subscriptionIdToCalendar.TryGetValue(subscriptionId, out var calendarId))
            {
                cache.TryGetValue(calendarId, out CalendarSubscriptionDto? dto);
                return dto;
            }
            return null;
        }

        public void Remove(string calendarId)
        {
            if (cache.TryGetValue(calendarId, out CalendarSubscriptionDto? dto))
            {
                if (dto != null)
                    subscriptionIdToCalendar.TryRemove(dto.SubscriptionId, out _);
            }
            cache.Remove(calendarId);
        }
    }
}
