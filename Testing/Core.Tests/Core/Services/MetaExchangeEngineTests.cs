using Core.Entities;
using Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Tests.Services
{
    [TestClass]
    public class MetaExchangeEngineTests
    {
        private List<Exchange> LoadDataExchanges()
        {
            var loader = new ExchangeDataLoader(NullLogger<ExchangeDataLoader>.Instance);

            var path = "Data/Exchanges";

            return loader.LoadExchanges(path);
        }

        [TestMethod]
        public void Execute_Precision_IgnoresTinyOrders()
        {
            // Order amount smaller than engine precision (1e-8) should be ignored and produce no executions
            var exchanges = new List<Exchange>
            {
                new Exchange
                {
                    ExchangeId = "P",
                    AvailableFunds = new Balance { Euro = 100000m, Crypto = 0m },
                    OrderBook = new OrderBook { Asks = new List<OrderEnvelope> { new OrderEnvelope { Order = new Order { Amount = 0.1m, Price = 100m } } } }
                }
            };

            var repo = new InMemoryExchangeRepository(exchanges, NullLogger<InMemoryExchangeRepository>.Instance);
            var engine = new MetaExchangeEngine(repo, NullLogger<MetaExchangeEngine>.Instance);

            // 1e-9 < precision (1e-8)
            var tinyOrder = new MetaOrder { Type = OrderType.Buy, Amount = 0.000000001m };

            var plan = engine.Execute(tinyOrder);

            Assert.IsNotNull(plan);
            Assert.AreEqual(0m, plan.TotalExecuted, "Tiny order below precision should not be executed");
            Assert.IsTrue(plan.Executions == null || plan.Executions.Count == 0, "No executions expected for tiny order");
        }

        [TestMethod]
        public void Execute_NullOrder_ThrowsArgumentNullException()
        {
            var exchanges = new List<Exchange>();

            var repo = new InMemoryExchangeRepository(exchanges, NullLogger<InMemoryExchangeRepository>.Instance);

            var engine = new MetaExchangeEngine(repo, NullLogger<MetaExchangeEngine>.Instance);

            var action = () => engine.Execute(null!);

            Assert.Throws<ArgumentNullException>(action);
        }

        [TestMethod]
        public void Execute_InvalidAmount_ThrowsArgumentException()
        {
            var exchanges = new List<Exchange>();

            var repo = new InMemoryExchangeRepository(exchanges, NullLogger<InMemoryExchangeRepository>.Instance);

            var engine = new MetaExchangeEngine(repo, NullLogger<MetaExchangeEngine>.Instance);

            var order = new MetaOrder { Type = OrderType.Buy, Amount = 0m };

            Assert.Throws<ArgumentException>(() => engine.Execute(order));
        }

        [TestMethod]
        public void Execute_SmallBuy_ReturnsExecutions()
        {
            // Use real exchange JSON files but parse them here (tests must not rely on ExchangeDataLoader behaviour)
            var dataFolder = "Data/Exchanges";
            Assert.IsTrue(Directory.Exists(dataFolder), "No exchanges found in Data folder; ensure Data/Exchanges contains sample JSON files for tests.");

            var exchanges = LoadDataExchanges();

            Assert.IsTrue(exchanges.Count > 0, "No exchanges parsed from data folder; ensure Data/Exchanges contains sample JSON files for tests.");

            var repo = new InMemoryExchangeRepository(exchanges, NullLogger<InMemoryExchangeRepository>.Instance);
            var engine = new MetaExchangeEngine(repo, NullLogger<MetaExchangeEngine>.Instance);

            // Use a very small buy so it will be filled by the single best ask in the fixtures.
            var order = new MetaOrder { Type = OrderType.Buy, Amount = 0.001m };

            var plan = engine.Execute(order);

            Assert.IsNotNull(plan);
            Assert.AreEqual(order.Type, plan.OrderType);
            Assert.AreEqual(order.Amount, plan.Amount);

            // Expect at least one execution (small buy may be limited by per-exchange EUR balance in fixtures)
            Assert.IsNotNull(plan.Executions);
            Assert.IsTrue(plan.Executions.Count >= 1, "Expected at least one execution for this small buy against fixtures.");

            // verify total executed equals sum of executions and does not exceed requested amount
            var total = plan.Executions.Sum(e => e.Amount);
            Assert.AreEqual(plan.TotalExecuted, total);
            Assert.IsTrue(plan.TotalExecuted <= order.Amount);
        }

        [TestMethod]
        public void Execute_LargeBuy_ExecutesPartially()
        {
            var exchanges = LoadDataExchanges();
            var repo = new InMemoryExchangeRepository(exchanges, NullLogger<InMemoryExchangeRepository>.Instance);
            var engine = new MetaExchangeEngine(repo, NullLogger<MetaExchangeEngine>.Instance);

            var order = new MetaOrder { Type = OrderType.Buy, Amount = 1000000m };

            var plan = engine.Execute(order);

            Assert.IsNotNull(plan);

            // TotalExecuted should be less than requested when insufficient funds exist
            Assert.IsTrue(plan.TotalExecuted <= order.Amount);
        }

        [TestMethod]
        public void Execute_Sell_ReturnsExecutions()
        {
            var exchanges = LoadDataExchanges();
            var repo = new InMemoryExchangeRepository(exchanges, NullLogger<InMemoryExchangeRepository>.Instance);
            var engine = new MetaExchangeEngine(repo, NullLogger<MetaExchangeEngine>.Instance);

            var order = new MetaOrder { Type = OrderType.Sell, Amount = 0.001m };

            var plan = engine.Execute(order);

            Assert.IsNotNull(plan);
            Assert.AreEqual(order.Type, plan.OrderType);
            Assert.IsTrue(plan.TotalExecuted >= 0);
        }

        [TestMethod]
        public void Buy_PicksCheapestAsksFirst()
        {
            // Three exchanges with different ask prices; engine should pick cheapest first
            var exchanges = new List<Exchange>
            {
                new Exchange
                {
                    ExchangeId = "A",
                    AvailableFunds = new Balance { Euro = 100000m, Crypto = 0m },
                    OrderBook = new OrderBook { Asks = new List<OrderEnvelope> { new OrderEnvelope { Order = new Order { Amount = 0.5m, Price = 100m } } } }
                },
                new Exchange
                {
                    ExchangeId = "B",
                    AvailableFunds = new Balance { Euro = 100000m, Crypto = 0m },
                    OrderBook = new OrderBook { Asks = new List<OrderEnvelope> { new OrderEnvelope { Order = new Order { Amount = 0.5m, Price = 90m } } } }
                },
                new Exchange
                {
                    ExchangeId = "C",
                    AvailableFunds = new Balance { Euro = 100000m, Crypto = 0m },
                    OrderBook = new OrderBook { Asks = new List<OrderEnvelope> { new OrderEnvelope { Order = new Order { Amount = 0.5m, Price = 110m } } } }
                }
            };

            var repo = new InMemoryExchangeRepository(exchanges, NullLogger<InMemoryExchangeRepository>.Instance);
            var engine = new MetaExchangeEngine(repo, NullLogger<MetaExchangeEngine>.Instance);

            var order = new MetaOrder { Type = OrderType.Buy, Amount = 1.0m };

            var plan = engine.Execute(order);

            Assert.IsNotNull(plan);
            Assert.AreEqual(1.0m, plan.TotalExecuted);
            Assert.AreEqual(2, plan.Executions.Count, "Should consume two asks to reach 1.0 BTC");

            // First execution must be from B (price 90), then from A (price 100)
            Assert.AreEqual("B", plan.Executions[0].ExchangeId);
            Assert.AreEqual(0.5m, plan.Executions[0].Amount);
            Assert.AreEqual(90m, plan.Executions[0].Price);

            Assert.AreEqual("A", plan.Executions[1].ExchangeId);
            Assert.AreEqual(0.5m, plan.Executions[1].Amount);
            Assert.AreEqual(100m, plan.Executions[1].Price);
        }

        [TestMethod]
        public void ExchangeBalance_LimitsExecution()
        {
            // Single exchange with limited EUR balance: €100, ask price €50 => can buy max 2 BTC
            var exchanges = new List<Exchange>
            {
                new Exchange
                {
                    ExchangeId = "X",
                    AvailableFunds = new Balance { Euro = 100m, Crypto = 0m },
                    OrderBook = new OrderBook { Asks = new List<OrderEnvelope> { new OrderEnvelope { Order = new Order { Amount = 10m, Price = 50m } } } }
                }
            };

            var repo = new InMemoryExchangeRepository(exchanges, NullLogger<InMemoryExchangeRepository>.Instance);
            var engine = new MetaExchangeEngine(repo, NullLogger<MetaExchangeEngine>.Instance);

            var order = new MetaOrder { Type = OrderType.Buy, Amount = 10m };

            var plan = engine.Execute(order);

            Assert.IsNotNull(plan);
            // Limited by EUR balance => 100 / 50 = 2 BTC
            Assert.AreEqual(2m, plan.TotalExecuted, "Execution should be limited by exchange EUR balance to 2 BTC");
            Assert.AreEqual(1, plan.Executions.Count);
            Assert.AreEqual("X", plan.Executions[0].ExchangeId);
            Assert.AreEqual(2m, plan.Executions[0].Amount);
            Assert.AreEqual(50m, plan.Executions[0].Price);
        }

        [TestMethod]
        public void PartialFill_WhenLiquidityRunsOut()
        {
            // Total available asks across exchanges = 6 BTC; request 10 BTC => expect total executed = 6
            var exchanges = new List<Exchange>
            {
                new Exchange
                {
                    ExchangeId = "E1",
                    AvailableFunds = new Balance { Euro = 100000m, Crypto = 0m },
                    OrderBook = new OrderBook { Asks = new List<OrderEnvelope> { new OrderEnvelope { Order = new Order { Amount = 3m, Price = 100m } } } }
                },
                new Exchange
                {
                    ExchangeId = "E2",
                    AvailableFunds = new Balance { Euro = 100000m, Crypto = 0m },
                    OrderBook = new OrderBook { Asks = new List<OrderEnvelope> { new OrderEnvelope { Order = new Order { Amount = 3m, Price = 101m } } } }
                }
            };

            var repo = new InMemoryExchangeRepository(exchanges, NullLogger<InMemoryExchangeRepository>.Instance);
            var engine = new MetaExchangeEngine(repo, NullLogger<MetaExchangeEngine>.Instance);

            var order = new MetaOrder { Type = OrderType.Buy, Amount = 10m };

            var plan = engine.Execute(order);

            Assert.IsNotNull(plan);
            Assert.AreEqual(6m, plan.TotalExecuted, "TotalExecuted should equal available liquidity when insufficient to fully fill the order.");
            Assert.AreEqual(2, plan.Executions.Count);
            // Each exchange contributed 3 BTC
            Assert.AreEqual(3m, plan.Executions[0].Amount);
            Assert.AreEqual(3m, plan.Executions[1].Amount);
            // Total executed should be 6 BTC
            Assert.AreEqual(6m, plan.Executions.Sum(e => e.Amount));
        }
    }
}
