using Echo.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Echo.Infrastructure.Services;

public sealed class LocalMediaStorage : IMediaStorage
{
    private readonly string _root;
    private readonly string _publicBaseUrl;

    public LocalMediaStorage(IHostEnvironment env, IConfiguration configuration)
    {
        var configured = configuration["Media:RootPath"];
        _root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(env.ContentRootPath, "App_Data", "media")
            : configured;
        Directory.CreateDirectory(_root);
        _publicBaseUrl = configuration["Media:PublicBaseUrl"]?.TrimEnd('/') ?? "/media";
    }

    public async Task<(string relativePath, long sizeBytes)> SaveAsync(
        Stream content,
        string contentType,
        string extension,
        CancellationToken cancellationToken = default)
    {
        var day = DateTime.UtcNow.ToString("yyyy/MM/dd");
        var dir = Path.Combine(_root, day.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(dir);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(dir, fileName);
        await using var fs = File.Create(fullPath);
        await content.CopyToAsync(fs, cancellationToken);
        var size = fs.Length;
        var relative = $"{day}/{fileName}".Replace('\\', '/');
        return (relative, size);
    }

    public string GetPublicUrl(string relativePath) => $"{_publicBaseUrl}/{relativePath.TrimStart('/')}";

    public string RootPath => _root;
}
