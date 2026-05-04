namespace GoldBank.SharedKernel.Results;

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "A null value was provided.");
    public static readonly Error Unauthorized = new("Error.Unauthorized", "Unauthorized access.");
    public static readonly Error Forbidden = new("Error.Forbidden", "Access denied.");
    public static readonly Error NotFound = new("Error.NotFound", "Resource not found.");
    public static readonly Error Conflict = new("Error.Conflict", "Resource conflict.");
    public static readonly Error Validation = new("Error.Validation", "Validation failed.");
}
