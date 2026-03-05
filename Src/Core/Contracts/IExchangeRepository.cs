using Core.Entities;

namespace Core.Contracts
{
    public interface IExchangeRepository
    {
        List<Exchange> GetAllExchanges();
        Exchange? GetExchangeById(string exchangeId);
    }
}
