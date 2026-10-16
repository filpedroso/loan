namespace Loans.Infrastructure.Outbox;

/// <summary>The <c>Outbox</c> configuration section.</summary>
public sealed class OutboxOptions
{
    /// <summary>False turns the worker off, e.g. in tests or when the worker runs as a separate process.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Time between ticks, which is also the time between retries.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>After this many failed attempts a message is dead-lettered.</summary>
    public int MaxAttempts { get; set; } = 10;

    public int BatchSize { get; set; } = 20;
}
