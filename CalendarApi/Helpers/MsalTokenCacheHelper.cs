using CalendarApi.Models;
using CalendarApi.Stores;
using Microsoft.Identity.Client;

public static class MsalTokenCacheHelper
{
    public static void EnableSerialization(
        ITokenCache tokenCache,
        AuthSession session,
        ILogger logger,
        SessionStore sessionStore)
    {
        tokenCache.SetBeforeAccess(args =>
        {
            try
            {
                logger.LogDebug(
                    "[TokenCache] BeforeAccess - Session {SessionId} HasCache: {HasCache}, Size: {Size}, UserId: {UserId}",
                    session.SessionId,
                    session.TokenCacheData != null,
                    session.TokenCacheData?.Length ?? 0,
                    session.UserId
                );

                if (string.IsNullOrEmpty(session.SessionId))
                {
                    logger.LogWarning("[TokenCache] Missing SessionId before access");
                    return;
                }

                // Safely run async Mongo operation without deadlocking ASP.NET
                Task.Run(async () =>
                {
                    var (found, freshSession) = await sessionStore.TryGetSessionAsync(session.SessionId);
                    if (found && freshSession?.TokenCacheData != null)
                    {
                        args.TokenCache.DeserializeMsalV3(freshSession.TokenCacheData);
                        session.TokenCacheData = freshSession.TokenCacheData;
                        logger.LogDebug("[TokenCache] Deserialized cache for session {SessionId}", session.SessionId);
                    }
                }).Wait();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[TokenCache] Failed to deserialize token cache for session {SessionId}", session.SessionId);
            }
        });

        tokenCache.SetAfterAccess(args =>
        {
            if (!args.HasStateChanged)
                return;

            try
            {
                session.TokenCacheData = args.TokenCache.SerializeMsalV3();

                // Safely persist token cache to Mongo
                Task.Run(async () =>
                {
                    await sessionStore.UpdateSessionAsync(session);
                }).Wait();

                logger.LogDebug(
                    "[TokenCache] AfterAccess - Saved cache for session {SessionId}, size {Size}, HasStateChanged: {HasStateChanged}",
                    session.SessionId,
                    session.TokenCacheData?.Length ?? 0,
                    args.HasStateChanged
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[TokenCache] Failed to persist token cache for session {SessionId}", session.SessionId);
            }
        });
    }
}
