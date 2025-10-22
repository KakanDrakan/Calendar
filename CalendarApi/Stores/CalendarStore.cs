using CalendarApi.Dtos;
using Microsoft.Extensions.Caching.Memory;

namespace CalendarApi.Stores
{
    public class CalendarStore
    {
        private readonly IMemoryCache cache;
        private readonly TimeSpan cacheDuration = TimeSpan.FromSeconds(30);

        public CalendarStore(IMemoryCache cache)
        {
            this.cache = cache;
        }

        public bool TryGetEvents(string calendarId, out List<EventDto>? events)
        {
            string key = $"CalendarEvents.{calendarId}";
            return cache.TryGetValue(key, out events);
        }

        public void SetEvents(string calendarId, List<EventDto> events)
        {
            string key = $"CalendarEvents.{calendarId}";
            cache.Set(key, events, cacheDuration);
        }
    }
}
