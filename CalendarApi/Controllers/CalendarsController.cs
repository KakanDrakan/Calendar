using CalendarApi.Contracts;
using CalendarApi.Dtos;
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
        public async Task<IActionResult> GetCalendars([FromQuery] string session)
        {
            if (!sessionStore.TryGetSession(session, out var authSession))
                return NotFound("Session not found.");

            var calendars = await authService.GetCalendarsAsync(authSession);
            return Ok(calendars.Select(c => new { c.Id, c.Name, c.Color }));
        }

        // POST /calendars/select
        [HttpPost("select")]
        public async Task<IActionResult> SelectCalendar([FromBody] CalendarSelectionDto dto)
        {
            Console.WriteLine($"[SelectCalendar] Received selection: SessionId={dto.SessionId}, CalendarId={dto.CalendarId}");

            if (!sessionStore.TryGetSession(dto.SessionId, out var session))
                return NotFound("Session not found.");

            session.SelectedCalendarId = dto.CalendarId;
            // Optional: update session state to authenticated/selected
            session.State = SessionState.Authenticated;
            sessionStore.UpdateSession(session);

            subscriptionService.CreateCalendarSubscriptionAsync(dto.CalendarId, dto.SessionId);

            // Notify any clients listening on that session group (desktop QR page)
            var sessionGroupName = $"session:{dto.SessionId}";
            await hubContext.Clients.Group(sessionGroupName)
                .SendAsync("CalendarSelected", new { sessionId = dto.SessionId, calendarId = dto.CalendarId });

            return Ok(new { success = true });
        }
    }
}