using CalendarApi.Contracts;
using CalendarApi.Dtos;
using CalendarApi.Helpers;
using CalendarApi.Services;
using CalendarApi.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph.Models;
using Newtonsoft.Json;
using System.Security.Cryptography.X509Certificates;

namespace CalendarApi.Controllers
{
    [ApiController]
    [Route("api/webhook")]
    public class WebhookController(IEventService eventService, CalendarUpdateService updateService) : ControllerBase
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

            NotificationRoot? notification;

            try
            {
                notification = System.Text.Json.JsonSerializer.Deserialize<NotificationRoot>(
                    body,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Deserialization failed: {ex.Message}");
                return BadRequest("Invalid payload");
            }

            if (notification?.Value == null || notification.Value.Count == 0)
            {
                Console.WriteLine("No notifications found in body:");
                return BadRequest("Empty notification payload.");
            }

            try
            {
                await updateService.HandleCalendarUpdate(notification);
            }
            catch (Exception ex)
            { 

            }

            
            return Ok();
        }
    }
}
