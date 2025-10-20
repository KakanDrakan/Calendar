using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

public class CalendarHub : Hub
{
    private static readonly ConcurrentDictionary<string, HashSet<string>> calendarConnections = new();
    public async Task JoinCalendar(string calendarId)
    {
        var groupName = GetCalendarGroupName(calendarId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        calendarConnections.AddOrUpdate(calendarId,
            _ => new HashSet<string> { Context.ConnectionId },
            (_, connections) =>
            {
                connections.Add(Context.ConnectionId);
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
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var entry in calendarConnections)
        {
            lock (entry.Value)
            {
                entry.Value.Remove(Context.ConnectionId);
                if (entry.Value.Count == 0)
                    calendarConnections.TryRemove(entry.Key, out _);
            }
        }

        return base.OnDisconnectedAsync(exception);
    }

    public static IEnumerable<string> GetActiveCalendars() => calendarConnections.Keys;

    private string GetCalendarGroupName(string calendarId) =>
        $"calendar:{calendarId}";
}