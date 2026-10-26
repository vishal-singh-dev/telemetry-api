using Api.Configuration;
using Api.Data;
using Api.Data.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    public CustomWebApplicationFactory()
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove existing DbContextOptions
            services.RemoveAll<DbContextOptions<AppDbContext>>();

            // Add InMemory AppDbContext with unique name per test factory
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
                options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            });

            // Ensure rate limiting is relaxed for general health/product/order tests
            services.Configure<RateLimitingOptions>(opts =>
            {
                opts.Enabled = true;
                opts.Policy = "FixedWindow";
                opts.PermitLimit = 1000;
                opts.WindowSeconds = 60;
                opts.QueueLimit = 0;
            });

            // Build service provider and seed database
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();

            if (!db.Products.Any())
            {
                var now = DateTime.UtcNow;
                db.Products.AddRange(
                    new Product { Id = 1, Name = "Widget Alpha", Description = "A fine test widget", Price = 19.99m, StockQuantity = 100, CreatedAtUtc = now, UpdatedAtUtc = now },
                    new Product { Id = 2, Name = "Widget Beta", Description = "Another test widget", Price = 49.99m, StockQuantity = 5, CreatedAtUtc = now, UpdatedAtUtc = now },
                    new Product { Id = 3, Name = "Widget Gamma", Description = "Out of stock widget", Price = 99.99m, StockQuantity = 0, CreatedAtUtc = now, UpdatedAtUtc = now }
                );
                db.SaveChanges();
            }
        });
    }
}
