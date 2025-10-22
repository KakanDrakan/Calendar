using CalendarApi.Dtos;
using Microsoft.AspNetCore.SignalR;
using CalendarApi.Contracts;

namespace CalendarApi.Services
{
    public class CalendarUpdateService
    {
        private readonly IHubContext<CalendarHub> hubContext;

        public CalendarUpdateService(IServiceProvider serviceProvider, IHubContext<CalendarHub> hubContext)
        {
            this.hubContext = hubContext;
        }

        public async Task NotifyCalendarUpdated(string calendarId, List<EventDto> events)
        {
            var groupName = $"calendar:{calendarId}";
            await hubContext.Clients.Group(groupName)
                .SendAsync("ReceiveCalendarUpdate", events);
        }
    }

}
