using Core.Entities;
using Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;

namespace Core.Tests.Services
{
    [TestClass]
    public class ExchangeDataLoaderTests
    {
        [TestMethod]
        public void LoadExchanges_FromDataDirectory_ReturnsAtLeastOne()
        {
            var loader = new ExchangeDataLoader(NullLogger<ExchangeDataLoader>.Instance);
            var path = "Data/Exchanges";

            var exchanges = loader.LoadExchanges(path);

            Assert.IsNotNull(exchanges);
            Assert.IsTrue(exchanges.Count > 0, "Expected at least one exchange to be loaded from data folder");
        }

        [TestMethod]
        public void LoadExchanges_ParsesOrderbookAndFunds_CorrectlyForKnownFixture()
        {
            var loader = new ExchangeDataLoader(NullLogger<ExchangeDataLoader>.Instance);
            var path = "Data/Exchanges";

            var exchanges = loader.LoadExchanges(path);

            // locate a known fixture that should be present in repository
            var ex = exchanges.FirstOrDefault(e => string.Equals(e.ExchangeId, "exchange-01", StringComparison.OrdinalIgnoreCase));
            Assert.IsNotNull(ex, "Expected exchange-01 fixture to be loaded");

            // verify available funds were parsed
            Assert.AreEqual(117520.12m, ex.AvailableFunds.Euro, "exchange-01 Euro balance parsed incorrectly");
            Assert.AreEqual(10.8503m, ex.AvailableFunds.Crypto, "exchange-01 Crypto balance parsed incorrectly");

            // verify asks were parsed and first ask has expected values (matches fixture)
            Assert.IsNotNull(ex.OrderBook);
            Assert.IsTrue(ex.OrderBook.Asks.Count > 0, "exchange-01 should contain asks in fixture");

            var firstAsk = ex.OrderBook.Asks[0].Order;
            Assert.IsNotNull(firstAsk);
            Assert.AreEqual(0.405m, firstAsk.Amount, "First ask amount mismatch for exchange-01");
            Assert.AreEqual(57299.73m, firstAsk.Price, "First ask price mismatch for exchange-01");
            Assert.AreEqual(OrderType.Sell, firstAsk.Type, "First ask type mismatch for exchange-01");
        }

        [TestMethod]
        public void LoadExchanges_PopulatesAtLeastOneAskAndBidAcrossFixtures()
        {
            var loader = new ExchangeDataLoader(NullLogger<ExchangeDataLoader>.Instance);
            var path = "Data/Exchanges";

            var exchanges = loader.LoadExchanges(path);

            Assert.IsTrue(exchanges.Any(e => e.OrderBook != null && e.OrderBook.Asks.Count > 0), "Expected at least one exchange to have asks parsed");
            Assert.IsTrue(exchanges.Any(e => e.OrderBook != null && e.OrderBook.Bids.Count > 0), "Expected at least one exchange to have bids parsed");
        }

        [TestMethod]
        public void LoadExchanges_InvalidFolder_ThrowsDirectoryNotFoundException()
        {
            var loader = new ExchangeDataLoader(NullLogger<ExchangeDataLoader>.Instance);
            var nonExisting = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            var action = () => loader.LoadExchanges(nonExisting);

            Assert.Throws<DirectoryNotFoundException>(action);
        }

        [TestMethod]
        public void LoadExchanges_SkipsInvalidJsonFiles()
        {
            var mockLogger = new Moq.Mock<ILogger<ExchangeDataLoader>>();

            var loader = new ExchangeDataLoader(mockLogger.Object);

            var temp = Path.Combine(Path.GetTempPath(), "exch_tests_" + Guid.NewGuid().ToString());

            Directory.CreateDirectory(temp);

            try
            {
                // create several valid files
                var validIds = new[] { "v1", "v2", "v3" };
                foreach (var id in validIds)
                {
                    var valid = new Exchange { ExchangeId = id, AvailableFunds = new Balance { Euro = 1000, Crypto = 1 }, OrderBook = new OrderBook() };
                    var file = Path.Combine(temp, id + ".json");
                    using (var fs = File.Create(file))
                    {
                        JsonSerializer.Serialize(fs, valid);
                    }
                }

                // create invalid files
                var invalidFiles = new[] { "bad1.json", "bad2.json" };
                File.WriteAllText(Path.Combine(temp, invalidFiles[0]), "{ this is not json");
                File.WriteAllText(Path.Combine(temp, invalidFiles[1]), "<xml></xml>");

                var exchanges = loader.LoadExchanges(temp);

                Assert.IsNotNull(exchanges);
                Assert.AreEqual(validIds.Length, exchanges.Count, "Only valid json files should be loaded");

                var loadedIds = exchanges.Select(e => e.ExchangeId).OrderBy(x => x).ToArray();
                CollectionAssert.AreEqual(validIds.OrderBy(x => x).ToArray(), loadedIds);

                // assert that errors were logged for invalid files using Moq Verify on ILogger.Log
                foreach (var invalidFile in invalidFiles)
                {
                    mockLogger.Verify(m => m.Log(
                        LogLevel.Error,
                        It.IsAny<EventId>(),
                        It.Is<It.IsAnyType>((v, t) => v.ToString().IndexOf(invalidFile, StringComparison.OrdinalIgnoreCase) >= 0),
                        It.IsAny<Exception>(),
                        It.IsAny<Func<It.IsAnyType, Exception, string>>() ),
                        Times.AtLeastOnce(),
                        $"Expected a log entry mentioning {invalidFile}");
                }
            }
            finally
            {
                try 
                { 
                    Directory.Delete(temp, true); 
                } 
                catch 
                { 
                }
            }
        }
    }
}
