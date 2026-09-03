namespace Echo.Domain;

public static class GeoConstants
{
    /// <summary>Feed / visibility radius in meters.</summary>
    public const double FeedRadiusMeters = 15_000;

    /// <summary>Max allowed distance between GPS and IP geolocation (meters).</summary>
    public const double IpMismatchMaxMeters = 100_000;

    /// <summary>Location reading must be fresher than this.</summary>
    public static readonly TimeSpan MaxLocationAge = TimeSpan.FromMinutes(2);

    public const int MaxBodyLength = 2000;
    public const int MaxMediaBytes = 5 * 1024 * 1024;
    public const int FeedPageSize = 30;
    public const int MapMaxPosts = 200;
}
