namespace IngestionApi.Application.Common;

/// <summary>
/// Exception thrown when a conflict occurs (e.g., unique constraint violation).
/// </summary>
public class ConflictException : Exception
{
    public ConflictException() : base("A conflict occurred with existing data.")
    {
    }

    public ConflictException(string message) : base(message)
    {
    }

    public ConflictException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
