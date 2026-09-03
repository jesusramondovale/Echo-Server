using Echo.Api;
using Echo.Application.Common;
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
public sealed class MediaController(IMediaService media) : ControllerBase
{
    [HttpPost("media")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<MediaDto> Upload(
        IFormFile file,
        [FromForm] int? width,
        [FromForm] int? height,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            throw new AppException(ErrorCodes.InvalidMedia, "File is required.", 400);
        }

        await using var stream = file.OpenReadStream();
        return await media.UploadAsync(
            User.GetUserId(),
            stream,
            file.ContentType,
            file.FileName,
            width,
            height,
            ct);
    }

    [HttpPut("me/avatar")]
    public Task<AvatarResponse> SetAvatar([FromBody] SetAvatarRequest request, CancellationToken ct)
        => media.SetAvatarAsync(User.GetUserId(), request.MediaId, ct);
}

public record SetAvatarRequest(Guid MediaId);
