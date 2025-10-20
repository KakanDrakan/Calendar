using CalendarApi.Dtos;
using Microsoft.AspNetCore.SignalR;
using CalendarApi.Contracts;

namespace CalendarApi.Services
{
    public class CalendarUpdateService : BackgroundService
    {
        private readonly IServiceProvider serviceProvider;
        private readonly IHubContext<CalendarHub> hubContext;

        public CalendarUpdateService(IServiceProvider serviceProvider, IHubContext<CalendarHub> hubContext)
        {
            this.serviceProvider = serviceProvider;
            this.hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var activeCalendars = CalendarHub.GetActiveCalendars().ToList();
                Console.WriteLine($"[Polling] Checking updates for {activeCalendars.Count} calendars...");

                using var scope = serviceProvider.CreateScope();
                var eventService = scope.ServiceProvider.GetRequiredService<IEventService>();

                foreach (var calendarId in activeCalendars)
                {
                    try
                    {
                        var events = await eventService.GetEventsInTimeRange(calendarId);

                        var groupName = $"calendar:{calendarId}";
                        await hubContext.Clients.Group(groupName)
                            .SendAsync("ReceiveCalendarUpdate", events, stoppingToken);

                        Console.WriteLine($"[Update] Sent update to group {groupName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Error] Fetching events for {calendarId}: {ex.Message}");
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        public async Task NotifyCalendarUpdated(string calendarId, List<EventDto> events)
        {
            var groupName = $"calendar:{calendarId}";
            await hubContext.Clients.Group(groupName)
                .SendAsync("ReceiveCalendarUpdate", events);
        }
    }

}
