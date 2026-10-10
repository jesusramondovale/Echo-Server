namespace Echo.Domain.Entities;

public class AnonymousUser
{
    public Guid Id { get; set; }
    public required string DeviceKeyHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public double? LastLat { get; set; }
    public double? LastLon { get; set; }
    public DateTimeOffset? LastLocationAt { get; set; }
    public Guid? AvatarMediaId { get; set; }
    public Media? AvatarMedia { get; set; }

    public ICollection<Post> Posts { get; set; } = new List<Post>();
    public ICollection<Reply> Replies { get; set; } = new List<Reply>();
    public ICollection<PostBoost> Boosts { get; set; } = new List<PostBoost>();
    public ICollection<PushToken> PushTokens { get; set; } = new List<PushToken>();
}
