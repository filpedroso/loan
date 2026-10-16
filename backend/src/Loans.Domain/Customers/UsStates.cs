using System.Collections.Frozen;

namespace Loans.Domain.Customers;

/// <summary>USPS codes of the 50 states plus DC.</summary>
public static class UsStates
{
    private static readonly FrozenSet<string> Codes = new[]
    {
        "AL", "AK", "AZ", "AR", "CA", "CO", "CT", "DE", "DC", "FL", "GA", "HI", "ID", "IL", "IN", "IA", "KS",
        "KY", "LA", "ME", "MD", "MA", "MI", "MN", "MS", "MO", "MT", "NE", "NV", "NH", "NJ", "NM", "NY", "NC",
        "ND", "OH", "OK", "OR", "PA", "RI", "SC", "SD", "TN", "TX", "UT", "VT", "VA", "WA", "WV", "WI", "WY",
    }.ToFrozenSet();

    /// <summary>Returns the code in upper case, or null if it isn't a state code.</summary>
    public static string? Normalize(string? code)
    {
        var upper = code?.Trim().ToUpperInvariant();
        return upper is not null && Codes.Contains(upper) ? upper : null;
    }
}
