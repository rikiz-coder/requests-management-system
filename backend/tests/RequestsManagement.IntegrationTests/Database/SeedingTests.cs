using Microsoft.EntityFrameworkCore;
using RequestsManagement.Application.Requests.Entities;
using RequestsManagement.Infrastructure.Persistence;
using RequestsManagement.IntegrationTests.Fixtures;

namespace RequestsManagement.IntegrationTests.Database;

/// <summary>Migrations and the seeder against a real SQL Server.</summary>
[Collection(SqlServerCollection.Name)]
public class SeedingTests(SqlServerFixture sql)
{
    private const int Count = 3_000;

    [Fact]
    public async Task Migrations_create_the_schema_and_the_planned_indexes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = sql.CreateDbContext();
        await sql.CreateSeeder(db).MigrateAsync(ct);

        var indexes = await db.Database
            .SqlQuery<string>($"SELECT name AS [Value] FROM sys.indexes WHERE object_id IN (OBJECT_ID('dbo.Requests'), OBJECT_ID('dbo.RequestStatusHistory')) AND name LIKE 'IX[_]%'")
            .ToListAsync(ct);

        Assert.Equal(
            [
                "IX_Requests_AssignedTo_Status",
                "IX_Requests_CreatedAt",
                "IX_Requests_OrganizationName",
                "IX_Requests_Priority_Status",
                "IX_Requests_Status_CreatedAt",
                "IX_RequestStatusHistory_RequestId_ChangedAt",
            ],
            indexes.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Reseed_loads_requests_with_history_and_is_repeatable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = sql.CreateDbContext();
        var seeder = sql.CreateSeeder(db);
        await seeder.MigrateAsync(ct);

        var first = await seeder.ReseedAsync(Count, ct);
        var firstSnapshot = await SnapshotAsync(db, ct);

        var second = await seeder.ReseedAsync(Count, ct);
        var secondSnapshot = await SnapshotAsync(db, ct);

        Assert.Equal(Count, first.Requests);
        Assert.Equal(Count, await db.Requests.CountAsync(ct));
        Assert.Equal(first.HistoryRows, await db.RequestStatusHistory.CountAsync(ct));
        Assert.Equal(first, second with { Elapsed = first.Elapsed });
        Assert.Equal(firstSnapshot, secondSnapshot);
        Assert.True(await seeder.HasDataAsync(ct));
    }

    [Fact]
    public async Task After_reseed_new_requests_continue_the_identity_and_get_a_rowversion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = sql.CreateDbContext();
        var seeder = sql.CreateSeeder(db);
        await seeder.MigrateAsync(ct);
        await seeder.ReseedAsync(Count, ct);

        var request = new ServiceRequest
        {
            Title = "בדיקה",
            OrganizationName = "ארגון בדיקה",
            Status = RequestStatus.New,
            Priority = RequestPriority.Low,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Requests.Add(request);
        await db.SaveChangesAsync(ct);

        Assert.Equal(Count + 1, request.Id);
        Assert.Equal(8, request.RowVersion.Length);
    }

    private static async Task<string> SnapshotAsync(RequestsDbContext db, CancellationToken ct)
    {
        var requests = await db.Requests.AsNoTracking().OrderBy(r => r.Id)
            .Select(r => new { r.Id, r.Title, r.Status, r.AssignedTo, r.UpdatedAt })
            .ToListAsync(ct);
        var history = await db.RequestStatusHistory.AsNoTracking().OrderBy(h => h.Id)
            .Select(h => new { h.Id, h.RequestId, h.NewStatus, h.ChangedAt })
            .ToListAsync(ct);
        return string.Join('\n', requests.Select(r => r.ToString()).Concat(history.Select(h => h.ToString())));
    }
}
