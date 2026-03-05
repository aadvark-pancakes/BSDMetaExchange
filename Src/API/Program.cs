using Core.Contracts;
using Core.Entities;
using Core.Services;
using API.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Exchange data loader
builder.Services.AddSingleton<IExchangeDataLoader, ExchangeDataLoader>();

// Load exchange data at startup and pass into repository
builder.Services.AddSingleton<IExchangeRepository>(sp =>
{
    var loader = sp.GetRequiredService<IExchangeDataLoader>();
    var logger = sp.GetRequiredService<ILogger<InMemoryExchangeRepository>>();

    var config = sp.GetRequiredService<IConfiguration>();
    var env = sp.GetRequiredService<IHostEnvironment>();

    // Prefer configuration value directly to avoid ordering issues during service registration
    var configuredPath = config.GetValue<string>("ExchangeSettings:ExchangeDataPath");

    var absolutePath = Path.Combine(env.ContentRootPath, configuredPath);

    List<Exchange> exchanges;
    try
    {
        exchanges = loader.LoadExchanges(absolutePath) ?? new List<Exchange>();
        logger.LogInformation("Loaded {Count} exchanges from path: {Path}", exchanges.Count, absolutePath);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to load exchanges from path: {Path}. Using empty repository.", absolutePath);
        exchanges = new List<Exchange>();
    }

    return new InMemoryExchangeRepository(exchanges, logger);
});


builder.Services.AddSingleton<IMetaExchangeEngine, MetaExchangeEngine>();

// Controllers
builder.Services.AddControllers();

var app = builder.Build();

// Global exception handling middleware
app.UseExceptionHandling();

// API endpoints
app.MapGet("/api/exchanges", (IExchangeRepository repo, int skip = 0, int take = 10) =>
{
    if (skip < 0)
        return Results.BadRequest("Query parameter 'skip' must be greater than or equal to 0.");

    if (take <= 0)
        return Results.BadRequest("Query parameter 'take' must be greater than 0.");

    var exchanges = repo.GetAllExchanges().AsQueryable();

    return Results.Ok(exchanges.Skip(skip).Take(take).ToList());
});

app.MapGet("/api/exchanges/{exchangeId}", (IExchangeRepository repo, string exchangeId) =>
{
    if(string.IsNullOrEmpty(exchangeId))
        return Results.BadRequest("Exchange ID must be provided.");

    var exchange = repo.GetExchangeById(exchangeId);
    return exchange is null ? Results.NotFound() : Results.Ok(exchange);
});

app.MapPost("/api/metaorders", (IMetaExchangeEngine engine, MetaOrder order) =>
{
    if (order is null)
        return Results.BadRequest("Order must no be null.");

    if(order.Amount <= 0)
        return Results.BadRequest("Order amount must be greater than zero.");

    var plan = engine.Execute(order);
    return Results.Ok(plan);
});

app.MapControllers();

app.Run();