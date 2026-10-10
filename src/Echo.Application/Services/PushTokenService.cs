using Echo.Application.Common;
using Echo.Application.Contracts;
using Echo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Echo.Application.Services;

public interface IPushTokenService
{
    Task<UpsertPushTokenResponse> UpsertAsync(
        Guid userId,
        UpsertPushTokenRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class PushTokenService(DbContext db) : IPushTokenService
{
    public async Task<UpsertPushTokenResponse> UpsertAsync(
        Guid userId,
        UpsertPushTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var token = request.Token?.Trim() ?? string.Empty;
        if (token.Length is < 20 or > 512)
        {
            throw new AppException(ErrorCodes.InvalidPushToken, "Push token is invalid.", 400);
        }

        var platform = request.Platform?.Trim().ToLowerInvariant() ?? string.Empty;
        if (platform is not ("android" or "ios"))
        {
            throw new AppException(ErrorCodes.InvalidPushToken, "platform must be android or ios.", 400);
        }

        var userExists = await db.Set<AnonymousUser>().AnyAsync(u => u.Id == userId, cancellationToken);
        if (!userExists)
        {
            throw new AppException(ErrorCodes.NotFound, "User not found.", 401);
        }

        var existing = await db.Set<PushToken>().FirstOrDefaultAsync(t => t.Token == token, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            db.Set<PushToken>().Add(new PushToken
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Token = token,
                Platform = platform,
                UpdatedAt = now
            });
        }
        else
        {
            existing.UserId = userId;
            existing.Platform = platform;
            existing.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new UpsertPushTokenResponse(token, platform);
    }
}
