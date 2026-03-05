using System;
using System.Text.Json.Serialization;

namespace Core.Entities
{
    public abstract class BaseEntity
    {
        [JsonIgnore]
        public Guid Id { get; set; }
        [JsonIgnore]
        public DateTime Created { get; set; }
        [JsonIgnore]
        public string? CreatedBy { get; set; }
        [JsonIgnore]
        public DateTime? LastModified { get; set; }

        protected BaseEntity()
        {
            Id = Guid.NewGuid();
            Created = DateTime.UtcNow;
        }
    }
}
