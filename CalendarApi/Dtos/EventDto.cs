namespace CalendarApi.Dtos
{
    public class EventDto
    {
        public string Id { get; set; }
        public string Subject { get; set; }
        public DateTimeOffset Start { get; set; }
        public DateTimeOffset End { get; set; }
        public string Location { get; set; }
        public string BodyPreview { get; set; }
        public bool IsAllDay { get; set; }
    }
}
