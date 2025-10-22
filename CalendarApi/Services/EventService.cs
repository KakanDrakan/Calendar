using CalendarApi.Contracts;
using CalendarApi.Dtos;
using CalendarApi.Helpers;
using CalendarApi.Stores;
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
        private readonly CalendarStore calendarStore;
        private readonly CalendarUpdateService updateService;
        private readonly GraphSubscriptionService subscriptionService;

        public EventService(GraphServiceClient graphServiceClient, CalendarStore calendarStore, CalendarUpdateService updateService, GraphSubscriptionService subscriptionService)
        {
            this.graphServiceClient = graphServiceClient;
            this.calendarStore = calendarStore;
            this.updateService = updateService;
            this.subscriptionService = subscriptionService;
        }

        public async Task<List<EventDto>> GetEventsInTimeRange(string calendarId, bool tryCache = true)
        {
            if (calendarStore.TryGetEvents(calendarId, out List<EventDto> cachedEvents) && tryCache)
            {
                Console.WriteLine($"Returning events from memory cache for calendar {calendarId}");
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

            ConsoleHelper.WriteTimeToConsole();
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

            calendarStore.SetEvents(calendarId, dtos);
            await subscriptionService.CreateCalendarSubscriptionAsync(calendarId, "users/tobias@code4value.com/events"); //CHANGE FOR PRODUCTION
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

        public async Task<int> DeleteSubscriptionsToResource(string resource)
        {
            return await subscriptionService.DeleteSubscriptionsForResourceAsync(resource);
        }

    }
}
