using System.Text.Json.Serialization;

namespace Core.Entities
{
    public class Exchange : BaseEntity
    {
        [JsonPropertyName("Id")]
        public string ExchangeId { get; set; } = null!;
        public Balance AvailableFunds { get; set; } = new();
        public OrderBook OrderBook { get; set; } = new();
    }
}
