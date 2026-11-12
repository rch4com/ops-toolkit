namespace MIPLabelCustomCheck;

/// <summary>Controls how much a <see cref="MipLabelChecker"/> call reads.</summary>
public sealed record LabelCheckOptions
{
    /// <summary>Reads label GUIDs only — the cheapest answer to "is this file labelled".</summary>
    public static LabelCheckOptions Default { get; } = new();

    /// <summary>Also reads each label's supplementary metadata into <see cref="LabelCheckResult.Details"/>.</summary>
    public static LabelCheckOptions WithDetails { get; } = new() { IncludeDetails = true };

    /// <summary>
    /// Whether to populate <see cref="LabelCheckResult.Details"/> with each label's supplementary
    /// metadata — tenant, name, method, and so on. Off by default.
    /// </summary>
    /// <remarks>
    /// Reading details parses metadata the file has already been opened for, so it costs
    /// additional work but no additional I/O. It is opt-in because a caller that only needs to
    /// answer "is this labelled" should not pay for parsing it does not use, and because the
    /// details are a snapshot of what a client wrote into the file, not authoritative policy.
    /// </remarks>
    public bool IncludeDetails { get; init; }
}
