using CalendarApi.Models;

namespace CalendarApi.Contracts
{
    public interface IAuthService
    {
        // Step 1: Build the Microsoft OAuth authorization URL
        string GetAuthorizationUrl(string sessionId);

        // Step 2: Exchange auth code for tokens
        Task<AuthSession?> ExchangeCodeForTokenAsync(string sessionId, string code);

        // Step 3: Get a valid access token (refresh if needed)
        Task<string?> GetAccessTokenAsync(AuthSession session);

        // Optional: Get the user's calendars
        Task<List<Microsoft.Graph.Models.Calendar>> GetCalendarsAsync(AuthSession session);
    }
}
