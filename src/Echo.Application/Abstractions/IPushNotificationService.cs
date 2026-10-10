namespace Echo.Application.Abstractions;

public interface IPushNotificationService
{
    Task NotifyPostBoostedAsync(Guid authorUserId, Guid postId, CancellationToken cancellationToken = default);

    Task NotifyNewPostNearbyAsync(
        Guid authorUserId,
        Guid postId,
        double lat,
        double lon,
        double radiusMeters,
        CancellationToken cancellationToken = default);
}
