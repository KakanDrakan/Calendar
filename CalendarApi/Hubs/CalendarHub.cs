using CalendarApi.Models;
using CalendarApi.Stores;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

public class CalendarHub : Hub
{
    private static readonly ConcurrentDictionary<string, HashSet<string>> calendarConnections = new();
    private static readonly ConcurrentDictionary<string, HashSet<string>> sessionConnections = new();

    private readonly SignalRTokenService tokenService;

    public CalendarHub(SignalRTokenService tokenService)
    {
        this.tokenService = tokenService;
    }


    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        string? token = null;

        // The client will send Authorization: Bearer <token> during negotiate/start
        if (http?.Request?.Headers.TryGetValue("Authorization", out var authHeader) == true)
        {
            var raw = authHeader.ToString();
            if (raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                token = raw.Substring("Bearer ".Length).Trim();
        }

        if (!string.IsNullOrEmpty(token) && tokenService.TryValidateToken(token, out var sessionId))
        {
            var groupName = $"session:{sessionId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            sessionConnections.AddOrUpdate(sessionId,
                _ => new HashSet<string> { Context.ConnectionId },
                (_, connections) =>
                {
                    lock (connections) connections.Add(Context.ConnectionId);
                    return connections;
                });

            Console.WriteLine($"Connection {Context.ConnectionId} auto-joined session group: {groupName}");
        }

        await base.OnConnectedAsync();
    }


    public async Task JoinCalendar(string calendarId)
    {
        var groupName = GetCalendarGroupName(calendarId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        calendarConnections.AddOrUpdate(calendarId,
            _ => new HashSet<string> { Context.ConnectionId },
            (_, connections) =>
            {
                lock (connections) connections.Add(Context.ConnectionId);
                return connections;
            });

        Console.WriteLine($"Connection {Context.ConnectionId} joined calendar group: {groupName}");
    }

    public async Task LeaveCalendar(string calendarId)
    {
        var groupName = GetCalendarGroupName(calendarId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        if (calendarConnections.TryGetValue(calendarId, out var set))
        {
            lock (set) set.Remove(Context.ConnectionId);
            if (set.Count == 0)
                calendarConnections.TryRemove(calendarId, out _);
        }

        Console.WriteLine($"Connection {Context.ConnectionId} left calendar group: {groupName}");
    }

    // Session group support
    public async Task JoinSession(string sessionId)
    {
        var groupName = GetSessionGroupName(sessionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        sessionConnections.AddOrUpdate(sessionId,
            _ => new HashSet<string> { Context.ConnectionId },
            (_, connections) =>
            {
                lock (connections) connections.Add(Context.ConnectionId);
                return connections;
            });

        Console.WriteLine($"Connection {Context.ConnectionId} joined session group: {groupName}");
    }

    public async Task LeaveSession(string sessionId)
    {
        var groupName = GetSessionGroupName(sessionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        if (sessionConnections.TryGetValue(sessionId, out var set))
        {
            lock (set) set.Remove(Context.ConnectionId);
            if (set.Count == 0)
                sessionConnections.TryRemove(sessionId, out _);
        }

        Console.WriteLine($"Connection {Context.ConnectionId} left session group: {groupName}");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var sessionsToExpire = new List<string>();
        var calendarsToRemove = new List<string>();

        // --- Handle calendar connections ---
        foreach (var entry in calendarConnections)
        {
            lock (entry.Value)
            {
                entry.Value.Remove(Context.ConnectionId);
                if (entry.Value.Count == 0)
                    calendarsToRemove.Add(entry.Key);
            }
        }

        foreach (var calendarId in calendarsToRemove)
            calendarConnections.TryRemove(calendarId, out _);

        // --- Handle session connections ---
        foreach (var entry in sessionConnections)
        {
            bool emptyAfterRemoval = false;
            lock (entry.Value)
            {
                entry.Value.Remove(Context.ConnectionId);
                if (entry.Value.Count == 0)
                {
                    emptyAfterRemoval = true;
                    sessionsToExpire.Add(entry.Key);
                }
            }

            if (emptyAfterRemoval)
                sessionConnections.TryRemove(entry.Key, out _);
        }

        // --- Perform async DB updates outside of locks ---
        if (sessionsToExpire.Count > 0)
        {
            var sessionStore = Context.GetHttpContext()?.RequestServices.GetService<SessionStore>();
            if (sessionStore != null)
            {
                foreach (var sessionId in sessionsToExpire)
                {
                    try
                    {
                        var session = await sessionStore.GetSessionAsync(sessionId);
                        if (session != null)
                        {
                            session.State = SessionState.Expired;
                            await sessionStore.UpdateSessionAsync(session);
                            Console.WriteLine($"[Hub] Session {sessionId} marked expired in DB.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Hub] Failed to expire session {sessionId}: {ex.Message}");
                    }
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }


    public static bool IsCalendarActive(string calendarId)
    => calendarConnections.TryGetValue($"calendar:{calendarId}", out var conns) && conns.Count > 0;

    public static IEnumerable<string> GetActiveCalendars() => calendarConnections.Keys;

    private string GetCalendarGroupName(string calendarId) => $"calendar:{calendarId}";
    private string GetSessionGroupName(string sessionId) => $"session:{sessionId}";
}