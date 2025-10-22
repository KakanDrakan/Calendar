using CalendarApi.Contracts;
using CalendarApi.Dtos;
using CalendarApi.Helpers;
using CalendarApi.Services;
using CalendarApi.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CalendarApi.Controllers
{
    [ApiController]
    [Route("api/webhook")]
    public class WebhookController(IEventService eventService, CalendarUpdateService updateService, RecentlyUpdatedResourceStore updatesStore) : ControllerBase
    {

        [HttpPost]
        public async Task<IActionResult> Post([FromQuery] string? validationToken)
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            //Console.WriteLine("Raw webhook payload:");
            //Console.WriteLine(body);

            // Handle subscription validation
            if (!string.IsNullOrEmpty(validationToken))
            {
                Console.WriteLine("Validation token received");
                return Content(validationToken, "text/plain", System.Text.Encoding.UTF8);
            }

            try
            {
                var notification = System.Text.Json.JsonSerializer.Deserialize<NotificationRoot>(body);
                if (notification?.Value == null || notification.Value.Count == 0)
                {
                    Console.WriteLine("No notifications in payload");
                    return BadRequest("No notifications received");
                }

                foreach (var change in notification.Value)
                {
                    ConsoleHelper.WriteTimeToConsole();
                    Console.WriteLine($"Change detected: {change.ChangeType} on {change.Resource}");
                    
                    if (updatesStore.IsInCache(change.ResourceData.Id)) continue;

                    updatesStore.SetUpdate(change.ResourceData.Id);

                    // You can extract calendarId from change.Resource if needed
                    var calendarId = "test"; // Replace with actual logic if needed

                    var updatedEvents = await eventService.GetEventsInTimeRange(calendarId, false);
                    await updateService.NotifyCalendarUpdated(calendarId, updatedEvents);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing webhook: {ex.Message}");
                return StatusCode(500, "Webhook processing failed");
            }
        }
    }
}
