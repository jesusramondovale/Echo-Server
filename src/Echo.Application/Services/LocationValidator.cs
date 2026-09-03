using Echo.Application.Abstractions;
using Echo.Application.Common;
using Echo.Domain;

namespace Echo.Application.Services;

public sealed class LocationValidator : ILocationValidator
{
    private readonly IGeoIpService _geoIp;
    private readonly bool _skipIpCheck;

    public LocationValidator(IGeoIpService geoIp, bool skipIpCheck = false)
    {
        _geoIp = geoIp;
        _skipIpCheck = skipIpCheck;
    }

    public async Task ValidateViewerLocationAsync(
        double lat,
        double lon,
        string? clientIp,
        CancellationToken cancellationToken = default)
    {
        if (double.IsNaN(lat) || double.IsNaN(lon) || lat is < -90 or > 90 || lon is < -180 or > 180)
        {
            throw new AppException(ErrorCodes.LocationRequired, "Valid GPS coordinates are required.", 400);
        }

        if (_skipIpCheck)
        {
            return;
        }

        var ipGeo = await _geoIp.ResolveAsync(clientIp, cancellationToken);
        if (ipGeo is null)
        {
            // Localhost / unknown IP: allow in development-style setups
            return;
        }

        var distance = GeoMath.HaversineMeters(lat, lon, ipGeo.Latitude, ipGeo.Longitude);
        if (distance > GeoConstants.IpMismatchMaxMeters)
        {
            throw new AppException(
                ErrorCodes.LocationMismatchIp,
                "GPS location is too far from the approximate network location.",
                403);
        }
    }
}
