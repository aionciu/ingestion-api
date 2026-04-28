namespace IngestionApi.Application.Features.Transactions.DTOs;

public record BagDto(
    decimal DeclaredValue,
    decimal ProcessedValue,
    decimal DifferenceValue,
    string Currency);
