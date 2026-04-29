using IngestionApi.Domain.ValueObjects;

namespace IngestionApi.Tests.Unit.Domain;

public class BagTests
{
    private static Bag Make(decimal d = 1, decimal p = 2, decimal diff = 3, string c = "USD") =>
        new() { DeclaredValue = d, ProcessedValue = p, DifferenceValue = diff, Currency = c };

    [Fact]
    public void Equals_SameValues_True()
    {
        Assert.True(Make().Equals(Make()));
        Assert.True(Make() == Make());
        Assert.False(Make() != Make());
    }

    [Fact]
    public void Equals_DifferentValues_False()
    {
        Assert.False(Make().Equals(Make(d: 9)));
        Assert.False(Make().Equals(Make(p: 9)));
        Assert.False(Make().Equals(Make(diff: 9)));
        Assert.False(Make().Equals(Make(c: "EUR")));
    }

    [Fact]
    public void Equals_Null_False()
    {
        Assert.False(Make().Equals((Bag?)null));
        Assert.False(Make().Equals((object?)null));
        Assert.False(Make() == null);
        Assert.False(null == Make());
        Assert.True((Bag?)null == (Bag?)null);
    }

    [Fact]
    public void Equals_SameReference_True()
    {
        var b = Make();
        Assert.True(b.Equals(b));
    }

    [Fact]
    public void Equals_Object_DispatchesToTyped()
    {
        object same = Make();
        Assert.True(Make().Equals(same));
        Assert.False(Make().Equals((object)"x"));
    }

    [Fact]
    public void GetHashCode_EqualValues_Equal()
    {
        Assert.Equal(Make().GetHashCode(), Make().GetHashCode());
    }
}
