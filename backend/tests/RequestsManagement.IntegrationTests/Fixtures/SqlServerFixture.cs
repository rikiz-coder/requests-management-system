using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RequestsManagement.Infrastructure.Persistence;
using RequestsManagement.Infrastructure.Persistence.Seeding;
using Testcontainers.MsSql;

namespace RequestsManagement.IntegrationTests.Fixtures;

/// <summary>
/// A real SQL Server 2022 in a container (Testcontainers), shared by the tests of one collection.
/// A real server is required: the EF Core in-memory provider does not enforce rowversion,
/// so a concurrency test against it would pass even if the mechanism were broken.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    private readonly MsSqlContainer _container = new MsSqlBuilder().WithImage(Image).Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "RequestsManagementTests",
        }.ConnectionString;
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    public RequestsDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<RequestsDbContext>().UseSqlServer(ConnectionString).Options);

    public DatabaseSeeder CreateSeeder(RequestsDbContext dbContext) =>
        new(dbContext, NullLogger<DatabaseSeeder>.Instance);
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SQL Server";
}
