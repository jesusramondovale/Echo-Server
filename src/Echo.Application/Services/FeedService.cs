using Echo.Application.Abstractions;
using Echo.Application.Common;
using Echo.Application.Contracts;
using Echo.Domain;
using Echo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace Echo.Application.Services;

public interface IFeedService
{
    Task<FeedResponse> GetFeedAsync(
        Guid userId,
        double lat,
        double lon,
        string? cursor,
        string? clientIp,
        CancellationToken cancellationToken = default);

    Task<PostDetailDto> GetPostAsync(
        Guid userId,
        Guid postId,
        double lat,
        double lon,
        string? clientIp,
        CancellationToken cancellationToken = default);

    Task<PostSummaryDto> CreatePostAsync(
        Guid userId,
        CreatePostRequest request,
        string? clientIp,
        CancellationToken cancellationToken = default);

    Task<ReplyDto> CreateReplyAsync(
        Guid userId,
        Guid postId,
        CreateReplyRequest request,
        string? clientIp,
        CancellationToken cancellationToken = default);

    Task<FeedResponse> GetMapAsync(
        Guid userId,
        double lat,
        double lon,
        string? clientIp,
        CancellationToken cancellationToken = default);
}

public sealed class FeedService : IFeedService
{
    private readonly DbContext _db;
    private readonly ILocationValidator _location;
    private readonly IRateLimiter _rateLimiter;
    private readonly IMediaStorage _mediaStorage;
    private static readonly GeometryFactory GeometryFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public FeedService(
        DbContext db,
        ILocationValidator location,
        IRateLimiter rateLimiter,
        IMediaStorage mediaStorage)
    {
        _db = db;
        _location = location;
        _rateLimiter = rateLimiter;
        _mediaStorage = mediaStorage;
    }

    public async Task<FeedResponse> GetFeedAsync(
        Guid userId,
        double lat,
        double lon,
        string? cursor,
        string? clientIp,
        CancellationToken cancellationToken = default)
    {
        await TouchUserAsync(userId, cancellationToken);
        await _location.ValidateViewerLocationAsync(lat, lon, clientIp, cancellationToken);

        var viewer = CreatePoint(lat, lon);
        DateTimeOffset? after = null;
        if (!string.IsNullOrWhiteSpace(cursor) && DateTimeOffset.TryParse(cursor, out var parsed))
        {
            after = parsed;
        }

        var query = _db.Set<Post>()
            .AsNoTracking()
            .Include(p => p.Media)
            .Where(p => !p.IsHidden && p.Location.IsWithinDistance(viewer, GeoConstants.FeedRadiusMeters));

        if (after is not null)
        {
            query = query.Where(p => p.CreatedAt < after);
        }

        var posts = await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(GeoConstants.FeedPageSize)
            .Select(p => new
            {
                p.Id,
                p.Body,
                p.CreatedAt,
                p.AuthorId,
                ReplyCount = p.Replies.Count,
                Media = p.Media,
                p.Location
            })
            .ToListAsync(cancellationToken);

        var items = posts.Select(p => ToSummary(
            p.Id,
            p.Body,
            p.CreatedAt,
            p.AuthorId,
            p.ReplyCount,
            p.Media,
            p.Location.Y,
            p.Location.X)).ToList();

        string? next = items.Count == GeoConstants.FeedPageSize
            ? items[^1].CreatedAt.ToString("O")
            : null;

        return new FeedResponse(items, next);
    }

    public async Task<PostDetailDto> GetPostAsync(
        Guid userId,
        Guid postId,
        double lat,
        double lon,
        string? clientIp,
        CancellationToken cancellationToken = default)
    {
        await TouchUserAsync(userId, cancellationToken);
        await _location.ValidateViewerLocationAsync(lat, lon, clientIp, cancellationToken);

        var viewer = CreatePoint(lat, lon);
        var post = await _db.Set<Post>()
            .AsNoTracking()
            .Include(p => p.Media)
            .Include(p => p.Replies).ThenInclude(r => r.Media)
            .FirstOrDefaultAsync(p => p.Id == postId && !p.IsHidden, cancellationToken);

        if (post is null)
        {
            throw new AppException(ErrorCodes.NotFound, "Post not found.", 404);
        }

        if (!post.Location.IsWithinDistance(viewer, GeoConstants.FeedRadiusMeters))
        {
            throw new AppException(ErrorCodes.OutOfRange, "Post is outside your 15 km zone.", 403);
        }

        var replies = post.Replies
            .OrderBy(r => r.CreatedAt)
            .Select(r => new ReplyDto(
                r.Id,
                r.Body,
                r.CreatedAt,
                AuthorGlyph.FromUserId(r.AuthorId),
                MapMedia(r.Media)))
            .ToList();

        return new PostDetailDto(
            post.Id,
            post.Body,
            post.CreatedAt,
            AuthorGlyph.FromUserId(post.AuthorId),
            MapMedia(post.Media),
            replies,
            post.Location.Y,
            post.Location.X);
    }

    public async Task<FeedResponse> GetMapAsync(
        Guid userId,
        double lat,
        double lon,
        string? clientIp,
        CancellationToken cancellationToken = default)
    {
        await TouchUserAsync(userId, cancellationToken);
        await _location.ValidateViewerLocationAsync(lat, lon, clientIp, cancellationToken);

        var viewer = CreatePoint(lat, lon);
        var posts = await _db.Set<Post>()
            .AsNoTracking()
            .Include(p => p.Media)
            .Where(p => !p.IsHidden && p.Location.IsWithinDistance(viewer, GeoConstants.FeedRadiusMeters))
            .OrderByDescending(p => p.CreatedAt)
            .Take(GeoConstants.MapMaxPosts)
            .Select(p => new
            {
                p.Id,
                p.Body,
                p.CreatedAt,
                p.AuthorId,
                ReplyCount = p.Replies.Count,
                Media = p.Media,
                p.Location
            })
            .ToListAsync(cancellationToken);

        var items = posts.Select(p => ToSummary(
            p.Id,
            p.Body,
            p.CreatedAt,
            p.AuthorId,
            p.ReplyCount,
            p.Media,
            p.Location.Y,
            p.Location.X)).ToList();

        return new FeedResponse(items, null);
    }

    public async Task<PostSummaryDto> CreatePostAsync(
        Guid userId,
        CreatePostRequest request,
        string? clientIp,
        CancellationToken cancellationToken = default)
    {
        await TouchUserAsync(userId, cancellationToken);
        await _location.ValidateViewerLocationAsync(request.Lat, request.Lon, clientIp, cancellationToken);
        ValidateBody(request.Body);

        if (!_rateLimiter.TryAcquire($"posts:{userId}", 10, TimeSpan.FromHours(1)))
        {
            throw new AppException(ErrorCodes.RateLimited, "Too many posts. Try again later.", 429);
        }

        await EnsureMediaExistsAsync(request.MediaId, cancellationToken);

        var post = new Post
        {
            Id = Guid.NewGuid(),
            AuthorId = userId,
            Body = request.Body.Trim(),
            MediaId = request.MediaId,
            Location = CreatePoint(request.Lat, request.Lon),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Set<Post>().Add(post);
        await _db.SaveChangesAsync(cancellationToken);

        Media? media = null;
        if (request.MediaId is Guid mid)
        {
            media = await _db.Set<Media>().AsNoTracking().FirstAsync(m => m.Id == mid, cancellationToken);
        }

        return ToSummary(
            post.Id,
            post.Body,
            post.CreatedAt,
            userId,
            0,
            media,
            request.Lat,
            request.Lon);
    }

    public async Task<ReplyDto> CreateReplyAsync(
        Guid userId,
        Guid postId,
        CreateReplyRequest request,
        string? clientIp,
        CancellationToken cancellationToken = default)
    {
        await TouchUserAsync(userId, cancellationToken);
        await _location.ValidateViewerLocationAsync(request.Lat, request.Lon, clientIp, cancellationToken);
        ValidateBody(request.Body);

        if (!_rateLimiter.TryAcquire($"replies:{userId}", 60, TimeSpan.FromHours(1)))
        {
            throw new AppException(ErrorCodes.RateLimited, "Too many replies. Try again later.", 429);
        }

        var viewer = CreatePoint(request.Lat, request.Lon);
        var post = await _db.Set<Post>().FirstOrDefaultAsync(p => p.Id == postId && !p.IsHidden, cancellationToken);
        if (post is null)
        {
            throw new AppException(ErrorCodes.NotFound, "Post not found.", 404);
        }

        if (!post.Location.IsWithinDistance(viewer, GeoConstants.FeedRadiusMeters))
        {
            throw new AppException(ErrorCodes.OutOfRange, "Post is outside your 15 km zone.", 403);
        }

        await EnsureMediaExistsAsync(request.MediaId, cancellationToken);

        var reply = new Reply
        {
            Id = Guid.NewGuid(),
            PostId = postId,
            AuthorId = userId,
            Body = request.Body.Trim(),
            MediaId = request.MediaId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Set<Reply>().Add(reply);
        await _db.SaveChangesAsync(cancellationToken);

        Media? media = null;
        if (request.MediaId is Guid mid)
        {
            media = await _db.Set<Media>().AsNoTracking().FirstAsync(m => m.Id == mid, cancellationToken);
        }

        return new ReplyDto(
            reply.Id,
            reply.Body,
            reply.CreatedAt,
            AuthorGlyph.FromUserId(userId),
            MapMedia(media));
    }

    private PostSummaryDto ToSummary(
        Guid id,
        string body,
        DateTimeOffset createdAt,
        Guid authorId,
        int replyCount,
        Media? media,
        double lat,
        double lon)
        => new(
            id,
            body,
            createdAt,
            AuthorGlyph.FromUserId(authorId),
            replyCount,
            MapMedia(media),
            lat,
            lon);

    private MediaDto? MapMedia(Media? media)
    {
        if (media is null) return null;
        return new MediaDto(
            media.Id,
            _mediaStorage.GetPublicUrl(media.Path),
            media.ContentType,
            media.Width,
            media.Height);
    }

    private static Point CreatePoint(double lat, double lon)
    {
        // NetTopologySuite / PostGIS: X = longitude, Y = latitude
        var point = GeometryFactory.CreatePoint(new Coordinate(lon, lat));
        point.SRID = 4326;
        return point;
    }

    private static void ValidateBody(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new AppException(ErrorCodes.BodyTooLong, "Body is required.", 400);
        }

        if (body.Trim().Length > GeoConstants.MaxBodyLength)
        {
            throw new AppException(ErrorCodes.BodyTooLong, $"Body must be at most {GeoConstants.MaxBodyLength} characters.", 400);
        }
    }

    private async Task EnsureMediaExistsAsync(Guid? mediaId, CancellationToken cancellationToken)
    {
        if (mediaId is null) return;
        var exists = await _db.Set<Media>().AnyAsync(m => m.Id == mediaId, cancellationToken);
        if (!exists)
        {
            throw new AppException(ErrorCodes.InvalidMedia, "Media not found.", 400);
        }
    }

    private async Task TouchUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _db.Set<AnonymousUser>().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            throw new AppException(ErrorCodes.NotFound, "User not found.", 401);
        }

        user.LastSeenAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
