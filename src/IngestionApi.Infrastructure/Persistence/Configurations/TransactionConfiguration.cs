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

        builder.Property(t => t.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(t => t.ModifiedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.OwnsMany(t => t.Bags, b =>
        {
            b.ToTable("TransactionBags");

            b.WithOwner()
                .HasForeignKey("TransactionId");

            b.HasKey("Id");

            b.Property<int>("Id")
                .ValueGeneratedOnAdd();

            b.Property(bag => bag.DeclaredValue)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            b.Property(bag => bag.ProcessedValue)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            b.Property(bag => bag.DifferenceValue)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            b.Property(bag => bag.Currency)
                .IsRequired()
                .HasMaxLength(3);

            // Unique constraint per bag: (BagId, OperationId, Currency, VendorId)
            b.HasIndex("TransactionId")
                .HasDatabaseName("IX_TransactionBags_Unique")
                .IsUnique();
        });
    }
}
