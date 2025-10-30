using CalendarApi.Dtos;
using CalendarApi.Helpers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Graph.Education.Classes.Item.Assignments.Item.Submissions.Item.Return;

namespace CalendarApi.Stores
{
    public class CalendarStore
    {
        private readonly IMemoryCache cache;
        private readonly TimeSpan cacheDuration = TimeSpan.FromDays(2);

        private const string EventToCalendarKey = "EventToCalendarMap";

        public CalendarStore(IMemoryCache cache)
        {
            this.cache = cache;
        }

        private string GetKey(string calendarId) => $"CalendarEvents.{calendarId}";

        public bool TryGetEvents(string calendarId, out List<EventDto>? events)
        {
            var exists = cache.TryGetValue(GetKey(calendarId), out List<EventDto>? unorderedEvents);
            events = unorderedEvents?.OrderBy(e => e.Start).ToList();
            return exists;
        }

        public List<EventDto> GetEvents(string calendarId)
        {
            return TryGetEvents(calendarId, out var events) ? events! : new List<EventDto>();
        }

        public void SetEvents(string calendarId, List<EventDto> events)
        {
            cache.Set(GetKey(calendarId), events, cacheDuration);
            ConsoleHelper.WriteLineColored($"[CalendarStore] SetEvents: {calendarId} ({events.Count} events)", ConsoleColor.DarkGray);

            // Update the event → calendar mapping
            var map = cache.Get<Dictionary<string, string>>(EventToCalendarKey) ?? new Dictionary<string, string>();
            foreach (var e in events)
            {
                map[e.Id] = calendarId;
            }
            cache.Set(EventToCalendarKey, map, cacheDuration);
        }

        public void UpsertEvent(string calendarId, EventDto updatedEvent)
        {
            // Retrieve existing events list or create a new one
            var events = GetEvents(calendarId);

            // Update if exists, else add
            var index = events.FindIndex(e => e.Id == updatedEvent.Id);
            if (index >= 0)
            {
                events[index] = updatedEvent;
            }
            else
            {
                events.Add(updatedEvent);
            }

            // Save updated list back to cache
            cache.Set(GetKey(calendarId), events, cacheDuration);

            // Update the event → calendar mapping
            var map = cache.Get<Dictionary<string, string>>(EventToCalendarKey) ?? new Dictionary<string, string>();
            map[updatedEvent.Id] = calendarId;
            cache.Set(EventToCalendarKey, map, cacheDuration);

            ConsoleHelper.WriteLineColored($"[CalendarStore] UpsertEvent: {updatedEvent.Id} into {calendarId} (total events: {events.Count})", ConsoleColor.DarkGreen);
        }

        public void RemoveEvent(string calendarId, string eventId)
        {
            // Resolve calendarId if missing
            calendarId ??= GetCalendarIdForEvent(eventId) ?? throw new Exception("CalendarId not found for event");

            var events = GetEvents(calendarId);
            events.RemoveAll(e => e.Id == eventId);

            // Save updated list back to cache
            cache.Set(GetKey(calendarId), events, cacheDuration);

            // Remove mapping
            var map = cache.Get<Dictionary<string, string>>(EventToCalendarKey);
            if (map != null && map.ContainsKey(eventId))
            {
                map.Remove(eventId);
                cache.Set(EventToCalendarKey, map, cacheDuration);
            }

            ConsoleHelper.WriteLineColored($"[CalendarStore] RemoveEvent: {eventId} from {calendarId} (remaining events: {events.Count})", ConsoleColor.DarkMagenta);
        }

        public string? GetCalendarIdForEvent(string eventId)
        {
            var map = cache.Get<Dictionary<string, string>>(EventToCalendarKey);
            return map != null && map.TryGetValue(eventId, out var calendarId) ? calendarId : null;
        }
    }
}
