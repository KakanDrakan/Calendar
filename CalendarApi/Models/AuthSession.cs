using Microsoft.AspNetCore.Mvc.ViewFeatures;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CalendarApi.Models
{
    public class AuthSession
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string SessionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public SessionState State { get; set; }
        public string? SelectedCalendarId { get; set; }
        public string? AccessToken { get; set; }
        public DateTime? TokenExpiresAt { get; set; }
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        [BsonIgnoreIfNull]
        [BsonRepresentation(BsonType.Binary)]
        public byte[]? TokenCacheData { get; set; }
    }

    public enum SessionState
    {
        Pending = 0,
        Authenticated = 1,
        CalendarSelected = 2,
        Expired = 3,
        Error = 4,
    }
}
