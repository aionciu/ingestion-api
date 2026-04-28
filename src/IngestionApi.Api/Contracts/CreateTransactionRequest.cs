namespace IngestionApi.Api.Contracts;

public record CreateTransactionRequest(
    string OperationId,
    string BagId,
    List<BagItemDto> Bags);

public record BagItemDto(
    decimal DeclaredValue,
    decimal ProcessedValue,
    decimal DifferenceValue,
    string Currency);
