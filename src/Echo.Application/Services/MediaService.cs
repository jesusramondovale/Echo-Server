using Echo.Application.Abstractions;
using Echo.Application.Common;
using Echo.Application.Contracts;
using Echo.Domain;
using Echo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Echo.Application.Services;

public interface IMediaService
{
    Task<MediaDto> UploadAsync(
        Guid userId,
        Stream content,
        string contentType,
        string fileName,
        int? width,
        int? height,
        CancellationToken cancellationToken = default);

    Task<AvatarResponse> SetAvatarAsync(
        Guid userId,
        Guid mediaId,
        CancellationToken cancellationToken = default);
}

public sealed class MediaService : IMediaService
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    private readonly DbContext _db;
    private readonly IMediaStorage _storage;
    private readonly IRateLimiter _rateLimiter;

    public MediaService(DbContext db, IMediaStorage storage, IRateLimiter rateLimiter)
    {
        _db = db;
        _storage = storage;
        _rateLimiter = rateLimiter;
    }

    public async Task<MediaDto> UploadAsync(
        Guid userId,
        Stream content,
        string contentType,
        string fileName,
        int? width,
        int? height,
        CancellationToken cancellationToken = default)
    {
        if (!_rateLimiter.TryAcquire($"uploads:{userId}", 40, TimeSpan.FromDays(1)))
        {
            throw new AppException(ErrorCodes.RateLimited, "Too many uploads today.", 429);
        }

        if (!AllowedTypes.Contains(contentType))
        {
            throw new AppException(ErrorCodes.InvalidMedia, "Only jpeg, png and webp are allowed.", 400);
        }

        if (content.CanSeek && content.Length > GeoConstants.MaxMediaBytes)
        {
            throw new AppException(ErrorCodes.InvalidMedia, "File too large (max 5 MB).", 400);
        }

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = contentType switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => ".jpg"
            };
        }

        await using var limited = new LimitedStream(content, GeoConstants.MaxMediaBytes);
        var (path, size) = await _storage.SaveAsync(limited, contentType, ext, cancellationToken);

        var media = new Media
        {
            Id = Guid.NewGuid(),
            Path = path,
            ContentType = contentType,
            Width = width,
            Height = height,
            SizeBytes = size,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Set<Media>().Add(media);
        await _db.SaveChangesAsync(cancellationToken);

        return new MediaDto(media.Id, _storage.GetPublicUrl(media.Path), media.ContentType, media.Width, media.Height);
    }

    public async Task<AvatarResponse> SetAvatarAsync(
        Guid userId,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Set<AnonymousUser>().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
                   ?? throw new AppException(ErrorCodes.NotFound, "User not found.", 401);

        var media = await _db.Set<Media>().AsNoTracking().FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken)
                    ?? throw new AppException(ErrorCodes.InvalidMedia, "Media not found.", 400);

        user.AvatarMediaId = mediaId;
        user.LastSeenAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new AvatarResponse(new MediaDto(
            media.Id,
            _storage.GetPublicUrl(media.Path),
            media.ContentType,
            media.Width,
            media.Height));
    }

    private sealed class LimitedStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _max;
        private long _read;

        public LimitedStream(Stream inner, long max)
        {
            _inner = inner;
            _max = max;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _read;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = _inner.Read(buffer, offset, count);
            _read += n;
            if (_read > _max)
            {
                throw new AppException(ErrorCodes.InvalidMedia, "File too large (max 5 MB).", 400);
            }

            return n;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var n = await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
            _read += n;
            if (_read > _max)
            {
                throw new AppException(ErrorCodes.InvalidMedia, "File too large (max 5 MB).", 400);
            }

            return n;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var n = await _inner.ReadAsync(buffer, cancellationToken);
            _read += n;
            if (_read > _max)
            {
                throw new AppException(ErrorCodes.InvalidMedia, "File too large (max 5 MB).", 400);
            }

            return n;
        }

        public override void Flush() => _inner.Flush();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
