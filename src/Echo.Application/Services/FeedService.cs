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

    Task<BoostResponse> BoostPostAsync(
        Guid userId,
        Guid postId,
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
    private readonly IPushNotificationService _push;
    private static readonly GeometryFactory GeometryFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    public FeedService(
        DbContext db,
        ILocationValidator location,
        IRateLimiter rateLimiter,
        IMediaStorage mediaStorage,
        IPushNotificationService push)
    {
        _db = db;
        _location = location;
        _rateLimiter = rateLimiter;
        _mediaStorage = mediaStorage;
        _push = push;
    }

    public async Task<FeedResponse> GetFeedAsync(
        Guid userId,
        double lat,
        double lon,
        string? cursor,
        string? clientIp,
        CancellationToken cancellationToken = default)
    {
        await TouchUserAsync(userId, lat, lon, cancellationToken);
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
            .Where(p => !p.IsHidden
                        && p.Location.IsWithinDistance(
                            viewer,
                            GeoConstants.FeedRadiusMeters + p.ReachBoost * GeoConstants.ReachBoostMetersPerLevel));

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
                p.ReachBoost,
                ViewerHasBoosted = p.Boosts.Any(b => b.UserId == userId),
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
            p.ReachBoost,
            p.ViewerHasBoosted,
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
        await TouchUserAsync(userId, lat, lon, cancellationToken);
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

        if (!IsWithinEffectiveRadius(post, viewer))
        {
            throw new AppException(ErrorCodes.OutOfRange, "Post is outside your visibility zone.", 403);
        }

        var viewerHasBoosted = await _db.Set<PostBoost>()
            .AsNoTracking()
            .AnyAsync(b => b.PostId == postId && b.UserId == userId, cancellationToken);

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
            post.ReachBoost,
            viewerHasBoosted,
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
        await TouchUserAsync(userId, lat, lon, cancellationToken);
        await _location.ValidateViewerLocationAsync(lat, lon, clientIp, cancellationToken);

        var viewer = CreatePoint(lat, lon);
        var posts = await _db.Set<Post>()
            .AsNoTracking()
            .Include(p => p.Media)
            .Where(p => !p.IsHidden
                        && p.Location.IsWithinDistance(
                            viewer,
                            GeoConstants.FeedRadiusMeters + p.ReachBoost * GeoConstants.ReachBoostMetersPerLevel))
            .OrderByDescending(p => p.CreatedAt)
            .Take(GeoConstants.MapMaxPosts)
            .Select(p => new
            {
                p.Id,
                p.Body,
                p.CreatedAt,
                p.AuthorId,
                p.ReachBoost,
                ViewerHasBoosted = p.Boosts.Any(b => b.UserId == userId),
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
            p.ReachBoost,
            p.ViewerHasBoosted,
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
        await TouchUserAsync(userId, request.Lat, request.Lon, cancellationToken);
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
            CreatedAt = DateTimeOffset.UtcNow,
            ReachBoost = 0
        };

        _db.Set<Post>().Add(post);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _push.NotifyNewPostNearbyAsync(
                userId,
                post.Id,
                request.Lat,
                request.Lon,
                GeoConstants.FeedRadiusMeters,
                cancellationToken);
        }
        catch
        {
            // Push must never fail post creation.
        }

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
            post.ReachBoost,
            viewerHasBoosted: false,
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
        await TouchUserAsync(userId, request.Lat, request.Lon, cancellationToken);
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

        if (!IsWithinEffectiveRadius(post, viewer))
        {
            throw new AppException(ErrorCodes.OutOfRange, "Post is outside your visibility zone.", 403);
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

    public async Task<BoostResponse> BoostPostAsync(
        Guid userId,
        Guid postId,
        double lat,
        double lon,
        string? clientIp,
        CancellationToken cancellationToken = default)
    {
        await TouchUserAsync(userId, lat, lon, cancellationToken);
        await _location.ValidateViewerLocationAsync(lat, lon, clientIp, cancellationToken);

        var viewer = CreatePoint(lat, lon);
        var post = await _db.Set<Post>().FirstOrDefaultAsync(p => p.Id == postId && !p.IsHidden, cancellationToken);
        if (post is null)
        {
            throw new AppException(ErrorCodes.NotFound, "Post not found.", 404);
        }

        if (post.AuthorId == userId)
        {
            throw new AppException(ErrorCodes.OwnPost, "You cannot boost your own post.", 403);
        }

        if (!IsWithinEffectiveRadius(post, viewer))
        {
            throw new AppException(ErrorCodes.OutOfRange, "Post is outside your visibility zone.", 403);
        }

        var alreadyBoosted = await _db.Set<PostBoost>()
            .AnyAsync(b => b.PostId == postId && b.UserId == userId, cancellationToken);
        if (alreadyBoosted)
        {
            throw new AppException(ErrorCodes.AlreadyBoosted, "You already boosted this post.", 409);
        }

        _db.Set<PostBoost>().Add(new PostBoost
        {
            PostId = postId,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        post.ReachBoost += 1;
        var authorId = post.AuthorId;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new AppException(ErrorCodes.AlreadyBoosted, "You already boosted this post.", 409);
        }

        try
        {
            await _push.NotifyPostBoostedAsync(authorId, postId, cancellationToken);
        }
        catch
        {
            // Push must never fail boost.
        }

        return new BoostResponse(post.ReachBoost, ViewerHasBoosted: true);
    }

    private static bool IsWithinEffectiveRadius(Post post, Point viewer)
        => post.Location.IsWithinDistance(
            viewer,
            GeoConstants.FeedRadiusMeters + post.ReachBoost * GeoConstants.ReachBoostMetersPerLevel);

    private PostSummaryDto ToSummary(
        Guid id,
        string body,
        DateTimeOffset createdAt,
        Guid authorId,
        int reachBoost,
        bool viewerHasBoosted,
        int replyCount,
        Media? media,
        double lat,
        double lon)
        => new(
            id,
            body,
            createdAt,
            AuthorGlyph.FromUserId(authorId),
            reachBoost,
            viewerHasBoosted,
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

    private async Task TouchUserAsync(Guid userId, double lat, double lon, CancellationToken cancellationToken)
    {
        var user = await _db.Set<AnonymousUser>().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            throw new AppException(ErrorCodes.NotFound, "User not found.", 401);
        }

        var now = DateTimeOffset.UtcNow;
        user.LastSeenAt = now;
        user.LastLat = lat;
        user.LastLon = lon;
        user.LastLocationAt = now;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
