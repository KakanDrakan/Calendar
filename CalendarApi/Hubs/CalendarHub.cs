using CalendarApi.Models;
using CalendarApi.Stores;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using System.Collections.Concurrent;

public class CalendarHub : Hub
{
    private readonly IConnectionMultiplexer redis;
    private readonly SignalRTokenService tokenService;

    public CalendarHub(SignalRTokenService tokenService, IConnectionMultiplexer redis)
    {
        this.tokenService = tokenService;
        this.redis = redis;
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

            Console.WriteLine($"Connection {Context.ConnectionId} auto-joined session group: {groupName}");
        }

        await base.OnConnectedAsync();
    }


    public async Task JoinCalendar(string subscriptionId)
    {
        var groupName = GetCalendarGroupName(subscriptionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);


        var db = redis.GetDatabase();
        await db.SetAddAsync($"active:calendar:{subscriptionId}", Context.ConnectionId);

        Console.WriteLine($"Connection {Context.ConnectionId} joined calendar group: {groupName}");
    }

    public async Task LeaveCalendar(string subscriptionId)
    {
        var groupName = GetCalendarGroupName(subscriptionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);


        var db = redis.GetDatabase();
        await db.SetRemoveAsync($"active:calendar:{subscriptionId}", Context.ConnectionId);

        Console.WriteLine($"Connection {Context.ConnectionId} left calendar group: {groupName}");
    }

    // Session group support
    public async Task JoinSession(string sessionId)
    {
        var groupName = GetSessionGroupName(sessionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        Console.WriteLine($"Connection {Context.ConnectionId} joined session group: {groupName}");
    }

    public async Task LeaveSession(string sessionId)
    {
        var groupName = GetSessionGroupName(sessionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        Console.WriteLine($"Connection {Context.ConnectionId} left session group: {groupName}");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var db = redis.GetDatabase();
        var endpoints = redis.GetEndPoints();
        var server = redis.GetServer(endpoints.First());

        foreach (var key in server.Keys(pattern: "active:calendar:*"))
        {
            await db.SetRemoveAsync(key, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private string GetCalendarGroupName(string calendarId) => $"calendar:{calendarId}";
    private string GetSessionGroupName(string sessionId) => $"session:{sessionId}";
}