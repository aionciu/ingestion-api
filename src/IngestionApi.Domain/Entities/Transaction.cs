using IngestionApi.Domain.Common;

namespace IngestionApi.Domain.Entities;

public class Transaction : Entity<Guid>
{
    public string OperationId { get; init; } = string.Empty;
    public string VendorId { get; init; } = string.Empty;
    public string BagId { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public decimal Value { get; init; }
    public TransactionType TransactionType { get; init; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ModifiedAt { get; private set; }

    // Constructor for EF Core initialization
    public Transaction()
    {
        CreatedAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }
}
