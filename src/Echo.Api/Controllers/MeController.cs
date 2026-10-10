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
public sealed class MeController(IPushTokenService pushTokens) : ControllerBase
{
    [HttpPut("me/push-token")]
    public Task<UpsertPushTokenResponse> UpsertPushToken(
        [FromBody] UpsertPushTokenRequest request,
        CancellationToken ct)
        => pushTokens.UpsertAsync(User.GetUserId(), request, ct);
}
