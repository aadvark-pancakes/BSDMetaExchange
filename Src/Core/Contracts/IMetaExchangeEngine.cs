using Core.Entities;

namespace Core.Contracts
{
    public interface IMetaExchangeEngine
    {
        BestExecutionPlan Execute(MetaOrder order);
    }
}
