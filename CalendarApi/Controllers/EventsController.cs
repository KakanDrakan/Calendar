using Azure.Security.KeyVault.Certificates;
using CalendarApi.Contracts;
using CalendarApi.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CalendarApi.Controllers
{
    [ApiController]
    [Route("api/events")]
    public class EventsController : ControllerBase
    {
        private readonly IEventService eventService;
        private readonly IAuthService authService;
        private readonly SessionStore sessionStore;
        private readonly ILogger<EventsController> logger;
        public EventsController(IEventService eventService, IAuthService authService, SessionStore sessionStore, ILogger<EventsController> logger) 
        { 
            this.eventService = eventService;
            this.authService = authService;
            this.sessionStore = sessionStore;
            this.logger = logger;
        }

        [HttpGet("{sessionId}/{calendarId}")]
        public async Task<IActionResult> GetEvents(string sessionId, string calendarId)
        {
            try
            {
                var events = await eventService.GetEventsForUserAsync(sessionId, calendarId);
                return Ok(events);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized("Session expired, please reauthenticate.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error fetching events for session {SessionId}", sessionId);
                return StatusCode(500, "Failed to fetch events from Microsoft Graph.");
            }
        }

        [HttpPost("test-broadcast")]
        public async Task<IActionResult> TestBroadcast()
        {
            try
            {
                await eventService.TestBroadcast();
                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during test broadcast: {ex.Message}");
                return StatusCode(500, "An error occurred while testing the broadcast.");
            }
        }
        [HttpDelete]
        public async Task<IActionResult> DeleteSubscriptionsToResource(string resource)
        {
            var amount = await eventService.DeleteSubscriptionsToResource(resource);
            return Ok(amount);
        }

    }
}
