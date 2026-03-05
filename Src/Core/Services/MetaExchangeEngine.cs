using Core.Contracts;
using Core.Entities;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace Core.Services
{
    public class MetaExchangeEngine : IMetaExchangeEngine
    {
        private readonly IExchangeRepository _repository;
        private readonly ILogger<MetaExchangeEngine> _logger;

        private const decimal precision = 0.00000001m;

        public MetaExchangeEngine(IExchangeRepository repository, ILogger<MetaExchangeEngine> logger)
        {
            _logger = logger;
            _repository = repository;
        }

        public BestExecutionPlan Execute(MetaOrder order)
        {
            validateOrder(order);

            _logger.LogInformation("Starting meta order execution: {OrderType} Amount={Amount}", order.Type, order.Amount);

            var exchanges = _repository.GetAllExchanges();

            // Create temporary balances so we can track spending / selling without modifying domain objects
            var euroBalances = exchanges.ToDictionary(e => e.ExchangeId, e => e.AvailableFunds.Euro);
            var cryptoBalances = exchanges.ToDictionary(e => e.ExchangeId, e => e.AvailableFunds.Crypto);

            // execution plan that will be returned
            var plan = new BestExecutionPlan
            {
                OrderType = order.Type,
                Amount = order.Amount,
                Executions = new List<ExchangeExecution>()
            };

            // Process the order (Buy or Sell) 
            ProcessOrder(order, exchanges, euroBalances, cryptoBalances, plan);

            return plan;
        }

        private void ProcessOrder(MetaOrder order, IEnumerable<Exchange> exchanges, Dictionary<string, decimal> euroBalances, Dictionary<string, decimal> cryptoBalances, BestExecutionPlan plan)
        {
            // Track how much BTC still needs to be executed
            decimal remaining = order.Amount; 

            _logger.LogDebug("Processing order {OrderType} Amount={Amount}", order.Type, order.Amount);

            // Flatten and sort all orders across exchanges, depending on Buy/Sell
            var orders = GetFlattenedOrders(exchanges, order.Type);

            foreach (var x in orders)
            {
                // Stop if order fully executed
                if (remaining <= precision) 
                    break;

                // Skip orders with invalid price (avoid divide-by-zero and nonsensical orders)
                var exchangeId = x.Exchange.ExchangeId;

                if (x.Order.Price <= 0)
                {
                    _logger.LogWarning("Skipping order on {ExchangeId} due to invalid price: {Price}", exchangeId, x.Order.Price);
                    continue;
                }

                // Calculate max BTC that can be executed on this exchange
                decimal maxAmount = order.Type switch
                {
                    // Buy: cannot exceed ask amount or available EUR / price
                    OrderType.Buy => Math.Min(x.Order.Amount, euroBalances[exchangeId] / x.Order.Price),

                    // Sell: cannot exceed bid amount or available BTC
                    OrderType.Sell => Math.Min(x.Order.Amount, cryptoBalances[exchangeId]),

                    _ => throw new InvalidOperationException("Unknown order type")
                };

                // Make sure to not buy/sell more than remaining
                decimal executionAmount = Math.Min(remaining, maxAmount);

                if (executionAmount < precision)
                {
                    _logger.LogDebug("Negligible execution amount on {ExchangeId}: {ExecutionAmount}", exchangeId, executionAmount);
                    continue;
                }

                // Skip if nothing can be executed
                if (executionAmount <= 0) 
                {
                    _logger.LogDebug("No execution possible on {ExchangeId}: maxAmount={MaxAmount}", exchangeId, maxAmount);
                    continue; 
                }

                plan.Executions.Add(new ExchangeExecution
                {
                    ExchangeId = exchangeId,
                    Amount = executionAmount,
                    Price = x.Order.Price
                });

                _logger.LogInformation("Planned execution on {ExchangeId}: Amount={Amount} Price={Price}", exchangeId, executionAmount, x.Order.Price);

                // Update temporary balances so future orders on same exchange respect limits
                if (order.Type == OrderType.Buy)
                {
                    euroBalances[exchangeId] -= executionAmount * x.Order.Price; // Spend EUR
                    cryptoBalances[exchangeId] += executionAmount;              // Increase BTC
                }
                else // Sell
                {
                    cryptoBalances[exchangeId] -= executionAmount;              // Decrease BTC
                    euroBalances[exchangeId] += executionAmount * x.Order.Price; // Receive EUR
                }

                // Reduce remaining BTC to process
                remaining -= executionAmount;

                if (remaining < precision)
                    remaining = 0;

                _logger.LogDebug("After execution on {ExchangeId} remaining={Remaining}", exchangeId, remaining);
            }

            // Record total executed for output
            plan.TotalExecuted = order.Amount - remaining;

            _logger.LogInformation("Order completed. Type={OrderType} Requested={Requested} Executed={Executed} Executions={Count}", order.Type, order.Amount, plan.TotalExecuted, plan.Executions.Count);
        }

        private IEnumerable<(Exchange Exchange, Order Order)> GetFlattenedOrders(IEnumerable<Exchange> exchanges,OrderType type)
        {
            return type switch
            {
                // asks, cheapest first
                OrderType.Buy => exchanges
                    .SelectMany(e => e.OrderBook.Asks, (e, o) => (Exchange: e, Order: o.Order))
                    .OrderBy(x => x.Order.Price),

                // bids highest first
                OrderType.Sell => exchanges
                    .SelectMany(e => e.OrderBook.Bids, (e, o) => (Exchange: e, Order: o.Order))
                    .OrderByDescending(x => x.Order.Price),

                _ => throw new InvalidOperationException("Unknown order type")
            };
        }

        private void validateOrder(MetaOrder order)
        {
            if (order == null)
            {
                _logger.LogError("Null order passed to Execute");
                throw new ArgumentNullException(nameof(order));
            }

            if (order.Amount <= 0)
            {
                _logger.LogError("Invalid order amount: {Amount}", order.Amount);
                throw new ArgumentException("Amount must be greater than zero.");
            }

        }
    }
}
