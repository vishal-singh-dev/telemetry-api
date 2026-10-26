using Api.Configuration;
using Api.Data;
using Api.Endpoints;
using Api.Health;
using Api.Observability;
using Api.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Configuration options binding
var appOptions = builder.Configuration.GetSection(ApplicationOptions.SectionName).Get<ApplicationOptions>() ?? new ApplicationOptions();
var dbOptions = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
var rateOptions = builder.Configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new RateLimitingOptions();
var requestLimitOptions = builder.Configuration.GetSection(RequestLimitsOptions.SectionName).Get<RequestLimitsOptions>() ?? new RequestLimitsOptions();
var testEndpointOptions = builder.Configuration.GetSection(TestEndpointsOptions.SectionName).Get<TestEndpointsOptions>() ?? new TestEndpointsOptions();
var obsOptions = builder.Configuration.GetSection(ObservabilityOptions.SectionName).Get<ObservabilityOptions>() ?? new ObservabilityOptions();
var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

// Validate configuration at startup
AppOptionsValidator.Validate(
    appOptions,
    dbOptions,
    rateOptions,
    requestLimitOptions,
    testEndpointOptions,
    obsOptions,
    builder.Environment.EnvironmentName);

// Register strongly typed options in DI
builder.Services.Configure<ApplicationOptions>(builder.Configuration.GetSection(ApplicationOptions.SectionName));
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<RateLimitingOptions>(builder.Configuration.GetSection(RateLimitingOptions.SectionName));
builder.Services.Configure<RequestLimitsOptions>(builder.Configuration.GetSection(RequestLimitsOptions.SectionName));
builder.Services.Configure<TestEndpointsOptions>(builder.Configuration.GetSection(TestEndpointsOptions.SectionName));
builder.Services.Configure<ObservabilityOptions>(builder.Configuration.GetSection(ObservabilityOptions.SectionName));
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));

// Configure Database
var connectionString = builder.Configuration.GetConnectionString("Postgres");
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("Host=;", StringComparison.OrdinalIgnoreCase))
    {
        // Fallback for tests or unconfigured local environments if not already registered
        options.UseInMemoryDatabase("FallbackDb");
        options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
    }
    else
    {
        var npgsqlBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            MinPoolSize = dbOptions.PoolMinSize,
            MaxPoolSize = dbOptions.PoolMaxSize,
            Timeout = dbOptions.CommandTimeoutSeconds
        };

        options.UseNpgsql(npgsqlBuilder.ConnectionString, npgsqlOptions =>
        {
            npgsqlOptions.CommandTimeout(dbOptions.CommandTimeoutSeconds);
            if (dbOptions.MaxRetryCount > 0)
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: dbOptions.MaxRetryCount,
                    maxRetryDelay: TimeSpan.FromSeconds(dbOptions.MaxRetryDelaySeconds),
                    errorCodesToAdd: null);
            }
        });

        if (dbOptions.EnableSensitiveDataLogging)
        {
            options.EnableSensitiveDataLogging();
        }
    }
});

// Configure Rate Limiting
builder.Services.AddConfiguredRateLimiting();

// Configure OpenTelemetry Tracing and Metrics
builder.Services.AddConfiguredOpenTelemetry(obsOptions);

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
if (appOptions.EnableSwagger)
{
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = appOptions.Name, Version = "v1" });
    });
}

// CORS
if (corsOptions.AllowedOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(corsOptions.AllowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });
}

var app = builder.Build();

// Apply migrations on startup if configured
if (dbOptions.ApplyMigrationsOnStartup && !string.IsNullOrWhiteSpace(connectionString) && !connectionString.Contains("FallbackDb"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        app.Logger.LogInformation("Applying database migrations on startup...");
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db);
        app.Logger.LogInformation("Database migrations applied and seed data initialized successfully.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Failed to apply database migrations on startup.");
        throw;
    }
}

// Swagger middleware
if (appOptions.EnableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{appOptions.Name} v1");
    });
}

// CORS middleware
if (corsOptions.AllowedOrigins.Length > 0)
{
    app.UseCors();
}

// Observability middleware for in-flight requests and body limits
app.UseMiddleware<ObservabilityMiddleware>();

// Rate limiting middleware
app.UseRateLimiter();

// Map Endpoints
app.MapHealthEndpoints();
app.MapRestrictedPrometheusScrapingEndpoint(obsOptions);
app.MapProductEndpoints();
app.MapOrderEndpoints();
app.MapTestEndpoints(app.Environment);

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
