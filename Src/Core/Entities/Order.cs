using System;

namespace Core.Entities
{
    public class Order : BaseEntity
    {
        public DateTime Time { get; set; }
        public OrderType Type { get; set; }
        public string Kind { get; set; } = "Limit";
        public decimal Amount { get; set; }
        public decimal Price { get; set; }
    }
}
