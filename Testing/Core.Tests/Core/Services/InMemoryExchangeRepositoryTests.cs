using Core.Entities;
using Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Tests.Services
{
    [TestClass]
    public class InMemoryExchangeRepositoryTests
    {
        [TestMethod]
        public void GetAllExchanges_ReturnsProvidedList()
        {
            var list = new List<Exchange>
            {
                new Exchange { ExchangeId = "ex-1" }
            };

            var repo = new InMemoryExchangeRepository(list, NullLogger<InMemoryExchangeRepository>.Instance);

            var result = repo.GetAllExchanges();

            Assert.AreSame(list, result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("ex-1", result[0].ExchangeId);
        }

        [TestMethod]
        public void GetExchangeById_ReturnsExchange_WhenExists()
        {
            var list = new List<Exchange>
            {
                new Exchange { ExchangeId = "ex-1" },
                new Exchange { ExchangeId = "ex-2" }
            };

            var repo = new InMemoryExchangeRepository(list, NullLogger<InMemoryExchangeRepository>.Instance);

            // existing id returns the exchange
            var found = repo.GetExchangeById("ex-1");
            Assert.IsNotNull(found);
            Assert.AreEqual("ex-1", found!.ExchangeId);
        }

        [TestMethod]
        public void GetExchangeById_ReturnsNull_WhenNotFound()
        {
            var list = new List<Exchange>
            {
                new Exchange { ExchangeId = "ex-1" },
                new Exchange { ExchangeId = "ex-2" }
            };

            var repo = new InMemoryExchangeRepository(list, NullLogger<InMemoryExchangeRepository>.Instance);

            // missing id returns null
            var missing = repo.GetExchangeById("no-such");
            Assert.IsNull(missing);
        }
    }
}
