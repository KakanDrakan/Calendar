using Azure.Security.KeyVault.Certificates;
using CalendarApi.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CalendarApi.Controllers
{
    [ApiController]
    [Route("api/events")]
    public class EventsController : ControllerBase
    {
        private readonly IEventService eventService;
        public EventsController(IEventService eventService) 
        { 
            this.eventService = eventService;
        }

        
        [HttpGet]
        public async Task<IActionResult> GetEventsInTimeRange()
        {
            string calendarId = "test";
            var events = await eventService.GetEventsInTimeRange(calendarId);
            return Ok(events);
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
