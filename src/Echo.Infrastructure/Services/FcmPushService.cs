using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Echo.Application.Abstractions;
using Echo.Application.Common;
using Echo.Application.Services;
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
            if (string.IsNullOrWhiteSpace(_projectId))
            {
                _projectId = null;
            }

            if (!string.IsNullOrWhiteSpace(serviceAccountPath) && File.Exists(serviceAccountPath))
            {
                _credential = GoogleCredential.FromFile(serviceAccountPath)
                    .CreateScoped("https://www.googleapis.com/auth/firebase.messaging");
                _projectId ??= ReadProjectIdFromFile(serviceAccountPath);
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
            }

            _enabled = _credential is not null && !string.IsNullOrWhiteSpace(_projectId);
            if (_enabled)
            {
                _logger.LogInformation("FCM push enabled for project {ProjectId}.", _projectId);
            }
            else
            {
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
        if (!EnsureEnabled("boost")) return;

        var tokens = await _db.Set<PushToken>()
            .AsNoTracking()
            .Where(t => t.UserId == authorUserId)
            .Select(t => new DeviceToken(t.Token, t.Locale))
            .ToListAsync(cancellationToken);

        await SendLocalizedAsync(
            tokens,
            PushCopy.ForBoost,
            new Dictionary<string, string>
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
        if (!EnsureEnabled("new_post")) return;

        var candidates = await _db.Set<PushToken>()
            .AsNoTracking()
            .Where(t => t.UserId != authorUserId
                        && t.User.LastLat != null
                        && t.User.LastLon != null)
            .Select(t => new
            {
                t.Token,
                t.Locale,
                Lat = t.User.LastLat!.Value,
                Lon = t.User.LastLon!.Value
            })
            .ToListAsync(cancellationToken);

        var tokens = candidates
            .Where(c => GeoMath.HaversineMeters(lat, lon, c.Lat, c.Lon) <= radiusMeters)
            .Select(c => new DeviceToken(c.Token, c.Locale))
            .Distinct()
            .ToList();

        await SendLocalizedAsync(
            tokens,
            PushCopy.ForNewPost,
            new Dictionary<string, string>
            {
                ["type"] = "new_post",
                ["postId"] = postId.ToString()
            },
            cancellationToken);
    }

    private bool EnsureEnabled(string kind)
    {
        if (_enabled) return true;
        _logger.LogWarning("Push {Kind} skipped: FCM is disabled.", kind);
        return false;
    }

    private async Task SendLocalizedAsync(
        IReadOnlyList<DeviceToken> tokens,
        Func<string, (string Title, string Body)> copyForLocale,
        Dictionary<string, string> data,
        CancellationToken cancellationToken)
    {
        if (tokens.Count == 0)
        {
            _logger.LogInformation("Push {Type} skipped: no target devices.", data.GetValueOrDefault("type"));
            return;
        }

        foreach (var group in tokens.GroupBy(t => PushCopy.Normalize(t.Locale)))
        {
            var copy = copyForLocale(group.Key);
            var batch = group.Select(t => t.Token).Distinct().ToList();
            _logger.LogInformation(
                "Push {Type} to {Count} device(s), locale {Locale}.",
                data.GetValueOrDefault("type"),
                batch.Count,
                group.Key);
            await SendToTokensAsync(batch, copy.Title, copy.Body, data, cancellationToken);
        }
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
                        android = new
                        {
                            priority = "HIGH",
                            notification = new
                            {
                                channelId = "echo_default",
                                notificationPriority = "PRIORITY_HIGH"
                            }
                        },
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
                if (IsStaleToken(response.StatusCode, responseBody))
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

    private static bool IsStaleToken(HttpStatusCode status, string body)
    {
        if (status == HttpStatusCode.NotFound
            || body.Contains("UNREGISTERED", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return body.Contains("INVALID_ARGUMENT", StringComparison.OrdinalIgnoreCase)
               && body.Contains("registration token", StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct DeviceToken(string Token, string? Locale);

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
