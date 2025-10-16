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

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetEventsInTimeRange()
        {
            var events = await eventService.GetEventsInTimeRange();
            return Ok(events);
        }
    }
}
