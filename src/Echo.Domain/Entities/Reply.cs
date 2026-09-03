namespace Echo.Domain.Entities;

public class Reply
{
    public Guid Id { get; set; }
    public Guid PostId { get; set; }
    public Post Post { get; set; } = null!;
    public Guid AuthorId { get; set; }
    public AnonymousUser Author { get; set; } = null!;
    public required string Body { get; set; }
    public Guid? MediaId { get; set; }
    public Media? Media { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
