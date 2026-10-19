using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RequestsManagement.Infrastructure.Persistence;

/// <summary>
/// Used only by the dotnet-ef tool (creating migrations), so it never has to start the API.
/// The connection string is not used to generate migrations.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<RequestsDbContext>
{
    public RequestsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RequestsDbContext>()
            .UseSqlServer("Server=localhost;Database=RequestsManagement;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new RequestsDbContext(options);
    }
}
