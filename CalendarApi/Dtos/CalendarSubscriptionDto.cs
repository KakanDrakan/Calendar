namespace CalendarApi.Dtos
{
    public class CalendarSubscriptionDto
    {
        public string CalendarId { get; set; }
        public string SubscriptionId { get; set; }
        public string UserId { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
    }
}
