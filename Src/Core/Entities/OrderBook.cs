namespace Core.Entities
{
    public class OrderBook : BaseEntity
    {
        // JSON fixtures wrap orders in an object { "Order": { ... } }, so use an envelope type
        public List<OrderEnvelope> Bids { get; set; } = new();
        public List<OrderEnvelope> Asks { get; set; } = new();
    }
}
