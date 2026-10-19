using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RequestsManagement.Application.Requests.Abstractions;
using RequestsManagement.Infrastructure.Caching;
using RequestsManagement.Infrastructure.Persistence;
using RequestsManagement.Infrastructure.Persistence.Repositories;
using RequestsManagement.Infrastructure.Persistence.Seeding;

namespace RequestsManagement.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "RequestsDb";

    /// <summary>Registers the EF Core / SQL Server implementation of the Application abstractions.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is missing. Set ConnectionStrings__{ConnectionStringName}.");

        services.AddDbContext<RequestsDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(maxRetryCount: 5)));

        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<IRequestRepository, RequestRepository>();

        // The cache is shared by all requests, so it is a singleton (as is IMemoryCache itself).
        services.AddMemoryCache();
        services.AddSingleton<ISummaryCache, MemorySummaryCache>();

        return services;
    }
}
