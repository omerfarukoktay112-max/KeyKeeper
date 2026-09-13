namespace KeyKeeper.Core.Autofill;

/// <summary>
/// Pure host-normalization/matching logic behind Android autofill's web-domain lookup (Phase 10).
/// Platform-independent and unit-tested directly — the Android-specific
/// <c>KeyKeeperAutofillService</c> (which can't be unit tested; it's a thin adapter over
/// <c>AssistStructure</c>/<c>FillRequest</c>, the same "can't unit test the OS API itself" situation
/// as <c>BiometricAuthService</c>/<c>DeviceSecurityChecker</c>) is the only caller.
///
/// Scope: this matches a saved <see cref="Models.LoginItem.Url"/> against the web domain Android's
/// Autofill framework reports for a browser-rendered form field (<c>ViewNode.WebDomain</c>).
/// Native (non-browser) app autofill requests carry no web domain at all — this phase doesn't
/// attempt to fuzzy-match those against a saved URL by package name; see ARCHITECTURE.md's Phase 10
/// "Explicitly out of scope" note.
/// </summary>
public static class AutofillDomainMatcher
{
    /// <summary>
    /// Extracts and normalizes the host from a URL or bare hostname: lowercased, no scheme, no
    /// path/query/fragment, no leading "www.". Returns <c>null</c> for input that yields no usable
    /// host at all (empty/whitespace, or a string with no recognizable host component).
    /// </summary>
    public static string? NormalizeHost(string? urlOrHost)
    {
        if (string.IsNullOrWhiteSpace(urlOrHost))
        {
            return null;
        }

        string candidate = urlOrHost.Trim();

        string? host = TryGetHost(candidate) ?? TryGetHost("https://" + candidate);
        if (host is null || host.Length == 0)
        {
            return null;
        }

        host = host.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host["www.".Length..];
        }

        return host.Length == 0 ? null : host;
    }

    private static string? TryGetHost(string candidate) =>
        Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) && !string.IsNullOrEmpty(uri.Host)
            ? uri.Host
            : null;

    /// <summary>
    /// True if <paramref name="requestHost"/> (the domain Android's Autofill framework reports for
    /// the field being filled) is the saved item's own domain, or a subdomain of it — e.g. a login
    /// saved against <c>"https://google.com"</c> matches an autofill request on
    /// <c>"accounts.google.com"</c>, but a login saved against <c>"accounts.google.com"</c> does
    /// <em>not</em> match a request on the bare <c>"google.com"</c> (narrower saved scope, not
    /// broadened). Deliberately not a substring/suffix check on the raw strings (that would let a
    /// saved <c>"google.com"</c> wrongly match an attacker's <c>"evilgoogle.com"</c>) — both sides
    /// go through <see cref="NormalizeHost"/> first and the subdomain check requires an exact
    /// label-boundary match (a literal <c>"."</c> immediately before the suffix).
    /// </summary>
    public static bool IsMatch(string? savedUrl, string? requestHost)
    {
        string? savedHost = NormalizeHost(savedUrl);
        string? normalizedRequestHost = NormalizeHost(requestHost);

        if (savedHost is null || normalizedRequestHost is null)
        {
            return false;
        }

        return normalizedRequestHost == savedHost
            || normalizedRequestHost.EndsWith("." + savedHost, StringComparison.Ordinal);
    }
}
