using Echo.Application.Abstractions;
using Echo.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Echo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IFeedService, FeedService>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IPushTokenService, PushTokenService>();
        return services;
    }
}
