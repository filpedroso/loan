namespace Loans.Domain;

/// <summary>Input that breaks a domain rule. <see cref="Field"/> names the field at fault.</summary>
public sealed class ValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
