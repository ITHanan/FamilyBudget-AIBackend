using Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Api.Tests;

public sealed class FamilyBudgetApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var databaseName = $"FamilyBudgetApiTests-{Guid.NewGuid()}";

        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Testing",
                ["Frontend:Url"] = "http://localhost",
                ["Testing:UseExternalDbContext"] = "true"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ILoggerProvider>();
            services.AddLogging(logging => logging.ClearProviders());
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDatabaseProvider>();
            foreach (var descriptor in services
                .Where(x =>
                    (x.ServiceType.FullName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (x.ImplementationType?.FullName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList())
            {
                services.Remove(descriptor);
            }
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));
        });
    }
}
