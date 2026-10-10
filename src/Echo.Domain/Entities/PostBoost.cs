namespace Echo.Domain.Entities;

public class PostBoost
{
    public Guid PostId { get; set; }
    public Post Post { get; set; } = null!;
    public Guid UserId { get; set; }
    public AnonymousUser User { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}
