namespace KeyKeeper.Core.Tools;

/// <summary>Result of <see cref="ITotpGenerator.GenerateCode"/>.</summary>
/// <param name="Code">The current 6-digit TOTP code.</param>
/// <param name="RemainingSeconds">Seconds left in the current 30-second step before
/// <paramref name="Code"/> changes -- drives the countdown shown next to the code.</param>
/// <param name="PeriodSeconds">The step size the code was computed with (always 30 for this app's
/// generator, exposed so UI can size a countdown ring without a hardcoded constant of its own).</param>
public sealed record TotpCode(string Code, int RemainingSeconds, int PeriodSeconds);
