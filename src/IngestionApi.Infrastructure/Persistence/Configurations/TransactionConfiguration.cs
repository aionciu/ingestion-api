using IngestionApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngestionApi.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.OperationId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(t => t.VendorId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(t => t.BagId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(t => t.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(t => t.Value)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(t => t.TransactionType)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(t => t.ModifiedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        // Unique constraint: (BagId, OperationId, Currency, VendorId, TransactionType)
        builder.HasIndex(t => new { t.BagId, t.OperationId, t.Currency, t.VendorId, t.TransactionType })
            .IsUnique()
            .HasDatabaseName("IX_Transaction_BagOpCurrVendorType");
    }
}
