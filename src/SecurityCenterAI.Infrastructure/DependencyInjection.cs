using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SecurityCenterAI.Infrastructure.Configuration;
using SecurityCenterAI.Infrastructure.Persistence;

namespace SecurityCenterAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services)
    {
        services
            .AddOptions<DatabaseOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                options.ConnectionString =
                    configuration.GetConnectionString(
                        DatabaseOptions.ConnectionStringName)
                    ?? string.Empty;
            })
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "ConnectionStrings:PostgreSQL es obligatorio.")
            .ValidateOnStart();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var databaseOptions = serviceProvider
                .GetRequiredService<IOptions<DatabaseOptions>>()
                .Value;
            options.UseNpgsql(databaseOptions.ConnectionString);
        });

        return services;
    }
}
