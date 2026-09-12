# KeyKeeper — Architecture

Offline-first, zero-knowledge, hardware-backed password/data vault for Android and iOS.

## Solution layout

```
KeyKeeper.slnx
├── KeyKeeper/              # .NET MAUI app (net10.0-android, net10.0-ios)
│   ├── Data/                # (empty — Data/ is now KeyKeeper.Core/Data, see below)
│   ├── Services/            # Platform glue: MauiVaultStoragePathProvider, MauiSecureKeyValueStore
│   ├── ViewModels/          # SetupViewModel, LockViewModel, VaultListViewModel + 5 item form ViewModels (Phase 3/4 UI — done)
│   ├── Views/                # SetupPage, LockPage, VaultListPage + 5 item form pages (Phase 3/4 UI — done)
│   ├── Resources/            # Standard MAUI resource structure (AppIcon, Fonts, Images, Raw, Splash, Styles)
│   └── Platforms/            # Platform-specific entry points (Android/, iOS/) — includes BiometricAuthService.cs per platform
├── KeyKeeper.Core/          # Plain net10.0 class library — pure logic, no MAUI/UI/platform references
│   ├── Auth/                  # Unlock/lock orchestration, lockout policy, inactivity timer (Phase 3 — done)
│   ├── Crypto/               # Key derivation, encryption, master-password verification (Phase 1 — done)
│   ├── Data/                 # Vault schema, SQLCipher connection service, migrations, unlock-metadata store, item repository (Phase 2/4 — done)
│   ├── Storage/               # Wrapped-key storage abstraction for biometric unlock (Phase 2 — done)
│   └── Models/               # Vault item domain models: LoginItem, CardItem, WifiItem, SecureNoteItem, LicenseItem (Phase 4 — done)
└── KeyKeeper.Core.Tests/    # xUnit tests for KeyKeeper.Core (net10.0)
    ├── Auth/
    ├── Crypto/
    ├── Data/
    └── Storage/
```

Note: `KeyKeeper/Data/` (inside the MAUI head project) is intentionally empty — see "Why the storage
layer lives in `KeyKeeper.Core`, not `KeyKeeper/Data`" below. It's kept as a folder rather than
deleted in case a later phase needs MAUI-specific data-layer glue that doesn't belong in Core.

### Why `Core` is a separate project, not just a folder

`KeyKeeper.Core` targets plain `net10.0` (no Android/iOS-specific target), so:
- it cannot accidentally take a dependency on `Microsoft.Maui.Controls` or any platform API —
  the crypto/model logic stays pure and portable;
- it can be unit tested with a normal `dotnet test` run, with no emulator/simulator/device
  needed — running crypto tests against a `net10.0-android` or `net10.0-ios` target directly
  would require an Android emulator or iOS simulator per test run, which is slow and not what
  "unit test" should mean for pure logic like this.

`KeyKeeper` (the MAUI head project) references `KeyKeeper.Core` via `ProjectReference` and
consumes its services through the interfaces (`IKeyDerivationService`, `IEncryptionService`,
`IMasterPasswordVerifier`) — no dependency injection registration exists yet (that's for the
phase that wires this into the app and the SQLCipher-backed `Data/` layer).

## Target frameworks

- `KeyKeeper`: `net10.0-android` (`SupportedOSPlatformVersion` 23.0 — see below) and
  `net10.0-ios` (`SupportedOSPlatformVersion` 15.0). Windows/MacCatalyst were intentionally
  removed — this app ships only to Android and iOS.
- `KeyKeeper.Core` / `KeyKeeper.Core.Tests`: plain `net10.0`.

### Android `SupportedOSPlatformVersion`: 23.0, not the MAUI template default of 21.0

Raised from 21.0 (Phase 3) once the `Xamarin.AndroidX.Biometric` package reference was added to
`KeyKeeper.csproj` for the Android `IBiometricAuthService` implementation (see Phase 3 below).
This isn't a workaround for a build error; it reflects a real platform requirement that was
always there once biometric unlock was in scope:
`androidx.biometric`'s `BiometricPrompt` API itself requires API 23+, so staying on 21 was never
actually viable for this app. The concrete build symptom before this change was a manifest-merge
failure (`AMM0000`/`XAAMM0000`, `uses-sdk:minSdkVersion 21 cannot be smaller than version 23`)
because a transitive `androidx.lifecycle-runtime` version pulled in by
`Xamarin.AndroidX.Biometric` itself declares `minSdk 23`.

### Build status

- `KeyKeeper.Core` and `KeyKeeper.Core.Tests`: `dotnet build` on each — 0 warnings / 0 errors.
  `dotnet test KeyKeeper.Core.Tests` — all tests pass (98 as of Phase 3, 126 as of Phase 4).
- `KeyKeeper` (MAUI head), Android: `dotnet build KeyKeeper/KeyKeeper.csproj -f net10.0-android`
  — 0 errors, including the AndroidManifest merge step. 27 `NU1608` warnings remain (pre-existing,
  unrelated to Phase 3 or the minSdk change): several `Xamarin.AndroidX.*` transitive packages
  resolve to versions outside another package's declared constraint range. Not yet chased down —
  worth revisiting if any of those packages' behavior actually matters at runtime.
- `KeyKeeper` (MAUI head), iOS: `dotnet build KeyKeeper/KeyKeeper.csproj -f net10.0-ios` — 0
  errors, 9 warnings (the same `NU1608` family). This confirms the **managed C# code compiles**
  against `net10.0-ios` (built for the `iossimulator-x64` RID) — it does *not* confirm a full
  device/simulator app bundle builds, since that additionally requires Xcode/a Mac build host,
  unavailable on this Windows machine (same limitation noted under Phase 2's Testing section for
  on-device SQLCipher tests).
- Both platform builds above were re-verified after Phase 3's `BiometricAuthService`
  implementations and the Phase 3 UI (`AppShell`, `App.xaml.cs`, `Views/`, `ViewModels/`) were
  added (same 0 errors, same pre-existing warning set on each platform).
- **Beyond a clean build**: the Android build was actually installed and driven on a running
  emulator this phase — real app launches, real navigation, a real `BiometricPrompt`, a real
  simulated fingerprint touch, all with concrete command/output evidence. See "Manual
  verification — Phase 3 UI on the Android emulator" under Phase 3 below for exactly what was
  run and what it showed; iOS remains compile-only verified (no Mac available).

## Packages

| Package | Version | Project | Purpose |
|---|---|---|---|
| `Konscious.Security.Cryptography.Argon2` | 1.3.1 | `KeyKeeper.Core` | Argon2id key derivation (master password → 256-bit key). |
| `sqlite-net-pcl` | 1.11.285 | `KeyKeeper.Core` | Typed SQLite ORM/data access (`VaultSchema`, `VaultDatabaseService`, `VaultMigrationService`). |
| `SQLitePCLRaw.bundle_e_sqlcipher` | 2.1.11 | `KeyKeeper` | SQLCipher-enabled native SQLite provider for the real app, on Android/iOS only (see Phase 2). |
| `SQLitePCLRaw.bundle_e_sqlite3` | 3.0.5 | `KeyKeeper.Core.Tests` | Plain (non-cipher) native SQLite provider, test-only — see Phase 2's testing section for why. |
| `Otp.NET` | 1.4.1 | `KeyKeeper` | TOTP generation for vault items storing 2FA secrets (later phase). |
| `CommunityToolkit.Mvvm` | 8.4.2 | `KeyKeeper` | Source-generated MVVM (`ObservableProperty`, `RelayCommand`) for `ViewModels/`. |
| `Xamarin.AndroidX.Biometric` | 1.1.0.33 | `KeyKeeper` (Android only) | `BiometricPrompt`/`BiometricManager` for the Android `IBiometricAuthService` implementation (Phase 3). Forced the `SupportedOSPlatformVersion` 21.0 → 23.0 change — see "Target frameworks" above. |
| `Microsoft.Extensions.TimeProvider.Testing` | 10.10.0 | `KeyKeeper.Core.Tests` | `FakeTimeProvider`, for deterministic lockout-expiry/inactivity-timeout tests (Phase 3) without real wall-clock waits. |
| `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `coverlet.collector` | latest via `dotnet new xunit` | `KeyKeeper.Core.Tests` | Unit test framework/runner. |

`Konscious.Security.Cryptography.Argon2` and `sqlite-net-pcl` both live only in `KeyKeeper.Core`
(available to the MAUI head transitively via its `ProjectReference`). `KeyKeeper.Core`
deliberately does **not** reference any `SQLitePCLRaw.bundle_*` package — see "Why the storage
layer lives in `KeyKeeper.Core`" below for why that's a composition-root decision instead.

## Platform permissions

This is a zero-knowledge, offline-first app: it must not have network access, and no
crypto-relevant secret should ever traverse a network boundary.

- **Android** (`KeyKeeper/Platforms/Android/AndroidManifest.xml`): the template-default
  `android.permission.INTERNET` and `android.permission.ACCESS_NETWORK_STATE` permissions were
  **removed**. No `<uses-permission>` entries remain. `android:allowBackup` was also flipped to
  `"false"` (Phase 2) — this alone disables both legacy Auto Backup and, on Android 12+, cloud
  backup/device-to-device transfer, regardless of `dataExtractionRules`, so the SQLCipher vault
  file in private storage can never leave the device via a backup.
- **iOS** (`KeyKeeper/Platforms/iOS/Info.plist`): no network-related entitlements or
  `NSAppTransportSecurity` exceptions are present. Separately (Phase 2), the vault database file
  itself is marked excluded from iCloud backup at the filesystem level — see
  `MauiVaultStoragePathProvider` below.

Any future feature that appears to need network access (e.g. optional encrypted cloud sync)
should be a deliberate, explicit, separately reviewed decision — not something added
incidentally by a dependency or template default.

## Phase 1: Cryptography core (`KeyKeeper.Core/Crypto`)

Pure, UI-independent, unit-tested cryptographic primitives. No database integration, no
dependency injection registration, and no UI exist yet — those are later phases.

### Key derivation — `IKeyDerivationService` / `Argon2idKeyDerivationService`

- Derives a 256-bit key from a master password (`char[]`, never `string`) and a salt, using
  Argon2id (`Konscious.Security.Cryptography.Argon2id`).
- Cost parameters (`MemorySizeKiB`, `Iterations`, `Parallelism`, `OutputLengthBytes`) are
  passed in explicitly via the `Argon2Parameters` record — nothing is hard-coded into the
  service itself.
- `Argon2Constants` documents the default parameters (`Argon2Parameters.Default`):
  **19 MiB memory, 2 iterations, parallelism 1** — the OWASP Password Storage Cheat Sheet's
  "m=19456, t=2, p=1" Argon2id row, chosen (over OWASP's higher-memory 46 MiB row) as a
  mobile-friendly starting point that won't pressure low-end/older phones. This is a
  documented placeholder, not a final answer — calibrate against real device benchmarks in a
  later phase.
- `GenerateSalt(sizeBytes = 16)` produces a cryptographically random salt via
  `RandomNumberGenerator`, at least 16 bytes (`Argon2Constants.MinSaltSizeBytes`), enforced by
  the method.

### Encryption — `IEncryptionService` / `AesGcmEncryptionService`

- AES-256-GCM via `System.Security.Cryptography.AesGcm`.
- Packed output format: **`nonce (12 bytes) || ciphertext || tag (16 bytes)`**, all in one
  `byte[]`; `Decrypt` splits this back apart. Constants `NonceSizeBytes` / `TagSizeBytes` are
  public on `AesGcmEncryptionService`.
- A fresh random 12-byte nonce is generated per `Encrypt` call — nonces are never reused.
- A wrong key or corrupted/tampered ciphertext causes AES-GCM tag verification to fail; this
  is caught and re-thrown as `VaultCryptographyException` (never a silently-wrong plaintext).
  The partially-populated output buffer is zeroed before the exception propagates.

### Master password verification — `IMasterPasswordVerifier` / `MasterPasswordVerifier`

- The master password itself is never stored in any form, anywhere.
- `CreateVerifier(derivedKey)` computes `HMAC-SHA256(key: derivedKey, message: fixed label)` —
  a one-way function of the derived key. The verifier can be persisted safely: it does not let
  an attacker reconstruct `derivedKey` (and therefore not the vault's encryption key either),
  unlike storing the derived key itself.
- `Verify(derivedKey, storedVerifier)` recomputes the verifier and compares it to the stored
  one using `CryptographicOperations.FixedTimeEquals` — constant-time, so failed attempts
  don't leak timing information.

### Memory hygiene — `SecureMemoryHelper`

- `Clear(byte[])` wraps `CryptographicOperations.ZeroMemory` (a JIT-reorder-proof zeroing
  primitive, unlike a plain loop or `Array.Clear`).
- `Clear(char[])` reinterprets the char buffer as bytes (`MemoryMarshal.AsBytes`) and zeroes
  that, since there's no char-native equivalent of `ZeroMemory`.
- `Clear(params byte[]?[])` clears several buffers in one call. All overloads are null/empty-safe.
- Every service above uses `try/finally` around its own intermediate sensitive buffers (e.g.
  the UTF-8 password bytes inside `Argon2idKeyDerivationService`, the computed verifier inside
  `MasterPasswordVerifier.Verify`, the partial plaintext on a failed `Decrypt`). Buffers owned
  by the *caller* (the master password `char[]`, a derived key returned from `DeriveKey`) are
  the caller's responsibility to clear once done — the services never assume ownership of
  something they didn't allocate.
- No sensitive data is ever carried in a `string` anywhere in `KeyKeeper.Core.Crypto` —
  passwords and keys are `char[]`/`byte[]` throughout, since .NET strings are immutable and
  can't be reliably wiped from memory.

### Tests (`KeyKeeper.Core.Tests/Crypto`, xUnit)

27 tests, all passing (`dotnet test KeyKeeper.Core.Tests`). xUnit was chosen over MSTest for
no strong reason beyond it being the more common default for new .NET class libraries —
either would have worked fine here.

Coverage includes, per component:
- **KDF**: same password+salt is deterministic; different salts/passwords produce different
  keys; two `GenerateSalt()` calls never collide; salt-too-short and empty-password validation.
- **Encryption**: correct-key round-trip; wrong-key decrypt throws `VaultCryptographyException`;
  tampering with the ciphertext region or the tag both throw; two `Encrypt` calls on identical
  plaintext produce different nonces and different packed output (but both still decrypt back
  to the original); packed-length invariant; too-short input rejected.
- **Verifier**: matching derived key verifies true, non-matching verifies false, deterministic
  for a fixed key, different keys produce different verifiers, plus two end-to-end tests that
  drive the real KDF → verifier flow for a correct and an incorrect master password.
- **`SecureMemoryHelper`**: byte/char buffers actually get zeroed, multi-buffer overload works,
  null/empty inputs don't throw.

Tests use much cheaper Argon2id parameters than production (`TestArgon2Parameters.Fast`, 8 MiB
/ 1 iteration) purely for test-suite speed — this has no bearing on the documented production
defaults in `Argon2Constants`.

## Phase 2: Storage layer (`KeyKeeper.Core/Data`, `KeyKeeper.Core/Storage`)

Schema and connection infrastructure only — no typed item models, no CRUD, no UI. Builds on
Phase 1's `IKeyDerivationService`/`IEncryptionService`/`IMasterPasswordVerifier` without
depending on them directly (see below).

### Why the storage layer lives in `KeyKeeper.Core`, not `KeyKeeper/Data`

`VaultSchema`, `VaultDatabaseService`, and `VaultMigrationService` all live in
`KeyKeeper.Core/Data` rather than the MAUI head project, for the same testability reason Phase 1
put crypto there: `sqlite-net-pcl` itself has no MAUI/platform dependency, so the only genuinely
platform-specific piece is *where the database file goes* — that's factored out behind
`IVaultStoragePathProvider`, implemented by `MauiVaultStoragePathProvider` in `KeyKeeper/Services`
(private app storage via `FileSystem.AppDataDirectory`, plus the iOS backup-exclusion described
above). `VaultDatabaseService` takes that interface as a constructor dependency and never touches
a MAUI API directly, so it's fully unit-testable with a fake path provider pointed at a temp
directory.

Likewise, **which native SQLite engine actually backs `sqlite-net-pcl` is a composition-root
decision**, not something `KeyKeeper.Core` or `VaultDatabaseService` decides — that project
references `sqlite-net-pcl` only, no `SQLitePCLRaw.bundle_*` package. The app
(`MauiProgram.cs`) calls `SQLitePCL.raw.SetProvider(...)` once at startup, and the test project
does the same with a different provider (see Testing, below). Two platforms, two different
provider classes — see the comment in `MauiProgram.cs`:
- **Android** links `e_sqlcipher` dynamically (a `.so` loaded via P/Invoke) → `SQLite3Provider_e_sqlcipher`.
- **iOS** statically links the same SQLCipher native code into the app binary → the generic
  `SQLite3Provider_internal` binding instead (confirmed against `SQLitePCLRaw.bundle_e_sqlcipher`'s
  own per-TFM dependency groups, which name `provider.internal` for iOS and `provider.e_sqlcipher`
  for Android — this wasn't guessed, it was verified by inspecting the actual package metadata
  and by getting a real `net10.0-ios` compile error/fix cycle).

`IWrappedKeyStorage`/`WrappedKeyStorage` (in `KeyKeeper.Core/Storage`) follow the identical
pattern against `ISecureKeyValueStore`, implemented by `MauiSecureKeyValueStore` in
`KeyKeeper/Services` (wrapping `Microsoft.Maui.Storage.SecureStorage`).

### How the key reaches SQLCipher

`sqlite-net-pcl`'s `SQLiteConnectionString` takes a `key` parameter typed `object`. Passed a
`byte[]` (what `VaultDatabaseService.Open` does with the 256-bit Argon2id-derived key), it's
handed to the native provider's raw `sqlite3_key()` call as-is, rather than being formatted into
a `PRAGMA key = "x'...'"` hex-string statement (what passing a `string` would do instead). This
was confirmed empirically, not assumed: a throwaway reflection probe against the actual installed
`sqlite-net-pcl` 1.11.285 assembly (constructors, `SQLiteException` shape) and two runtime checks
(byte-key round-trip; opening a hand-corrupted file) informed this design and the tests below —
notably, that the plain (non-cipher) provider treats any key as a silent no-op, and that a
corrupted/wrong-key file fails with `SQLiteException { Result = NonDBFile, Message = "file is not
a database" }` — the exact signature SQLCipher itself produces for a wrong key.

`VaultDatabaseService.Open(byte[] key)`:
1. Validates the key is exactly 32 bytes.
2. Returns the existing connection if already open (idempotent).
3. Resolves the path via `IVaultStoragePathProvider`, creates the directory if needed.
4. Opens with `PRAGMA foreign_keys = ON`, then forces an immediate real read
   (`SELECT count(*) FROM sqlite_master`) — SQLite(Cipher) opens lazily, so without this a wrong
   key would only fail confusingly on some later, unrelated query instead of failing loudly here.
5. Runs `VaultSchema.EnsureCreated` (idempotent `CREATE TABLE/INDEX IF NOT EXISTS`) and
   `IVaultMigrationService.EnsureUpToDate`.
6. Any `SQLiteException` from steps 4–5 is caught, the connection is closed, and it's rethrown as
   `VaultDatabaseException` — one clear, documented failure type instead of sqlite-net-pcl's shape.

Note that `VaultDatabaseService` does **not** depend on `IKeyDerivationService` — it takes an
already-derived key. Deriving that key (master-password flow) or fetching it via
`IWrappedKeyStorage` (biometric flow) is the job of a future "vault unlock" orchestration service,
not built yet.

### Schema (`VaultSchema`)

Five tables, all created via idempotent raw SQL DDL (not sqlite-net-pcl's attribute-based
`CreateTable<T>()`, since there are no typed item models yet — see design notes in the file
itself for the full reasoning):
- **`VaultMetadata`**: single-row table (`Id INTEGER PRIMARY KEY CHECK (Id = 1)`) holding
  `SchemaVersion`, `Salt`, the four Argon2 parameters, `MasterPasswordVerifier`, `CreatedAtUtc`.
- **`VaultItems`**: `Id` (TEXT/GUID), `ItemType` (int, see `VaultItemType`), `FolderId`,
  `IsFavorite`, `CreatedAtUtc`/`UpdatedAtUtc`, and `EncryptedPayload` (BLOB — AES-GCM output from
  `IEncryptionService` wrapping type-specific fields as JSON). Deliberately one opaque blob
  column instead of per-type columns, so adding an item type or field never needs a migration.
- **`Folders`**: `Id`, `Name`, `ParentFolderId` (self-referencing FK, nested folders).
- **`Tags`** / **`ItemTags`**: tags table plus a many-to-many join table (composite PK).
- `Folders.Name`/`Tags.Name` are plain columns, not application-layer-encrypted like item
  payloads — intentional, not an oversight: SQLCipher already encrypts the whole database file
  at the page level, so without the database key an attacker can't read *any* table. The extra
  AES-GCM envelope on item payloads exists for a different reason (generic, per-type-schema-free
  storage), not because page-level encryption is insufficient for names.

### Versioning (`IVaultMigrationService` / `VaultMigrationService`)

`ReadSchemaVersion` returns `null` for a not-yet-created vault (no `VaultMetadata` row), or the
stored version. `EnsureUpToDate` is a no-op today (schema version 1 has no predecessor) and
throws `VaultDatabaseException` if the stored version is *newer* than this build supports; the
file has a worked comment showing the exact shape a future `if (version < N) { ... }` migration
step should take.

### Wrapped-key storage (`IWrappedKeyStorage` / `WrappedKeyStorage`)

Two independent ways to obtain the same vault key:
- **Master password flow**: `IKeyDerivationService.DeriveKey` re-derives the key from the
  password + stored salt every time. Nothing persisted for this path.
- **Biometric flow**: the key is fetched from here instead — OS-backed secure storage (Android
  Keystore-backed `SecureStorage` / iOS Keychain). A successful biometric/passcode check is what
  should gate *reading* it back; wiring an actual prompt (`BiometricPrompt` /
  `LocalAuthentication`) in front of `RetrieveAsync` is a later phase — this is just the storage
  primitive that flow will use.

"Wrapped" reflects that the platform secure store itself encrypts the value at rest; this type
doesn't additionally AES-wrap the key before storing it. Base64 is an unavoidable string
intermediate at the `SecureStorage` boundary (its API is string-based) — noted in code, not
hidden.

### Testing, and an important limitation

`SQLitePCLRaw.bundle_e_sqlcipher`'s native binaries are published **only for Android/iOS RIDs**
(confirmed by inspecting the actual NuGet packages: `SQLitePCLRaw.lib.e_sqlcipher.android` and
`.ios` exist; there is no `.win`/`.linux`/`.osx` equivalent, unlike the plain
`SQLitePCLRaw.lib.e_sqlite3`, which ships for every desktop RID). This means **real SQLCipher
encryption/key-rejection cannot run via `dotnet test` on this Windows desktop machine** — there's
no native library for `dotnet test` to load. An Android emulator/device or iOS
simulator/device is required to exercise actual SQLCipher key rejection end-to-end; that's not
part of this repo yet (no `maui-devicetests` project — see below).

Given that, `KeyKeeper.Core.Tests/Data` takes a deliberately honest two-part approach instead of
skipping the requirement or faking a pass:
1. **Real SQLite, plain provider** (`SQLitePCLRaw.bundle_e_sqlcipher` swapped for
   `SQLitePCLRaw.bundle_e_sqlite3`, test-only, via `raw.SetProvider(new SQLite3Provider_e_sqlite3())`
   in `VaultDatabaseTestCollection`'s fixture): schema creation, idempotency (same connection
   twice; a fresh connection to the same file, simulating app relaunch), foreign-key enforcement,
   and the full `VaultMigrationService` read/no-op/reject-future-version logic all run for real.
2. **Key-rejection error path, simulated**: rather than testing "right AES key vs. wrong AES key"
   (impossible without the native cipher library), the test opens a hand-written garbage file.
   This was verified empirically (see "How the key reaches SQLCipher" above) to fail with the
   *exact same* `SQLiteException`/`NonDBFile`/"file is not a database" signature SQLCipher itself
   produces for a wrong key — so `VaultDatabaseService`'s catch-and-wrap-into-`VaultDatabaseException`
   logic is genuinely exercised, even though the underlying AES key check itself is not.

What this does **not** prove: that a *correct* SQLCipher key actually decrypts a real
SQLCipher-encrypted file, or that an *incorrect* one is rejected via the real cipher path. That
guarantee is SQLCipher's own (extensively tested upstream) responsibility, and `Open()`'s wiring
of the key into `SQLiteConnectionString` was verified by direct inspection of the sqlite-net-pcl
API rather than left to guesswork — but it is not exercised end-to-end on-device by this repo yet.
**Recommended follow-up**: add a `dotnet new maui-devicetests` project targeting
`net10.0-android` (an Android emulator, `pixel_7_-_api_36_0`, is already configured on this
machine) with the same three scenarios, run via `dotnet test -f net10.0-android` against a
booted emulator/device. iOS device tests need Xcode/a simulator, unavailable on Windows.

`WrappedKeyStorage` has no such limitation — it only depends on `ISecureKeyValueStore`, so its
tests use an in-memory fake (`InMemorySecureKeyValueStore`) instead of the real
`SecureStorage`/Keystore/Keychain, which is intentional: WrappedKeyStorage's own
wrapping/round-trip logic is what these tests verify, not the OS secure store's own security
guarantees (Google's/Apple's tested responsibility, not this repo's).

### Dependency injection (`MauiProgram.cs`)

Registered (all singletons): `IVaultStoragePathProvider` → `MauiVaultStoragePathProvider`,
`IVaultMigrationService` → `VaultMigrationService`, `IVaultDatabaseService` →
`VaultDatabaseService`, `ISecureKeyValueStore` → `MauiSecureKeyValueStore`, `IWrappedKeyStorage`
→ `WrappedKeyStorage`. Phase 1's crypto services (`IKeyDerivationService`, `IEncryptionService`,
`IMasterPasswordVerifier`) are still **not** DI-registered — scoped out of this phase
deliberately, left for whichever future service orchestrates the actual unlock flow.

## Phase 3: Authentication orchestration (`KeyKeeper.Core/Auth`, `KeyKeeper.Core/Data`)

Ties Phase 1's crypto (`IKeyDerivationService`, `IMasterPasswordVerifier`) and Phase 2's storage
(`IVaultDatabaseService`, `IWrappedKeyStorage`) together into one place — first-run setup,
master-password login, biometric login, persisted progressive lockout, and inactivity auto-lock
— so ViewModels don't have to orchestrate all four themselves. No ViewModels/XAML consume it yet.

### `IAuthenticationService` / `AuthenticationService`

Single entry point for unlocking/locking the vault:

- **`InitializeVaultAsync(char[] masterPassword)`** — first-run setup only (throws
  `InvalidOperationException` if already initialized). Generates a fresh salt, derives a key with
  `Argon2Parameters.Default`, computes and stores the master-password verifier via
  `IVaultUnlockStore`, then opens the vault database with that key — the vault ends up unlocked
  immediately, so the user isn't asked to log in again right after setup.
- **`UnlockWithMasterPasswordAsync(char[] masterPassword)`** — re-derives the key from the stored
  salt/parameters, verifies it, and opens the database on success. Subject to lockout (see
  `LockoutPolicy` below): a locked-out call returns `AuthenticationResultKind.LockedOut`
  *without deriving anything* — the whole point of lockout is to stop attackers before they pay
  the (deliberately slow) Argon2id cost, so `CheckLockout` runs before the KDF, not after.
  If the verifier matches but `IVaultDatabaseService.Open` itself throws
  `VaultDatabaseException` (corruption, not a wrong password), that exception is left to
  propagate rather than being reported as `WrongPassword` — the caller needs to know this isn't
  a password problem.
- **`TryUnlockWithBiometricsAsync()`** — checks `IWrappedKeyStorage` for a stored key, checks
  `IBiometricAuthService.CheckAvailabilityAsync`, then prompts via
  `IBiometricAuthService.AuthenticateAsync`. Deliberately does **not** touch the master-password
  lockout counter on cancellation/failure — a declined biometric prompt isn't a wrong-password
  guess, so it shouldn't count toward locking the user out of their own master password.
- **`EnableBiometricUnlockAsync()`** — clones the currently-held vault key and stores it via
  `IWrappedKeyStorage`. Throws `InvalidOperationException` if the vault isn't currently unlocked.
- **`Lock()`** — stops the inactivity timer, zeroes the in-memory key
  (`SecureMemoryHelper.Clear`), and closes the database connection. Safe to call when already
  locked (used both for explicit user-initiated lock and the inactivity timeout below).
- **`AutoLockTimeout`** (default 2 minutes) / **`NotifyUserActivity()`** — the inactivity window
  and the call ViewModels make on user interaction to postpone it.
- **`event Action? Locked`** — raised at the end of `Lock()`, but only when the vault actually
  transitioned from unlocked to locked (not on a redundant `Lock()` call while already locked).
  Added specifically for the UI layer (below): it's the one signal that covers *every* path that
  can lock the vault, including the inactivity timer firing on its own with no UI-initiated call
  site — without it, nothing would tell a visible `VaultPlaceholderPage` that it needs to
  navigate back to the lock screen when the timeout fires while the user is just sitting there.
  Raised outside the internal lock (`_gate`) and, for the inactivity-timer path, on whatever
  background thread `TimeProvider.CreateTimer`'s callback runs on — not the UI thread — so
  subscribers that touch UI must marshal onto it themselves (see `App.xaml.cs` below).

`AuthenticationService` takes an optional `TimeProvider` (defaults to `TimeProvider.System`) so
lockout-expiry and inactivity-timeout logic can be driven by a fake clock in tests instead of
real wall-clock waits.

### Lockout policy (`LockoutPolicy`)

A progressive schedule, escalating with consecutive wrong-password attempts — same "documented,
tunable placeholder" spirit as `Argon2Constants` (Phase 1), calibrate once there's real-world
data on legitimate-user vs. attacker impact:

| Consecutive failures | Lockout duration |
|---|---|
| 3 | 5 seconds |
| 5 | 30 seconds |
| 8 | 2 minutes |
| 12 | 15 minutes |

The failure count and lockout expiry are persisted (`LockoutState`, via `IVaultUnlockStore`), not
kept in memory — otherwise force-closing and relaunching the app would silently reset the clock
for an attacker mid-lockout.

### Inactivity auto-lock (`IInactivityTimerService` / `InactivityTimerService`)

A one-shot, restartable countdown built on `TimeProvider` (not `System.Threading.Timer`
directly) specifically so tests can advance a `Microsoft.Extensions.Time.Testing.FakeTimeProvider`
instead of waiting on real time. `AuthenticationService` starts it on every successful
unlock/initialize, calls `Reset()` on `NotifyUserActivity()`, and its `TimedOut` event is wired
straight to `Lock()`. Distinct from an instant lock-on-background, which is a separate concern
(`App.OnSleep`, not built yet).

### Biometric prompt (`IBiometricAuthService`)

Deliberately kept free of any vault/key logic: it only answers "is biometric hardware available"
and "did the user just authenticate". `AuthenticationService` is what turns a
`BiometricAuthOutcome.Success` into actually unlocking the vault (via the already-stored
`IWrappedKeyStorage` key, not something `IBiometricAuthService` itself touches).

Two implementations, one per platform, both named `KeyKeeper.Services.BiometricAuthService` —
each lives under its own `Platforms/<Android|iOS>/BiometricAuthService.cs`, and since the MAUI
SDK only compiles a given `Platforms/<X>/` folder into that platform's `TargetFramework`, exactly
one of the two classes is ever part of a build; `MauiProgram.cs` registers the interface once,
unconditionally, the same way `MauiVaultStoragePathProvider`/`MauiSecureKeyValueStore` already do.

#### Android (`Platforms/Android/BiometricAuthService.cs`) — `AndroidX.Biometric`

- `CheckAvailabilityAsync` calls `BiometricManager.From(Application.Context).CanAuthenticate(...)`
  — only needs an application `Context`, no foreground `Activity`, so it never shows UI. Requests
  `BiometricManager.Authenticators.BiometricStrong` only (Class 3 — TEE/StrongBox-backed
  fingerprint/face/iris), never the weaker `BIOMETRIC_WEAK`, since this gates the vault's
  decryption key.
- `AuthenticateAsync` needs a live `FragmentActivity` to host `BiometricPrompt` (it's backed by a
  `DialogFragment`). Obtained via `Microsoft.Maui.ApplicationModel.Platform.CurrentActivity as
  FragmentActivity` — defensively: if that's `null` (no foreground activity right now) or not a
  `FragmentActivity` (it always is in practice, since `MainActivity : MauiAppCompatActivity`,
  which extends `AppCompatActivity`, which extends `FragmentActivity` — but the cast doesn't
  *assume* that), the method returns `BiometricAuthOutcome.NotAvailable` immediately rather than
  throwing or crashing.
- Constructing the `BiometricPrompt` and calling `Authenticate()` are both wrapped in
  `MainThread.BeginInvokeOnMainThread(...)`. This isn't defensive boilerplate — it's a fix for a
  real crash hit during manual verification (see below): `BiometricPrompt` internally drives the
  `FragmentManager` and throws `IllegalStateException: Must be called from main thread of
  fragment host` if it isn't. `AuthenticationService.TryUnlockWithBiometricsAsync` awaits
  `IWrappedKeyStorage.RetrieveAsync()` before calling here, and that await's continuation isn't
  guaranteed to resume on the UI thread — it didn't, reliably, in testing — so this dispatch is
  required, not optional.
- `BiometricPrompt.AuthenticationCallback.OnAuthenticationError`'s Android error codes map onto
  `BiometricAuthOutcome` as:

  | Android error code | `BiometricAuthOutcome` |
  |---|---|
  | `ErrorUserCanceled`, `ErrorNegativeButton`, `ErrorCanceled` | `Cancelled` |
  | `ErrorNoBiometrics`, `ErrorHwNotPresent`, `ErrorHwUnavailable`, `ErrorSecurityUpdateRequired`, `ErrorNoDeviceCredential` | `NotAvailable` |
  | `ErrorLockout`, `ErrorLockoutPermanent`, everything else | `Failed` |

  `ErrorLockout`/`ErrorLockoutPermanent` (too many wrong biometric attempts) map to `Failed`, not
  `NotAvailable`: the hardware itself is fine, this specific attempt failed. `OnAuthenticationFailed`
  (a single non-matching scan, e.g. wrong finger) is **not** treated as terminal — `BiometricPrompt`
  keeps its sheet open for the user to retry, so that callback doesn't complete the result task at
  all; only `OnAuthenticationError` or `OnAuthenticationSucceeded` do.

#### iOS (`Platforms/iOS/BiometricAuthService.cs`) — `LocalAuthentication`

- Uses `LAContext` with `LAPolicy.DeviceOwnerAuthenticationWithBiometrics` specifically (Face
  ID/Touch ID only) — deliberately **not** `LAPolicy.DeviceOwnerAuthentication`, which would
  silently fall back to the device passcode. That would make "biometric unlock" secretly become
  "passcode unlock" without `AuthenticationService` (or the user) knowing the difference,
  breaking the two-independent-unlock-paths design described above. This keeps Android and iOS
  symmetric: both request their platform's strongest biometric-only class, no credential fallback.
- `CheckAvailabilityAsync` and the pre-check inside `AuthenticateAsync` both call
  `LAContext.CanEvaluatePolicy`, which — unlike Android's `BiometricManager.CanAuthenticate` —
  can itself report `LAStatus.BiometryLockout` before any prompt is shown. Since
  `BiometricAvailability` has no dedicated "temporarily locked" case and Android's pre-check
  can't distinguish it either, both platforms fold that into "not currently usable"
  (`NotSupported` for iOS's pre-check).
- `EvaluatePolicyAsync`'s `NSError`/`LAStatus` failures map onto `BiometricAuthOutcome`
  symmetrically with Android's table above:

  | `LAStatus` | `BiometricAuthOutcome` |
  |---|---|
  | `UserCancel`, `AppCancel`, `SystemCancel`, `UserFallback` | `Cancelled` |
  | `BiometryNotAvailable`, `BiometryNotEnrolled`, `PasscodeNotSet` | `NotAvailable` |
  | `BiometryLockout`, everything else (`AuthenticationFailed`, `InvalidContext`, ...) | `Failed` |

- **Requires `NSFaceIDUsageDescription` in `Info.plist`** (added this phase) — without it, iOS
  doesn't show an error or throw; it just silently fails every Face ID request, which would have
  made `AuthenticateAsync` mysteriously always return a failure outcome on Face ID devices with
  no obvious cause. Touch ID doesn't need this key, but Face ID does, unconditionally.

#### Why neither side has (or can have) an xUnit test

Both classes are thin adapters over real OS APIs (`BiometricPrompt`, `LAContext`) that don't
exist outside a running Android/iOS process — there's no in-process way to unit test "did a real
fingerprint scan succeed" any more than Phase 2 could unit test real SQLCipher key rejection
without a device (see that Testing section). The thing that *can* be unit tested — does
`AuthenticationService` call `IBiometricAuthService` correctly and react correctly to each
`BiometricAuthOutcome`, including that a cancelled/failed biometric attempt never touches the
master-password lockout counter — *is* tested, against a fake `IBiometricAuthService`, in
`AuthenticationServiceTests` (Phase 3, above). The Android side's actual native behavior *was*
additionally verified for real, manually, on an emulator — see "Manual verification — Phase 3 UI
on the Android emulator" below, including a real bug this verification caught and fixed
(`BiometricPrompt` needing an explicit UI-thread dispatch). The iOS side remains unverified beyond
compiling — no Mac available in this environment.

### Unlock metadata storage (`IVaultUnlockStore` / `VaultUnlockStore`, `KeyKeeper.Core/Data`)

Backs `AuthenticationService`'s KDF salt/parameters/verifier and the persisted `LockoutState`
with their own small, deliberately **unencrypted** SQLite file — separate from the
SQLCipher-encrypted vault database (`VaultDatabaseService`). This isn't an oversight: SQLCipher
requires the correct key before it will read *anything* from an encrypted database, including a
hypothetical metadata row holding the very salt needed to derive that key — an unresolvable
chicken-and-egg problem. A KDF salt and a verification value aren't secrets in the way the
derived key or the password are (see `MasterPasswordVerifier`'s doc comment), so storing them
unencrypted is standard practice, not a weakened design. The file still lives in private,
non-backed-up app storage (`IVaultStoragePathProvider.GetUnlockMetadataFilePath`).

Implements `IDisposable` (closes its connection), matching `VaultDatabaseService`'s pattern —
added during Phase 3 test-writing after `VaultUnlockStoreTests`' cleanup revealed the connection
was otherwise never closed.

### Phase 3 UI (`KeyKeeper/Views`, `KeyKeeper/ViewModels`, `AppShell`, `App.xaml.cs`)

Wires `IAuthenticationService` into an actual, navigable app — the MAUI template's default
`MainPage` (a counter button) is gone; `AppShell` now routes between three mutually-exclusive
full-screen states based on vault lock state. This is deliberately still thin: no vault item
CRUD, folder/tag management, or search — that's Phase 4's scope, not touched here.

#### `AppShell` — three routes, and a routing constraint learned the hard way

```xml
<ShellContent Route="setup" ContentTemplate="{DataTemplate views:SetupPage}" />
<ShellContent Route="lock" ContentTemplate="{DataTemplate views:LockPage}" />
<ShellContent Route="vault" ContentTemplate="{DataTemplate views:VaultPlaceholderPage}" />
```

All three are declared as real `ShellContent` (each becomes its own implicit
`ShellItem`/`ShellSection`), not just two of them with the third pushed onto whichever page
happened to be declared. The first version of this file declared only `"lock"` in XAML and
registered `"setup"`/`"vault"` purely via `Routing.RegisterRoute(string, Type)` (no `ShellContent`
backing them) — reachable only by pushing onto an existing page stack, not as the entire stack.
`App.xaml.cs`'s `GoToAsync("//setup")` on a fresh install crashed immediately with `System.
Exception: Global routes currently cannot be the only page on the stack, so absolute routing to
global routes is not supported.` (confirmed via `adb logcat`, see below) — an absolute route needs
a real `ShellContent` to land on. `FlyoutBehavior="Disabled"` hides the hamburger menu that three
top-level `ShellItem`s would otherwise imply; no tab bar appears either, since tabs are a
same-`ShellSection` construct, not something three separate `ShellItem`s trigger.

#### `App.xaml.cs` — decides the initial route, and re-routes to `lock` on any lock event

`App` takes `IAuthenticationService` via constructor injection (MAUI resolves `App` itself
through the DI container). `CreateWindow` creates the `Window`/`AppShell` and, once
`Window.Created` fires, navigates to `//setup` (no `VaultMetadata` yet), `//lock` (initialized but
locked — the actual case on every real cold start, since nothing has unlocked the freshly
DI-constructed `AuthenticationService` yet), or `//vault` (already unlocked — logically possible,
practically dead code at real launch, kept for completeness). Separately, `App`'s constructor
subscribes to `IAuthenticationService.Locked` and navigates to `//lock` from there, marshalled via
`MainThread.BeginInvokeOnMainThread` since that event can fire off the UI thread (the inactivity
timer). This is the **single place** that decides "the vault just locked, show the lock screen" —
both the explicit "Kilitle" button on `VaultPlaceholderPage` and the inactivity auto-lock timer
funnel through `AuthenticationService.Lock()` → the `Locked` event → here, rather than each having
its own separate navigation call.

#### `SetupPage` / `SetupViewModel`

Master password entered twice (`Password`/`ConfirmPassword`), a minimum length of 8
(`SetupViewModel.MinimumPasswordLength` — a documented placeholder like `Argon2Constants`, not a
real strength check; that's the later "Araçlar" phase), and a match check, before calling
`IAuthenticationService.InitializeVaultAsync`. On success, raises a `Completed` event (kept out of
the ViewModel to avoid a Shell/navigation dependency there) that the page's code-behind turns into
`GoToAsync("//vault")`. The `char[]` handed to `InitializeVaultAsync` is cleared via
`SecureMemoryHelper.Clear` in a `finally` block; the bound `Password`/`ConfirmPassword` *strings*
themselves can't be reliably wiped (MAUI's `Entry.Text` is string-based) — same acknowledged
string-boundary tradeoff as `WrappedKeyStorage`'s Base64 comment, noted here rather than hidden.

#### `LockPage` / `LockViewModel`

- Master-password `Entry` + "Kilidi Aç" button → `UnlockWithMasterPasswordAsync`.
- `OnAppearing` calls `IBiometricAuthService.CheckAvailabilityAsync` to decide whether to show a
  "Biyometrik ile Aç" button at all — it only appears when biometrics are genuinely usable right
  now, not merely enrolled-in-principle.
- On `WrongPassword`/`LockedOut` results that carry a `RemainingLockout`, starts a
  one-second `IDispatcherTimer` that live-updates a `mm:ss` countdown label and disables both
  inputs (`AreInputsEnabled`) until it reaches zero — a fresh relaunch mid-lockout won't show this
  countdown before the user's first attempt, since `IAuthenticationService` deliberately doesn't
  expose raw lockout state outside an actual unlock attempt (see its doc comment); this only
  reacts to what an attempt's `AuthenticationResult` reports.
- A successful password or biometric unlock raises an `Unlocked` event → `GoToAsync("//vault")`,
  same pattern as `SetupPage`'s `Completed`.

#### `VaultPlaceholderPage` / `VaultPlaceholderViewModel`

The Phase 4 stand-in: "Kasa açıldı", a "Kilitle" button (`AuthenticationService.Lock()` — no
direct navigation call here; see `App.xaml.cs` above for why), and — added specifically to make
the biometric flow reachable at all for manual verification — a "Biyometrik Kilidi Etkinleştir"
button (`EnableBiometricUnlockAsync`) shown whenever biometrics are available. Without this,
`LockPage`'s biometric button would exist but could never actually succeed: `TryUnlockWithBiometricsAsync`
short-circuits to `BiometricUnavailable` whenever `IWrappedKeyStorage` has never had a key stored
into it, and nothing else in this phase's scope calls `EnableBiometricUnlockAsync`. This one small
addition is why "biometric unlock actually works" was verifiable at all this phase — confirmed by
testing the *absence* of this button's effect too (see below: tapping "Biyometrik ile Aç" before
ever visiting this button correctly reports "kullanılamıyor", no OS prompt shown).

Pages take their ViewModel via constructor injection and set it as `BindingContext` themselves;
DI registration for all three page/ViewModel pairs is covered under "Dependency injection" near
the end of this Phase 3 section, alongside the backend registrations.

### Manual verification — Phase 3 UI on the Android emulator

Every scenario below was actually driven on a running `pixel_7_-_api_36_0` emulator (Android 16,
`google_apis_playstore` image) via `adb`/`uiautomator`, not inferred from a clean build. UI
automation used `adb shell input tap <x> <y>` against coordinates read from
`adb shell uiautomator dump` (an accessibility-tree XML dump — not a screenshot; the OS biometric
enrollment and `BiometricPrompt` screens set `FLAG_SECURE`, which blocks `screencap` and renders
solid black, so the dump's text content is the real evidence for those specific steps), with
`adb shell screencap` for the ordinary app screens.

**Setup**:
1. `adb shell locksettings set-pin 1234` — a device credential is a precondition for enrolling a
   fingerprint at all; set once, headlessly, no UI needed.
2. `dotnet build KeyKeeper/KeyKeeper.csproj -f net10.0-android -t:Run -p:AdbTarget="-s emulator-5554"`
   — builds, installs, and launches the app on the emulator in one step.

Screenshots and the crash log excerpt referenced below are checked into
`docs/phase3-verification/` in this repo.

**Scenario 1 — first-run setup creates the vault**: filled both password fields
(`Test1234pass`/`Test1234pass`) on `SetupPage` (`01-setup-page.png`), tapped "Kasayı Oluştur".
Result: navigated to `VaultPlaceholderPage` (`02-vault-after-setup.png`), `uiautomator dump`
showing `text="Kasa açıldı"`.

**Scenario 2 — explicit lock**: tapped "Kilitle" on `VaultPlaceholderPage`. Result: navigated back
to `LockPage` (`03-lock-page.png`), dump showing `text="Kasa Kilitli"`.

**Scenario 3 — wrong password / progressive lockout**: three consecutive wrong-password
submissions. Result, confirmed by screenshot (`04-lockout-countdown.png`): `"Yanlış şifre (3.
yanlış deneme)."` in red, a live `"Kalan süre: 00:03"` countdown, and both the password field and
"Kilidi Aç" button visibly disabled. After the countdown reached zero, the correct password was
accepted and unlocked the vault (`"Kasa açıldı"` again) — confirming the lockout state (and the
failed-attempt counter behind it) was actually reset on success, not just that the UI timer
expired.

**Scenario 4 — biometric unlock, including the negative case**: with a fingerprint not yet
enrolled, `LockPage` correctly did **not** show a "Biyometrik ile Aç" button at all (`Check
AvailabilityAsync` reporting not-available). A fingerprint was then enrolled for real, through the
actual OS wizard, entirely via `adb`/`uiautomator` (Settings → Security & privacy → Device unlock
→ Pixel Imprint → PIN re-entry → "Touch the sensor" → `adb emu finger touch 1` × 12 → "Fingerprint
added"). Back in the app: tapping "Biyometrik ile Aç" **before** ever tapping "Biyometrik Kilidi
Etkinleştir" correctly reported `"Biyometrik kimlik doğrulama şu anda kullanılamıyor."` with **no**
OS prompt shown (`IWrappedKeyStorage` had no key yet — `TryUnlockWithBiometricsAsync`'s
short-circuit, verified for real). After unlocking with the password and tapping "Biyometrik
Kilidi Etkinleştir" (confirmed by `"Biyometrik kilit açma etkinleştirildi."`), locking, and tapping
"Biyometrik ile Aç" again: this **crashed** the app the first time —
`java.lang.IllegalStateException: Must be called from main thread of fragment host` at
`androidx.biometric.BiometricPrompt.authenticate`, captured via `adb logcat` down to the full
managed stack trace (`KeyKeeper.Services.BiometricAuthService.AuthenticateAsync` →
`AuthenticationService.TryUnlockWithBiometricsAsync` → `LockViewModel.UnlockWithBiometricsAsync`)
— full excerpt in `docs/phase3-verification/biometric-thread-crash-logcat-excerpt.txt`. Fixed
with the `MainThread.BeginInvokeOnMainThread` wrap described above; rebuilt, redeployed, retried:
`mCurrentFocus` reported the real system `Window{... BiometricPrompt}`, showing the correct
title/subtitle/cancel text ("Kimlik Doğrulama" / "Kasayı açmak için kimliğinizi doğrulayın" /
"İptal"). `adb emu finger touch 1` against that live prompt → the app returned to
`VaultPlaceholderPage` (`"Kasa açıldı"`), confirmed both via `uiautomator dump` and a screenshot
(`05-biometric-unlocked.png` — an ordinary app screen once past the secure system dialog).

**What this does and doesn't prove**: real device biometric hardware behavior (fingerprint
quality, sensor timing, permanent-lockout thresholds) is Android's own tested responsibility, not
re-proven here — what *was* proven is that this app's specific integration (threading,
error-code mapping, the `IWrappedKeyStorage`-gated short-circuit, the full
setup → lock → wrong-password-lockout → correct-password → enable-biometric →
lock → biometric-unlock round trip) genuinely works end-to-end on a real emulator, not just "looks
right in the code". iOS has no equivalent verification — no Mac/Xcode available in this
environment; `LAContext`/Face ID/Touch ID behavior is unverified beyond the managed code
compiling against `net10.0-ios`.

### Tests (`KeyKeeper.Core.Tests/Auth`, `.../Data/VaultUnlockStoreTests.cs`)

98 tests in the suite as of Phase 3 (`dotnet test KeyKeeper.Core.Tests`), all passing (95 from the
backend work, plus 3 covering the `Locked` event added for the UI layer:
`Lock_WhenUnlocked_RaisesLockedEvent`, `Lock_WhenAlreadyLocked_DoesNotRaiseLockedEvent`,
`InactivityTimeout_AlsoRaisesLockedEvent`). New this phase, across four files:

- **`LockoutPolicyTests`**: tier-boundary coverage for `GetLockoutDuration`.
- **`InactivityTimerServiceTests`**: start/reset/stop, timeout firing, restart-from-running — all
  driven by `FakeTimeProvider.Advance`, no real waiting.
- **`AuthenticationServiceTests`**: init (including double-init rejection), correct/wrong
  password, lockout escalation through all tiers and expiry, that lockout blocks even a
  *correct* password without deriving, verifier-matches-but-database-fails propagation,
  biometric unlock's three outcomes (unavailable/cancelled/success) including that a cancelled
  biometric prompt leaves the password-lockout counter untouched, enabling biometric unlock,
  explicit lock, and inactivity auto-lock (including that `NotifyUserActivity` postpones it).
  Uses fakes for `IVaultUnlockStore`/`IVaultDatabaseService`/`IBiometricAuthService` (no real
  SQLite/hardware involved — that's Phase 2's and the not-yet-written biometric
  implementation's own testing responsibility) but the *real* `Argon2idKeyDerivationService` /
  `MasterPasswordVerifier` / `WrappedKeyStorage` / `InactivityTimerService`, matching the
  established "prefer real cheap components over mocks" style from Phase 2's
  `WrappedKeyStorageTests`.
- **`VaultUnlockStoreTests`**: real (plain-provider) SQLite, same pattern as
  `VaultDatabaseServiceTests` — round-tripping metadata/lockout state, overwrite semantics, and
  visibility across a fresh instance on the same file (simulating app relaunch).

### Dependency injection (`MauiProgram.cs`)

Added this phase, all singletons: `IVaultUnlockStore` → `VaultUnlockStore`,
`IInactivityTimerService` → `InactivityTimerService`, `IKeyDerivationService` →
`Argon2idKeyDerivationService`, `IMasterPasswordVerifier` → `MasterPasswordVerifier`,
`IBiometricAuthService` → `BiometricAuthService` (per-platform, see above), and
`IAuthenticationService` → `AuthenticationService`. `AuthenticationService`'s constructor also
takes an optional `TimeProvider` (defaults to `TimeProvider.System` when not supplied); no
`TimeProvider` is separately registered, so the container resolves it via that default rather
than a DI registration.

Also added, all transient (see "Phase 3 UI" above for why transient, not singleton):
`SetupViewModel`/`SetupPage`, `LockViewModel`/`LockPage`,
`VaultPlaceholderViewModel`/`VaultPlaceholderPage`.

## Phase 4: Item CRUD (`KeyKeeper.Core/Models`, `KeyKeeper.Core/Data`, `KeyKeeper/Views`, `KeyKeeper/ViewModels`)

Fills in the JSON shape inside `VaultItems.EncryptedPayload` (schema-only since Phase 2) with five
typed item models, a repository to CRUD them, and a real "logged in" UI: `VaultListPage` replaces
the Phase 3 `VaultPlaceholderPage` stand-in, backed by five per-type add/edit form pages. No
folders, tags, or search UI this phase — see "out of scope" below.

### Item models (`KeyKeeper.Core/Models`)

Five plain, `System.Text.Json`-serializable records, each implementing `IVaultItemPayload`
(`static abstract VaultItemType ItemType` + a `Title` every type has):
- **`LoginItem`**: `Title`, `Username`, `Password`, `Url`, `TotpSecret` (stored Base32, not yet
  turned into a live TOTP code — that's the later "Araçlar" phase).
- **`CardItem`**: `Title`, `CardholderName`, `CardNumber`, `ExpiryMonth`/`ExpiryYear` (separate
  ints, not a formatted "MM/YY" string, so expiry checks never need to parse one), `Cvv`,
  `CardType` (enum: Visa/Mastercard/American Express/Discover/Diğer).
- **`WifiItem`**: `Title`, `Ssid`, `Password` (nullable — `Open` networks legitimately have none),
  `SecurityProtocol` (enum), `IsHidden`.
- **`SecureNoteItem`**: `Title`, `Content` — the only type with no single "the secret" field
  singled out for masking; the whole point of a note is that `Content` itself is sensitive.
- **`LicenseItem`**: `Title`, `LicenseKey`, `SoftwareName`, `ExpiresAtUtc` (nullable — perpetual
  licenses never expire).

Encryption is one level up, in the repository, not these types' concern — see `IVaultItemPayload`'s
doc comment for why individual sensitive `string` fields here (passwords, CVVs, license keys, ...)
aren't independently wiped: the same acknowledged string-boundary tradeoff as `WrappedKeyStorage`'s
Base64 comment and `SetupViewModel.Password`.

### `IAuthenticationService.EncryptWithVaultKey`/`DecryptWithVaultKey` — new this phase

Before this phase, `AuthenticationService` only used the vault key internally (deriving it,
handing it to `IVaultDatabaseService` to open SQLCipher). `IVaultItemRepository` needs to encrypt
arbitrary item-payload JSON under that same key, but the raw key itself should never leave
`AuthenticationService` — so two new methods were added to encrypt/decrypt *through* it instead of
exposing the key: `EncryptWithVaultKey(byte[])`/`DecryptWithVaultKey(byte[])`, both throwing
`InvalidOperationException` when the vault is locked. Key custody stays exactly as narrow as it
already was for Phases 1-3; only what can be done *with* the key got wider.

### Repository — `IVaultItemRepository` / `VaultItemRepository` (`KeyKeeper.Core/Data`)

CRUD (`Create<TPayload>`/`Get`/`List`/`Update<TPayload>`/`Delete`) plus favorite/folder/tag
management (`SetFavorite`, `SetFolder`, `AddTag`/`RemoveTag`) against the `VaultItems`/`Tags`/
`ItemTags` tables `VaultSchema` already created in Phase 2. Synchronous, not `Task`-returning,
matching every other Data-layer type — these are local SQLite calls, not the kind of slow I/O that
warrants async.

- **Generic over `TPayload : IVaultItemPayload`**: `Create`/`Update` take the payload's own CLR
  type, reading `TPayload.ItemType` (the static abstract member) to know what to store in the
  `ItemType` column — callers can't pass a payload that disagrees with itself about its type.
  `Update` additionally throws `ArgumentException` if the payload's type doesn't match the
  *existing* stored item's type (an item's type is immutable after creation) and
  `KeyNotFoundException` if the id doesn't exist.
- **`VaultItemRecord`**: the decrypted return shape — `Payload` is boxed as `object` (not generic),
  because `List()` returns a heterogeneous mix of item types in one call; callers pattern-match on
  `ItemType` to know which concrete type `Payload` actually is.
- **Memory hygiene**: `EncryptPayload` serializes straight to a `byte[]` via
  `JsonSerializer.SerializeToUtf8Bytes` (never materializing an intermediate JSON `string` at all),
  encrypts it, then zeroes the buffer in a `finally`; `DecryptPayload` mirrors this on the way back
  out. The one buffer holding the entire plaintext payload exists only as long as it takes to
  encrypt/deserialize it.
- **One opaque `EncryptedPayload` BLOB column**, not per-type columns (a Phase 2 schema decision) —
  adding a new item type or field to an existing one never needs a migration.

### UI — `VaultListPage` replaces `VaultPlaceholderPage`, five item form pages (`KeyKeeper/Views`, `KeyKeeper/ViewModels`)

`AppShell`'s `"vault"` route now points at `VaultListPage` (previously `VaultPlaceholderPage`,
which is deleted). The five form pages are deliberately **not** declared as `ShellContent` —
nothing navigates to them absolutely; they're only ever pushed relatively on top of `"vault"`
(`GoToAsync("login-form")` or `"login-form?id=..."`) from `VaultListViewModel`, registered as
plain global routes in `AppShell.xaml.cs`'s constructor (`Routing.RegisterRoute`).

#### `VaultListPage` / `VaultListViewModel`

Keeps the two lock-lifecycle responsibilities the Phase 3 placeholder had (the "Kilitle" button →
`AuthenticationService.Lock()`; "Biyometrik Kilidi Etkinleştir" → `EnableBiometricUnlockAsync`,
still needed so `LockPage`'s biometric button has a stored key to use at all) and adds the real
vault content: a type-filtered-free `CollectionView` of every item (newest-updated first, via
`IVaultItemRepository.List()`), an "Ekle" toolbar item that opens a `DisplayActionSheet` with the
five item types, and tap-to-open on any row. `InitializeAsync` (called from `OnAppearing`) reloads
both the biometric-button visibility and the item list every time the page becomes visible again —
including on returning from a form page after a create/edit/delete, not just on first load. No
search, folder/tag filtering UI, or favorites toggle here — `IVaultItemRepository` already supports
that CRUD; only the UI for it is deferred (see "out of scope" below).

`VaultItemListEntry` flattens a decrypted `VaultItemRecord` into one title/subtitle/icon/color per
row (computed once via `VaultItemListEntry.From`, instead of the list's `DataTemplate` pattern-
matching `Payload` itself): a card's subtitle masks its number to `•••• 1234` even in the list, not
just its own form; a note's subtitle is always the literal "Güvenli not", never its content. Icons
are a single emoji + flat color per type (🔑/💳/📶/📝/🔖) — a placeholder pairing, not a design pass.

#### `VaultItemFormViewModelBase<TPayload>` — shared add/edit/view logic for all five forms

One page per item type doubles as both its add form and its edit/detail view, distinguished by
whether `ItemId` is set (from the page's `ApplyQueryAttributes`): `null` means create mode (reached
from `VaultListPage`'s "Ekle" menu), a real id means edit mode (reached by tapping a list row).
There's no separate read-only "detail" page — viewing an existing item's fields *is* its edit form,
initially masked for sensitive fields via each subclass's own per-field "Göster" reveal toggle.

Generic over `TPayload` (not just `IVaultItemPayload`) so `Save` can call
`IVaultItemRepository.Create<TPayload>`/`Update<TPayload>` directly — the compiler, not a runtime
switch, guarantees the payload a page builds matches the item type it's the form for. `Save`
validates only that `Title` isn't blank before persisting; `Delete` (shown only in edit mode, via
`IsEditMode`) removes the item outright, no confirmation at the ViewModel level (each page's
code-behind shows an `DisplayAlert` confirmation first — see `LoginFormPage.OnDeleteClicked` for
the pattern all five follow). Both raise an event (`Saved`/`Deleted`) the page turns into
`GoToAsync("..")`, the same "ViewModel raises an event, code-behind navigates" split as
`SetupViewModel.Completed`/`LockViewModel.Unlocked` from Phase 3.

Each of the five concrete ViewModels (`LoginFormViewModel`, `CardFormViewModel`,
`WifiFormViewModel`, `NoteFormViewModel`, `LicenseFormViewModel`) is a thin subclass: bound
properties for its type's fields, a per-sensitive-field `IsXVisible`/`IsXMasked` pair + toggle
command (mirroring `LockPage`'s master-password masking), and `BuildPayload`/`LoadFromPayload`
overrides. `CardFormViewModel`/`WifiFormViewModel` additionally expose a static `string[]` of
`Picker` display options (`CardTypeOptions`, `SecurityProtocolOptions`) alongside an int
`...Index` property that casts directly to/from the corresponding enum — index N must stay enum
value N since there's no separate lookup.

### Manual verification — Phase 4 UI on the Android emulator

Driven on a running `pixel_7_-_api_36_0` emulator (Android 16) via `adb shell input tap`/`input
text` against coordinates read from `adb shell uiautomator dump`, with `adb shell screencap` for
screenshots — same tooling as Phase 3. Screenshots referenced below are checked into
`docs/phase4-verification/` in this repo.

**Setup**: created a fresh vault (`01-vault-empty.png` — empty-state message, "Ekle"/"Kilitle"
toolbar).

**Scenario 1 — create one item of each of the five types**, via "Ekle" → type → fill fields →
"Kaydet", confirming after each save that the new row appears in the list with the right
icon/title/subtitle and prior items are undisturbed:
- Login "GitHub" / user "octocat" (`02-login-form-filled.png`, `03-list-after-login.png`).
- Card "Ana Kartim", Visa, masked to `•••• 1111` in the list (`04-card-form-filled.png`,
  `05-list-after-card.png`).
- WiFi "Ev WiFi" / SSID `EvAgi_5G`, WPA2 (`06-wifi-form-filled.png`, `07-list-after-wifi.png`).
- Secure Note "Wi-Fi Yedek Kodlari", subtitle correctly showing the literal "Güvenli not" rather
  than its content (`08-note-form-filled.png`, `09-list-after-note.png`).
- License "Office 365" / "Microsoft" (`10-license-form-filled.png`).

**Scenario 2 — lock/unlock round-trip preserves all five items**: the vault auto-locked itself
mid-session (`11-locked-after-license.png` — see the inactivity-timer finding below) rather than
via the manual "Kilitle" button this time, which if anything is a stronger test: unlocking again
with the master password (`12-unlocked-all-five-items.png`) shows all five items still present with
correct icons/titles/subtitles, confirming the SQLCipher-encrypted rows and each item's per-payload
AES-GCM envelope both survived a real close-and-reopen of the database connection, not just an
in-memory list that happened not to be cleared.

**Scenario 3 — decrypted round-trip, full field verification, not just list subtitles**: opening
the Card item in edit mode and revealing both masked fields via "Göster" showed every field exactly
as entered — `4111111111111111` / `123` / `12` / `2028` / "John Doe" / "Ana Kartim" / "Visa"
(`13-card-edit-revealed.png`). Opening the Login item and revealing its password similarly showed
`Username=octocat`, `Password=S3cretPass`, `Url=https://github.com` exactly as entered
(`14-login-edit-revealed.png`). This confirms the encrypt-serialize/decrypt-deserialize round trip
for every field, not just the two the list view happens to surface.

**Two findings from driving this manually, neither a Phase 4 regression**:
- **The Phase 3 inactivity auto-lock fires unconditionally, not on real inactivity**: nothing in
  the UI layer ever calls `IAuthenticationService.NotifyUserActivity()` — no page wires it to
  touch/navigation events — so `AuthenticationService`'s 2-minute `AutoLockTimeout` elapses exactly
  2 minutes after unlock regardless of how actively the app is being used, rather than 2 minutes
  after the *last* interaction like the name implies. It surfaced here only because filling five
  forms with screenshots in between took long enough to hit it (confirmed as `11-locked-after-
  license.png` above). Pre-existing since Phase 3 (`NotifyUserActivity` was added to the interface
  then but never called from anywhere); not fixed here as it's outside this phase's CRUD scope —
  worth a dedicated fix (wiring it to `Shell.Current.Navigated` or a tap-through gesture recognizer
  at the `AppShell` level) before this app is used for real.
- **`adb shell input text` silently truncates at the first space** unless spaces are encoded as
  literal `%s` (e.g. `input text "Ana%sKartim"`, not `input text "Ana Kartim"`) — the quoting is
  consumed by the local shell before reaching the remote `adb shell`, so multi-word values like
  "Ana Kartim" or "John Doe" arrived as just "Ana"/"John" on the first attempt, caught by dumping
  the field's value via `uiautomator dump` before saving (not by trusting the screenshot alone) and
  corrected by clearing the field (`input keyevent --longpress KEYCODE_MOVE_END` + repeated
  `KEYCODE_DEL`) and retyping with `%s`. A testing-tooling gotcha, not an app bug.

### Tests (`KeyKeeper.Core.Tests/Data/VaultItemRepositoryTests.cs`, plus 4 new cases in `Auth/AuthenticationServiceTests.cs`)

126 tests in the suite as of Phase 4 (up from 98 at the end of Phase 3), all passing. The new
coverage:
- **`VaultItemRepositoryTests`** (24 tests, real SQLite + the real crypto stack underneath, same
  "prefer real cheap components over mocks" style as `WrappedKeyStorageTests`/
  `VaultUnlockStoreTests`): create-then-get round-trips for all five item types; `Get` on a missing
  id returns `null`; `Create` on a locked vault propagates `InvalidOperationException`; `List` with
  and without a type filter; `Update` bumping `UpdatedAtUtc` and rejecting a mismatched payload
  type or missing id; `Delete` (including the no-op case); favorite/folder set-and-read-back
  (including clearing a folder via `null`); tag add/remove including de-duplication and two items
  sharing one `Tags` row; and two memory-hygiene tests confirming the plaintext/decrypted payload
  buffer is actually zeroed after use, not just discarded.
- **4 new cases in `AuthenticationServiceTests`** for the new `EncryptWithVaultKey`/
  `DecryptWithVaultKey` methods: both throw `InvalidOperationException` while locked, a
  encrypt-then-decrypt round-trip while unlocked, and that decrypting after re-locking throws even
  if the ciphertext was produced while unlocked.

### Dependency injection (`MauiProgram.cs`)

Added this phase: `IVaultItemRepository` → `VaultItemRepository` (singleton, alongside the other
backend services). Pages/ViewModels, all transient like Phase 3's: `VaultListViewModel`/
`VaultListPage` (replacing the removed `VaultPlaceholderViewModel`/`VaultPlaceholderPage`
registrations) and one transient pair each for `LoginFormViewModel`/`LoginFormPage`,
`CardFormViewModel`/`CardFormPage`, `WifiFormViewModel`/`WifiFormPage`,
`NoteFormViewModel`/`NoteFormPage`, `LicenseFormViewModel`/`LicenseFormPage`.

## Explicitly out of scope so far

- Folder/tag management UI and search/filtering (Phase 5) — `IVaultItemRepository` already
  supports favorite/folder/tag CRUD (Phase 4); only the UI for it is deferred. `VaultListPage`
  shows a flat, unfiltered, newest-first list of every item.
- No on-device/emulator integration tests exercising real SQLCipher key acceptance/rejection
  (see Phase 2's Testing section) — desktop tests cover everything except the actual cipher.
- The iOS `BiometricAuthService` implementation is unverified against real Face ID/Touch ID
  hardware — no Mac/Xcode available in this environment (the Android side *was* verified for
  real, on-emulator — see "Manual verification — Phase 3 UI on the Android emulator" under
  Phase 3).
- No password-strength meter on `SetupPage` (only a minimum-length floor) — flagged as its own
  later "Araçlar" phase, not attempted here.
- No instant lock-on-background (`App.OnSleep`) — only the inactivity timer and explicit "Kilitle"
  currently lock the vault; backgrounding the app doesn't.
