using Core.Contracts;
using Core.Entities;
using Microsoft.Extensions.Logging;

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

            // Flatten and sort all orders across exchanges, depending on Buy/Sell
            var orders = GetFlattenedOrders(exchanges, order.Type);

            foreach (var x in orders)
            {
                // Stop if order fully executed
                if (remaining <= precision) 
                    break;

                // Skip orders with invalid price (avoid divide-by-zero and nonsensical orders)
                if (x.Order.Price <= 0)
                    continue;

                var exchangeId = x.Exchange.ExchangeId;

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
                    continue;

                // Skip if nothing can be executed
                if (executionAmount <= 0) 
                    continue; 

                plan.Executions.Add(new ExchangeExecution
                {
                    ExchangeId = exchangeId,
                    Amount = executionAmount,
                    Price = x.Order.Price
                });

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
            }

            // Record total executed for output
            plan.TotalExecuted = order.Amount - remaining;
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
                throw new ArgumentNullException(nameof(order));

            if (order.Amount <= 0)
                throw new ArgumentException("Amount must be greater than zero.");

        }
    }
}
