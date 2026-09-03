using System.Security.Cryptography;
using System.Text;

namespace Echo.Application.Common;

public static class CryptoHash
{
    public static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public static class AuthorGlyph
{
    /// <summary>Stable non-identifying label derived from user id (not reversible to a person).</summary>
    public static string FromUserId(Guid userId)
    {
        var hash = SHA256.HashData(userId.ToByteArray());
        var hue = hash[0];
        var shape = hash[1] % 6;
        var shapes = new[] { "●", "■", "▲", "◆", "★", "✦" };
        return $"{shapes[shape]}{hue:X2}";
    }
}

public static class GeoMath
{
    private const double EarthRadiusMeters = 6_371_000;

    public static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        static double ToRad(double deg) => deg * Math.PI / 180.0;
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
                  * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }
}
