using CalendarApi.Contracts;
using CalendarApi.Dtos;
using CalendarApi.Helpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Graph.Models;
using Newtonsoft.Json;
using System.Linq.Expressions;
using System.Security.Cryptography.X509Certificates;

namespace CalendarApi.Services
{
    public class CalendarUpdateService
    {
        private readonly IHubContext<CalendarHub> hubContext;
        private readonly IConfiguration config;

        public CalendarUpdateService(IServiceProvider serviceProvider, IHubContext<CalendarHub> hubContext, IConfiguration config)
        {
            this.hubContext = hubContext;
            this.config = config;
        }

        public async Task NotifyCalendarUpdated(string subscriptionId, List<EventDto> events)
        {
            ConsoleHelper.WriteTimeToConsole();
            Console.WriteLine($"Notifying calendar update to subscription {subscriptionId} with {events.Count} events");
            var groupName = $"calendar:{subscriptionId}";
            await hubContext.Clients.Group(groupName)
                .SendAsync("ReceiveCalendarUpdate", events);
        }

        public async Task NotifyEventDeleted(string subscriptionId, string eventId)
        {
            ConsoleHelper.WriteTimeToConsole();
            Console.WriteLine($"Notifying deletion of event {eventId} to subscription {subscriptionId}");
            var groupName = $"calendar:{subscriptionId}";
            await hubContext.Clients.Group(groupName)
            .SendAsync("ReceiveEventDeleted", eventId);
        }

        public async Task NotifyEventUpsert(string subscriptionId, EventDto dto)
        {
            ConsoleHelper.WriteTimeToConsole();
            Console.WriteLine($"Notifying event upsert");
            var groupName = $"calendar:{subscriptionId}";
            await hubContext.Clients.Group(groupName)
                .SendAsync("ReceiveEventUpsert", dto);
        }

        public async Task HandleCalendarUpdate(NotificationRoot notification)
        {
            X509Certificate2? cert = null;
            try
            {
                cert = new X509Certificate2(
                config["Graph:EncryptionCertPath"],
                config["Graph:EncryptionCertPassword"],
                X509KeyStorageFlags.Exportable);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load decryption certificate: {ex.Message}");
            }

            foreach (var change in notification.Value)
            {
                if (change.ClientState != config["Graph:SubscriptionClientState"]) break;
                string? eventId = change.ResourceData?.Id ?? change.ResourceData?.ODataId;

                if (eventId == null || eventId == "") 
                    throw new ArgumentException("EventId is null or empty", "eventId");

                if (change.ChangeType.Equals("deleted", StringComparison.OrdinalIgnoreCase)) 
                    await NotifyEventDeleted(change.SubscriptionId, eventId);

                if (change.EncryptedContent != null)
                {
                    try
                    {
                        var decryptedJson = GraphNotificationDecryptor.DecryptNotification(change.EncryptedContent, cert);
                        if (decryptedJson == null || decryptedJson == "{}") continue;
                        
                        EventDto eventDto = JsonConvert.DeserializeObject<EventDto>(decryptedJson);
                        eventDto.Id = eventId;
                        

                        await NotifyEventUpsert(change.SubscriptionId, eventDto);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error decrypting notification: {ex.Message}");
                    }
                }

            }
        }
    }

}
