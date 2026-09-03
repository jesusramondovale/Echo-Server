using Echo.Application.Contracts;
using Echo.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Echo.Api.Controllers;

[ApiController]
[Route("api/session")]
public sealed class SessionController(ISessionService sessions) : ControllerBase
{
    [HttpPost("bootstrap")]
    [AllowAnonymous]
    [EnableRateLimiting("bootstrap")]
    public Task<BootstrapResponse> Bootstrap([FromBody] BootstrapRequest request, CancellationToken ct)
        => sessions.BootstrapAsync(request.DeviceKey, ct);
}
