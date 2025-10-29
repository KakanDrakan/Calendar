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

            // Subscription validation handshake
            if (!string.IsNullOrEmpty(validationToken))
            {
                Console.WriteLine("Validation token received");
                return Content(validationToken, "text/plain", System.Text.Encoding.UTF8);
            }

            try
            {
                var notification = System.Text.Json.JsonSerializer.Deserialize<NotificationRoot>(body);
                if (notification?.Value == null || notification.Value.Count == 0)
                    return BadRequest("No notifications received");

                var updates = new List<GraphChangeNotification>();
                var deletes = new HashSet<string>();

                // First pass: collect all deletes
                foreach (var change in notification.Value)
                {
                    if (change.ChangeType.Equals("deleted", StringComparison.OrdinalIgnoreCase))
                    {
                        deletes.Add(change.ResourceData?.Id ?? "");
                    }
                }

                // Second pass: collect updates that are not part of a create/delete
                foreach (var change in notification.Value)
                {
                    if (change.ChangeType.Equals("updated", StringComparison.OrdinalIgnoreCase))
                    {
                        var eventId = change.ResourceData?.Id ?? "";
                        if (!deletes.Contains(eventId)) // ignore updates for events that are being deleted
                        {
                            updates.Add(change);
                        }
                    }
                }

                // Now process deletes first, then “true” updates
                var deletesToProcess = notification.Value.Where(c => c.ChangeType.Equals("deleted", StringComparison.OrdinalIgnoreCase));
                var updatesToProcess = updates;

                foreach (var del in deletesToProcess)
                {
                    Console.WriteLine(); ConsoleHelper.WriteTimeToConsole();
                    Console.WriteLine($"Received change: SubscriptionId={del.SubscriptionId.Substring(0, 5)}, ChangeType={del.ChangeType}, Resource={del.Resource.Substring(6, 14)}, ResourceId={del.ResourceData?.Id.Substring(0, 8)}");
                    await eventService.HandleEventChangeAsync(del);
                }

                foreach (var upd in updatesToProcess)
                {
                    Console.WriteLine(); ConsoleHelper.WriteTimeToConsole();
                    Console.WriteLine($"Received change: SubscriptionId={upd.SubscriptionId.Substring(0, 5)}, ChangeType={upd.ChangeType}, Resource={upd.Resource.Substring(6, 14)}, ResourceId={upd.ResourceData?.Id.Substring(0, 8)}");
                    await eventService.HandleEventChangeAsync(upd);
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
