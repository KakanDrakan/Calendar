using Microsoft.Graph.Models;
using Newtonsoft.Json;

namespace CalendarApi.Dtos
{
    public class EventDto
    {
        public string Id { get; set; }
        public string Subject { get; set; }
        public DateTimeTimeZone Start { get; set; }
        public DateTimeTimeZone End { get; set; }
        public Location? Location { get; set; }
        public string? BodyPreview { get; set; }
        public bool IsAllDay { get; set; }
        public string? CalendarId { get; set; }
    }
}
