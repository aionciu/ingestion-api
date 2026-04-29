using IngestionApi.Domain.Entities;

namespace IngestionApi.Tests.Unit.Domain;

public class TransactionTests
{
    [Fact]
    public void Constructor_SetsTimestampsToUtcNow()
    {
        var before = DateTime.UtcNow;
        var t = new Transaction();
        var after = DateTime.UtcNow;

        Assert.InRange(t.CreatedAt, before, after);
        Assert.InRange(t.ModifiedAt, before, after);
    }

    [Fact]
    public void Properties_AreInitialized()
    {
        var t = new Transaction
        {
            OperationId = "op",
            VendorId = "v",
            BagId = "b",
            Currency = "USD",
            Value = 12.5m,
            TransactionType = TransactionType.Declared
        };

        Assert.Equal("op", t.OperationId);
        Assert.Equal("v", t.VendorId);
        Assert.Equal("b", t.BagId);
        Assert.Equal("USD", t.Currency);
        Assert.Equal(12.5m, t.Value);
        Assert.Equal(TransactionType.Declared, t.TransactionType);
        Assert.Equal(Guid.Empty, t.Id);
    }

    [Theory]
    [InlineData(TransactionType.Declared)]
    [InlineData(TransactionType.Processed)]
    [InlineData(TransactionType.Difference)]
    public void TransactionType_AllValues(TransactionType type)
    {
        var t = new Transaction { TransactionType = type };
        Assert.Equal(type, t.TransactionType);
    }
}
