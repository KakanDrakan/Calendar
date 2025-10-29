using CalendarApi.Dtos;
using Microsoft.Extensions.Caching.Memory;

namespace CalendarApi.Stores
{
    public class RecentlyUpdatedResourceStore
    {
        private readonly IMemoryCache cache;
        private readonly TimeSpan cacheDuration = TimeSpan.FromSeconds(3);

        public RecentlyUpdatedResourceStore(IMemoryCache cache)
        {
            this.cache = cache;
        }

        public bool IsInCache(string changeType, string updateId)
        {
            string key = $"EventUpdates.{changeType}.{updateId}";
            return cache.TryGetValue(key, out var checkedUpdate);
        }

        public void SetUpdate(string changeType, string updateId)
        {
            string key = $"EventUpdates.{changeType}.{updateId}";
            cache.Set(key, updateId, cacheDuration);
        }
    }
}
