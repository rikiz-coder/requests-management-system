using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RequestsManagement.Infrastructure;
using RequestsManagement.Infrastructure.Persistence.Seeding;

// Usage:
//   seeder                 Applies migrations; generates the test data only if the database is empty.
//   seeder --reseed        Applies migrations; deletes all requests and generates them again.
//   seeder --count 250000  Number of requests to generate (default 100,000).
//
// Connection string: ConnectionStrings__RequestsDb (environment variable) or appsettings.json.

var options = SeederOptions.Parse(args);
if (options is null)
{
    Console.Error.WriteLine("Usage: seeder [--reseed] [--count <number>]");
    return 2;
}

// Only "--Key=Value" arguments are configuration overrides; the tool's own flags are not passed on.
// appsettings.json is read from the tool's own folder, wherever it is started from.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args.Where(a => a.Contains('=')).ToArray(),
    ContentRootPath = AppContext.BaseDirectory,
});
builder.Services.AddInfrastructure(builder.Configuration);
using var host = builder.Build();

using var scope = host.Services.CreateScope();
var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };

await seeder.MigrateAsync(cancellation.Token);

if (!options.Reseed && await seeder.HasDataAsync(cancellation.Token))
{
    logger.LogInformation("The database already contains requests; nothing to do. Use --reseed to recreate them.");
    return 0;
}

var result = await seeder.ReseedAsync(options.Count, cancellation.Token);
logger.LogInformation("Created {Requests:N0} requests and {History:N0} history rows in {Seconds:0.0}s",
    result.Requests, result.HistoryRows, result.Elapsed.TotalSeconds);
return 0;

internal sealed record SeederOptions(bool Reseed, int Count)
{
    public static SeederOptions? Parse(string[] args)
    {
        var reseed = false;
        var count = DatabaseSeeder.DefaultRequestCount;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--reseed":
                    reseed = true;
                    break;
                case "--count" when i + 1 < args.Length && int.TryParse(args[i + 1], out var n) && n > 0:
                    count = n;
                    i++;
                    break;
                default:
                    // Configuration overrides such as --ConnectionStrings:RequestsDb=... are read by the host.
                    if (args[i].StartsWith("--", StringComparison.Ordinal) && args[i].Contains('='))
                    {
                        break;
                    }

                    return null;
            }
        }

        return new SeederOptions(reseed, count);
    }
}
