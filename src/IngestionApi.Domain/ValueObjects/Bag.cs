namespace IngestionApi.Domain.ValueObjects;

public class Bag
{
    public decimal DeclaredValue { get; set; }
    public decimal ProcessedValue { get; set; }
    public decimal DifferenceValue { get; set; }
    public string Currency { get; set; } = string.Empty;
}
