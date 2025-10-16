using CalendarApi.Contracts;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace CalendarApi.Services
{
    public class EventService : IEventService
    {
        private readonly GraphServiceClient graphServiceClient;
        public EventService(GraphServiceClient graphServiceClient)
        {
            this.graphServiceClient = graphServiceClient;
        }
        public async Task<List<Event>> GetEventsInTimeRange()
        {
            var startDate = DateTime.UtcNow;
            var endDate = startDate.AddDays(30);
            var events = await graphServiceClient.Me.CalendarView
                .GetAsync(config =>
                {
                    config.QueryParameters.StartDateTime = startDate.ToString("o");
                    config.QueryParameters.EndDateTime = endDate.ToString("o");
                    config.QueryParameters.Select = new[] { "subject", "start", "end", "location", "body" };
                    config.QueryParameters.Orderby = new[] { "start/dateTime" };
                });

            return events.Value;
        }
    }
}
