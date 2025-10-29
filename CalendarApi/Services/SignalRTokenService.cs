using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

public class SignalRTokenService
{
    private readonly byte[] key;

    public SignalRTokenService(IConfiguration config)
    {
        var secret = config["SignalR:TokenSecret"];
        if (string.IsNullOrEmpty(secret))
            throw new InvalidOperationException("Please set TokenSecret in configuration.");
        key = Encoding.UTF8.GetBytes(secret);
    }

    public string CreateToken(string sessionId, TimeSpan validFor)
    {
        var expires = DateTimeOffset.UtcNow.Add(validFor).ToUnixTimeSeconds();
        var payload = $"{sessionId}:{expires}";
        var sig = ComputeHmac(payload);
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{payload}:{sig}"));
        return token;
    }

    public bool TryValidateToken(string token, out string? sessionId)
    {
        sessionId = null;
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = decoded.Split(':');
            if (parts.Length != 3) return false;
            var sid = parts[0];
            if (!long.TryParse(parts[1], out var expires)) return false;
            var sig = parts[2];

            var payload = $"{sid}:{expires}";
            var expected = ComputeHmac(payload);
            if (!CryptographicEquals(sig, expected)) return false;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now > expires) return false;

            sessionId = sid;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string ComputeHmac(string message)
    {
        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToBase64String(hash);
    }

    private static bool CryptographicEquals(string a, string b)
    {
        var xa = Convert.FromBase64String(a);
        var xb = Convert.FromBase64String(b);
        return CryptographicOperations.FixedTimeEquals(xa, xb);
    }
}