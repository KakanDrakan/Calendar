using CalendarApi.Dtos;
using Microsoft.Graph.Models;

namespace CalendarApi.Contracts
{
    public interface IEventService
    {
        //public Task<List<EventDto>> GetEventsInTimeRange(string calendarId, bool tryCache = true);
        public Task<List<EventDto>> GetEventsForUserAsync(string sessionId, string calendarId);
        public Task TestBroadcast();
        public Task<int> DeleteSubscriptionsToResource(string resource);
        Task<EventDto?> GetEventByIdAsync(string userId, string calendarId, string eventId, bool useDefault);
        Task HandleEventChangeAsync(GraphChangeNotification change);
    }
}
