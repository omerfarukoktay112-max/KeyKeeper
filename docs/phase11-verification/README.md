# Phase 11 verification evidence

All gathered on the `pixel_7_-_api_36_0` emulator (Android 16, `google_apis_playstore`, x86_64),
against the Release+R8 build, `adb`/`uiautomator`-driven, same methodology as every prior phase's
manual verification (see `ARCHITECTURE.md`).

## Files

- `p11_01_vault_list_r8_release.png` — `adb shell screencap` of the vault list (two real items:
  a Login and a Card, created and reloaded through the R8-obfuscated build). Solid black by
  design: Phase 7's `FLAG_SECURE` blocks screenshot capture of every page this app owns, confirmed
  still active post-R8. Content was instead confirmed via `uiautomator dump`'s accessibility tree
  (unaffected by `FLAG_SECURE`) — see the transcript below.
- `p11_02_new_icon_splash.png` — the OS splash screen showing the new padlock mark. The one screen
  not covered by `FLAG_SECURE` (it renders before `MainActivity.OnCreate` sets the flag), so it's
  the one screenshot in this app that actually shows pixels.

## What was verified and how (see `ARCHITECTURE.md`'s Phase 11 section for full detail)

1. **R8 obfuscation is real, not just configured**: `KeyKeeper/bin/Release/net10.0-android/mapping.txt`
   (~17 MB) was produced by a clean `dotnet publish -c Release -f net10.0-android` with
   `AndroidLinkTool=r8` set. JNI-registered classes (`MainActivity`,
   `BiometricAuthService$AuthenticationCallback`) map to themselves in that file — the SDK's own
   R8 keep rules protect them automatically.

2. **JSON/reflection round-trip survives R8 + the .NET trimmer**: fresh install of the signed
   Release APK → vault setup → created a Login item (title/username/password/TOTP secret) and a
   Card item → navigated away and back to the vault list → both items reappeared correctly
   (`Create` → encrypt → `List` → decrypt → `JsonSerializer.Deserialize<T>` all executed for real,
   post-R8/post-trim). No crash; `adb logcat -d | grep -i "FATAL\|AndroidRuntime: "` showed nothing
   from `com.companyname.keykeeper` across the whole session.

3. **`FLAG_SECURE` still active post-R8**: `p11_01_vault_list_r8_release.png` is solid black,
   the same signature Phase 7-9 documented.

4. **Argon2id timing** (temporary `#if PERF_MEASUREMENT` `Stopwatch`, added and fully reverted —
   see `ARCHITECTURE.md`), via real master-password unlock on this emulator:
   ```
   Argon2id derive: 457ms (cold, first call)
   Argon2id derive: 270ms
   Argon2id derive: 282ms
   ```

5. **Signed artifact is genuine**: `apksigner verify --print-certs` on
   `com.companyname.keykeeper-Signed.apk` reports certificate SHA-256
   `2af9eb7f472263ca95be18cc09b62d953a7ef44d96b19afc481f6e637f4f7c87`, matching `keytool -list`
   against `keystore/keykeeper-release.jks` (not committed) exactly — this is a real release
   signature, not the SDK's auto-generated debug-like fallback.

## Vault-list `uiautomator` text content (post-R8, `FLAG_SECURE` bypassed via accessibility tree)

```
Kasa | Ekle | Klasörler | Şifre Üret | Yedekleme | Kilitle
Ara (isim, kullanıcı adı, URL...)
Tümü | Klasörsüz | Yalnızca favoriler
🔑 GitHub / octocat ☆
💳 Visa / •••• 1111 ☆
```
