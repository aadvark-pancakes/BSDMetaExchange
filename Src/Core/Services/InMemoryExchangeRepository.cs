using Core.Contracts;
using Core.Entities;
using Microsoft.Extensions.Logging;

namespace Core.Services
{
    public class InMemoryExchangeRepository : IExchangeRepository
    {
        private readonly List<Exchange> exchanges;
        private readonly ILogger<InMemoryExchangeRepository> logger;

        public InMemoryExchangeRepository(List<Exchange> exchanges, ILogger<InMemoryExchangeRepository> logger)
        {
            if(exchanges == null)
                throw new ArgumentNullException(nameof(exchanges), "Exchanges list cannot be null.");
            
            this.exchanges = exchanges;
            this.logger = logger;
        }

        public List<Exchange> GetAllExchanges()
        {
            return exchanges;
        }

        public Exchange? GetExchangeById(string exchangeId)
        {
            return exchanges.FirstOrDefault(x => x.ExchangeId == exchangeId);
        }
    }
}
