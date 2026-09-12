using KeyKeeper.Core.Auth;

namespace KeyKeeper;

/// <summary>
/// The setup/lock/vault route decision, extracted out of <see cref="App"/> (Phase 7) so both the
/// normal launch path and <c>SecurityWarningPage</c>'s "continue anyway" path share one source of
/// truth instead of duplicating this ternary.
/// </summary>
internal static class AppRouting
{
    public static string DetermineInitialRoute(IAuthenticationService auth) =>
        !auth.IsVaultInitialized
            ? "//setup"
            : auth.IsUnlocked
                ? "//vault"
                : "//lock";
}
