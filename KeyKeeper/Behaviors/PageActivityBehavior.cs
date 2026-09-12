using KeyKeeper.Core.Auth;

namespace KeyKeeper.Behaviors;

/// <summary>
/// Fixes a Phase 3 gap (found during Phase 4 manual verification): nothing in the UI ever called
/// <see cref="IAuthenticationService.NotifyUserActivity"/>, so the inactivity auto-lock measured
/// time-since-unlock, not time-since-last-interaction. This reports activity on page navigation
/// (<see cref="Page.Appearing"/>) and on the natural "the user just did something" event of every
/// interactive control already on the page (<see cref="Button.Clicked"/>,
/// <see cref="InputView.TextChanged"/> for <see cref="Entry"/>/<see cref="Editor"/>,
/// <see cref="Switch.Toggled"/>, <see cref="Picker.SelectedIndexChanged"/>,
/// <see cref="DatePicker.DateSelected"/>).
///
/// Applied to every page app-wide via the implicit `Page` style in Styles.xaml
/// (<c>ApplyToDerivedTypes="True"</c>) — no per-page wiring, current or future. Page-to-page
/// navigation itself (e.g. tapping a `VaultListPage` row, which has no `Button`/`Entry` of its
/// own) is covered separately, by `App.xaml.cs` subscribing to `Shell.Navigated` — that one hook
/// alone would miss continued interaction (typing, toggling) on a page the user hasn't left yet,
/// which is what this class adds.
///
/// Resolves <see cref="IAuthenticationService"/> via <see cref="IPlatformApplication.Current"/>'s
/// service provider rather than constructor injection: an implicit style instantiates this class
/// directly through the XAML/style engine, outside the DI container, so there's no constructor
/// for the container to inject into — this is the standard MAUI pattern for behaviors/converters
/// that need a DI-registered service.
///
/// Phase 9 also folded a second, unrelated concern in here — a subtle page-appear fade-in (opacity
/// 0→1 over 180ms, <see cref="OnAppearing"/>'s last line) — rather than a separate behavior class.
/// A style's <c>Behaviors</c> setter only successfully attaches a single behavior instance in this
/// codebase's XAML source-generation setup (a second sibling under the same
/// <c>Setter.Value</c> fails to compile: <c>MAUIX2002</c>), and <see cref="Page.Appearing"/> is
/// already this class's own hook — reusing it for a second, independent app-wide effect was less
/// code than introducing a multi-behavior attachment mechanism just to keep the two concerns in
/// separate classes.
/// </summary>
public sealed class PageActivityBehavior : Behavior<Page>
{
    private bool _descendantsAttached;

    protected override void OnAttachedTo(Page bindable)
    {
        base.OnAttachedTo(bindable);
        bindable.Appearing += OnAppearing;
    }

    protected override void OnDetachingFrom(Page bindable)
    {
        bindable.Appearing -= OnAppearing;
        base.OnDetachingFrom(bindable);
    }

    private void OnAppearing(object? sender, EventArgs e)
    {
        NotifyActivity();

        // Deferred to the first Appearing, not OnAttachedTo -- by Appearing time Content is
        // guaranteed fully built; done only once since these pages' content doesn't change shape
        // after construction (each Shell navigation creates a fresh transient page instance, so
        // Appearing firing more than once per instance doesn't happen in practice here anyway).
        if (!_descendantsAttached && sender is Page page)
        {
            _descendantsAttached = true;
            AttachToDescendants(page);
        }

        if (sender is Page fadingPage)
        {
            FadeIn(fadingPage);
        }
    }

    private static async void FadeIn(Page page)
    {
        page.Opacity = 0;
        await page.FadeTo(1, 180, Easing.CubicOut);
    }

    private void AttachToDescendants(IVisualTreeElement node)
    {
        foreach (IVisualTreeElement child in node.GetVisualChildren())
        {
            switch (child)
            {
                case Button button: button.Clicked += OnActivityEvent; break;
                case Entry entry: entry.TextChanged += OnActivityEvent; break;
                case Editor editor: editor.TextChanged += OnActivityEvent; break;
                case Switch toggle: toggle.Toggled += OnActivityEvent; break;
                case Picker picker: picker.SelectedIndexChanged += OnActivityEvent; break;
                case DatePicker date: date.DateSelected += OnActivityEvent; break;
            }

            AttachToDescendants(child);
        }
    }

    private void OnActivityEvent(object? sender, EventArgs e) => NotifyActivity();

    private static void NotifyActivity() =>
        IPlatformApplication.Current?.Services.GetService<IAuthenticationService>()?.NotifyUserActivity();
}
