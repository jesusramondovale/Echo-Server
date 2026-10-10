using Echo.Api;
using Echo.Application.Contracts;
using Echo.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Echo.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
[EnableRateLimiting("api")]
public sealed class FeedController(IFeedService feed) : ControllerBase
{
    [HttpGet("feed")]
    public Task<FeedResponse> GetFeed(
        [FromQuery] double lat,
        [FromQuery] double lon,
        [FromQuery] string? cursor,
        CancellationToken ct)
        => feed.GetFeedAsync(User.GetUserId(), lat, lon, cursor, HttpContext.GetClientIp(), ct);

    [HttpGet("map")]
    public Task<FeedResponse> GetMap(
        [FromQuery] double lat,
        [FromQuery] double lon,
        CancellationToken ct)
        => feed.GetMapAsync(User.GetUserId(), lat, lon, HttpContext.GetClientIp(), ct);

    [HttpGet("posts/{id:guid}")]
    public Task<PostDetailDto> GetPost(
        Guid id,
        [FromQuery] double lat,
        [FromQuery] double lon,
        CancellationToken ct)
        => feed.GetPostAsync(User.GetUserId(), id, lat, lon, HttpContext.GetClientIp(), ct);

    [HttpPost("posts")]
    public Task<PostSummaryDto> CreatePost([FromBody] CreatePostRequest request, CancellationToken ct)
        => feed.CreatePostAsync(User.GetUserId(), request, HttpContext.GetClientIp(), ct);

    [HttpPost("posts/{id:guid}/replies")]
    public Task<ReplyDto> CreateReply(Guid id, [FromBody] CreateReplyRequest request, CancellationToken ct)
        => feed.CreateReplyAsync(User.GetUserId(), id, request, HttpContext.GetClientIp(), ct);

    [HttpPost("posts/{id:guid}/boost")]
    public Task<BoostResponse> BoostPost(
        Guid id,
        [FromQuery] double lat,
        [FromQuery] double lon,
        CancellationToken ct)
        => feed.BoostPostAsync(User.GetUserId(), id, lat, lon, HttpContext.GetClientIp(), ct);
}
