namespace Echo.Application.Common;

public sealed class AppException : Exception
{
    public string Code { get; }
    public int StatusCode { get; }

    public AppException(string code, string message, int statusCode = 400)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }
}

public static class ErrorCodes
{
    public const string LocationRequired = "LOCATION_REQUIRED";
    public const string LocationMismatchIp = "LOCATION_MISMATCH_IP";
    public const string OutOfRange = "OUT_OF_RANGE";
    public const string RateLimited = "RATE_LIMITED";
    public const string NotFound = "NOT_FOUND";
    public const string InvalidDeviceKey = "INVALID_DEVICE_KEY";
    public const string InvalidMedia = "INVALID_MEDIA";
    public const string BodyTooLong = "BODY_TOO_LONG";
}
