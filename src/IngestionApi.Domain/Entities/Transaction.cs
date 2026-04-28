using IngestionApi.Domain.Common;
using IngestionApi.Domain.ValueObjects;

namespace IngestionApi.Domain.Entities;

public class Transaction : Entity<Guid>
{
    public string OperationId { get; init; } = string.Empty;
    public string VendorId { get; init; } = string.Empty;
    public string BagId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime ModifiedAt { get; private set; }
    public ICollection<Bag> Bags { get; set; } = new List<Bag>();

    // Constructor for EF Core initialization
    public Transaction()
    {
        CreatedAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }
}
