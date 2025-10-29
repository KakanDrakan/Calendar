using Azure.Core;
using CalendarApi.Contracts;
using CalendarApi.Dtos;
using CalendarApi.Helpers;
using CalendarApi.Stores;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;

namespace CalendarApi.Services
{
    public class MicrosoftAuthService : IAuthService
    {
        private readonly IConfiguration config;
        private readonly SessionStore sessionStore;
        private readonly ILogger<MicrosoftAuthService> logger;

        public MicrosoftAuthService(IConfiguration config, SessionStore sessionStore, ILogger<MicrosoftAuthService> logger)
        {
            this.config = config;
            this.sessionStore = sessionStore;
            this.logger = logger;
        }

        // Step 1: Get the Microsoft OAuth authorize URL
        public string GetAuthorizationUrl(string sessionId)
        {
            var clientId = config["AzureAd:ClientId"];
            var tenantId = config["AzureAd:TenantId"];
            var redirectUri = config["AzureAd:RedirectUri"];

            var scopes = Uri.EscapeDataString("Calendars.Read offline_access");

            return $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize" +
                   $"?client_id={clientId}" +
                   $"&response_type=code" +
                   $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                   $"&response_mode=query" +
                   $"&scope={scopes}" +
                   $"&state={sessionId}" +
                   $"&prompt=select_account";
        }

        // Step 2: Exchange the auth code for tokens
        public async Task<AuthSession?> ExchangeCodeForTokenAsync(string sessionId, string code)
        {
            if (!sessionStore.TryGetSession(sessionId, out var session))
                return null;

            var clientId = config["AzureAd:ClientId"];
            var tenantId = config["AzureAd:TenantId"];
            var clientSecret = config["AzureAd:ClientSecret"];
            var redirectUri = config["AzureAd:RedirectUri"];

            var app = ConfidentialClientApplicationBuilder
                .Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}/v2.0"))
                .WithRedirectUri(redirectUri)
                .Build();

            // Enable serialization for both token caches before token acquisition
            MsalTokenCacheHelper.EnableSerialization(app.UserTokenCache, session, logger, sessionStore);
            MsalTokenCacheHelper.EnableSerialization(app.AppTokenCache, session, logger, sessionStore);

            var result = await app
                .AcquireTokenByAuthorizationCode(new[] { "Calendars.Read", "offline_access" }, code)
                .ExecuteAsync();

            // Update session with all token information
            session.AccessToken = result.AccessToken;
            session.TokenExpiresAt = result.ExpiresOn.UtcDateTime;
            session.State = SessionState.Authenticated;
            session.UserId = result.Account.HomeAccountId.Identifier;
            session.UserName = result.Account.Username;

            session.ExpiresAt = DateTime.UtcNow.AddDays(1);

            // Update session with the new information
            sessionStore.UpdateSession(session);

            logger.LogInformation("Initial token acquired for user {UserName} (ID: {UserId})", 
                session.UserName, session.UserId);
            logger.LogDebug("Token cache size: {Size}", session.TokenCacheData?.Length ?? 0);

            sessionStore.UpdateSession(session);

            return session;
        }

        // Step 3: Get calendars from Graph
        public async Task<List<Calendar>> GetCalendarsAsync(AuthSession session)
        {
            var credential = new DelegateCredential((_, _) =>
                new ValueTask<AccessToken>(
                    new AccessToken(session.AccessToken!, session.TokenExpiresAt ?? DateTimeOffset.UtcNow.AddMinutes(50))
                ));

            var graphClient = new GraphServiceClient(credential);

            var response = await graphClient.Me.Calendars.GetAsync();
            return response?.Value?.ToList() ?? new List<Calendar>();
        }

        public async Task<string?> GetAccessTokenAsync(AuthSession session)
        {
            var clientId = config["AzureAd:ClientId"];
            var tenantId = config["AzureAd:TenantId"];
            var clientSecret = config["AzureAd:ClientSecret"];
            var redirectUri = config["AzureAd:RedirectUri"];

            var app = ConfidentialClientApplicationBuilder
                .Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}/v2.0"))
                .WithRedirectUri(redirectUri)
                .Build();

            // Enable serialization for both token caches
            MsalTokenCacheHelper.EnableSerialization(app.UserTokenCache, session, logger, sessionStore);
            MsalTokenCacheHelper.EnableSerialization(app.AppTokenCache, session, logger, sessionStore);

            try
            {
                if (string.IsNullOrEmpty(session.UserId))
                {
                    logger.LogWarning("Cannot refresh token - session has no UserId");
                    session.State = SessionState.Expired;
                    return null;
                }

                logger.LogInformation("Attempting token refresh for user {UserName} (ID: {UserId})", 
                    session.UserName ?? "unknown", session.UserId);

                logger.LogDebug("Token cache state - HasCache: {HasCache}, Size: {Size}", 
                    session.TokenCacheData != null, 
                    session.TokenCacheData?.Length ?? 0);

                var account = await app.GetAccountAsync(session.UserId);
                if (account == null)
                {
                    logger.LogWarning("Account not found in MSAL cache for user {UserName}", session.UserName);
                    session.State = SessionState.Expired;
                    return null;
                }

                logger.LogDebug("Found account in MSAL cache. Attempting silent token acquisition");
                var result = await app.AcquireTokenSilent(
                    new[] { "Calendars.Read", "offline_access" }, account
                ).ExecuteAsync();

                // Update session with new token info
                session.AccessToken = result.AccessToken;
                session.TokenExpiresAt = result.ExpiresOn.UtcDateTime;
                
                // If we successfully got a new token, ensure the session is marked as active
                if (session.State == SessionState.Expired)
                {
                    logger.LogInformation("Reactivating previously expired session for user {UserName}", session.UserName);
                    session.State = SessionState.Authenticated;
                }

                sessionStore.UpdateSession(session);
                
                logger.LogDebug("Token refresh successful. New expiration: {ExpiresAt}", session.TokenExpiresAt);
                return result.AccessToken;
            }
            catch (MsalUiRequiredException)
            {
                logger.LogWarning("Silent token refresh failed for user {User}", session.UserName);
                session.State = SessionState.Expired;
                return null;
            }
        }

    }
}
