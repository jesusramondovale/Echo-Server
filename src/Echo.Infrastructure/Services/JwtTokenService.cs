using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Echo.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Echo.Infrastructure.Services;

public sealed class JwtTokenService : ITokenService
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly SymmetricSecurityKey _key;
    private readonly TimeSpan _lifetime;

    public JwtTokenService(IConfiguration configuration)
    {
        _issuer = configuration["Jwt:Issuer"] ?? "echo";
        _audience = configuration["Jwt:Audience"] ?? "echo-client";
        var secret = configuration["Jwt:Secret"]
                     ?? throw new InvalidOperationException("Jwt:Secret is required.");
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var days = int.TryParse(configuration["Jwt:LifetimeDays"], out var d) ? d : 365;
        _lifetime = TimeSpan.FromDays(days);
    }

    public (string AccessToken, DateTimeOffset ExpiresAt) CreateToken(Guid userId)
    {
        var expires = DateTimeOffset.UtcNow.Add(_lifetime);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var credentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
