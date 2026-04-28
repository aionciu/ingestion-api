namespace IngestionApi.Domain.ValueObjects;

public class Bag : IEquatable<Bag>
{
    public decimal DeclaredValue { get; init; }
    public decimal ProcessedValue { get; init; }
    public decimal DifferenceValue { get; init; }
    public string Currency { get; init; } = string.Empty;

    public bool Equals(Bag? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return DeclaredValue == other.DeclaredValue &&
               ProcessedValue == other.ProcessedValue &&
               DifferenceValue == other.DifferenceValue &&
               Currency == other.Currency;
    }

    public override bool Equals(object? obj) => Equals(obj as Bag);

    public override int GetHashCode()
    {
        return HashCode.Combine(DeclaredValue, ProcessedValue, DifferenceValue, Currency);
    }

    public static bool operator ==(Bag? left, Bag? right) => left?.Equals(right) ?? right is null;
    public static bool operator !=(Bag? left, Bag? right) => !(left == right);
}
