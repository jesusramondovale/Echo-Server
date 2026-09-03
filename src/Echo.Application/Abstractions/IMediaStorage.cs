namespace Echo.Application.Abstractions;

public interface IMediaStorage
{
    Task<(string relativePath, long sizeBytes)> SaveAsync(
        Stream content,
        string contentType,
        string extension,
        CancellationToken cancellationToken = default);

    string GetPublicUrl(string relativePath);
}
