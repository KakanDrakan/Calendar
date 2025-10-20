using CalendarApi.Contracts;
using CalendarApi.Dtos;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System;
using TimeZoneConverter;

namespace CalendarApi.Services
{
    public class EventService : IEventService
    {
        private readonly GraphServiceClient graphServiceClient;
        private readonly IMemoryCache memoryCache;
        private readonly CalendarUpdateService updateService;

        public EventService(GraphServiceClient graphServiceClient, IMemoryCache memoryCache, CalendarUpdateService updateService)
        {
            this.graphServiceClient = graphServiceClient;
            this.memoryCache = memoryCache;
            this.updateService = updateService;
        }

        public async Task<List<EventDto>> GetEventsInTimeRange(string calendarId)
        {
            string cacheKey = $"CalendarEvents.{calendarId}";
            if (memoryCache.TryGetValue(cacheKey, out List<EventDto> cachedEvents))
            {
                Console.WriteLine("Returning events from memory cache.");
                return cachedEvents;
            }

            var startDate = DateTime.UtcNow;
            var endDate = startDate.AddDays(30);

            var response = await graphServiceClient.Users["tobias@code4value.com"].CalendarView

                .GetAsync(config =>
                {
                    config.QueryParameters.StartDateTime = startDate.ToString("o");
                    config.QueryParameters.EndDateTime = endDate.ToString("o");
                    config.QueryParameters.Select = new[] { "id", "subject", "start", "end", "location", "bodyPreview", "isAllDay" };
                    config.QueryParameters.Orderby = new[] { "start/dateTime" };
                });
            Console.WriteLine($"Fetched events from Graph API for calendar {calendarId}");

            var dtos = response?.Value?.Select(e =>
            {
                var startDateTime = ConvertToZonedTime(e.Start);
                var endDateTime = ConvertToZonedTime(e.End);

                return new EventDto
                {
                    Id = e.Id,
                    Subject = e.Subject,
                    Start = startDateTime,
                    End = endDateTime,
                    Location = e.Location?.DisplayName,
                    BodyPreview = e.BodyPreview,
                    IsAllDay = e.IsAllDay ?? false
                };
            }).ToList() ?? new List<EventDto>();

            memoryCache.Set(cacheKey, dtos, TimeSpan.FromSeconds(30));

            return dtos;
        }

        public async Task TestBroadcast()
        {
            var seed = DateTime.Now.Millisecond;

            var testEvents = new List<EventDto>
            {
                new EventDto
                {
                    Id = Guid.NewGuid().ToString(),
                    Subject = $"Test Event {seed}",
                    Start = DateTimeOffset.Now.AddHours(1),
                    End = DateTimeOffset.Now.AddHours(2),
                    Location = "Conference Room A",
                    BodyPreview = "This is a test event.",
                    IsAllDay = false
                }
            };
            await updateService.NotifyCalendarUpdated("test", testEvents);
        }

        private DateTimeOffset ConvertToZonedTime(DateTimeTimeZone timeZoneValue)
        {
            if (timeZoneValue == null || string.IsNullOrEmpty(timeZoneValue.DateTime))
                return DateTime.MinValue;

            var localTime = DateTime.Parse(timeZoneValue.DateTime);
            var timeZoneId = timeZoneValue.TimeZone;

            try
            {
                var tz = TZConvert.TryIanaToWindows(timeZoneId, out var windowsId)
                    ? TimeZoneInfo.FindSystemTimeZoneById(windowsId)
                    : TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

                return TimeZoneInfo.ConvertTimeToUtc(localTime, tz).ToLocalTime();
            }
            catch (TimeZoneNotFoundException)
            {
                return localTime; // Fallback
            }
        }

    }
}
