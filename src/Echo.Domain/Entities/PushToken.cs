namespace Echo.Domain.Entities;

public class PushToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AnonymousUser User { get; set; } = null!;
    public required string Token { get; set; }
    public required string Platform { get; set; }
    /// <summary>Language code used for push copy (en, es, de, fr, it, pt).</summary>
    public string? Locale { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
