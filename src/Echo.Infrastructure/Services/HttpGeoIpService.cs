using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Echo.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Echo.Infrastructure.Services;

/// <summary>Resolves IP geolocation via ip-api.com (free, no key). Skips private/local addresses.</summary>
public sealed class HttpGeoIpService : IGeoIpService
{
    private readonly HttpClient _http;
    private readonly ILogger<HttpGeoIpService> _logger;

    public HttpGeoIpService(HttpClient http, ILogger<HttpGeoIpService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<GeoPoint?> ResolveAsync(string? ipAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ipAddress) || IsPrivateOrLocal(ipAddress))
        {
            return null;
        }

        try
        {
            var url = $"http://ip-api.com/json/{ipAddress}?fields=status,lat,lon";
            var result = await _http.GetFromJsonAsync<IpApiResponse>(url, cancellationToken);
            if (result is null || !string.Equals(result.Status, "success", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new GeoPoint(result.Lat, result.Lon);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GeoIP lookup failed for {Ip}", RoundIp(ipAddress));
            return null;
        }
    }

    private static bool IsPrivateOrLocal(string ip)
    {
        if (!IPAddress.TryParse(ip, out var address))
        {
            return true;
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10
                   || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                   || (bytes[0] == 192 && bytes[1] == 168)
                   || (bytes[0] == 169 && bytes[1] == 254);
        }

        return false;
    }

    private static string RoundIp(string ip)
    {
        var parts = ip.Split('.');
        return parts.Length == 4 ? $"{parts[0]}.{parts[1]}.x.x" : "x";
    }

    private sealed class IpApiResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lon")]
        public double Lon { get; set; }
    }
}
