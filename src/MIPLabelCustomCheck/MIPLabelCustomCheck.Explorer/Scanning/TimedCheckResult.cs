namespace MIPLabelCustomCheck.Explorer.Scanning;

/// <summary>The outcome of one timed <see cref="MipLabelChecker.Check(string)"/> call.</summary>
/// <param name="Result">The check's result, or <see langword="null"/> when it threw.</param>
/// <param name="Error">The exception the check threw, or <see langword="null"/> when it succeeded.</param>
/// <param name="Elapsed">Wall-clock time the call took, recorded whether it succeeded or threw.</param>
public sealed record TimedCheckResult(
    LabelCheckResult? Result,
    Exception? Error,
    TimeSpan Elapsed)
{
    /// <summary>Whether the check completed without throwing.</summary>
    public bool IsSuccess => Error is null;
}
