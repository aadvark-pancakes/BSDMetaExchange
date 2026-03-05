using Core.Contracts;
using Core.Entities;
using Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

//Set up logging
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Information);
});

ILogger<MetaExchangeEngine> engineLogger = loggerFactory.CreateLogger<MetaExchangeEngine>();
ILogger<ExchangeDataLoader> dataLoaderLogger = loggerFactory.CreateLogger<ExchangeDataLoader>();
ILogger<InMemoryExchangeRepository> repositoryLogger = loggerFactory.CreateLogger<InMemoryExchangeRepository>();

Console.WriteLine($"Current directory: {Environment.CurrentDirectory}");

// Load configuration
var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

var dataPath = config["ExchangeSettings:ExchangeDataPath"];

if (string.IsNullOrWhiteSpace(dataPath))
    throw new Exception("DataPath not configured.");

// Load exchanges
var exchangeDataLoader = new ExchangeDataLoader(dataLoaderLogger);
var exchanges = exchangeDataLoader.LoadExchanges(dataPath);

if (exchanges.Count == 0)
    throw new Exception("No exchanges loaded.");

// Create services
var repository = new InMemoryExchangeRepository(exchanges, repositoryLogger);

var engine = new MetaExchangeEngine(repository, engineLogger);

// Interactive loop
RunProgram(engine);

void RunProgram(IMetaExchangeEngine engine)
{
    Console.WriteLine("Meta Exchange Console");
    Console.WriteLine("---------------------");
    Console.WriteLine("Enter orders or type EXIT to quit.");
    Console.WriteLine();

    while (true)
    {
        try
        {
            var order = ReadOrder();

            if (order == null)
                break;

            var plan = engine.Execute(order);

            PrintPlan(plan);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

        Console.WriteLine();
    }
}

MetaOrder? ReadOrder()
{
    Console.WriteLine("Enter Order Type (Buy/Sell) or EXIT:");

    var typeInput = Console.ReadLine();

    if (string.Equals(typeInput, "EXIT",
        StringComparison.OrdinalIgnoreCase))
        return null;

    if (!Enum.TryParse<OrderType>(
        typeInput,
        true,
        out var type))
    {
        throw new Exception("Invalid order type. Must be either Buy or Sell.");
    }

    Console.WriteLine("Enter BTC Amount:");

    var amountInput = Console.ReadLine();

    if (!decimal.TryParse(amountInput, out var amount))
        throw new Exception("Invalid amount.");

    if (amount <= 0)
        throw new Exception("Amount must be greater than zero.");

    return new MetaOrder
    {
        Type = type,
        Amount = amount
    };
}

void PrintPlan(BestExecutionPlan plan)
{
    Console.WriteLine();
    Console.WriteLine("Execution Plan");
    Console.WriteLine("--------------");

    Console.WriteLine($"Requested: {plan.Amount} BTC");
    Console.WriteLine($"Executed:  {plan.TotalExecuted} BTC");

    Console.WriteLine();

    foreach (var execution in plan.Executions)
    {
        Console.WriteLine(
            $"{execution.ExchangeId} | " +
            $"{execution.Amount} BTC @ " +
            $"{execution.Price} EUR");
    }

    Console.WriteLine();
}
