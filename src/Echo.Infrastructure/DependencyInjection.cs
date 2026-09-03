using Echo.Application;
using Echo.Application.Abstractions;
using Echo.Application.Services;
using Echo.Infrastructure.Persistence;
using Echo.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Echo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
                               ?? throw new InvalidOperationException("Connection string 'Default' is required.");

        services.AddDbContext<EchoDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.MigrationsAssembly(typeof(EchoDbContext).Assembly.FullName);
            }));

        services.AddScoped<DbContext>(sp => sp.GetRequiredService<EchoDbContext>());
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IRateLimiter, InMemoryRateLimiter>();
        services.AddSingleton<IMediaStorage, LocalMediaStorage>();
        services.AddHttpClient<IGeoIpService, HttpGeoIpService>();

        var skipIp = configuration.GetValue("Geo:SkipIpCheck", false);
        services.AddScoped<ILocationValidator>(sp =>
            new LocationValidator(sp.GetRequiredService<IGeoIpService>(), skipIp));

        services.AddApplication();
        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EchoDbContext>();
        await db.Database.MigrateAsync();
    }
}
