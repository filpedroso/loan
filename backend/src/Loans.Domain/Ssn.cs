namespace Loans.Domain;

/// <summary>A US Social Security Number. It prints masked, so it can't leak into logs by accident.</summary>
public sealed record Ssn
{
    private Ssn(string value) => Value = value;

    /// <summary>The 9 digits, without dashes.</summary>
    public string Value { get; }

    public static Ssn Parse(string? input)
    {
        var digits = input?.Trim().Replace("-", "") ?? "";
        if (digits.Length != 9 || !digits.All(char.IsAsciiDigit))
            throw new ValidationException(nameof(Ssn), "Must have 9 digits.");

        return new Ssn(digits);
    }

    public override string ToString() => $"***-**-{Value[^4..]}";
}
