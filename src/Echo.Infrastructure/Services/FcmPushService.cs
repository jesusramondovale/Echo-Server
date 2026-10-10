using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Echo.Application.Abstractions;
using Echo.Application.Common;
using Echo.Domain.Entities;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Echo.Infrastructure.Services;

public sealed class FcmPushService : IPushNotificationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly DbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FcmPushService> _logger;
    private readonly string? _projectId;
    private readonly GoogleCredential? _credential;
    private readonly bool _enabled;

    public FcmPushService(
        DbContext db,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<FcmPushService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        _projectId = configuration["Firebase:ProjectId"];
        var clientEmail = configuration["Firebase:ClientEmail"];
        var privateKey = configuration["Firebase:PrivateKey"];
        var serviceAccountPath = configuration["Firebase:ServiceAccountPath"];

        try
        {
            if (!string.IsNullOrWhiteSpace(serviceAccountPath) && File.Exists(serviceAccountPath))
            {
                _credential = GoogleCredential.FromFile(serviceAccountPath)
                    .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
                _projectId ??= ReadProjectIdFromFile(serviceAccountPath);
                _enabled = !string.IsNullOrWhiteSpace(_projectId);
            }
            else if (!string.IsNullOrWhiteSpace(_projectId)
                     && !string.IsNullOrWhiteSpace(clientEmail)
                     && !string.IsNullOrWhiteSpace(privateKey))
            {
                var normalizedKey = privateKey.Replace("\\n", "\n", StringComparison.Ordinal);
                var saJson = JsonSerializer.Serialize(new Dictionary<string, string>
                {
                    ["type"] = "service_account",
                    ["project_id"] = _projectId,
                    ["private_key_id"] = "configured",
                    ["private_key"] = normalizedKey,
                    ["client_email"] = clientEmail,
                    ["client_id"] = "",
                    ["auth_uri"] = "https://accounts.google.com/o/oauth2/auth",
                    ["token_uri"] = "https://oauth2.googleapis.com/token",
                    ["auth_provider_x509_cert_url"] = "https://www.googleapis.com/oauth2/v1/certs",
                    ["client_x509_cert_url"] = ""
                });
                _credential = GoogleCredential.FromJson(saJson)
                    .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
                _enabled = true;
            }
            else
            {
                _enabled = false;
                _logger.LogWarning(
                    "Firebase credentials missing (Firebase:ProjectId/ClientEmail/PrivateKey or ServiceAccountPath). Push notifications disabled.");
            }
        }
        catch (Exception ex)
        {
            _enabled = false;
            _logger.LogWarning(ex, "Failed to initialize Firebase credentials. Push notifications disabled.");
        }
    }

    public async Task NotifyPostBoostedAsync(
        Guid authorUserId,
        Guid postId,
        CancellationToken cancellationToken = default)
    {
        if (!_enabled) return;

        var tokens = await _db.Set<PushToken>()
            .AsNoTracking()
            .Where(t => t.UserId == authorUserId)
            .Select(t => t.Token)
            .Distinct()
            .ToListAsync(cancellationToken);

        await SendToTokensAsync(
            tokens,
            title: "Your echo got a boost",
            body: "Someone extended your post's reach by 1 km.",
            data: new Dictionary<string, string>
            {
                ["type"] = "boost",
                ["postId"] = postId.ToString()
            },
            cancellationToken);
    }

    public async Task NotifyNewPostNearbyAsync(
        Guid authorUserId,
        Guid postId,
        double lat,
        double lon,
        double radiusMeters,
        CancellationToken cancellationToken = default)
    {
        if (!_enabled) return;

        var candidates = await _db.Set<PushToken>()
            .AsNoTracking()
            .Where(t => t.UserId != authorUserId
                        && t.User.LastLat != null
                        && t.User.LastLon != null)
            .Select(t => new
            {
                t.Token,
                Lat = t.User.LastLat!.Value,
                Lon = t.User.LastLon!.Value
            })
            .ToListAsync(cancellationToken);

        var tokens = candidates
            .Where(c => GeoMath.HaversineMeters(lat, lon, c.Lat, c.Lon) <= radiusMeters)
            .Select(c => c.Token)
            .Distinct()
            .ToList();

        await SendToTokensAsync(
            tokens,
            title: "New echo nearby",
            body: "Someone posted within your area.",
            data: new Dictionary<string, string>
            {
                ["type"] = "new_post",
                ["postId"] = postId.ToString()
            },
            cancellationToken);
    }

    private async Task SendToTokensAsync(
        IReadOnlyList<string> tokens,
        string title,
        string body,
        Dictionary<string, string> data,
        CancellationToken cancellationToken)
    {
        if (tokens.Count == 0 || _credential is null || string.IsNullOrWhiteSpace(_projectId))
        {
            return;
        }

        string accessToken;
        try
        {
            accessToken = await _credential.UnderlyingCredential.GetAccessTokenForRequestAsync(
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to obtain FCM access token.");
            return;
        }

        var client = _httpClientFactory.CreateClient("fcm");
        var url = $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send";
        var staleTokens = new List<string>();

        foreach (var token in tokens)
        {
            try
            {
                var payload = new
                {
                    message = new
                    {
                        token,
                        notification = new { title, body },
                        data,
                        android = new { priority = "high" },
                        apns = new
                        {
                            headers = new Dictionary<string, string> { ["apns-priority"] = "10" },
                            payload = new { aps = new { sound = "default" } }
                        }
                    }
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Content = new StringContent(
                    JsonSerializer.Serialize(payload, JsonOptions),
                    Encoding.UTF8,
                    "application/json");

                using var response = await client.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    continue;
                }

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                if (response.StatusCode is System.Net.HttpStatusCode.NotFound
                    || responseBody.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase)
                    || responseBody.Contains("INVALID_ARGUMENT", StringComparison.OrdinalIgnoreCase))
                {
                    staleTokens.Add(token);
                }

                _logger.LogWarning(
                    "FCM send failed for token ending …{Suffix}: {Status} {Body}",
                    token.Length > 8 ? token[^8..] : token,
                    (int)response.StatusCode,
                    responseBody);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "FCM send error.");
            }
        }

        if (staleTokens.Count > 0)
        {
            await _db.Set<PushToken>()
                .Where(t => staleTokens.Contains(t.Token))
                .ExecuteDeleteAsync(cancellationToken);
        }
    }

    private static string? ReadProjectIdFromFile(string path)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.TryGetProperty("project_id", out var id) ? id.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
