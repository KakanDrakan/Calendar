using CalendarApi.Models;
using MongoDB.Driver;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace CalendarApi.Stores
{
    public class SessionStore
    {
        private readonly IMongoCollection<AuthSession> _sessions;

        public SessionStore(IMongoDatabase database)
        {
            _sessions = database.GetCollection<AuthSession>("AuthSessions");

            var indexKeys = Builders<AuthSession>.IndexKeys
                .Ascending(s => s.SessionId)
                .Ascending(s => s.ExpiresAt);

            var indexModel = new CreateIndexModel<AuthSession>(indexKeys);
            _sessions.Indexes.CreateOne(indexModel);
        }

        public async Task<AuthSession> CreateSessionAsync()
        {
            var session = new AuthSession
            {
                SessionId = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                State = SessionState.Pending
            };
            await _sessions.InsertOneAsync(session);
            return session;
        }

        public async Task<AuthSession?> GetSessionAsync(string sessionId)
        {
            return await _sessions
                .Find(s => s.SessionId == sessionId)
                .FirstOrDefaultAsync();
        }

        public async Task<(bool found, AuthSession? session)> TryGetSessionAsync(string sessionId)
        {
            var session = await GetSessionAsync(sessionId);
            if (session == null)
                return (false, null);

            var now = DateTime.UtcNow;

            if (session.ExpiresAt < now)
            {
                session.State = SessionState.Expired;
                await UpdateSessionAsync(session);
                return (true, session);
            }

            if (session.TokenExpiresAt.HasValue && session.TokenExpiresAt.Value.AddMinutes(-5) < now)
            {
                session.State = SessionState.Expired;
                await UpdateSessionAsync(session);
            }
            return (true, session);
        }

        public async Task UpdateSessionAsync(AuthSession session)
        {
            if (session.TokenExpiresAt.HasValue && session.State != SessionState.Expired)
            {
                session.ExpiresAt = DateTime.UtcNow.AddDays(1);
            }

            var update = Builders<AuthSession>.Update
                .Set(s => s.ExpiresAt, session.ExpiresAt)
                .Set(s => s.State, session.State)
                .Set(s => s.UserId, session.UserId)
                .Set(s => s.UserName, session.UserName)
                .Set(s => s.AccessToken, session.AccessToken)
                .Set(s => s.TokenExpiresAt, session.TokenExpiresAt)
                .Set(s => s.SelectedCalendarId, session.SelectedCalendarId)
                .Set(s => s.TokenCacheData, session.TokenCacheData);

            await _sessions.UpdateOneAsync(s => s.SessionId == session.SessionId, update, new UpdateOptions { IsUpsert = true });
        }


        public async Task RemoveSessionAsync(string sessionId)
            => await _sessions.DeleteOneAsync(s => s.SessionId == sessionId);

        public async Task CleanupExpiredAsync()
        {
            var now = DateTime.UtcNow;
            await _sessions.DeleteManyAsync(s => s.ExpiresAt < now);
        }
    }
}
