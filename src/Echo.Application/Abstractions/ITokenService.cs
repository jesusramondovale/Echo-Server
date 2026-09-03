namespace Echo.Application.Abstractions;

public interface ITokenService
{
    (string AccessToken, DateTimeOffset ExpiresAt) CreateToken(Guid userId);
}
