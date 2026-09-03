using Echo.Application.Abstractions;
using Echo.Application.Common;
using Echo.Application.Contracts;
using Echo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Echo.Application.Services;

public interface ISessionService
{
    Task<BootstrapResponse> BootstrapAsync(string deviceKey, CancellationToken cancellationToken = default);
}

public sealed class SessionService : ISessionService
{
    private readonly DbContext _db;
    private readonly ITokenService _tokens;

    public SessionService(DbContext db, ITokenService tokens)
    {
        _db = db;
        _tokens = tokens;
    }

    public async Task<BootstrapResponse> BootstrapAsync(string deviceKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceKey) || deviceKey.Length < 16 || deviceKey.Length > 128)
        {
            throw new AppException(ErrorCodes.InvalidDeviceKey, "deviceKey must be 16-128 characters.", 400);
        }

        var hash = CryptoHash.Sha256Hex(deviceKey.Trim());
        var users = _db.Set<AnonymousUser>();
        var user = await users.FirstOrDefaultAsync(u => u.DeviceKeyHash == hash, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (user is null)
        {
            user = new AnonymousUser
            {
                Id = Guid.NewGuid(),
                DeviceKeyHash = hash,
                CreatedAt = now,
                LastSeenAt = now
            };
            users.Add(user);
        }
        else
        {
            user.LastSeenAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        var (token, expires) = _tokens.CreateToken(user.Id);
        return new BootstrapResponse(token, expires, user.Id);
    }
}
