namespace Echo.Application.Abstractions;

public interface ILocationValidator
{
    Task ValidateViewerLocationAsync(
        double lat,
        double lon,
        string? clientIp,
        CancellationToken cancellationToken = default);
}
