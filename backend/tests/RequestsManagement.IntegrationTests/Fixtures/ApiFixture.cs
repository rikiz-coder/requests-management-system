using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RequestsManagement.Application.Requests.Contracts;
using RequestsManagement.Application.Requests.Entities;
using RequestsManagement.Infrastructure.Persistence;
using RequestsManagement.Infrastructure.Persistence.Seeding;
using Testcontainers.MsSql;

namespace RequestsManagement.IntegrationTests.Fixtures;

/// <summary>
/// The real API (WebApplicationFactory) on a real SQL Server 2022 (Testcontainers), seeded once with
/// deterministic data. Tests read their expectations from the database at run time, so the order in
/// which they run does not matter.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const int SeedCount = 3_000;
    public const string User = "בודק אוטומטי";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly MsSqlContainer _container = new MsSqlBuilder(SqlServerFixture.Image).Build();
    private WebApplicationFactory<Program>? _factory;

    public WebApplicationFactory<Program> Factory => _factory ?? throw new InvalidOperationException("Not initialized.");

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        var connectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "RequestsApiTests",
        }.ConnectionString;

        await using (var db = new RequestsDbContext(
            new DbContextOptionsBuilder<RequestsDbContext>().UseSqlServer(connectionString).Options))
        {
            var seeder = new DatabaseSeeder(db, NullLogger<DatabaseSeeder>.Instance);
            await seeder.MigrateAsync(CancellationToken.None);
            await seeder.ReseedAsync(SeedCount, CancellationToken.None);
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:RequestsDb", connectionString);
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    public HttpClient CreateClient(string? user = User)
    {
        var client = Factory.CreateClient();
        if (user is not null)
        {
            // URL-encoded, as a browser must send it (header values are ASCII).
            client.DefaultRequestHeaders.Add("X-User-Name", Uri.EscapeDataString(user));
        }

        return client;
    }

    /// <summary>Runs a query directly against the database, outside the API.</summary>
    public async Task<T> QueryDbAsync<T>(Func<RequestsDbContext, Task<T>> query)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RequestsDbContext>();
        return await query(db);
    }

    /// <summary>Id of some request currently in <paramref name="status"/>, skipping the given ids.</summary>
    public Task<int> FindRequestAsync(RequestStatus status, params int[] except) =>
        QueryDbAsync(db => db.Requests.AsNoTracking()
            .Where(r => r.Status == status && !except.Contains(r.Id))
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .FirstAsync());

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "API";
}
