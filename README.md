# KeyKeeper

Offline-first, zero-knowledge, hardware-backed password/data vault for Android and iOS, built on
.NET MAUI. No network access, no cloud sync, no telemetry — the encrypted vault never leaves the
device.

See [`ARCHITECTURE.md`](ARCHITECTURE.md) for the full phase-by-phase design history (crypto,
storage, auth, item types, organization/search, tools, security hardening, backup, UI polish,
release hardening) and the reasoning behind every non-obvious decision.

## Solution layout

- `KeyKeeper/` — the .NET MAUI app (`net10.0-android`, `net10.0-ios`)
- `KeyKeeper.Core/` — plain `net10.0` class library: crypto, storage, auth, models, tools,
  security, backup. No MAUI/UI/platform references — this is what `KeyKeeper.Core.Tests` exercises.
- `KeyKeeper.Core.Tests/` — xUnit tests for `KeyKeeper.Core`

## Prerequisites

- .NET SDK with the `android` and `ios` workloads (`dotnet workload install android ios`)
- Android: Android SDK + an emulator or device (API 23+); building for `net10.0-ios` compiles the
  managed code but a full device/simulator app bundle needs Xcode on a Mac

## Build

```
dotnet build KeyKeeper.Core/KeyKeeper.Core.csproj
dotnet build KeyKeeper/KeyKeeper.csproj -f net10.0-android
dotnet build KeyKeeper/KeyKeeper.csproj -f net10.0-ios
```

## Test

```
dotnet test KeyKeeper.Core.Tests/KeyKeeper.Core.Tests.csproj
```

## Run (Android emulator/device)

```
dotnet build KeyKeeper/KeyKeeper.csproj -f net10.0-android -t:Run -p:AdbTarget="-s <device-serial>"
```

## Release build (Android)

Release builds enable R8 shrinking/obfuscation automatically (`KeyKeeper.csproj`, Release-only).
To produce a real signed APK/AAB you need your own release keystore — **never commit it or its
password** (`keystore/` is gitignored for exactly this reason):

```
keytool -genkeypair -v -keystore keystore/keykeeper-release.jks -alias keykeeper \
  -keyalg RSA -keysize 2048 -validity 10000

dotnet publish KeyKeeper/KeyKeeper.csproj -c Release -f net10.0-android \
  -p:AndroidPackageFormat=apk \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningKeyStore=<absolute path to your .jks> \
  -p:AndroidSigningKeyAlias=keykeeper \
  -p:AndroidSigningKeyPass=<password> \
  -p:AndroidSigningStorePass=<password>
```

Use `-p:AndroidPackageFormat=aab` instead of `apk` for a Play Store submission bundle. See
`ARCHITECTURE.md`'s "Phase 11: Release hardening" section for what's already been verified about
this build (R8 obfuscation, on-device JSON/reflection safety, performance, signing) and what's
still an open decision before a real store listing (notably: `ApplicationId` is still the MAUI
template default and needs to be changed to a real, owned identifier before first release — that
choice is permanent once shipped).
