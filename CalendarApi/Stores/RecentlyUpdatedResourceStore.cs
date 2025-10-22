using CalendarApi.Dtos;
using Microsoft.Extensions.Caching.Memory;

namespace CalendarApi.Stores
{
    public class RecentlyUpdatedResourceStore
    {
        private readonly IMemoryCache cache;
        private readonly TimeSpan cacheDuration = TimeSpan.FromSeconds(5);

        public RecentlyUpdatedResourceStore(IMemoryCache cache)
        {
            this.cache = cache;
        }

        public bool IsInCache(string updateId)
        {
            string key = $"EventUpdates.{updateId}";
            return cache.TryGetValue(key, out var checkedUpdate);
        }

        public void SetUpdate(string updateId)
        {
            string key = $"EventUpdates.{updateId}";
            cache.Set(key, updateId, cacheDuration);
        }
    }
}
