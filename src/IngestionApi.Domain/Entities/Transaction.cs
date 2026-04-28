using IngestionApi.Domain.Common;
using IngestionApi.Domain.ValueObjects;

namespace IngestionApi.Domain.Entities;

public class Transaction : Entity<Guid>
{
    public string OperationId { get; set; } = string.Empty;
    public string VendorId { get; set; } = string.Empty;
    public string BagId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public ICollection<Bag> Bags { get; set; } = new List<Bag>();
}
