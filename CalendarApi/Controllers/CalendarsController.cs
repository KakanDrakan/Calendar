using CalendarApi.Contracts;
using CalendarApi.Dtos;
using CalendarApi.Models;
using CalendarApi.Services;
using CalendarApi.Stores;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace CalendarApi.Controllers
{
    [ApiController]
    [Route("calendars")]
    public class CalendarsController(IAuthService authService, SessionStore sessionStore, IHubContext<CalendarHub> hubContext, GraphSubscriptionService subscriptionService) : ControllerBase
    {
        // GET /calendars?session={sessionId}
        [HttpGet]
        public async Task<IActionResult> GetCalendars([FromQuery] string sessionId)
        {
            var (found, session) = await sessionStore.TryGetSessionAsync(sessionId);
            if (!found || session?.State == SessionState.Expired)
            {
                return Unauthorized();
            }


            var calendars = await authService.GetCalendarsAsync(session);
            return Ok(calendars.Select(c => new { c.Id, c.Name, c.Color }));
        }

        // POST /calendars/select
        [HttpPost("select")]
        public async Task<IActionResult> SelectCalendar([FromBody] CalendarSelectionDto dto)
        {
            Console.WriteLine($"[SelectCalendar] Received selection: SessionId={dto.SessionId}, CalendarId={dto.CalendarId}");

            var (found, session) = await sessionStore.TryGetSessionAsync(dto.SessionId);
            if (!found || session?.State == SessionState.Expired)
            {
                return Unauthorized();
            }


            session.SelectedCalendarId = dto.CalendarId;
            // Optional: update session state to authenticated/selected
            session.State = SessionState.Authenticated;
            await sessionStore.UpdateSessionAsync(session);

            var sub = await subscriptionService.CreateCalendarSubscriptionAsync(dto.CalendarId, dto.SessionId);

            // Notify any clients listening on that session group (desktop QR page)
            var sessionGroupName = $"session:{dto.SessionId}";
            await hubContext.Clients.Group(sessionGroupName)
                .SendAsync("CalendarSelected", new { sessionId = dto.SessionId, calendarId = dto.CalendarId, subscriptionId = sub?.SubscriptionId ?? null});

            return Ok(new { success = true });
        }
    }
}