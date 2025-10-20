using CalendarApi.Dtos;
using Microsoft.Graph.Models;

namespace CalendarApi.Contracts
{
    public interface IEventService
    {
        public Task<List<EventDto>> GetEventsInTimeRange();
    }
}
