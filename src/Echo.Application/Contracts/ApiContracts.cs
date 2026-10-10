namespace Echo.Application.Contracts;

public record BootstrapRequest(string DeviceKey);

public record BootstrapResponse(string AccessToken, DateTimeOffset ExpiresAt, Guid UserId);

public record CreatePostRequest(string Body, double Lat, double Lon, Guid? MediaId);

public record CreateReplyRequest(string Body, double Lat, double Lon, Guid? MediaId);

public record MediaDto(Guid Id, string Url, string ContentType, int? Width, int? Height);

public record PostSummaryDto(
    Guid Id,
    string Body,
    DateTimeOffset CreatedAt,
    string AuthorGlyph,
    int ReachBoost,
    bool ViewerHasBoosted,
    int ReplyCount,
    MediaDto? Media,
    double Lat,
    double Lon);

public record ReplyDto(
    Guid Id,
    string Body,
    DateTimeOffset CreatedAt,
    string AuthorGlyph,
    MediaDto? Media);

public record PostDetailDto(
    Guid Id,
    string Body,
    DateTimeOffset CreatedAt,
    string AuthorGlyph,
    int ReachBoost,
    bool ViewerHasBoosted,
    MediaDto? Media,
    IReadOnlyList<ReplyDto> Replies,
    double Lat,
    double Lon);

public record BoostResponse(int ReachBoost, bool ViewerHasBoosted);

public record FeedResponse(IReadOnlyList<PostSummaryDto> Items, string? NextCursor);

public record AvatarResponse(MediaDto Avatar);

public record UpsertPushTokenRequest(string Token, string Platform);

public record UpsertPushTokenResponse(string Token, string Platform);
