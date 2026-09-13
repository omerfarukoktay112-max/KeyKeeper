namespace KeyKeeper.Services;

/// <summary>
/// The intent action/extra-key names <see cref="KeyKeeperAutofillService"/>'s authentication
/// <c>PendingIntent</c> and <see cref="MainActivity"/> agree on -- split out from
/// <see cref="KeyKeeperAutofillService"/> itself (which is <c>[SupportedOSPlatform("android26.0")]</c>,
/// since it depends on the real Autofill Framework types) purely so these plain string constants
/// stay callable from <c>MainActivity.OnCreate</c>/<c>OnNewIntent</c>, which run on every supported
/// Android version (23+), without the platform-compatibility analyzer treating a bare string
/// constant as if it were itself API-26-only.
/// </summary>
internal static class AutofillAuthContract
{
    public const string Action = "com.companyname.keykeeper.AUTOFILL_AUTH";
    public const string ExtraUsernameId = "keykeeper.autofill.username_id";
    public const string ExtraPasswordId = "keykeeper.autofill.password_id";
    public const string ExtraRequestHost = "keykeeper.autofill.request_host";
}
