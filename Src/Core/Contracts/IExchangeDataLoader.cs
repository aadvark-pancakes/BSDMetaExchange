using Core.Entities;

namespace Core.Contracts
{
    public interface IExchangeDataLoader
    {
        List<Exchange> LoadExchanges(string folderPath);
    }
}
