using NetTopologySuite.Geometries;

namespace Echo.Domain.Entities;

public class Post
{
    public Guid Id { get; set; }
    public Guid AuthorId { get; set; }
    public AnonymousUser Author { get; set; } = null!;
    public required string Body { get; set; }
    public Guid? MediaId { get; set; }
    public Media? Media { get; set; }
    public required Point Location { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsHidden { get; set; }
    /// <summary>Extra km of visibility beyond the base 15 km radius.</summary>
    public int ReachBoost { get; set; }

    public ICollection<Reply> Replies { get; set; } = new List<Reply>();
    public ICollection<PostBoost> Boosts { get; set; } = new List<PostBoost>();
}
