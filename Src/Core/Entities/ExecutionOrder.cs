namespace Core.Entities
{
    public class ExecutionOrder : BaseEntity
    {
        public string ExchangeId { get; set; } = null!;
        public OrderType Type { get; set; }
        public decimal Amount { get; set; }
        public decimal Price { get; set; }
    }
}
