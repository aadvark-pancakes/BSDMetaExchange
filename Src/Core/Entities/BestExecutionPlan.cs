namespace Core.Entities
{
    public class BestExecutionPlan
    {
        public OrderType OrderType { get; set; }
        public decimal Amount { get; set; }
        public decimal TotalExecuted { get; set; }
        public List<ExchangeExecution> Executions { get; set; } = new();
    }
}
