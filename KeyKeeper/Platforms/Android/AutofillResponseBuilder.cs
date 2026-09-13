using System.Runtime.Versioning;
using Android.Service.Autofill;
using Android.Views.Autofill;
using Android.Widget;
using KeyKeeper.Core.Autofill;
using KeyKeeper.Core.Data;
using KeyKeeper.Core.Models;

namespace KeyKeeper.Services;

/// <summary>
/// Builds the actual <see cref="FillResponse"/> (real usernames/passwords) from an
/// <em>already-unlocked</em> vault -- shared by <see cref="KeyKeeperAutofillService.OnFillRequest"/>
/// (vault already unlocked when the request arrives) and <see cref="AutofillCompletionHandler.
/// Complete"/> (vault just unlocked via the authentication continuation). Neither caller passes
/// this class anything if the vault is locked -- see both callers' own doc comments for that
/// guarantee.
/// </summary>
[SupportedOSPlatform("android26.0")]
internal static class AutofillResponseBuilder
{
    public static FillResponse? Build(
        IVaultItemRepository repository,
        string? requestHost,
        AutofillId? usernameId,
        AutofillId? passwordId)
    {
        if (requestHost is null || (usernameId is null && passwordId is null))
        {
            return null;
        }

        List<LoginItem> matches = repository.List(VaultItemType.Login)
            .Select(record => record.Payload as LoginItem)
            .Where(login => login is not null && AutofillDomainMatcher.IsMatch(login.Url, requestHost))
            .Select(login => login!)
            .ToList();

        if (matches.Count == 0)
        {
            return null;
        }

        var responseBuilder = new FillResponse.Builder();
        foreach (LoginItem item in matches)
        {
            string label = string.IsNullOrEmpty(item.Username) ? item.Title : $"{item.Title} ({item.Username})";
            var datasetBuilder = new Dataset.Builder(BuildPresentation(label));

            if (usernameId is not null && !string.IsNullOrEmpty(item.Username))
            {
                datasetBuilder.SetValue(usernameId, AutofillValue.ForText(item.Username));
            }

            if (passwordId is not null && !string.IsNullOrEmpty(item.Password))
            {
                datasetBuilder.SetValue(passwordId, AutofillValue.ForText(item.Password));
            }

            responseBuilder.AddDataset(datasetBuilder.Build());
        }

        return responseBuilder.Build();
    }

    /// <summary>A plain system list-item layout, not a custom one -- keeps this phase from needing
    /// a hand-written Android layout XML resource just to show one line of text per suggestion.</summary>
    public static RemoteViews BuildPresentation(string text)
    {
        var views = new RemoteViews(global::Android.App.Application.Context.PackageName, global::Android.Resource.Layout.SimpleListItem1);
        views.SetTextViewText(global::Android.Resource.Id.Text1, text);
        return views;
    }
}
