using CalendarApi.Dtos;
using CalendarApi.Stores;
using Microsoft.Identity.Client;

public static class MsalTokenCacheHelper
{
    public static void EnableSerialization(ITokenCache tokenCache, AuthSession session, ILogger logger, SessionStore sessionStore)
    {
        tokenCache.SetBeforeAccess(args =>
        {
            logger.LogDebug("[TokenCache] BeforeAccess - Session {SessionId} HasCache: {HasCache}, CacheSize: {Size}, UserId: {UserId}",
                session.SessionId,
                session.TokenCacheData != null,
                session.TokenCacheData?.Length ?? 0,
                session.UserId);

            if (session.TokenCacheData != null)
            {
                try
                {
                    args.TokenCache.DeserializeMsalV3(session.TokenCacheData);
                    logger.LogDebug("[TokenCache] Successfully deserialized cache for session {SessionId}", session.SessionId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[TokenCache] Failed to deserialize cache for session {SessionId}", session.SessionId);
                }
            }
        });

        tokenCache.SetAfterAccess(args =>
        {
            if (args.HasStateChanged)
            {
                try
                {
                    session.TokenCacheData = args.TokenCache.SerializeMsalV3();
                    sessionStore.UpdateSession(session);
                    
                    logger.LogDebug("[TokenCache] AfterAccess - Saved cache for session {SessionId}, size {Size}, HasStateChanged: {HasStateChanged}",
                        session.SessionId, session.TokenCacheData?.Length ?? 0, args.HasStateChanged);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[TokenCache] Failed to persist token cache for session {SessionId}", session.SessionId);
                }
            }
        });
    }
}