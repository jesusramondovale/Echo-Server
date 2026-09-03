namespace Echo.Domain.Entities;

public class Media
{
    public Guid Id { get; set; }
    public required string Path { get; set; }
    public required string ContentType { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public long SizeBytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
