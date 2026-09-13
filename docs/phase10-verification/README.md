# Phase 10 verification evidence

Gathered on the `pixel_7_-_api_36_0` emulator (Android 16, `google_apis_playstore`, x86_64),
`adb`/`uiautomator`-driven, same methodology as every prior phase. See `ARCHITECTURE.md`'s
Phase 10 section for full narrative detail — this file is the evidence index.

## Files

- `p10_01_wifi_native_field_test.png` — Android Settings' Wi-Fi "Add network" screen, the native
  target used to confirm `KeyKeeperAutofillService.OnFillRequest` is genuinely invoked by the OS
  (see below).

## What was verified and how

1. **Manifest merge**: `KeyKeeper/obj/Debug/net10.0-android/android/AndroidManifest.xml` contains:
   ```
   <service android:name="crc64c1ca95f6d8b20750.KeyKeeperAutofillService" android:exported="true"
            android:label="KeyKeeper" android:permission="android.permission.BIND_AUTOFILL_SERVICE">
     <intent-filter>
       <action android:name="android.service.autofill.AutofillService" />
     </intent-filter>
   </service>
   ```

2. **KeyKeeper selected as the OS's autofill service**: after picking it via
   `Settings.ActionRequestSetAutofillService`, `adb shell dumpsys autofill` reported:
   ```
   defaultService=com.companyname.keykeeper/crc64c1ca95f6d8b20750.KeyKeeperAutofillService
   ```

3. **`SettingsPage` reflects real OS state**: `uiautomator dump` of the page showed
   `"KeyKeeper şu anda sistemin otomatik doldurma servisi olarak seçili."`

4. **`OnFillRequest` genuinely invoked, with correct analysis** — a temporary
   `#if PERF_MEASUREMENT`-gated `Console.WriteLine` inside `OnFillRequest` (added, observed via
   `adb logcat`, then fully reverted before committing — never shipped) logged, when the Wi-Fi
   "Network name" field (`p10_01_wifi_native_field_test.png`) was focused:
   ```
   [KeyKeeperPerf] OnFillRequest called
   [KeyKeeperPerf] webDomain= fieldsFound=0 username=False password=False
   ```
   Correct: a Wi-Fi network-name field has no web domain and isn't a username/password field, so
   the service correctly found nothing to offer — with zero vault access either way (this field
   was reached while the vault was actually unlocked, but `OnFillRequest`'s own early-return at
   "no web domain, no matching fields" means the unlock-state branch was never even reached).

5. **Chrome did not delegate to the OS Autofill Framework for a real login page, on this
   session** — using the *same* instrumented build that had just logged a real call above,
   navigating Chrome (133.0.6943.137) to `https://github.com/login` (matching a `LoginItem` saved
   with `Url = "https://github.com"` for this test) and focusing the username field produced
   **zero** log output, and `adb shell dumpsys autofill` reported `"No sessions"`. This was
   investigated, not just observed once:
   - `chrome://flags` was searched for a delegation flag; the closest match found was
     `#autofill-third-party-mode-content-provider` ("Enables querying the third party autofill
     mode state from the Chrome app") — toggling it and relaunching Chrome did not change the
     outcome.
   - Chrome's own Settings → Google Password Manager screen showed no visible "use another
     autofill service" toggle on this build.
   - The comparative evidence (native Wi-Fi field: `OnFillRequest` called; Chrome web field, same
     instrumented build, same session: never called) places this specifically as Chrome's own
     delegation behavior on this emulator/Chrome-channel, not a defect in
     `KeyKeeperAutofillService`.
   - **Recommended follow-up**: retry on a physical device or a Chrome channel/version confirmed
     to support third-party autofill delegation, to get a full live "suggestion appears in a real
     browser" demonstration. Until then, the web-domain-matching *logic* itself is verified by the
     28 `AutofillDomainMatcherTests`, and the OS-invocation/field-analysis *mechanism* is verified
     on a native target as above.

6. **No regression to ordinary lock/unlock**: the vault auto-locked mid-session (inactivity
   timeout); unlocking normally navigated back to `VaultListPage` with the previously created
   `LoginItem`s intact, confirming `LockPage.OnUnlocked`'s new `HasPendingRequest` branch correctly
   falls through to the original navigation when there's no pending autofill request.

7. **iOS**: `dotnet build KeyKeeper/KeyKeeper.csproj -f net10.0-ios` — 0 errors, 9 warnings,
   unchanged from Phase 9/11 — confirming this phase's shared-code changes and the permanent no-op
   iOS Autofill implementations don't regress the iOS target.

## Test item used for this verification

Created via the app's own UI (not seeded directly): a `LoginItem` titled "GitHub Test",
username `octocat`, password `AutofillPass1`, URL `https://github.com`.
