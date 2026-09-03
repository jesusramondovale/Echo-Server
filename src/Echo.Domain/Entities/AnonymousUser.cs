namespace Echo.Domain.Entities;

public class AnonymousUser
{
    public Guid Id { get; set; }
    public required string DeviceKeyHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public Guid? AvatarMediaId { get; set; }
    public Media? AvatarMedia { get; set; }

    public ICollection<Post> Posts { get; set; } = new List<Post>();
    public ICollection<Reply> Replies { get; set; } = new List<Reply>();
}
