namespace Echo.Application.Abstractions;

public sealed record GeoPoint(double Latitude, double Longitude);

public interface IGeoIpService
{
    /// <summary>Returns approximate location for the client IP, or null if unknown / local.</summary>
    Task<GeoPoint?> ResolveAsync(string? ipAddress, CancellationToken cancellationToken = default);
}
