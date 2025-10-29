using MongoDB.Bson.Serialization.Attributes;

namespace CalendarApi.Models
{
    public class CalendarSubscription
    {
        [BsonId]
        [BsonRepresentation(MongoDB.Bson.BsonType.String)]
        public string SubscriptionId { get; set; }
        public string CalendarId { get; set; }
        public string UserId { get; set; }
        public string? Resource { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
    }
}
