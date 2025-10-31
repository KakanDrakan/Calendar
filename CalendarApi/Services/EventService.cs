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
        private readonly CalendarUpdateService updateService;
        private readonly GraphSubscriptionService subscriptionService;
        private readonly IAuthService authService;
        private readonly SessionStore sessionStore;


        public EventService(GraphServiceClient graphServiceClient, CalendarUpdateService updateService, GraphSubscriptionService subscriptionService, IAuthService authService, SessionStore sessionStore)
        {
            this.graphServiceClient = graphServiceClient;
            this.updateService = updateService;
            this.subscriptionService = subscriptionService;
            this.authService = authService;
            this.sessionStore = sessionStore;
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
                Start = e.Start,
                End = e.End,
                Location = e.Location,
                BodyPreview = e.BodyPreview,
                IsAllDay = e.IsAllDay ?? false,
                CalendarId = calendarId
            }).ToList() ?? new List<EventDto>();

            return events;
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
