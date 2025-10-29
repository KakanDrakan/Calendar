using Azure.Core;
using CalendarApi.Contracts;
using CalendarApi.Dtos;
using CalendarApi.Helpers;
using CalendarApi.Models;
using CalendarApi.Stores;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using System;
using System.Reflection.Metadata.Ecma335;
using TimeZoneConverter;

namespace CalendarApi.Services
{
    public class EventService : IEventService
    {
        private readonly GraphServiceClient graphServiceClient;
        private readonly CalendarStore calendarStore;
        private readonly CalendarUpdateService updateService;
        private readonly GraphSubscriptionService subscriptionService;
        private readonly IAuthService authService;
        private readonly SessionStore sessionStore;
        private readonly RecentlyUpdatedResourceStore updatesStore;
        private readonly SubscriptionStore subscriptionStore;


        public EventService(GraphServiceClient graphServiceClient, CalendarStore calendarStore, CalendarUpdateService updateService, GraphSubscriptionService subscriptionService, IAuthService authService, SessionStore sessionStore, RecentlyUpdatedResourceStore updatesStore, SubscriptionStore subscriptionStore)
        {
            this.graphServiceClient = graphServiceClient;
            this.calendarStore = calendarStore;
            this.updateService = updateService;
            this.subscriptionService = subscriptionService;
            this.authService = authService;
            this.sessionStore = sessionStore;
            this.updatesStore = updatesStore;
            this.subscriptionStore = subscriptionStore;
        }

        public async Task<List<EventDto>> GetEventsForUserAsync(string sessionId, string calendarId)
        {
            var (found, session) = await sessionStore.TryGetSessionAsync(sessionId);
            if (!found || session?.State == SessionState.Expired)
            {
                throw new UnauthorizedAccessException("Session not found or expired.");
            }


            var accessToken = await authService.GetAccessTokenAsync(session);
            if (accessToken == null)
                throw new UnauthorizedAccessException("User must reauthenticate.");

            var credential = new DelegateCredential((_, _) =>
                new ValueTask<AccessToken>(
                    new AccessToken(accessToken, session.TokenExpiresAt ?? DateTimeOffset.UtcNow.AddMinutes(50))
                ));

            var graphClient = new GraphServiceClient(credential);

            var start = DateTime.UtcNow;
            var end = start.AddDays(30);

            var response = await graphClient.Me.Calendars[calendarId].CalendarView.GetAsync(cfg =>
            {
                cfg.QueryParameters.StartDateTime = start.ToString("o");
                cfg.QueryParameters.EndDateTime = end.ToString("o");
                cfg.QueryParameters.Orderby = new[] { "start/dateTime" };
            });
            var events = response?.Value?.Select(e => new EventDto
            {
                Id = e.Id,
                Subject = e.Subject,
                Start = ConvertToZonedTime(e.Start),
                End = ConvertToZonedTime(e.End),
                Location = e.Location?.DisplayName,
                BodyPreview = e.BodyPreview,
                IsAllDay = e.IsAllDay ?? false,
                CalendarId = calendarId
            }).ToList() ?? new List<EventDto>();

            // Update calendar store
            calendarStore.SetEvents(calendarId, events);

            return events;
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

        public async Task<EventDto?> GetEventByIdAsync(string userId, string? calendarId, string eventId, bool useDefaultCalendar = false)
        {
            try
            {
                Microsoft.Graph.Models.Event? response;

                if (useDefaultCalendar || string.IsNullOrEmpty(calendarId))
                {
                    // Default calendar path
                    response = await graphServiceClient.Users[userId].Events[eventId].GetAsync(r =>
                    {
                        r.QueryParameters.Select = new[] { "id", "subject", "start", "end", "location", "bodyPreview", "isAllDay", "calendar" };
                    });
                }
                else
                {
                    // Specific calendar path
                    response = await graphServiceClient.Users[userId].Calendars[calendarId].Events[eventId].GetAsync(r =>
                    {
                        r.QueryParameters.Select = new[] { "id", "subject", "start", "end", "location", "bodyPreview", "isAllDay" };
                    });
                }

                if (response == null) return null;

                var dto = new EventDto
                {
                    Id = response.Id,
                    Subject = response.Subject,
                    Start = ConvertToZonedTime(response.Start),
                    End = ConvertToZonedTime(response.End),
                    Location = response.Location?.DisplayName,
                    BodyPreview = response.BodyPreview,
                    IsAllDay = response.IsAllDay ?? false,
                    CalendarId = calendarId ?? response.Calendar?.Id
                };

                if (!string.IsNullOrEmpty(dto.CalendarId))
                    calendarStore.UpsertEvent(dto.CalendarId, dto);

                return dto;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetEventByIdAsync] Failed for event {eventId}: {ex.Message}");
                return null;
            }
        }

        public async Task HandleEventChangeAsync(GraphChangeNotification change)
        {
            try
            {
                // Parse resource path: extract userId, calendarId, eventId
                if (!ParseHelper.TryParseResource(change.Resource, out var userId, out var calendarId, out var eventId))
                    return;

                // Deduplicate processing
                if (updatesStore.IsInCache(change.ChangeType ,eventId))
                    return;
                updatesStore.SetUpdate(change.ChangeType, eventId);

                // Resolve calendarId from subscription if missing
                if (string.IsNullOrEmpty(calendarId))
                {
                    var sub = await subscriptionStore.GetBySubscriptionIdAsync(change.SubscriptionId);
                    calendarId = sub?.CalendarId;
                    Console.WriteLine($"Resolved calendarId {calendarId} for event {eventId} from subscription");
                }

                //ConsoleHelper.WriteTimeToConsole();
                //Console.WriteLine($"[{change.ChangeType}] Processing change for event {eventId}");

                // Route based on change type
                if (change.ChangeType.Equals("deleted", StringComparison.OrdinalIgnoreCase))
                    await HandleDeleteChangeAsync(calendarId, eventId);
                else
                    await HandleUpsertChangeAsync(change, userId, calendarId, eventId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling event change: {ex.Message}");
            }
        }

        private async Task HandleDeleteChangeAsync(string? calendarId, string eventId)
        {
            calendarId ??= calendarStore.GetCalendarIdForEvent(eventId);
            if (calendarId == null)
            {
                Console.WriteLine($"[Webhook] Cannot delete {eventId}: calendarId unknown");
                return;
            }

            //Console.WriteLine($"[Webhook] Deleting event {eventId} from calendar {calendarId}");
            calendarStore.RemoveEvent(calendarId, eventId);
            await updateService.NotifyCalendarUpdated(calendarId, calendarStore.GetEvents(calendarId));
        }

        private async Task HandleUpsertChangeAsync(GraphChangeNotification change, string userId, string? calendarId, string eventId)
        {
            // Determine whether this is a default calendar resource path
            var useDefaultCalendar = change.Resource.Contains("/events/", StringComparison.OrdinalIgnoreCase) &&
                                     !change.Resource.Contains("/calendars/", StringComparison.OrdinalIgnoreCase);

            var updatedEvent = await GetEventByIdAsync(userId, calendarId, eventId, useDefaultCalendar);
            //ConsoleHelper.WriteLineColored($"calendarId: {updatedEvent.CalendarId??null}", ConsoleColor.Red);
            if (updatedEvent == null)
            {
                Console.WriteLine($"[Webhook] Event not found for {eventId}");
                return;
            }

            calendarId ??= updatedEvent.CalendarId
                      ?? calendarStore.GetCalendarIdForEvent(updatedEvent.Id)
                      ?? throw new Exception($"Cannot upsert event {updatedEvent.Id}: calendarId unknown");

            Console.WriteLine($"[Webhook] Upserting event {updatedEvent.Id} into calendar {calendarId}");
            calendarStore.UpsertEvent(calendarId, updatedEvent);
            await updateService.NotifyCalendarUpdated(calendarId, calendarStore.GetEvents(calendarId));
        }

    }
}
