using CalendarApi.Dtos;
using System.Collections.Concurrent;

namespace CalendarApi.Stores
{
    public class SessionStore
    {
        private readonly ConcurrentDictionary<string, AuthSession> _sessions = new();

        public AuthSession CreateSession()
        {
            var session = new AuthSession
            {
                SessionId = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                State = SessionState.Pending
            };
            _sessions[session.SessionId] = session;
            return session;
        }

        public bool TryGetSession(string sessionId, out AuthSession? session)
        {
            if (!_sessions.TryGetValue(sessionId, out var foundSession))
            {
                session = null;
                return false;
            }

            session = foundSession;

            // Check if the session or token is expired
            var now = DateTime.UtcNow;
            if (session.ExpiresAt < now)
            {
                session.State = SessionState.Expired;
                return true;
            }

            // If token is expiring soon (within 5 minutes), mark for refresh
            if (session.TokenExpiresAt.HasValue && session.TokenExpiresAt.Value.AddMinutes(-5) < now)
            {
                session.State = SessionState.Expired;
            }

            return true;
        }

        public void UpdateSession(AuthSession session)
        {
            // Extend session expiration when updating with valid token
            if (session.TokenExpiresAt.HasValue && session.State != SessionState.Expired)
            {
                session.ExpiresAt = DateTime.UtcNow.AddDays(1);
            }
            
            _sessions[session.SessionId] = session;
        }

        public void RemoveSession(string sessionId)
            => _sessions.TryRemove(sessionId, out _);

        public IEnumerable<AuthSession> GetAll() => _sessions.Values;

        public void CleanupExpired()
        {
            var now = DateTime.UtcNow;
            foreach (var s in _sessions.Values.Where(s => s.ExpiresAt < now))
                _sessions.TryRemove(s.SessionId, out _);
        }
    }
}
