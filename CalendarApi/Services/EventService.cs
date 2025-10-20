using CalendarApi.Contracts;
using CalendarApi.Dtos;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System;
using TimeZoneConverter;

namespace CalendarApi.Services
{
    public class EventService : IEventService
    {
        private readonly GraphServiceClient graphServiceClient;
        public EventService(GraphServiceClient graphServiceClient)
        {
            this.graphServiceClient = graphServiceClient;
        }


        public async Task<List<EventDto>> GetEventsInTimeRange()
        {
            var startDate = DateTime.UtcNow;
            var endDate = startDate.AddDays(30);

            var events = await graphServiceClient.Users["tobias@code4value.com"].CalendarView

                .GetAsync(config =>
                {
                    config.QueryParameters.StartDateTime = startDate.ToString("o");
                    config.QueryParameters.EndDateTime = endDate.ToString("o");
                    config.QueryParameters.Select = new[] { "id", "subject", "start", "end", "location", "bodyPreview" };
                    config.QueryParameters.Orderby = new[] { "start/dateTime" };
                });

            var dtos = events.Value.Select(e =>
            {
                var startDateTime = DateTime.Parse(e.Start.DateTime);
                var endDateTime = DateTime.Parse(e.End.DateTime);

                bool isAllDay = startDateTime.TimeOfDay == TimeSpan.Zero &&
                                endDateTime.TimeOfDay == TimeSpan.Zero &&
                                (endDateTime - startDateTime).TotalHours == 24;

                return new EventDto
                {
                    Id = e.Id,
                    Subject = e.Subject,
                    Start = startDateTime,
                    End = endDateTime,
                    Location = e.Location?.DisplayName,
                    BodyPreview = e.BodyPreview,
                    IsAllDay = isAllDay
                };
            }).ToList();


            return dtos;
        }

        private DateTimeOffset ConvertToZonedTime(DateTimeTimeZone timeZoneValue)
        {
            if (timeZoneValue == null || string.IsNullOrEmpty(timeZoneValue.DateTime))
                return DateTimeOffset.MinValue;

            var parsedUtc = DateTime.Parse(timeZoneValue.DateTime);

            var timeZoneId = timeZoneValue.TimeZone;
            var tz = TimeZoneInfo.FindSystemTimeZoneById(TZConvert.IanaToWindows(timeZoneId));

            var localTime = TimeZoneInfo.ConvertTimeToUtc(parsedUtc, tz);
            return new DateTimeOffset(localTime, tz.GetUtcOffset(localTime));
        }

        

    }
}
