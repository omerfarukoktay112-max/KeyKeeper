# KeyKeeper — Architecture

Offline-first, zero-knowledge, hardware-backed password/data vault for Android and iOS.

## Solution layout

```
KeyKeeper.slnx
├── KeyKeeper/              # .NET MAUI app (net10.0-android, net10.0-ios)
│   ├── Behaviors/            # PageActivityBehavior — app-wide inactivity-timer activity reporting (post-Phase-4 fix) + page-appear fade-in (Phase 9 — done)
│   ├── Data/                # (empty — Data/ is now KeyKeeper.Core/Data, see below)
│   ├── Services/            # Platform glue: MauiVaultStoragePathProvider, MauiSecureKeyValueStore, FolderTreeFlattener (Phase 5), MauiClipboard (Phase 6 — done)
│   ├── ViewModels/          # SetupViewModel, LockViewModel, VaultListViewModel, FolderManagementViewModel, PasswordGeneratorViewModel, SecurityWarningViewModel (Phase 7), BackupViewModel (Phase 8) + 5 item form ViewModels
│   ├── Views/                # SetupPage, LockPage, VaultListPage, FolderManagementPage, PasswordGeneratorPage, SecurityWarningPage (Phase 7), BackupPage (Phase 8) + 5 item form pages
│   ├── Resources/            # Standard MAUI resource structure (AppIcon, Fonts, Images, Raw, Splash, Styles)
│   └── Platforms/            # Platform-specific entry points (Android/, iOS/) — BiometricAuthService.cs and DeviceSecurityChecker.cs (Phase 7) per platform; Android also gets Resources/xml/data_extraction_rules.xml, iOS also gets SecureOverlayService.cs (Phase 7 — done)
├── KeyKeeper.Core/          # Plain net10.0 class library — pure logic, no MAUI/UI/platform references
│   ├── Auth/                  # Unlock/lock orchestration, lockout policy, inactivity timer (Phase 3 — done)
│   ├── Crypto/               # Key derivation, encryption, master-password verification (Phase 1 — done)
│   ├── Data/                 # Vault schema, SQLCipher connection service, migrations, unlock-metadata store, item/folder/tag repositories, search-text builder (Phase 2/4/5 — done)
│   ├── Storage/               # Wrapped-key storage abstraction for biometric unlock (Phase 2 — done)
│   ├── Models/               # Vault item domain models: LoginItem, CardItem, WifiItem, SecureNoteItem, LicenseItem (Phase 4 — done)
│   ├── Tools/                 # Password generator, strength analyzer, TOTP generator, clipboard auto-clear (Phase 6 — done)
│   ├── Security/              # Device-security check + warning orchestration (Phase 7 — done)
│   └── Backup/                # Versioned .vault export/import, CSV export (Phase 8 — done)
└── KeyKeeper.Core.Tests/    # xUnit tests for KeyKeeper.Core (net10.0)
    ├── Auth/
    ├── Crypto/
    ├── Data/
    ├── Storage/
    ├── Tools/
    ├── Security/
    └── Backup/
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
  `dotnet test KeyKeeper.Core.Tests` — all tests pass (98 as of Phase 3, 126 as of Phase 4, 161 as of
  Phase 5, 195 as of Phase 6, 202 as of Phase 7, 220 as of Phase 8, unchanged at Phase 9 — a
  visual/UX-only phase, see its own section — 248 as of Phase 10).
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
- **Post-Phase-9 cleanup**: Phase 9's animation code (`PageActivityBehavior.FadeIn`, `LockPage`'s
  `ShakeAsync`) and two `Shell.Current.DisplayAlert` call sites (`BackupViewModel`,
  `FolderManagementViewModel`) briefly pushed the Android warning count from 27 to 38 — obsolete
  `ViewExtensions.FadeTo`/`TranslateTo`/`Page.DisplayAlert` (`CS0618`, MAUI 10 deprecated these in
  favor of `FadeToAsync`/`TranslateToAsync`/`DisplayAlertAsync`, same parameter lists) plus two
  unresolved XML-doc `cref`s (`CS1574`) in `LoginFormViewModel`/`VaultItemFormViewModelBase` that
  were missing their qualifying type name (`<see cref="Initialize"/>` needed to be
  `VaultItemFormViewModelBase{TPayload}.Initialize`; `<see cref="Create{TPayload}"/>`/
  `Update{TPayload}` needed the `IVaultItemRepository.` prefix already used by the neighboring
  crefs in the same comment). Fixed by renaming to the `*Async` APIs and qualifying the two crefs
  — both builds are back to their pre-existing warning counts: **27** on Android, **9** on iOS,
  both entirely the `NU1608` family, 0 errors on either.
- **Post-Phase-10**: Android now shows 24 warnings (18 `NU1608` + 6 `CA1422`, the latter genuinely
  necessary — see Phase 10's own Build status subsection for why); 0 errors, in both Debug and
  Release. iOS unchanged at 9 `NU1608` warnings, 0 errors. The `NU1608` count itself reads 18 here
  rather than the 27 recorded above — re-measured directly this phase in both configs and
  reproducible in each, so it's not a Debug/Release difference; no package reference was touched
  this phase, so the likely explanation is a NuGet dependency-resolution shift between sessions
  (floating transitive version ranges resolving differently) rather than anything this phase did.
  Not chased down further — worth reconciling in a future phase rather than treated as settled.
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
| `Otp.NET` | 1.4.1 | `KeyKeeper.Core` | TOTP generation for vault items storing 2FA secrets (`ITotpGenerator`/`TotpGenerator`, Phase 6). Moved from `KeyKeeper` this phase — see "Phase 6: Tools". |
| `zxcvbn-core` | 7.0.92 | `KeyKeeper.Core` | Offline password-strength estimation (`IPasswordStrengthAnalyzer`/`PasswordStrengthAnalyzer`, Phase 6) — see that section for why zxcvbn over a hand-rolled heuristic. |
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

**Fixed, post-Phase-4**: for all of Phase 3 and Phase 4, `NotifyUserActivity()` existed and was
fully unit-tested, but nothing in the UI ever actually called it — no page wired it to real
touch/typing/navigation — so this timer measured time-since-unlock, not time-since-last-
interaction (caught during Phase 4's manual verification; see its "Manual verification" section
for how). Now wired app-wide; see "Fix: inactivity timer now tracks real user interaction" after
Phase 4 below for `PageActivityBehavior` and the `Shell.Navigated` hook that close this gap.

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
  then but never called from anywhere); not fixed here as it's outside this phase's CRUD scope.
  **Fixed afterward** — see "Fix: inactivity timer now tracks real user interaction" below.
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

## Fix: inactivity timer now tracks real user interaction (`KeyKeeper/Behaviors/PageActivityBehavior.cs`)

Closes the gap found during Phase 4's manual verification (see above): before this fix,
`IAuthenticationService.NotifyUserActivity()` existed and was fully unit-tested, but nothing in
the UI ever called it, so `InactivityTimerService` measured time-since-unlock, not time-since-
last-interaction — a 2-minute-active session could auto-lock mid-use.

### `PageActivityBehavior` — one Behavior, applied app-wide via an implicit style

A `Behavior<Page>` that, on the page's `Appearing` event, walks the page's visual tree once (via
`IVisualTreeElement.GetVisualChildren()`, MAUI's own tree-traversal API — not a specific layout
type) and subscribes to the natural "the user just did something" event of every interactive
control it finds: `Button.Clicked`, `Entry`/`Editor.TextChanged`, `Switch.Toggled`,
`Picker.SelectedIndexChanged`, `DatePicker.DateSelected`. Each of those, plus the page's own
`Appearing`, calls `NotifyUserActivity()`.

Applied to *every* page with a single `Setter Property="Behaviors"` added to the existing implicit
`<Style TargetType="Page" ApplyToDerivedTypes="True">` in `Styles.xaml` — no per-page XAML or
code-behind changes, current or future pages included. This was the deciding factor over the
alternative considered (a common base `ContentPage` subclass every page would need to inherit
from instead of `ContentPage` directly): a style-based `Behavior` needs zero changes to the six
existing pages that already exist, and imposes nothing on whoever writes the next one.

`NotifyUserActivity` is reached via `IAuthenticationService`, resolved through
`IPlatformApplication.Current.Services` rather than constructor injection — an implicit style
instantiates a `Behavior` directly through the XAML/style engine, outside the DI container, so
there's no constructor for the container to inject into. This is the standard MAUI pattern for
exactly this situation (behaviors, converters, triggers needing a DI-registered service).

### Why this alone doesn't cover *every* tap: `Shell.Navigated` for page-to-page navigation

`PageActivityBehavior` only sees controls that exist in a page's own content — it can't see
`VaultListPage`'s row taps (a `Grid` + `TapGestureRecognizer` bound to a `Command`, not a
`Button`) or the toolbar's "Ekle"/"Kilitle" (`ToolbarItem`, a `MenuItem`, not part of the visual
content tree at all). Rather than special-case every such control, `App.xaml.cs` separately
subscribes to `Shell.Navigated` once, in `CreateWindow`, right after constructing `AppShell`:
every `GoToAsync` call anywhere in the app — including the one a row tap or a chosen "Ekle" item
type triggers — reports activity this way instead. The one edge case still missed: opening the
"Ekle" action sheet and tapping "İptal" (no navigation results) doesn't register as activity;
judged not worth special-casing a `DisplayActionSheet` cancellation for.

### Tests (`KeyKeeper.Core.Tests/Auth/InactivityTimerServiceTests.cs`)

`PageActivityBehavior` itself is UI/platform code with no practical xUnit seam (same reasoning as
Phase 3's biometric prompt — see "Why neither side has (or can have) an xUnit test" — a `Behavior`
needs a live page's visual tree and a running `IPlatformApplication`, neither available to a
desktop test run). What *is* unit-tested, newly, is the timer behavior this UI wiring now
actually exercises in practice: two tests added to the existing `InactivityTimerServiceTests`,
128 tests in the suite in total (up from 126) —
- **`Reset_CalledPeriodicallyAcrossSeveralTimeoutWindows_NeverFires`**: ten `Reset()` calls spaced
  30 seconds apart (5 minutes of simulated activity, well past the 2-minute `Timeout`) — confirms
  `TimedOut` never fires as long as activity keeps arriving more often than `Timeout`, the
  regression test for the exact gap this fix closes.
- **`Reset_CalledPeriodicallyThenStops_StillFiresTimeoutAfterLastReset`**: the other half — activity
  postpones the lock only while it keeps happening; once it genuinely stops, `TimedOut` still
  fires `Timeout` after the *last* `Reset()`, not forgotten or postponed indefinitely.

### Manual verification — Android emulator

Driven on the same running `pixel_7_-_api_36_0` emulator as Phases 3-4, via `adb shell input`, with
timestamps measured from the moment of unlock (`t=0s`). Screenshots checked into
`docs/inactivity-fix-verification/`.

**Scenario 1 — active use past the old unconditional 2-minute cutoff never locks**: unlocked the
vault (`01-unlocked-start.png`), then performed four separate interaction bursts (opening/closing
an item, adding a note and typing its title, opening another item, opening a third) spaced roughly
25-45 seconds apart — at `t=28s`, `t=74-78s`, `t=129-131s`, and `t=172-175s`. The `t=129s` and
`t=172s` bursts are the ones that matter: both land *past* the old 2-minute (120s) unconditional
timeout, and the vault was still unlocked and showing all five items at every check
(`02-burst1.png` through `05-burst4-then-idle-start.png`) — confirming sustained, realistic
interaction no longer auto-locks mid-session.

**Scenario 2 — genuine inactivity still locks**: immediately after the `t=175s` interaction, the
emulator was left completely untouched (no `adb` input of any kind) for over two minutes.
Confirmed locked afterward — `Kasa Kilitli` / the master-password prompt shown again
(`06-idle-then-locked.png`) — proving this fix didn't accidentally disable the auto-lock itself,
only its false-positive firing during active use.

## Phase 5: Organization & search (`KeyKeeper.Core/Data`, `KeyKeeper/ViewModels`, `KeyKeeper/Views`, `KeyKeeper/Services`)

Turns the `Folders`/`Tags`/`ItemTags` tables (schema-only since Phase 2) and the `IsFavorite`
flag/`SetFavorite` method (added but never called by any UI since Phase 4) into real,
UI-reachable features: folder create/rename/delete with nesting, tag create/rename/delete/cleanup,
folder/tag/favorite filtering, and in-memory search — plus a star toggle, since Phase 4 added
`IsFavorite` but nothing ever *set* it.

### `IFolderRepository` / `FolderRepository` — the delete-cascade decision

Create (optionally nested via `parentFolderId`), rename, delete, `ListAll()` (flat; every caller
that needs a tree builds it from `ParentFolderId` itself — see `FolderTreeFlattener` below).

The interesting design question — what happens to a folder's contents when it's deleted — was
already answered by Phase 2's schema, not something this phase had to decide fresh:
`Folders.ParentFolderId` is `ON DELETE CASCADE` (deleting a folder cascades to delete every
descendant folder too) while `VaultItems.FolderId` is `ON DELETE SET NULL` (an item directly
inside a deleted folder, or inside one of its cascade-deleted descendants, becomes unfiled — never
deleted, never blocking the delete). `FolderRepository.Delete` is therefore a single
`DELETE FROM Folders WHERE Id = ?` — SQLite's own foreign-key enforcement (`PRAGMA foreign_keys =
ON`, set once in `VaultDatabaseService`) does the rest. The reasoning, stated once here since nothing
in Phase 2 had a reason to say it yet: a folder is organizational metadata; an item is data the
user typed a master password to protect. Deleting the former must never destroy or hold hostage
the latter — so unfiling beats both deleting the item and blocking the folder delete until the
user manually relocates it. Verified directly (`FolderRepositoryTests`: deleting a parent orphans
items several folders deep, not just directly inside it) and end-to-end in the real UI (see Manual
verification below).

### `ITagRepository` / `TagRepository` — split responsibility from `IVaultItemRepository`

Tag *entity* CRUD (create, rename, delete outright, `DeleteUnused` cleanup) — deliberately not
overlapping `IVaultItemRepository.AddTag`/`RemoveTag` (Phase 4), which already own the item-tag
*association* (tagging an item, creating the tag on demand if it's new) and are untouched here.
`Create` exists so a "manage tags" UI could pre-create a tag before attaching it to anything;
`Delete` relies on `ItemTags.TagId`'s `ON DELETE CASCADE` to remove it from every item that had it
in one statement, same pattern as the folder cascade above.

No dedicated tag-management screen this phase (unlike folders) — tags are created inline from
each item form's "Yeni etiket" field (see below); `ITagRepository` exists for completeness and
`DeleteUnused` and is exercised entirely through its own tests, not new UI.

### `IVaultItemRepository` additions — `ListByFolder`/`ListByTag`/`ListFavorites`

Three focused methods, not one combined query API: `ListByFolder(string? folderId)` (`null` means
unfiled, matching `SetFolder`'s existing convention — not "every item"), `ListByTag(string tagId)`,
`ListFavorites()`. Each is independently correct and independently tested
(`VaultItemRepositoryTests`), but `VaultListViewModel` does not call them per filter change — see
its own doc comment for why (folder + tags + favorites-only + search text can all be active
simultaneously, which a repository call per criterion can't express without either a combinatorial
method explosion or a bespoke query-object API neither this phase's scope called for). The
ViewModel instead loads the full list once, as Phase 4 already did, and filters the in-memory
`VaultItemListEntry` collection client-side — exactly what in-memory search also requires, so one
mechanism serves both needs.

### `VaultItemSearchText` — the search-safety boundary, made unit-testable

A pure static function, `KeyKeeper.Core.Data.VaultItemSearchText.Build(VaultItemType, object)`,
building the exact text an item is searchable by: title plus a few non-secret descriptive fields
per type (username/URL for a login, cardholder name/card brand for a card, SSID for Wi-Fi,
software name for a license) — never a payload's actual secret field (password, CVV, license key,
Wi-Fi password) and never `SecureNoteItem.Content` either, on the same reasoning the list view
already masks a note's subtitle to the literal "Güvenli not": search runs over the already-
decrypted in-memory list the UI holds for display anyway, so nothing here decrypts anything extra
— but if the searchable set ever grew to include a secret field, every keystroke while searching
would linger that field's plaintext through a filtered results list far more casually than the
deliberate per-field "Göster" reveal toggle each form already requires before showing it at all.

Kept in `KeyKeeper.Core`, not the MAUI head project's `VaultItemListEntry` (its only caller),
specifically so this exact claim is unit-testable without a UI test harness — see
`VaultItemSearchTextTests`, which asserts both halves for every item type: the safe fields *are*
included, and the secret field explicitly is *not*.

### `FolderTreeFlattener` (`KeyKeeper/Services`) — shared indentation, not a real tree control

`IFolderRepository.ListAll()` returns a flat, parent-referencing list; MAUI has no built-in
TreeView, and a handful of folders in a personal vault doesn't warrant building one. `Flatten`
turns the flat list into a depth-first, indented display order (`IndentedFolder` — id, name,
depth; `DisplayName` prefixes `"— "` once per depth) shared by every place that shows the folder
hierarchy: `VaultListPage`'s filter row, `FolderManagementPage`'s list, and every item form's
folder picker.

**Bug found and fixed here during this phase's own manual verification**: the first
implementation grouped folders by `ParentFolderId` into a `Dictionary<string?, ...>` and looked up
root folders (`ParentFolderId == null`) via `TryGetValue(null, ...)`. This throws
`ArgumentNullException` unconditionally — a `Dictionary` with a reference-type key rejects `null`
at every call site regardless of the key type's nullable annotation (`string?` is a compile-time
hint only; nullable annotations don't change `Dictionary`'s runtime null-key behavior). Because
this ran on *every* call, with or without any folders existing yet, it broke two things
simultaneously the moment this phase's UI was first driven on the emulator: `VaultListViewModel.Load()`
threw partway through (silently, since `OnAppearing` calls it via `_ = InitializeAsync()`,
fire-and-forget) so the item list rendered as a permanently empty `CollectionView` with no
items *and* no empty-state message either (`IsEmpty` never got the chance to become `true`); and
navigating into any item form crashed the app outright, since there `Initialize()` runs
synchronously inside an *awaited* `GoToAsync` chain where the same exception isn't silently
swallowed. Fixed by computing root folders as their own list (`Where(f => f.ParentFolderId is
null)`) instead of a dictionary lookup keyed by `null`, so the dictionary only ever holds keys for
folders that are themselves a parent — never `null`. This single bug is *why* several of the
manual-verification screenshots below needed a second pass to actually show data.

### UI — `VaultListPage` filters, `FolderManagementPage`, per-form folder/tag fields

`VaultListPage` gained, above the item list: a `SearchBar` (bound to `VaultListViewModel.SearchText`);
a horizontally-scrolling row of folder filter chips (`FolderFilterOptionViewModel` — "Tümü" /
"Klasörsüz" always first, then every real folder via `FolderTreeFlattener`, single-select); a
wrapping row of tag filter chips (`TagChipViewModel`, multi-select, OR'ed together); a "Yalnızca
favoriler" `Switch`; and a star (`⭐`/`☆`) on each row toggling `IsFavorite` via
`ToggleFavoriteCommand` — the only place `IVaultItemRepository.SetFavorite` is actually called
from, closing the Phase 4 gap where the flag existed with nothing to ever set it.

**Both filter chip rows use `BindableLayout` inside a plain layout, not a second `CollectionView`**
— a deliberate choice, not the original one. The first implementation put the folder-filter chips
in their own `CollectionView` (`SelectionMode="Single"`, two-way-bound `SelectedItem`) alongside
the pre-existing item-list `CollectionView` lower on the same page. Two `CollectionView`s on one
Android page turned out to be exactly the conditions for a known class of MAUI/Android rendering
bug: the second `CollectionView` silently rendered zero items regardless of its `ItemsSource`'s
actual contents (compounding the `FolderTreeFlattener` bug above — even after fixing that, this
issue remained until traced separately). Replaced with a `ScrollView` + `HorizontalStackLayout` +
`BindableLayout.ItemsSource`, single-select handled explicitly via a `SelectFolderFilterCommand`
that flips one `FolderFilterOptionViewModel.IsSelected` at a time — the same rendering approach the
tag chips already used successfully. Filtering logic itself (folder + tags + favorites-only +
search, all AND'ed, tags OR'ed among themselves) lives in `VaultListViewModel.ApplyFilters`.

`FolderManagementPage` (routed as `"folder-management"`, reached via `VaultListPage`'s new
"Klasörler" toolbar item): create a folder (name + parent picker), and per-row "Yeniden Adlandır"
(`Shell.Current.DisplayPromptAsync`) / "Sil" (`DisplayAlert` confirmation, wording the folder's
own delete semantics explicitly: contents become unfiled, not deleted) — both native dialogs
called directly from `FolderManagementViewModel`, the same "ViewModel calls `Shell.Current`
directly for a lightweight dialog" precedent `VaultListViewModel.AddAsync` already set with
`DisplayActionSheetAsync` in Phase 4. Deliberately this simple: no inline-editable tree, no
drag-to-reparent — more UI investment than "basit bir yönetim ekranı" called for.

Each of the five item forms (`VaultItemFormViewModelBase<TPayload>`, shared base) gained a folder
`Picker` (`FolderOptions`/`SelectedFolderOption`, "Klasörsüz" always first) and a tag section: a
`FlexLayout` of removable tag chips (`Tags`) plus a "Yeni etiket" `Entry` + "Ekle" button
(`AddTagCommand`, disabled via `CanAddTag` while the field is blank or duplicates an existing
chip). `Initialize()` now also loads folder options in both modes and, in edit mode, the item's
current folder and tag snapshot; `Save()` passes the selected folder straight into `Create` for a
new item or applies it via `SetFolder` unconditionally after `Update` for an existing one (simpler
than tracking whether it changed), and diffs `Tags` against the snapshot to call `AddTag`/`RemoveTag`
only for what actually changed.

### Second bug found and fixed here: unguarded repository calls crash the app on a lock race

Distinct from the `FolderTreeFlattener` bug above, found while manually testing folder deletion:
`FolderManagementViewModel`'s commands (and `VaultListViewModel.Load`/`ToggleFavorite`,
and `VaultItemFormViewModelBase.Initialize`/`Delete`) called their repository methods with no
guard against the vault having locked out from under them. This is a real, reachable race, not a
theoretical one: `RenameAsync`'s prompt and `DeleteAsync`'s confirmation are *native* OS dialogs,
outside this page's own visual tree, so `Behaviors/PageActivityBehavior` (the post-Phase-4
inactivity-timer fix) can't see interaction with them — the countdown keeps running underneath a
dialog the user is actively reading. Confirming a folder deletion after the vault happened to
auto-lock in the background threw `System.InvalidOperationException: The vault database is not
open.` on the very next repository call, uncaught, crashing the app (`FATAL EXCEPTION: main`) —
reproduced for real on-device, not just reasoned about. Fixed by wrapping every such call in a
try/catch for `InvalidOperationException`/`VaultDatabaseException` that no-ops (or sets
`ErrorMessage` where a command already had one, like `FolderManagementViewModel`'s `TryRun`
helper): `App.xaml.cs`'s central `IAuthenticationService.Locked` subscriber is already navigating
to the lock screen the moment the real lock fires, so these guards only need to survive the race,
not handle it themselves.

### Manual verification — Android emulator

Driven on the same running `pixel_7_-_api_36_0` emulator as prior phases, via `adb shell input`/
`uiautomator dump`. Screenshots checked into `docs/phase5-verification/`.

**Nested folders**: created a root folder "Finans" (`03-root-folder-created.png`), then a child
folder "Kartlar" under it via the parent picker (`04-nested-folder-created.png`, showing "Finans"
and indented "— Kartlar" in the same list) — confirming `FolderTreeFlattener`'s depth-first,
indented ordering matches the real parent-child relationship, not just the creation order.

**Assigning an item to a folder and tagging it**: opened "Ev WiFi", set its folder to "— Kartlar",
added a new tag "onemli" via the inline entry, saved (`05-item-with-folder-and-tag-saved.png` —
the new "onemli" chip now appears in `VaultListPage`'s tag filter row, proving a tag created this
way is immediately visible to `ITagRepository.ListAll()`, not just locally to the form).

**Folder filtering**: selecting the "— Kartlar" chip narrowed the list to exactly "Ev WiFi"
(`06-folder-filter-working.png`), the other four items (including the ones with no folder at all)
correctly excluded.

**Tag filtering, and combined-filter AND logic**: selecting the "onemli" chip alone also narrowed
the list to "Ev WiFi" (`07-tag-filter-working.png`). With "onemli" still selected, marking "GitHub"
as a favorite (see below) and then also enabling "Yalnızca favoriler" showed **zero** items
(`11-favorites-filter-AND-tag-filter.png`) — GitHub is a favorite but isn't tagged "onemli", and
"Ev WiFi" is tagged "onemli" but isn't a favorite — correctly proving the active filters combine
with AND, not OR.

**Search, including the security-critical negative case**: searching "github" correctly matched
only the GitHub login item on its title (`08-search-matches-username.png`). Searching the *exact
password* stored on that same item, `S3cretPass`, returned **zero** results —
`09-search-excludes-password-SECURITY.png` shows the search box containing exactly that string
with the empty-state message showing, not the GitHub item — direct, on-device confirmation that
`VaultItemSearchText` genuinely excludes secret fields at runtime, not just in its unit tests.

**Favoriting**: tapping the star on the GitHub row filled it in gold immediately, without also
opening the item (confirming the star's own `TapGestureRecognizer` takes precedence over the row's
enclosing one — `10-favorite-star-toggled.png`); the star's state was confirmed to persist across
a lock/unlock cycle.

**Folder deletion cascade, end-to-end**: deleting "Finans" showed the expected confirmation wording
naming both the cascade and the orphaning behavior (`12-folder-delete-confirmation-dialog.png`).
Confirming it removed **both** "Finans" and "— Kartlar" from `FolderManagementPage`'s list in one
action (`13-cascade-delete-both-folders-gone.png`) — proving the schema's cascade FK actually
fires through `FolderRepository.Delete`'s single DELETE statement — while "Ev WiFi" (which had
been filed under the now-deleted "Kartlar") remained fully present back on `VaultListPage`, simply
no longer showing a folder chip for it (`14-items-preserved-after-cascade-delete.png`) — the data
itself was never at risk, exactly as `IFolderRepository.Delete`'s doc comment commits to.

### Tests (`KeyKeeper.Core.Tests/Data/FolderRepositoryTests.cs`, `TagRepositoryTests.cs`, `VaultItemSearchTextTests.cs`, plus additions to `VaultItemRepositoryTests.cs`)

161 tests in the suite as of Phase 5 (up from 126 at the end of Phase 4), all passing:
- **`FolderRepositoryTests`**: create/get/rename/delete round-trips; creating with a non-existent
  parent throws `KeyNotFoundException`; `Delete` cascades through multiple levels of nesting
  (grandchild folders, not just direct children); `Delete` orphans items directly inside the
  deleted folder *and* items inside cascade-deleted descendant folders, rather than deleting or
  blocking on them — the regression test for the delete-cascade decision this phase's `IFolderRepository`
  doc comment commits to.
- **`TagRepositoryTests`**: create/get/rename/delete round-trips; duplicate-name creation and
  rename-to-an-existing-name both throw `ArgumentException`; deleting a tag removes it from every
  item that had it (via the schema's own cascade); `DeleteUnused` removes only tags attached to
  zero items, leaving tags still in use untouched.
- **`VaultItemSearchTextTests`**: one test per item type, each asserting both that its non-secret
  fields *are* included and that its secret field(s) — password, CVV, license key, Wi-Fi password,
  `SecureNoteItem.Content` — are explicitly *not*; plus a null-optional-fields case confirming
  `Build` doesn't throw when every non-title field is absent.
- **New in `VaultItemRepositoryTests`**: `ListByFolder` (direct-children case and the `null` =
  unfiled case), `ListByTag`, `ListFavorites` — each confirming the method returns exactly the
  matching item(s) and excludes non-matching ones.

No test exists for `PageActivityBehavior`, the two MAUI rendering/crash bugs found above, or any
other UI-layer code introduced this phase, for the same reason Phase 3's biometric prompt has
none (see "Why neither side has (or can have) an xUnit test") — these need a live page's visual
tree, a running `IPlatformApplication`, or real Android/MAUI rendering behavior, none of which a
desktop `dotnet test` run provides. That gap is exactly why both bugs surfaced only during manual
on-emulator verification, not the automated test run — the manual verification pass **is** their
test coverage.

### Dependency injection (`MauiProgram.cs`)

Added this phase: `IFolderRepository` → `FolderRepository`, `ITagRepository` → `TagRepository`
(both singletons, alongside `IVaultItemRepository`). Pages/ViewModels, transient like every other
page: `FolderManagementViewModel`/`FolderManagementPage`. `VaultListViewModel` and all five item
form ViewModels' constructors grew new parameters (`IFolderRepository`/`ITagRepository` as
needed) — no registration changes needed for those beyond the existing transient registrations,
since the container resolves constructor parameters automatically.

## Phase 6: Tools (`KeyKeeper.Core/Tools`, `KeyKeeper/ViewModels`, `KeyKeeper/Views`, `KeyKeeper/Services`)

"Araçlar" — four independent, previously-flagged gaps closed in one phase: a real password
generator (Phase 3/setup only ever enforced a minimum length), a live strength indicator (also
flagged as out of scope after Phase 5), TOTP code generation (`LoginItem.TotpSecret` had existed
as a field since Phase 4 with nothing to ever compute a code from it), and clipboard auto-clearing
(every "Kopyala" button before this phase — none existed yet — would otherwise have left a secret
sitting on the OS clipboard indefinitely). All four are pure logic in `KeyKeeper.Core/Tools`,
tested with no MAUI dependency, same pattern Phases 1–3 established for `Crypto`/`Storage`/`Auth`.

### Password generator — `IPasswordGenerator` / `PasswordGenerator`

Built on `RandomNumberGenerator`, never `Random` — consistent with every other secret-generating
path in this app. Rather than pooling every selected character category and sampling uniformly
(which can legitimately produce a password missing an entire requested category), `Generate` draws
one character from each selected category first, fills the remainder from the combined pool, then
shuffles the whole buffer with a `RandomNumberGenerator`-driven Fisher-Yates — so the guaranteed
characters aren't always in the leading positions. `PasswordGeneratorOptions` carries length (default
16) and four `Include*` flags plus `ExcludeAmbiguousCharacters` (strips `0O1lI` from every category,
for passwords a person has to retype from a screen); `Generate` throws `ArgumentException` if no
category is selected or `Length` is shorter than the number of selected categories, rather than
silently defaulting either.

Two call sites, deliberately different in scope: the standalone `PasswordGeneratorPage` /
`PasswordGeneratorViewModel` exposes every option for generating a password to use *outside* the
vault, while `LoginFormViewModel.GeneratePasswordCommand` (the "Üret" button next to the password
field) is a fixed 16-character, every-category call — no page round-trip for the common case of
just wanting a strong password for the item being edited right now.

### Password strength analysis — `IPasswordStrengthAnalyzer` / `PasswordStrengthAnalyzer`, `zxcvbn-core`

Built on the `zxcvbn-core` package (a .NET port of Dropbox's zxcvbn) rather than a hand-rolled
heuristic set: zxcvbn already detects exactly the weakness patterns this tool needs — repeated
characters, sequential keyboard runs ("qwerty", "12345"), dates, dictionary words and l33t
substitutions — has no dependencies of its own, and runs entirely offline against its own bundled
dictionaries, consistent with this app's zero-knowledge/offline-first requirement (see
`IPasswordStrengthAnalyzer`'s doc comment: deliberately no breach-check API, unlike a
HaveIBeenPwned-style design). zxcvbn's own 0–4 `Score` maps onto this app's own four-value
`PasswordStrengthLevel` (Zayıf/Orta/Güçlü/Çok Güçlü) — 0→Weak, 1→Medium, 2→Strong, 3 *and* 4→VeryStrong,
since the top two zxcvbn tiers don't need a separate bucket in this app's simpler UI.
`PasswordStrengthResult.Entropy` converts zxcvbn's `GuessesLog10` (log10 of the estimated guess
count) to bits (log2), the more familiar unit for this app's UI, rather than exposing the raw
guess-count figure. `Analyze(string.Empty)` returns `Weak`/0/0 rather than throwing, since every
caller here is a live as-you-type binding that starts out empty.

`PasswordStrengthPresentation` (`KeyKeeper/ViewModels`) is the one place the Turkish label, colour,
and 0–1 progress-bar fraction for each `PasswordStrengthLevel` are defined, shared by
`LoginFormViewModel`, `WifiFormViewModel`, and `PasswordGeneratorViewModel` rather than duplicated
per form. Each of those three ViewModels wires strength analysis to its password field via a
CommunityToolkit.Mvvm-generated `partial void On<Property>Changed` hook (`OnPasswordChanged` on the
two form ViewModels), so the indicator updates on every keystroke with no explicit event wiring.
**Not** wired into `SetupViewModel`/`SetupPage` — the master-password field there still only
enforces `MinimumPasswordLength` (8), unchanged from Phase 3; see "Explicitly out of scope" below.

### TOTP — `ITotpGenerator` / `TotpGenerator`, built on `Otp.NET`

`Otp.NET` moved from `KeyKeeper` to `KeyKeeper.Core` this phase (see the `.csproj` diffs) for the
same reason `sqlite-net-pcl` and `Konscious.Security.Cryptography.Argon2` already live there: it's
pure logic with no MAUI dependency, so it and its RFC 6238 test vectors belong beside every other
pure-logic piece, available to the MAUI head project transitively through the existing
`ProjectReference`. `TotpGenerator` takes an optional `TimeProvider` (defaults to
`TimeProvider.System`) — same pattern as `InactivityTimerService` (Phase 3) and
`ClipboardClearingService` below — so RFC 6238's own published test vectors can be verified against
fixed instants instead of real-time waits. Fixed at the standard 30-second step / SHA-1 / 6-digit
configuration: what every mainstream authenticator app and otpauth:// URI assumes when no algorithm
is specified, and what `LoginItem.TotpSecret` is documented to hold. `GenerateCode` throws
`ArgumentException` for a null/blank secret or one that isn't valid Base32 (the format issuers
normally hand out), rather than letting `Otp.NET`'s own exception shape leak through.

`LoginFormViewModel` wires this to a live display: entering/editing `TotpSecret` (via
`OnTotpSecretChanged`) starts a 1-second `IDispatcherTimer` (`RefreshTotpTimer`/`CreateTotpTimer`,
same pattern as the clipboard countdown below) that keeps `TotpDisplayCode` and
`TotpRemainingSeconds` current; an invalid Base32 secret sets `TotpError` instead of throwing into
the UI, and stops the timer. `LoginFormPage.OnDisappearing` calls `StopTotpTimer()` so the timer
doesn't keep running (and keep the ViewModel alive) after navigating away — the same "stop the
timer on disappear" discipline the clipboard countdown below also requires.

### Clipboard auto-clear — `IClipboard`/`MauiClipboard`, `IClipboardClearingService`/`ClipboardClearingService`

`IClipboard` (`KeyKeeper.Core/Tools`) is a minimal seam over the platform clipboard, the same
testability reasoning as `ISecureKeyValueStore` (Phase 2): `ClipboardClearingService` depends only
on the interface, so it's unit-tested against a `FakeClipboard`, with `MauiClipboard`
(`KeyKeeper/Services`, wrapping `Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard`, aliased
as `CoreIClipboard` in `MauiProgram.cs` to avoid colliding with MAUI's own same-named interface)
the real implementation.

`CopyAsync(value, delay)` sets the clipboard immediately, then schedules a clear via
`TimeProvider.CreateTimer` (fake-clock-testable, same pattern as `TotpGenerator`/
`InactivityTimerService`). The clear itself **re-reads the clipboard first and only clears it if it
still holds exactly the value this call copied there** — without that check, the timer firing after
the user copied a password, then immediately copied something unrelated to paste elsewhere, would
wipe out *that* unrelated value instead of leaving it alone. Calling `CopyAsync` again before an
earlier delay elapses cancels the earlier pending clear and replaces it with a new one for the new
value (`ClipboardClearingServiceTests` covers this explicitly).

Every form's clipboard-copy button routes through one shared implementation,
`VaultItemFormViewModelBase.CopyToClipboardAsync` (bound with the field itself as
`CommandParameter` — `Password` on `LoginFormPage`/`WifiFormPage`, `CardNumber`/`Cvv` on
`CardFormPage`, `LicenseKey` on `LicenseFormPage`), fixed at a 30-second clear delay
(`ClipboardClearDelay`, picked from the middle of the phase's 30–45s guidance). Each page's
`OnDisappearing` calls the ViewModel's `StopClipboardCountdown()` so the 1-second UI countdown timer
(`ClipboardStatus`, e.g. "Panoya kopyalandı, 29 saniye sonra temizlenecek.") doesn't keep ticking
after navigating away. `PasswordGeneratorViewModel` has its own near-identical
`CopyAsync`/`StartClipboardCountdown`/`UpdateClipboardStatus` implementation rather than sharing
`VaultItemFormViewModelBase`'s (it isn't an item form, so it doesn't derive from that base) — a
known, small duplication, not extracted into a shared helper this phase.

### UI additions

`PasswordGeneratorPage` (routed as `"password-generator"`, reached via `VaultListPage`'s new "Şifre
Üret" toolbar item): length slider (8–64), the four category switches, the ambiguous-character
toggle, a "Şifre Üret" button, and — once a password exists — its strength bar/label and a
"Kopyala" button with the same clearing countdown as every item form. `LoginFormPage` and
`WifiFormPage` gained a strength bar/label under their password field; `LoginFormPage` additionally
gained the "Üret"/"Kopyala" buttons and, when `TotpSecret` is set, a live code + countdown card and
an error label for invalid Base32. `CardFormPage` and `LicenseFormPage` gained "Kopyala" next to
`CardNumber`/`Cvv` and `LicenseKey` respectively (no strength meter — those aren't passwords).

### Manual verification — Android emulator

Screenshots checked into `docs/phase6-verification/` (`p6_00` through `p6_26`), covering: first-run
setup and vault creation; opening the standalone generator, generating a password, and watching its
strength bar read "Çok Güçlü" (`p6_06`); copying it and seeing the live "Panoya kopyalandı, 29 saniye
sonra temizlenecek." countdown (`p6_07`); creating a login item, using the in-form "Üret" button, and
entering a TOTP secret; the live TOTP code changing between two later screenshots on the same item
(`909382` at "14sn" in `p6_17`, `357711` at "22sn" in `p6_18` — different codes, different countdowns,
confirming the 1-second refresh timer is actually recomputing the code, not showing a static value);
reopening the saved login and seeing the TOTP card still live; and the clipboard-clear round trip
(`p6_21`–`p6_26`): copying the generated password, pasting it elsewhere to confirm it matches
(`p6_22`), waiting past the 30-second delay, then pasting again (`p6_24`) — the pasted value was a
*different* string than what had been copied, i.e. the clipboard no longer held the original secret,
consistent with the auto-clear having actually fired rather than merely being reasoned about.

### Tests (`KeyKeeper.Core.Tests/Tools`)

195 tests in the suite as of Phase 6 (up from 161 at the end of Phase 5), all passing:
- **`PasswordGeneratorTests`**: requested length is honored across several sizes; two calls never
  produce the same password; selecting only one category produces only that category's characters;
  selecting all four guarantees at least one character from each (on a long-enough password, so the
  guarantee — not luck — is what's being tested); `ExcludeAmbiguousCharacters` never lets `0O1lI`
  through, checked across 50 runs; no category selected, or a length shorter than the selected
  category count, both throw `ArgumentException`.
- **`PasswordStrengthAnalyzerTests`**: an empty password is `Weak`/0/0; known-weak passwords
  ("password", "123456", "qwerty", "aaaaaaaa") classify `Weak`; known-strong passwords classify
  `Strong` or `VeryStrong`; a stronger password's entropy is strictly greater than a weaker one's.
- **`TotpGeneratorTests`**: all five of RFC 6238 Appendix B's official test vectors (truncated to
  6 digits — an 8-digit RFC code's last 6 digits are always identical to the 6-digit code, since
  `(x mod 10^8) mod 10^6 == x mod 10^6`), with the Base32 secret itself derived from the RFC's raw
  ASCII bytes at test time rather than hand-transcribed; `PeriodSeconds` is always 30; remaining
  time within a step is computed correctly; the same secret at two different instants produces
  different codes; null/empty/whitespace and invalid-Base32 secrets both throw `ArgumentException`.
- **`ClipboardClearingServiceTests`** (against `FakeClipboard` + `FakeTimeProvider`): `CopyAsync`
  sets the clipboard immediately; the clipboard is cleared once the delay elapses; it is untouched
  before the delay elapses; if the user copies something else in the meantime, that value survives
  the scheduled clear untouched (the security-relevant case); calling `CopyAsync` again before the
  first delay elapses cancels the first pending clear so only the second one ever fires.

No test exists for the `IDispatcherTimer`-driven UI wiring (TOTP live countdown, clipboard countdown
labels) or `PasswordStrengthPresentation`'s label/colour/fraction mapping, for the same structural
reason Phase 3's biometric prompt and Phase 5's `PageActivityBehavior` have none — see "Why neither
side has (or can have) an xUnit test" under Phase 3. The manual verification pass above is this
UI-layer code's actual coverage.

### Dependency injection (`MauiProgram.cs`)

Added this phase, all singletons (stateless, or — for the clipboard clearer — needing exactly one
shared pending-clear timer app-wide, same reasoning as the repositories): `IPasswordGenerator` →
`PasswordGenerator`, `IPasswordStrengthAnalyzer` → `PasswordStrengthAnalyzer`, `ITotpGenerator` →
`TotpGenerator`, `CoreIClipboard` → `MauiClipboard`, `IClipboardClearingService` →
`ClipboardClearingService`. `PasswordGeneratorViewModel`/`PasswordGeneratorPage` registered
transient, same as every other page. Every item-form ViewModel's constructor grew an
`IClipboardClearingService` parameter (via the shared base class); `LoginFormViewModel` and
`WifiFormViewModel` additionally take `IPasswordStrengthAnalyzer`, and `LoginFormViewModel` also
takes `IPasswordGenerator` and `ITotpGenerator` — no registration changes needed beyond the five new
singletons above, since the container resolves constructor parameters automatically.

## Phase 7: Security hardening (Güvenlik Zırhı) (`KeyKeeper.Core/Security`, `KeyKeeper/Platforms`, `KeyKeeper/AppShell.xaml`, `KeyKeeper/Views`, `KeyKeeper/ViewModels`)

Four previously-untouched hardening gaps, closed together: screen-capture/app-switcher exposure,
root/jailbreak + debugger detection with a warning screen, a re-verification (plus one new
defense-in-depth entry) of backup exclusion, and the manual-verification/ARCHITECTURE.md write-up
this section itself is. Explicitly not touched: anything belonging to a future export/import
(.vault/CSV) phase.

### Screen-capture and app-switcher protection

**Android — `FLAG_SECURE`, always on for the whole app session.** Set once,
`Window?.AddFlags(WindowManagerFlags.Secure)` in `MainActivity.OnCreate` — not toggled per
lock/unlock state. This is a window-level flag, so setting it once already covers every page
automatically (the "one central mechanism, not per-page" requirement falls out of the platform
API for free), and on Android the same single flag blocks screenshot/screen-recording capture
*and* the recents/app-switcher thumbnail simultaneously. Deliberately **not** toggled in sync with
`IAuthenticationService.Locked`/unlock: that would reintroduce exactly the kind of async
background-thread-vs-UI-thread race Phase 5's "unguarded repository calls" bug already
demonstrated once in this codebase. Blocking screenshots of the lock/setup screens too costs
nothing — nothing there is sensitive, but there's no reason to allow it either.

**iOS — `SecureOverlayService`, a central blur+logo overlay, not per-page code.** iOS has no
`FLAG_SECURE` equivalent: no API blocks screen recording outright, and the app-switcher preview is
simply a snapshot of whatever the window looked like the instant the app resigned active. Instead,
`Platforms/iOS/SecureOverlayService.cs` covers the entire key window with a `UIVisualEffectView`
blur + centered "KeyKeeper" label whenever either of two independent triggers is true (shown if
either, hidden only once both clear):
- `UIScreen.CapturedDidChangeNotification` / `UIScreen.MainScreen.Captured` — screen
  recording/mirroring has started.
- The app is about to resign active (`applicationWillResignActive`/`applicationDidBecomeActive`,
  wired via `MauiProgram.cs`'s `ConfigureLifecycleEvents(events => events.AddiOS(...))` rather than
  touching `AppDelegate.cs`) — covers both a real backgrounding (the app-switcher snapshot is taken
  at exactly this point) and transient inactivity (an incoming call, Control Center), the same
  technique widely used by banking/password-manager apps.

`SecureOverlayService.Start()` is called once from the `FinishedLaunching` lifecycle hook to begin
observing capture state; `OnResignActivation`/`OnActivated` call `SetResignedActive(true/false)`.
`UIScreen.Captured` (deprecated iOS 17.2+ in favor of `UITraitCollection.SceneCaptureState`) and
`UIApplication.Windows` (deprecated iOS 15+ in favor of `UIWindowScene.Windows`) are both used
anyway, with the deprecation warnings explicitly suppressed at each call site: their replacements
either require iOS 17+ (this app's `SupportedOSPlatformVersion` is 15.0) or require adopting
`UIScene`/`SceneDelegate`, which this single-`AppDelegate` app deliberately doesn't have. Unverified
beyond compiling — no Mac/Xcode available in this environment, the same limitation as every other
iOS-only piece of this app since Phase 2's SQLCipher testing section.

### Root/jailbreak + debugger detection (`IDeviceSecurityChecker`, `IDeviceSecurityWarningService`)

`IDeviceSecurityChecker.CheckDeviceSecurity()` returns a `DeviceSecurityCheckResult`
(`IsRootedOrJailbroken`, `IsDebuggerAttached`, `Signals`). No single check here is authoritative in
either direction — a custom ROM can legitimately carry a `test-keys` build tag with no root at all;
a well-hidden root can pass a naive su-path check — so each platform implementation runs several
independent checks and reports every one that found evidence via `Signals`, rather than trying to
produce one single "definitely rooted" verdict from one method. The policy that turns evidence into
a warning is deliberately simple: `DeviceSecurityCheckResult.RequiresWarning` is true the moment
**any** signal fires, biased toward false positives over false negatives — safe only because the
response (below) is a dismissable warning, never a block.

**Android signals** (`Platforms/Android/DeviceSecurityChecker.cs`): su-binary presence at common
install paths; a known root-manager/root-broker package installed (Magisk, SuperSU, KingRoot, and
others via `PackageManager.GetPackageInfo` in a per-package try/catch); `Build.Tags` containing
`test-keys`; `Build.Type` not equal to `"user"` (engineering/userdebug builds, common on custom
ROMs, ship with a debuggable system and `adbd` running as root). Every individual check is wrapped
in its own try/catch — a check that throws (e.g. a `PackageManager` quirk on some OEM skin) counts
as "no evidence from that check" and never takes down the whole result, consistent with the overall
bias toward an extra dismissable warning over a crash.

**iOS signals** (`Platforms/iOS/DeviceSecurityChecker.cs`): known jailbreak app/file paths
(Cydia/Sileo/Zebra/Substrate and others); a sandbox-escape write attempt to a `/private/...` path
(a regular sandboxed app should always fail this; success is the signal, and the probe file is
deleted either way); `cydia://` URL-scheme openability via `CanOpenUrl` (requires declaring
`cydia` in `Info.plist`'s `LSApplicationQueriesSchemes` — without it iOS 9+ makes `CanOpenUrl`
always answer `false` regardless of whether Cydia is installed, silently turning the check into a
no-op); a non-empty `DYLD_INSERT_LIBRARIES` environment variable (tweak-injection/dynamic
instrumentation signal — not exclusively a jailbreak indicator, but tampering either way).

**Debugger detection, both platforms**: `#if !DEBUG`-gated, hardcoded `false` in Debug builds.
A debugger is legitimately attached during every normal development run (`dotnet build ...
-t:Run`, Visual Studio's own debugger) — checking this in Debug builds would fire on every single
dev session, not a real attack. Android reads `Android.OS.Debug.IsDebuggerConnected` directly. iOS
uses the classic Apple Technical Q&A QA1361 ("Detecting the Debugger") technique:
`sysctl(CTL_KERN, KERN_PROC, KERN_PROC_PID, getpid())` to fetch this process's `kinfo_proc`, then
inspects the `P_TRACED` bit of `kp_proc.p_flag` — read directly by its known byte offset (32: the
legacy BSD `extern_proc`'s `p_un` union (16 bytes) + `p_vmspace` pointer (8) + `p_sigacts` pointer
(8), then `p_flag` immediately follows) rather than marshaling the entire struct field-by-field
into C#; both approaches land on the identical bytes. Unverified on real hardware for the same
reason as the rest of the iOS-only code this phase.

### The warning screen — non-blocking, user decides, not a forced close

`IDeviceSecurityWarningService`/`DeviceSecurityWarningService` is the actually-tested orchestration
piece: `EvaluateDeviceSecurity()` runs the checker and sets `PendingWarning`/`HasPendingWarning`;
`AcknowledgeAndContinue()` clears it. No persistence across launches by design — it re-evaluates
fresh every cold start, so an acknowledged warning can reappear next launch rather than being
silently remembered forever. Nothing in this phase's scope asked for a snooze/remember-choice
mechanism, and a rooted/jailbroken state realistically doesn't change between launches anyway.

**Why a dismissable warning instead of closing the app outright**: closing on every custom
ROM/rooted-but-legitimate-owner device converts a security signal into a guaranteed
support-ticket/uninstall event, while the realistic attacker this check is meant to catch isn't
actually stopped by a client-side check either way — it can be patched out by the same tooling
that achieved root in the first place. The genuine value here is informing a legitimate user that
their device's OS-level guarantees (Keystore/Keychain-backed secure storage, sandboxing — several
earlier phases' security properties lean on these) are weaker than normal; the decision to proceed
anyway is theirs, not this app's to make for them.

`AppShell.xaml` gained a 4th top-level `ShellContent` (`Route="security-warning"` →
`SecurityWarningPage`), for the same routing reason `setup`/`lock`/`vault` are real `ShellContent`
entries and not `Routing.RegisterRoute`-only global routes (see Phase 3's routing section): an
absolute `GoToAsync("//security-warning")` needs a real `ShellContent` to land on. **A real bug
found during this phase's own manual verification**: the first version of this file declared
`security-warning` *first*, which makes Shell instantiate it as its own implicit default page —
`SecurityWarningPage`'s `OnAppearing` (and therefore `SecurityWarningViewModel.Initialize()`) ran
before `App.xaml.cs`'s `NavigateToInitialRouteAsync` had called `EvaluateDeviceSecurity()` at all,
so the warning message rendered as an empty string; the later `GoToAsync("//security-warning")`
turned out to be a same-route no-op once Shell already considered itself there, so `OnAppearing`
never re-fired to pick up the real `PendingWarning`. Fixed by keeping `setup` first (as it always
was) and placing `security-warning` last — it only needs to be *reachable* as a real `ShellContent`,
not Shell's implicit default. Confirmed fixed via `uiautomator dump` showing the real Turkish
warning message rendered correctly, and that tapping "Yine de Devam Et" correctly continues on to
the lock screen — see `docs/phase7-verification/p7_05_security_warning_message_uiautomator_dump.txt`.

`AppRouting.DetermineInitialRoute(IAuthenticationService)` (new, `KeyKeeper/AppRouting.cs`) extracts
the setup/lock/vault ternary that used to live only inside `App.xaml.cs.NavigateToInitialRouteAsync`,
so both the normal launch path and `SecurityWarningViewModel`'s "continue anyway" path share one
source of truth instead of duplicating it. `SecurityWarningViewModel` (thin, like every other
ViewModel in this app) builds a plain-language Turkish message from `PendingWarning` — never
dumping the raw `Signals` list to the user — and offers a single `ContinueCommand`, deliberately no
"close app" action.

### Backup exclusion — re-verified, not rebuilt

**Android**: `android:allowBackup="false"` was already set in Phase 2 — re-confirmed, not
reintroduced. This phase adds one explicit, technically-redundant Android 12+
`android:dataExtractionRules="@xml/data_extraction_rules"` (`Platforms/Android/Resources/xml/
data_extraction_rules.xml`, excluding the whole `root` domain from both `cloud-backup` and
`device-transfer`) as defense-in-depth: `allowBackup=false` already disables Auto Backup and, on
Android 12+, cloud backup/device-to-device transfer regardless of this file's contents (per Phase
2's own comment in `AndroidManifest.xml`), but this documents intent explicitly and keeps the app's
backup posture correct even if `allowBackup` were ever accidentally flipped in a future change.
Verified against the actual built/merged manifest, not just the source XML — `aapt dump xmltree`
against the built APK confirms both `android:allowBackup=0x0` and a resolved
`android:dataExtractionRules` resource, and a second `aapt dump xmltree` against the packaged
`res/xml/data_extraction_rules.xml` itself confirms its `exclude domain="root"` content survived
compilation — see `docs/phase7-verification/p7_04_manifest_backup_flags.txt`.

**iOS**: no code change needed. `MauiVaultStoragePathProvider` (Phase 2) already excludes the vault
database *and* the unencrypted unlock-metadata database (plus their WAL/SHM/journal sidecars) from
iCloud backup via `NSFileManager.SetSkipBackupAttribute`. Re-read and re-confirmed this phase, not
rebuilt. `ISecureKeyValueStore`/`MauiSecureKeyValueStore` (wrapping `SecureStorage`, i.e. the iOS
Keychain) needs no separate opt-out either — this app never opts into iCloud Keychain
synchronization (`kSecAttrSynchronizable`), so nothing stored there can leave the device via that
path in the first place.

### Tests (`KeyKeeper.Core.Tests/Security/DeviceSecurityWarningServiceTests.cs`)

202 tests in the suite as of Phase 7 (up from 195 at the end of Phase 6), all passing. The 7 new
ones exercise `DeviceSecurityWarningService` against a fake `IDeviceSecurityChecker` — the same
"test the reaction, not the OS" split `AuthenticationServiceTests` already established with
`FakeBiometricAuthService` (Phase 3): a clean result never sets a pending warning; a rooted,
debugger-attached, or both result does; `AcknowledgeAndContinue()` clears a pending warning without
re-running the check; and a fresh `EvaluateDeviceSecurity()` call after acknowledging can resurface
the warning again (the regression test for the "no persistence across launches" design decision
above).

No test exists for either platform's `DeviceSecurityChecker` body, `FLAG_SECURE`,
`SecureOverlayService`, `SecurityWarningPage`/its ViewModel's MAUI wiring, or
`dataExtractionRules` — same structural reason as every prior platform-only piece of this app (see
Phase 3's "Why neither side has (or can have) an xUnit test"): these need a live OS process, a real
window/visual tree, or real Android/iOS platform behavior, none of which a desktop `dotnet test`
run provides. The manual verification below is this layer's actual coverage.

### Manual verification — Android emulator (`pixel_7_-_api_36_0`)

Driven on the same running emulator as prior phases, via `adb`/`uiautomator`, plus `aapt` against
the built APK for the manifest checks. Screenshots and evidence files checked into
`docs/phase7-verification/`.

**FLAG_SECURE blocks screenshots, unconditionally, on every screen**: `adb shell screencap -p`
against the lock screen came back a solid black image
(`p7_01_flag_secure_screenshot_blocked.png`) — the identical result Phase 3 already established
for `FLAG_SECURE`-protected system dialogs (`BiometricPrompt`/enrollment screens). The same command
against `SecurityWarningPage` (below) produced an identical black image
(`p7_03_security_warning_page_screencap_also_blocked.png`, byte-identical in size to p7_01) —
direct confirmation the protection really is unconditional across every page, not just the ones it
was consciously tested against.

**App-switcher thumbnail is blank, not the real screen**: `adb shell input keyevent
APP_SWITCH` followed by a screencap of the recents screen (itself not blocked, since it's the
launcher's own UI, not KeyKeeper's) shows KeyKeeper's card rendered as a solid black rectangle under
its `.NET` app icon, instead of the real "Kasa Kilitli" lock screen
(`p7_02_app_switcher_thumbnail_blank.png`) — confirming the second half of `FLAG_SECURE`'s effect
alongside the screenshot block, from the same single flag.

**Backup manifest flags, on the real built/merged manifest**: see
`p7_04_manifest_backup_flags.txt` — `aapt dump xmltree` against the built APK confirms
`allowBackup=0x0` and a resolved `dataExtractionRules` resource, and a second `aapt dump xmltree`
against that packaged resource confirms its `exclude domain="root"` content survived compilation.

**Root/jailbreak positive case: cannot be verified on this non-rooted emulator** — documented
honestly, same spirit as Phase 2's on-device SQLCipher limitation. Unit-tested via fakes only (see
Tests above). To still confirm the warning screen itself renders and behaves correctly, Android's
`DeviceSecurityChecker.CheckDeviceSecurity()` was temporarily edited to add one unconditional
forced signal, producing a real positive `RequiresWarning` on this otherwise-clean emulator; this
is what caught the `AppShell` default-route bug described above, and after the fix, confirmed via
`uiautomator dump` that the real Turkish warning message renders correctly and that "Yine de Devam
Et" correctly continues on to the lock screen
(`p7_05_security_warning_message_uiautomator_dump.txt`). The forced signal was reverted immediately
after this check, before anything was committed — confirmed by rebuilding and relaunching once more
and seeing the lock screen appear directly, with no warning, exactly as it should on a genuinely
clean device.

**Debugger-attached positive case**: same honesty caveat as root/jailbreak — unit-tested via fakes
only, not manually verified positive on-device (the check is Release-only by design, and this
emulator's app was built/run in Debug).

**iOS**: compile-only (`dotnet build KeyKeeper/KeyKeeper.csproj -f net10.0-ios`) — no Mac/Xcode
available in this environment, the same limitation as every other iOS-only piece of this app since
Phase 2.

### Dependency injection (`MauiProgram.cs`)

Added this phase: `IDeviceSecurityChecker` → `DeviceSecurityChecker` (singleton, per-platform class
resolved automatically, the same one-class-per-`Platforms/<X>/`-folder pattern as
`IBiometricAuthService`), `IDeviceSecurityWarningService` → `DeviceSecurityWarningService`
(singleton — the one evaluation done at launch and the acknowledgement made from
`SecurityWarningPage` need to act on the same in-memory state). `SecurityWarningViewModel`/
`SecurityWarningPage` registered transient, same as every other page. On iOS only, `MauiProgram.cs`
also wires `SecureOverlayService.Start()`/`SetResignedActive(...)` into
`ConfigureLifecycleEvents(events => events.AddiOS(...))` — not a DI registration, since
`SecureOverlayService` is a static class with no per-instance state to inject.

## Phase 8: Backup — versioned .vault export/import, CSV export (`KeyKeeper.Core/Backup`, `KeyKeeper/ViewModels`, `KeyKeeper/Views`)

Whole-vault backup: a versioned, encrypted `.vault` export/import format, and a separate,
deliberately-warned-about plaintext CSV export. Closes the gap `KeyKeeper.Core/Data` had had since
Phase 2 — no way to get data out of (or back into) the vault as a portable file at all.

### `.vault` file format (`VaultBackupHeader`, `VaultBackupFile`)

On-disk layout: `MAGIC (8 bytes, "KKVLTBKP") || header length (4-byte little-endian int32) ||
header JSON (UTF-8) || encrypted payload (rest of file)`. A length-prefixed binary framing was
chosen over a text delimiter specifically so the header's own JSON content can never be misread as
part of a separator, and the encrypted payload — arbitrary binary, not text — never needs escaping.

**The header is deliberately unencrypted** (`VaultBackupHeader`: `FormatVersion`, `Salt`,
the four Argon2id parameters, `CreatedAtUtc`) — exactly the same reasoning
`KeyKeeper.Core.Data.VaultUnlockMetadata` already established for the live vault's own unlock
metadata (Phase 3): a KDF salt and cost parameters are meant to be public, and without the master
password an attacker who only has this header can't derive the key that decrypts the payload.

**Why `FormatVersion` exists, restated explicitly (also asked for by name in this phase's brief):**
a `.vault` file is meant to outlive the exact version of the app that wrote it — restoring a
years-old backup into a much newer install must still work, or at minimum fail with a clear,
specific message rather than silently misinterpreting old data as if it were the current shape.
Writing the version once, up front, into every file (rather than trying to sniff the payload's
shape after the fact) is what makes that possible at all. `IBackupFormatMigrator` is where that
version gets acted on:

```csharp
public interface IBackupFormatMigrator
{
    int CurrentFormatVersion { get; }
    void EnsureSupported(int version); // throws UnsupportedBackupVersionException if version > CurrentFormatVersion
}
```

`BackupFormatMigrator` is, deliberately, a near-empty version-1 skeleton today — same
"settle the shape of migration handling now, before there's anything to migrate" spirit as
`KeyKeeper.Core.Data.VaultMigrationService` (Phase 2). When a future phase changes the encrypted
payload's JSON shape, the transform step goes inside `EnsureSupported` (or a dedicated migrate
step called from `BackupService`), following the exact pattern `VaultMigrationService.EnsureUpToDate`
already established for the live schema — not a redesign from scratch.

### Encrypted payload (`VaultBackupPayload`, `BackupFolder`/`BackupTag`/`BackupItem`)

A single JSON object holding every folder, tag, and item — `BackupItem.Payload` is a
`JsonElement` (the item's type-specific fields, e.g. a `LoginItem`'s shape) rather than a second
boxed `object`, since it needs to round-trip through JSON either way and this avoids a pointless
extra encode/decode through a concrete C# type during import (`BackupService` just re-serializes
the `JsonElement` straight back out and re-encrypts it — no `VaultItemType`-keyed switch needed on
import at all, unlike export, which does need one to call `JsonSerializer.SerializeToElement(payload,
payload.GetType())` against the boxed runtime type).

### `IBackupService` / `BackupService`

**Key separation on export, not reuse of the live vault's own salt**: `ExportAsync` derives a
completely fresh Argon2id key — a new random salt, `Argon2Parameters.Default` cost — for the
backup's own encryption, rather than reusing the live vault's salt from `IVaultUnlockStore` (which
would derive the *exact same key* SQLCipher already uses for the vault database file). A backup is
a separate artifact with its own lifecycle — cloud storage, email, an old phone — so giving it
independent key material means a compromise of one (backup file or live vault) doesn't directly
hand over the key to the other, even though both are ultimately derived from the same master
password. This costs a second Argon2id derivation per export (one to verify the entered password
against the live vault's own verifier — so a typo is reported as `WrongPassword` instead of
silently producing an unreadable backup — one to derive the backup's own key): acceptable for an
explicit, infrequent, user-initiated operation.

**Import is a two-step, confirm-before-destroy flow**, not a single call:

```csharp
Task<BackupImportResult> PrepareImportAsync(string filePath, char[] masterPassword);
Task CommitImportAsync(BackupImportPreview preview);
```

`PrepareImportAsync` fully validates the password and file — reads it, checks the magic/length
framing, checks `FormatVersion` via `IBackupFormatMigrator`, derives the key from the *header's*
salt/parameters (not the live vault's), and decrypts — entirely without touching the current
vault's data, and returns a `BackupImportResult` whose `Kind` is one of `Success`, `WrongPassword`,
`CorruptFile`, or `UnsupportedVersion` (each with its own Turkish `ErrorMessage`) rather than
throwing for any of these three expected-failure paths — the same "expected failures are a result,
not an exception" precedent `KeyKeeper.Core.Auth.AuthenticationResult` already set for
unlock/lockout. Only a `Success` result carries a `BackupImportPreview` (folder/tag/item counts —
what a confirmation dialog shows the user — plus the decrypted payload itself, held only in
memory). `CommitImportAsync` is the only method that actually touches the live vault, and only
once a UI layer has shown that preview to the user and gotten explicit confirmation.

**Replace, not merge (MVP scope decision)**: `CommitImportAsync` always *replaces* every
folder/tag/item currently in the vault with what the preview holds — it does not attempt to merge
the two sets. A merge needs a real conflict-resolution design (same id in both, same title but
different fields, a folder that exists in one but not the other, ...) that nothing in this phase's
scope asked for; replace is simple, predictable, and matches what "restore a backup" usually means
(recover exactly what a backup captured) rather than "combine two vaults" (a plausible future
feature, deliberately not this one). Documented on `BackupImportPreview` itself, not just here.

**How the replace is actually done**: `CommitImportAsync` doesn't go through
`IVaultItemRepository`/`IFolderRepository`/`ITagRepository`'s `Create` methods — those always
assign a fresh id and "now" timestamps, which would silently discard the backup's original
ids/timestamps/favorite flags on restore. Instead it runs raw SQL directly against
`IVaultDatabaseService`'s connection: `PRAGMA foreign_keys = OFF` (SQLite only honors a change to
this pragma outside a pending transaction, so it has to happen *before* the transaction starts, not
inside it), then inside one transaction: delete `ItemTags`/`VaultItems`/`Tags`/`Folders`, then
insert every folder/tag/item from the backup with its original id preserved, re-encrypting each
item's payload under the *currently open* vault's own key via
`IAuthenticationService.EncryptWithVaultKey` (never the backup's own key — the two are
independently derived and only coincide if the target vault happens to share the same master
password, which import must not assume). `PRAGMA foreign_keys = ON` is restored after the
transaction commits. Toggling FK enforcement this way means folders/items/item-tags can be
reinserted in whatever order the backup file lists them, without a topological sort.

Export, by contrast, reads through the already-tested repositories
(`IFolderRepository.ListAll()`/`ITagRepository.ListAll()`/`IVaultItemRepository.List()`) rather than
raw SQL — every item comes back already decrypted under the live vault key, so `BackupService`
never touches `VaultItems.EncryptedPayload` directly on the way out.

### CSV export (`ICsvExportService`/`CsvExportService`)

One flat CSV, one row per item, columns being the union of every item type's fields (a per-type
column is blank on a row of a different type) — chosen over five separate per-type files or a
nested format, matching what other password managers' plaintext exports typically do and keeping
"open this in a spreadsheet" genuinely simple. Hand-rolled RFC 4180 quoting (comma/quote/newline
triggers wrapping in double quotes, with internal quotes doubled) — no CsvHelper-style dependency,
consistent with this app's minimal-dependencies posture, since the format needed here is small and
fixed. Every field, including passwords/CVVs/license keys, appears in the clear — this is a
deliberate, explicitly-warned-about escape hatch (see `BackupViewModel` below), not a
safe-by-default export path.

### UI (`KeyKeeper/ViewModels/BackupViewModel.cs`, `KeyKeeper/Views/BackupPage.xaml`)

One page, "Yedekleme," reachable from `VaultListPage`'s toolbar (`Routing.RegisterRoute("backup",
...)`, pushed relatively like `folder-management`/`password-generator` — not a `ShellContent`, since
nothing navigates to it absolutely). One shared, masked `MasterPassword` `Entry` feeds all three
actions below — re-typed and re-verified for *each* one (`IBackupService.VerifyMasterPasswordAsync`
for the CSV path, since it doesn't otherwise need a key derivation of its own), never reused from
whatever unlocked the vault originally, so a hijacked UI session still can't export/restore/CSV-dump
without the real master password:

- **"Yedek Oluştur (.vault)"** — `ExportVaultCommand`: calls `ExportAsync`, writes the result to
  `FileSystem.CacheDirectory`, and hands it to `Share.Default.RequestAsync` (a `ShareFileRequest`).
- **"Yedekten Geri Yükle"** — `ImportVaultCommand`: `FilePicker.Default.PickAsync` (any file type —
  registering a custom `.vault` UTI/MIME association is more platform config than this phase's
  scope called for; `VaultBackupFile.Unpack`'s magic-byte check already rejects an unrelated file
  with a clear message regardless of what the user picks) → `PrepareImportAsync` → on
  `Success`, a `DisplayAlert` showing the exact folder/tag/item counts and an explicit "bu işlem
  geri alınamaz" warning → only on confirmation, `CommitImportAsync`.
- **"CSV Olarak Dışa Aktar (Uyarılı)"** — `ExportCsvCommand`: a `DisplayAlert` warning the file will
  be entirely unencrypted, *then* the shared master-password field is checked via
  `VerifyMasterPasswordAsync` (so accidentally tapping through the warning still requires typing the
  real password), then the CSV is built from `IVaultItemRepository.List()` and shared the same way
  as the `.vault` file.

**No `MAUI FileSaver` package** ("write to a location the user picks") — the built-in
`Microsoft.Maui.ApplicationModel.DataTransfer.Share` API already covers it: both Android's and
iOS's native share sheets include a "Save to Files"-equivalent destination alongside whatever apps
can receive the file, so sharing a temp file accomplishes the same "user picks where it ends up"
outcome without a new dependency — consistent with this app's already-small package footprint (see
the Packages table above). The exported file is first written to `FileSystem.CacheDirectory` —
app-private, and on both platforms excluded from backups by OS convention without needing the
`NSFileManager` attribute dance `MauiVaultStoragePathProvider` does for `Library/` (iOS's `Caches/`
isn't backed up by default; Android's cache dir is excluded from Auto Backup the same way
`allowBackup="false"` already excludes everything else) — purely so `Share`/the file picker have a
real path to hand off, not because that temp copy is itself the persisted backup.

**A real bug found during this phase's own manual verification, fixed before commit**: the first
version of `BackupViewModel` set `ErrorMessage` on a `WrongPassword`/`CorruptFile`/
`UnsupportedVersion`/locked-vault outcome without clearing the still-set `StatusMessage` from
immediately beforehand ("Yedek dosyası okunuyor ve doğrulanıyor..."), so both labels rendered
simultaneously — confirmed via a real wrong-password import attempt on the emulator, where
`uiautomator`'s accessibility dump showed both `TextView`s present with their real text (the
`ScrollView` had simply pushed the newly-visible error label below the fold, not hidden it). Fixed
by clearing `StatusMessage` at every point `ErrorMessage` gets set for an outcome, in all three
commands.

### Tests (`KeyKeeper.Core.Tests/Backup/BackupServiceTests.cs`, `CsvExportServiceTests.cs`)

220 tests in the suite as of Phase 8 (up from 202 at the end of Phase 7), all passing. The 18 new
ones use a `VaultHarness` — a complete, real vault stack (real Argon2id/AES-GCM crypto, a real
SQLite-backed vault database via the plain test provider, real repositories, a real
`BackupService`) — and, critically, round-trip tests construct *two independent* harnesses (a
"source" vault being exported from, a "target" vault being restored into, each with its own master
password) specifically to prove import never depends on the exporting vault's own database, key, or
even still existing — exactly the "reset the app, restore on a fresh vault" scenario this feature
exists for:

- Export/import round-trips successfully with the correct password, preserving item/folder/tag
  counts.
- `CommitImportAsync` replaces the target vault's pre-existing content entirely (a pre-existing item
  is gone afterward) while faithfully preserving the imported item's id, folder, favorite flag, and
  tags, and its payload decrypts correctly under the *target* vault's own key (proving the
  re-encryption step, not just that bytes moved).
- Wrong password on export and on import are each reported as a distinct result kind, not thrown as
  exceptions, and never touch anything.
- A structurally unrelated random file, and a file truncated mid-header (long enough to pass the
  magic-byte check but too short for its own declared header length), are each reported as
  `CorruptFile` — including the `ArgumentException` path `AesGcmEncryptionService.Decrypt` throws for
  an encrypted payload too short to contain a nonce+tag, which `PrepareImportAsync` explicitly
  catches alongside `VaultCryptographyException` so no exception ever escapes uncaught.
- A `.vault` file whose header `FormatVersion` is tampered to a future value is reported as
  `UnsupportedVersion`, distinctly from either of the above.
- `VerifyMasterPasswordAsync` returns true/false correctly without touching any file.
- `CsvExportService` produces the documented header row and correct, unencrypted values for all
  five item types, and correctly quotes fields containing a comma or a double quote.

No test exists for `BackupViewModel`'s MAUI wiring (`FilePicker`/`Share`/`DisplayAlert` calls) or for
`VaultBackupFile`'s exact byte layout in isolation (covered indirectly, through `BackupService`) —
same structural reason as every other MAUI-head-only piece in this app (see Phase 3's "Why neither
side has (or can have) an xUnit test"): these need a live OS process. The manual verification below
is this layer's actual coverage.

### Manual verification — Android emulator (`pixel_7_-_api_36_0`)

Driven on the same running emulator as prior phases, via `adb`/`uiautomator`, after a clean
uninstall/reinstall so this phase's setup/data entry happened for real, not on top of leftover state
from an earlier phase's testing. Screenshots and uiautomator-dump text excerpts are checked into
`docs/phase8-verification/`.

**A real export, from a real running vault**: created a folder ("Is"), a Login item ("GitHub" /
"octocat" / a password / a URL, tagged "onemli", filed in "Is"), entered the master password on the
new "Yedekleme" page, and tapped "Yedek Oluştur (.vault)". Confirmed via `adb shell run-as
com.companyname.keykeeper ls` that a real 803-byte `keykeeper-yedek-<timestamp>.vault` file was
written to the app's private cache directory, and the real Android share sheet appeared offering
it ("Sharing 1 file", `keykeeper-yedek-20260912-154033.vault") — `p8_01_export_share_sheet.png`.
The file itself was pulled off-device via `adb exec-out run-as ... cat` (not plain `adb shell cat`,
which allocates a pty and corrupts binary output by translating embedded `\n` bytes to `\r\n` — this
was caught empirically: a first attempt via `adb shell cat > file` produced a 804-byte file for an
803-byte source) — see `p8_07_real_exported_vault_file.vault`.

**A real restore, from that real file, with full data fidelity**: pushed the exported file to the
emulator's `Download/` folder, opened "Yedekten Geri Yükle," entered the master password, and picked
the file via the real Android document picker (`com.google.android.documentsui`). The app correctly
showed the confirmation dialog with the exact real counts — "1 öğe, 1 klasör, 1 etiket içe
aktarılacak..." (`p8_06_import_confirmation_dialog_uiautomator_dump.txt`) — confirming, and the
vault list still showed the folder/tag/item correctly afterward. Opening the restored item and
revealing its password field showed the exact original value, confirming byte-for-byte fidelity
through the full encrypt → decrypt → re-encrypt → decrypt cycle
(`p8_02_import_item_detail_after_restore_uiautomator_dump.txt`) — this was run as a same-vault
restore (replacing the vault's own just-exported data with itself) rather than a second, separate
install, since driving the real Android document picker by coordinates is itself slow enough that
the 2-minute inactivity auto-lock repeatedly fired mid-navigation during this phase's own testing
(see below); the cross-*vault* restore property (a second, independently-keyed target vault, with
its own pre-existing different data, ending up with exactly the source vault's content afterward) is
what `BackupServiceTests.CommitImportAsync_ReplacesTheTargetVaultsExistingContentsWithTheImportedData`
proves instead, unaffected by that on-device timing constraint.

**Wrong password rejected, distinctly**: entered a deliberately wrong password and picked the same
real file. The app correctly reported "Ana parola yanlış ya da dosya bozulmuş." rather than treating
it as any other failure —
`p8_05_wrong_password_import_error_uiautomator_dump.txt`.

**CSV export's warning + re-confirmation shown for real**: tapping "CSV Olarak Dışa Aktar (Uyarılı)"
showed the unencrypted-data warning dialog (`p8_03_csv_export_warning_dialog_uiautomator_dump.txt`);
after acknowledging it and having the master password entered, the real Android share sheet
appeared offering a real `keykeeper-export-<timestamp>.csv`
(`p8_04_csv_export_share_sheet_uiautomator_dump.txt`). The file was pulled the same
`exec-out run-as ... cat` way and confirmed to contain the item's data in the clear with the
documented column set — `p8_08_real_exported_csv.csv`.

**An observed, accepted limitation, not a bug**: the vault's 2-minute inactivity auto-lock timer
(Phase 3) keeps running while a system file picker or share sheet is in the foreground (this app
never pauses it on backgrounding — see "No instant lock-on-background" below), and driving either
one by real screen-coordinate taps through `adb`/`uiautomator` — rather than a real person tapping
at human speed — took long enough on several attempts that the vault auto-locked mid-flow, before a
result could be observed. This isn't a code defect: `PrepareImportAsync` itself never requires the
vault to be unlocked (only `CommitImportAsync` and export do, correctly reflected in both methods'
documented exceptions), so an auto-lock mid-file-pick doesn't corrupt anything, it just means
`IAuthenticationService.Locked`'s central lock-screen navigation (`App.xaml.cs`) can win a race
against whatever the Backup page was about to show. Working around this would mean special-casing
the inactivity timer around backup operations specifically, which nothing in this phase's scope
asked for.

**iOS**: compile-only (`dotnet build KeyKeeper/KeyKeeper.csproj -f net10.0-ios`) — no Mac/Xcode
available in this environment, the same limitation as every other iOS-only piece of this app since
Phase 2.

### Dependency injection (`MauiProgram.cs`)

Added this phase: `IBackupFormatMigrator` → `BackupFormatMigrator`, `IBackupService` →
`BackupService`, `ICsvExportService` → `CsvExportService` (all singletons — stateless/cheap to
construct, same lifetime reasoning as the repositories they sit alongside). `BackupViewModel`/
`BackupPage` registered transient, same as every other page.

## Phase 9: Visual/UX polish (`KeyKeeper/Resources/Styles`, `KeyKeeper/Behaviors`, `KeyKeeper/Views`, `KeyKeeper/ViewModels`)

Pure presentation-layer work — a consistency sweep across every screen, distinct empty states,
accessibility fixes, two small animations, and confirming dark-theme support — with no new
features and, deliberately, almost no logic changes (the two exceptions are noted below). Nothing
in `KeyKeeper.Core` changed this phase.

### Consistency sweep — named styles replacing repeated, drifting inline styling

Before this phase, every page independently inlined the same handful of visual choices, and they
had quietly drifted: `TextColor="Red"`/`BackgroundColor="Red"` for every error message and
destructive button (across `SetupPage`, `LockPage`, all 5 item forms, `FolderManagementPage`,
`PasswordGeneratorPage`, `BackupPage`), and `Style="{StaticResource SubHeadline}"` (a 24pt
page-subtitle style, originally written for `SetupPage`'s welcome description) reused for small,
transient clipboard/status captions on 5 different pages purely because it was already in scope,
not because a one-line status caption should render like a page heading.

`Resources/Styles/Colors.xaml` gained `ErrorTextLight`/`ErrorTextDark` and
`DangerLight`/`DangerDark` — plain hardcoded `"Red"` is legible in light mode but low-contrast
against the dark theme's near-black page background (`OffBlack`, `#1f1f1f`); these are real
Light/Dark pairs, not one color reused for both. `Resources/Styles/Styles.xaml` gained six named
styles built on them: `ErrorLabel`, `StatusCaption` (13pt, muted, centered), `DangerButton`, plus
three chip/card styles (`TagChipBorder`, `TagRemoveLabel`, `FilterChipBorder`, `ResultCard`) that
had been the same `Border`/`Label` properties copy-pasted across every item-form page and
`VaultListPage`/`PasswordGeneratorPage`/`LoginFormPage` respectively. Every page listed above now
references one of these instead of repeating (and inconsistently theming) the values directly —
this *is* the "ortak stil sözlüğü" this phase asked for; `Styles.xaml` already existed since the
MAUI template, it just hadn't accumulated this app's own repeated patterns yet.

**Two real layout bugs found and fixed by this sweep, not just cosmetic drift**:
- `BackupPage`'s three section-description labels ("Tüm kasanızın şifreli, taşınabilir bir
  kopyasını oluşturur.", ...) used `Style="{StaticResource SubHeadline}"` — meaning they rendered
  at 24pt, the same size as a page's main subtitle, instead of as small body captions. This was
  never caught visually during Phase 8 because `BackupPage`, like every other page, can't be
  screenshotted on this Android emulator (see Manual verification below) — Phase 9's *text-only*
  review (reading the XAML, not a screenshot) is what caught it.
- `VaultListPage`'s item-row subtitle also carried `Style="{StaticResource SubHeadline}"`
  (immediately overriding just its `FontSize` back down to 13, but not its inherited
  `HorizontalTextAlignment="Center"`/`HorizontalOptions="Center"`), while the title label above it
  in the same `VerticalStackLayout` was left-aligned (the default) — every row's subtitle was
  centered under a left-aligned title. Fixed by giving the subtitle `StatusCaption` (also fixed
  explicitly left-aligned, since `StatusCaption` itself defaults to centered for its more common
  transient-status use).

### Empty states (`VaultListViewModel.EmptyStateMessage`/`ShowAddFirstItemButton`)

Before this phase, `VaultListPage` showed one static "Henüz öğe yok. Eklemek için sağ üstteki
'Ekle' düğmesine dokunun." for every reason its item list could be empty — a genuinely empty
vault, a search with no matches, or an empty folder/tag/favorites filter — even though only the
first of those is actually fixed by adding a new item. `EmptyStateMessage` (a plain computed
property, re-evaluated and re-notified at the end of `ApplyFilters()`, same place `IsEmpty`
already was) now distinguishes:

| Condition | Message |
|---|---|
| The vault has zero items at all | "Henüz öğe yok. Başlamak için ilk öğeni ekle." |
| A search query matches nothing | `"<query>" için sonuç bulunamadı.` |
| A specific folder filter matches nothing | "Bu klasörde henüz öğe yok." |
| The "Klasörsüz" filter matches nothing | "Klasörsüz öğe yok." |
| Any other filter combination matches nothing | "Bu filtrelerle eşleşen öğe yok." |

`ShowAddFirstItemButton` (`IsEmpty && _allItems.Count == 0`) gates a new "İlk Öğeni Ekle" button,
bound to the same `AddCommand` the toolbar's "Ekle" already used — shown only for the first case,
since a search/filter with no matches isn't fixed by adding an item, it's fixed by changing or
clearing the filter. Verified for real: an empty vault shows the button and it opens the same "Yeni
öğe" action sheet; a non-matching search shows its own message with no button; an empty folder
filter shows its own message with no button (see Manual verification).

### Loading/feedback states — already consistent, reviewed not rewritten

Every `IsBusy`-driven operation in this app (master-password unlock/setup, item save, backup
export/import/CSV) already followed one pattern before this phase: an `ActivityIndicator` bound to
`IsBusy`, an `ErrorLabel`-styled message bound to `HasError`, both toggled the same way. This
phase's review confirmed that consistency rather than introducing a new pattern — the only actual
change here is `ErrorLabel` replacing hardcoded `"Red"` everywhere, covered above.
`FolderManagementViewModel`/`PasswordGeneratorViewModel`/`VaultListViewModel` have no `IsBusy` at
all, deliberately: their operations are local, synchronous SQL/in-memory calls with no perceptible
delay, so there is nothing for a spinner to cover.

### Accessibility

- **Touch targets**: `VaultListPage`'s favorite-star column was 40px wide (`ColumnDefinitions="48,*,40"`)
  — below the ~44pt guideline — widened to 44 (`"48,*,44"`), and the star `Label` itself given an
  explicit `HeightRequest`/`WidthRequest="44"` (it previously had no fixed size, just `FontSize="22"`
  centered in its column). The removable-tag "✕" affordance (`TagRemoveLabel`) is a deliberate,
  smaller-than-44 compromise — see that style's own doc comment in `Styles.xaml` for why a full
  44x44 target there would make every tag chip enormous; it was still padded (`Padding="6,6"`)
  to noticeably grow its actual hit area over the previous bare glyph.
- **Screen-reader labels** (`SemanticProperties.Description`) added to every icon-only or
  otherwise-unlabeled tappable that had none before: the favorite star (`"Favorilere ekle"`/
  `"Favorilerden çıkar"`, switching with the same `DataTrigger` that already switches its glyph),
  the folder/tag filter chips on `VaultListPage` (`"Klasör filtresi: <name>"`/`"Etiket filtresi:
  <name>"`), the removable-tag chips on every item form (`"Etiket: <name>"`/`"Etiketi kaldır:
  <name>"`), the vault-list row itself (`"<title> öğesini aç"`), and the security-warning page's
  "⚠️" glyph (`"Uyarı"`). Text-labeled buttons (every `Button.Text`) already have an accessible
  name for free and weren't touched — adding a redundant `AutomationProperties.Name` equal to the
  visible text would have added noise, not accessibility.
- **Color contrast**: the `ErrorTextLight`/`ErrorTextDark` and `DangerLight`/`DangerDark` pairs
  above exist specifically so error/destructive-action text and button fills stay legible in both
  themes, rather than the one plain `"Red"` this app used everywhere before, which was only ever
  checked against a light background.

### Animation and micro-interactions

- **Page-appear fade-in**: every page now fades in from fully transparent over 180ms
  (`Easing.CubicOut`) instead of appearing with a hard cut — the only motion added, per this
  phase's "abartısız" (understated) brief; no slide, no scale. Folded into the existing
  `PageActivityBehavior` (attached app-wide via the base `Page` style, same as before) rather than
  a second behavior class: a style's `Behaviors` setter in this codebase's XAML source-generation
  setup only successfully attaches a single behavior instance — a second sibling under the same
  `Setter.Value` fails to compile (`MAUIX2002`) — so reusing `PageActivityBehavior`'s own
  `Page.Appearing` hook for this second, unrelated effect was less code and less risk than
  introducing a multi-behavior attachment mechanism just to keep the two concerns in separate
  classes. See that class's own doc comment.
- **Wrong-password shake** (`LockPage`, optional per the brief, implemented since it was cheap):
  the moment `LockViewModel.HasError` flips true (a wrong password or lockout), `LockPage`'s
  code-behind runs a small four-step `TranslateTo` shake on the password `Entry` — purely
  decorative view-layer feedback, so it lives in the page's code-behind (which already subscribes
  to the ViewModel for the `Unlocked` event) rather than becoming a new responsibility of
  `LockViewModel` itself. Verified on-device: entering a wrong password shows the usual
  "Yanlış şifre (1. yanlış deneme)." message with no crash and no logcat exception, and the
  vault still unlocks normally afterward with the correct password.
- Deliberately not attempted: per-item insert/remove animations on `VaultListPage`'s
  `CollectionView` (MAUI's `CollectionView` doesn't support this cleanly without a custom
  `ItemsLayout`/renderer, and the brief's "abartısız" instruction argued against reaching for one)
  — the existing "reload the whole list, then fade the page in" behavior (the page-appear fade
  above, on returning from a form's save) already gives a returning list a soft transition instead
  of a hard cut, which was judged sufficient.

### Dark/light theme

This app already targeted both themes from Phase 0 onward — every color in `Styles.xaml`/
`Colors.xaml` has always used `AppThemeBinding Light=.../Dark=...` (the MAUI template's own
convention, never changed), and `Resources/Styles/Styles.xaml`'s base `Page`/`Shell` styles set an
explicit themed background (`White`/`OffBlack`) rather than leaving it transparent. Phase 9 did not
introduce theme support — it audits and fixes the handful of places that had quietly fallen outside
that convention: every hardcoded `"Red"` (now `ErrorLabel`/`DangerButton`, both theme-aware) and
every tag-chip/result-card `BackgroundColor="{StaticResource Gray200}"` with no `Dark` counterpart
at all (now `AppThemeBinding Light=Gray200, Dark=Gray600` via `TagChipBorder`/`ResultCard`).
Toggled system dark mode on-device (`adb shell cmd uimode night yes`) and confirmed the app keeps
running and re-renders the same content with no crash — see Manual verification for why this
couldn't be a pixel-level before/after comparison.

### Tests

Still 220 (unchanged from Phase 8) — this phase's only `KeyKeeper.Core` change is none at all;
`VaultListViewModel.EmptyStateMessage`/`ShowAddFirstItemButton` are new, but they live in the MAUI
head project, which (like every ViewModel in it) has no dedicated test project, consistent with
this whole codebase's split (`KeyKeeper.Core.Tests` only ever covered `KeyKeeper.Core`). Re-ran the
full suite after every round of changes this phase; still 0 failures throughout.

### Manual verification — Android emulator (`pixel_7_-_api_36_0`)

**A hard constraint specific to this phase**: Phase 7's `FLAG_SECURE` (always on, for the whole
app session, on every page — see that section) means **no page belonging to this app can be
captured by `adb shell screencap` at all**, confirmed again explicitly this phase (a screenshot of
the freshly-reinstalled `SetupPage` came back solid black, identical to every blocked screenshot in
Phases 7-8). A visual polish phase would normally lean on before/after screenshots; here, on
Android, that's structurally impossible without weakening the exact security property Phase 7 added
on purpose — not attempted. iOS has no equivalent blanket block (`SecureOverlayService` only
engages on active screen-recording/backgrounding), but no Mac/Xcode is available in this
environment either (the same limitation as every iOS-only piece of this app since Phase 2).
Verification below instead relies on `uiautomator`'s accessibility-tree dump (which reads every
page's real, rendered text content regardless of `FLAG_SECURE`, since that flag blocks pixel
capture, not the accessibility tree) plus the few overlays that *aren't* part of this app's own
window and so aren't blocked (native `DisplayActionSheetAsync` sheets) — screenshotted where used.
Evidence is in `docs/phase9-verification/`.

- **Empty vault state**: a freshly set-up, empty vault showed "Henüz öğe yok. Başlamak için ilk
  öğeni ekle." and an "İlk Öğeni Ekle" button (`p9_01`); tapping it opened the same "Yeni öğe"
  action sheet the toolbar's "Ekle" does (screenshotted — not `FLAG_SECURE`-blocked, `p9_02`).
- **Adding an item and the vault-list row layout**: added a WiFi item with a tag; the list showed
  it with the title left-aligned above its now-also-left-aligned subtitle (the centering bug
  fixed above), a tag filter chip in the new `FilterChipBorder` style, and the favorite-star
  column/label's bounds confirmed at 44x44 (was 40x40) via the dump's reported pixel bounds scaled
  by this device's density (`p9_03`).
- **Search-with-no-matches state**: typing a non-matching query showed
  `"zzznotfound" için sonuç bulunamadı.` with no add-item button (`p9_04`).
- **Empty-folder state**: created a folder with nothing in it, selected its filter chip, and saw
  "Bu klasörde henüz öğe yok." with no add-item button (`p9_06`).
- **Wrong-password shake**: entering a wrong password on the lock screen produced the usual
  distinctly-worded error with no crash/exception in `logcat`, and the vault still unlocked
  normally with the correct password immediately after (`p9_05`).
- **Dark mode**: toggled system-wide (`adb shell cmd uimode night yes`), confirmed the app kept
  running and the same screen's content re-dumped identically (structural correctness); reverted
  afterward. Pixel-level theme correctness rests on `Styles.xaml`/`Colors.xaml`'s
  `AppThemeBinding` definitions themselves (reviewed directly, not screenshotted) — the same basis
  every prior phase's dark-mode support has stood on, per the FLAG_SECURE constraint above.
- **Style consolidation didn't regress anything**: the `ResultCard` style (replacing duplicated
  inline `Border` styling on `PasswordGeneratorPage` and `LoginFormPage`) was exercised by actually
  generating a password and confirming the card, strength label, and "Kopyala" button all still
  rendered correctly (`p9_07`).
- **iOS**: compile-only (`dotnet build KeyKeeper/KeyKeeper.csproj -f net10.0-ios`) — no Mac/Xcode
  available in this environment, the same limitation as every other iOS-only piece of this app
  since Phase 2.

## Phase 10: Autofill (Android-only)

Android `AutofillService` integration — KeyKeeper can offer to fill saved `LoginItem` credentials
into other apps' and the browser's login forms. **iOS is explicitly out of scope for this phase.**
iOS autofill is a Credential Provider Extension — a separate app target with its own entitlement,
UI, and lifecycle, nothing like Android's in-process `Service`-based model this phase builds on. If
this project ever gets Mac/Xcode access, that's a self-contained future phase, not an extension of
this one. `KeyKeeper.Core/Autofill`'s two interfaces already have permanent, no-op iOS
implementations (`Platforms/iOS/AutofillCompletionHandler.cs`, `AutofillServiceStatusChecker.cs`)
precisely so the cross-platform code (`LockPage`, `SettingsViewModel`) never needs an `#if ANDROID`
— see each interface's doc comment.

### Domain matching (`KeyKeeper.Core/Autofill/AutofillDomainMatcher.cs`)

The one piece of this phase's logic that's pure and platform-independent, so it's the one piece
unit-tested directly (28 tests, `AutofillDomainMatcherTests.cs`) rather than only verified
on-device. `NormalizeHost` strips scheme/path/port/`www.`/casing from a URL or bare hostname;
`IsMatch` then requires the autofill request's host to be the saved item's own host **or a
subdomain of it** (`accounts.google.com` matches a login saved against `google.com`; the reverse
does not — a narrower saved scope shouldn't broaden). Deliberately not a raw substring/suffix check
on unnormalized strings — that would let a saved `google.com` wrongly match an attacker's
`evilgoogle.com` or `google.com.evil.com`; both sides are normalized first and the subdomain check
requires an exact label boundary (a literal `.` immediately before the suffix). Scope: this matches
against a browser-rendered field's web domain (`ViewNode.WebDomain`) only — a native (non-browser)
app's autofill request carries no web domain at all, and this phase doesn't attempt to fuzzy-match
those against a saved URL by package name; see "Explicitly out of scope" below.

### `KeyKeeperAutofillService` (`KeyKeeper/Platforms/Android/KeyKeeperAutofillService.cs`)

Registered via `[Service(Permission = "android.permission.BIND_AUTOFILL_SERVICE", ...)]` +
`[IntentFilter(["android.service.autofill.AutofillService"])]` — the same "declare via a C#
attribute, let the build merge it into `AndroidManifest.xml`" mechanism `MainActivity`'s
`[Activity]` already uses (see Phase 3's manifest-merge notes); nothing was hand-edited into
`AndroidManifest.xml` directly, and the merged manifest was inspected to confirm the `<service>`
element, its permission, and its intent-filter all landed correctly (see Manual verification
below). `BIND_AUTOFILL_SERVICE` is what restricts binding to the OS itself — no app, including this
one, can bind to it directly.

**The security invariant this whole class is built around**: `OnFillRequest` never reads a single
field of vault data while `IAuthenticationService.IsUnlocked` is false. Concretely, in source order:
1. Parse the latest `AssistStructure` (`request.FillContexts[^1].Structure`); bail out
   (`callback.OnSuccess(null)`) if there isn't one.
2. Walk the view tree (`CollectFields`/`ClassifyNode`) collecting `(AutofillId, FieldKind)` pairs
   and the first `ViewNode.WebDomain` found. Field classification prefers explicit `View`
   autofill hints (`AutofillHintUsername`/`Password`/`EmailAddress`) and falls back to the raw
   Android input-type variation flags (`TextVariationPassword`/`WebPassword`/`VisiblePassword`,
   `TextVariationEmailAddress`/`WebEmailAddress`) when a field carries no hint at all — a
   best-effort heuristic, not exhaustive, same "documented, tunable" spirit as `Argon2Constants`/
   `LockoutPolicy`, not a claim every possible login form is recognized.
3. No web domain, or no username/password field found at all → `callback.OnSuccess(null)`. **No
   vault access has happened yet at this point, regardless of lock state** — steps 1-3 only look at
   the `AssistStructure` the OS itself handed over.
4. Resolve `IAuthenticationService`/`IVaultItemRepository` from `IPlatformApplication.Current`'s
   service provider (not constructor injection — like `PageActivityBehavior`, this class is
   instantiated by the Android OS itself, outside this app's DI container, so there's no
   constructor for it to inject into).
5. **Only if `IAuthenticationService.IsUnlocked` is true**: build the real `FillResponse` (see
   `AutofillResponseBuilder` below) and return it.
6. **If locked**: build a `FillResponse` whose only action is `FillResponse.Builder.
   SetAuthentication(autofillIds, pendingIntent.IntentSender, presentation)` — a single
   "KeyKeeper ile aç 🔒" suggestion covering every autofillable field found. The `PendingIntent`
   targets `MainActivity` itself (not a bespoke second lock-screen Activity) with a custom action
   (`AutofillAuthContract.Action`) and the matched `AutofillId`s + web domain as extras — tapping it
   opens **this app's own, completely ordinary lock screen**, nothing autofill-specific. **The real
   credentials are derived only after that unlock actually succeeds** (see the completion flow
   below) — step 6 itself never touches `IVaultItemRepository`.

`AutofillResponseBuilder` (`internal static`, shared by `OnFillRequest`'s step 5 and the completion
flow's step below — the only two places in this app that ever call it) filters
`IVaultItemRepository.List(VaultItemType.Login)` through `AutofillDomainMatcher.IsMatch`, and
builds one `Dataset` per match with a `RemoteViews` presentation (`"{Title} ({Username})"`, using
Android's own `simple_list_item_1` system layout rather than a hand-written layout resource this
phase didn't need) and the real username/password values.

`OnSaveRequest` is implemented (the abstract base class requires it) but is a deliberate no-op
(`callback.OnSuccess()`) — see "Explicitly out of scope" below for why it's never actually invoked
in practice.

### Authentication-required continuation (`MainActivity`, `AutofillCompletionHandler`, `LockPage`)

`IAutofillCompletionHandler` (`KeyKeeper.Core/Autofill`) is what lets the cross-platform unlock flow
finish this job without `LockPage`/`LockViewModel` ever referencing an Android type:

- `MainActivity.OnCreate`/`OnNewIntent` (both needed — `LaunchMode.SingleTop` means either can fire
  depending on whether the task was already running) recognize the authentication `PendingIntent`'s
  launch via its custom action and call `AutofillCompletionHandler.SetPendingRequest(usernameId,
  passwordId, requestHost)` — pulled from `Intent` extras, resolved via `IPlatformApplication.
  Current`'s service provider (registered as a singleton in `MauiProgram.cs` for exactly this: it
  needs to be the same instance `MainActivity` writes to and `LockPage` later reads from).
- The user sees and interacts with the ordinary lock screen — nothing here bypasses master-password
  verification, lockout, or biometric unlock.
- `LockPage.OnUnlocked` (Phase 3's original handler, extended this phase) checks
  `IAutofillCompletionHandler.HasPendingRequest` first: if set, calls `Complete()` instead of the
  usual `GoToAsync("//vault")` — there's no vault screen to show, the user never left their other
  app. On iOS, `HasPendingRequest` is permanently false, so this always falls through to the
  original navigation; no behavior change there.
- `AutofillCompletionHandler.Complete()` (Android) re-checks `IsUnlocked` (defensive; expected
  always true here), calls the **same** `AutofillResponseBuilder.Build` `OnFillRequest` uses, wraps
  the result as `AutofillManager.ExtraAuthenticationResult` on an `Intent`, and calls
  `Activity.SetResult`/`Finish()` on `Platform.CurrentActivity` — handing the real credentials back
  to the OS Autofill framework exactly once, only now, only because the vault is actually unlocked.

### Settings page (`KeyKeeper/Views/SettingsPage.xaml`, `SettingsViewModel`, `IAutofillServiceStatusChecker`)

A MAUI app cannot make itself the OS's default autofill service programmatically on either
platform — the user must choose it themselves in system settings. `SettingsPage` (routed as
`"settings"`, reached from `VaultListPage`'s new "Ayarlar" toolbar item) shows current status
(`IAutofillServiceStatusChecker.IsEnabled()` — Android: `AutofillManager.HasEnabledAutofillServices`
via the OS's `Java.Lang.Class`-typed `GetSystemService` overload, since this binding doesn't expose
a named `Context` field for every system service string constant; guarded by
`OperatingSystem.IsAndroidVersionAtLeast(26)`, since the whole Autofill Framework is API 26+ while
this app's `SupportedOSPlatformVersion` is 23 — see "Target frameworks") and a button
(`OpenAutofillSettingsCommand`) that launches `Settings.ActionRequestSetAutofillService` scoped to
this app's own package (`"package:" + PackageName`) — sending the user straight to the system
picker with KeyKeeper as an option, not the generic autofill-settings list. iOS's implementation is
a permanent "not available" stub (`IsEnabled()` always false, `OpenSettings()` a no-op) — Phase 10
scope.

### Security assessment

Re-read directly from the code (not assumed) for this write-up:
- `OnFillRequest` calls `IVaultItemRepository`/reads any decrypted data **only** inside the
  `if (auth.IsUnlocked)` branch (step 5 above) or inside `AutofillCompletionHandler.Complete()`
  (itself gated on `_auth.IsUnlocked`) — there is no third code path that reaches
  `AutofillResponseBuilder.Build`.
- The locked-path response (step 6) is built entirely from data the OS already handed over in the
  `AssistStructure` (field ids, web domain) — no repository call, no `IAuthenticationService` call
  beyond the `IsUnlocked` check itself.
- `AutofillCompletionHandler` holds `AutofillId`s and a domain string, **never** decrypted vault
  data, between `SetPendingRequest` and `Complete()`/`Cancel()` — see its own doc comment.
- **Interaction with the clipboard clearer (Phase 6)**: autofill fills a form field directly
  (`Dataset.SetValue`), the same mechanism a browser's own saved-password autofill uses — it never
  touches `IClipboard`/the system clipboard at all. There's no interaction to reconcile: autofill is
  strictly an *additional*, clipboard-free way to get a credential into a field, and if anything
  reduces how often a user needs Phase 6's "Kopyala" button (and therefore the clipboard-exposure
  window `ClipboardClearingService` exists to bound) in the first place.

### Tests

`KeyKeeper.Core.Tests/Autofill/AutofillDomainMatcherTests.cs` — 28 tests: scheme/`www.`/casing/
port/path normalization, same-host and subdomain matches, narrower-saved-scope and unrelated-domain
non-matches (including the `evilgoogle.com`/`google.com.evil.com` label-boundary attack shapes),
null/empty-input handling. `dotnet test KeyKeeper.Core.Tests` — 248/248 passing (220 pre-Phase-10 +
28 new), unchanged production logic elsewhere.

`KeyKeeperAutofillService` itself (and `AutofillCompletionHandler`/`AutofillResponseBuilder`) are,
like `BiometricAuthService`/`DeviceSecurityChecker` before them, thin adapters over real OS APIs
(`AssistStructure`, `FillRequest`, the Autofill Framework's own dataset/response types) that don't
exist outside a running Android process — there's no in-process way to unit test "did the OS
actually offer this suggestion" any more than Phase 2 could unit test real SQLCipher key rejection
without a device. That's what Manual verification below is for.

### Manual verification — Android emulator (`pixel_7_-_api_36_0`)

Evidence in `docs/phase10-verification/`. Every item below was actually driven or actually observed
on-device, not inferred from a clean build:

- **Manifest merge**: `obj/Debug/net10.0-android/android/AndroidManifest.xml` contains
  `<service android:name="crc64....KeyKeeperAutofillService" android:exported="true"
  android:label="KeyKeeper" android:permission="android.permission.BIND_AUTOFILL_SERVICE">` with
  the `android.service.autofill.AutofillService` intent-filter action — confirming the
  attribute-based registration actually landed, the same verification method Phase 3 used for
  `MainActivity`'s manifest entry.
- **KeyKeeper selectable and selected as the OS's autofill service**: `adb shell am start -a
  android.settings.REQUEST_SET_AUTOFILL_SERVICE` showed KeyKeeper alongside "None"/"Google" in the
  system picker; after selecting it and confirming, `adb shell dumpsys autofill` reported
  `defaultService=com.companyname.keykeeper/crc64....KeyKeeperAutofillService` — KeyKeeper is
  genuinely the system's chosen provider, not just theoretically eligible.
- **`SettingsPage` correctly reflects real OS state**: with KeyKeeper selected, the page's status
  label read "KeyKeeper şu anda sistemin otomatik doldurma servisi olarak seçili." — confirming
  `IAutofillServiceStatusChecker.IsEnabled()` genuinely queries live OS state, not a cached/assumed
  value.
- **`OnFillRequest` is genuinely invoked by the OS, with correct field/domain analysis**: focusing
  the "Network name" field on Android Settings' own Wi-Fi "Add network" screen (a plain native
  `EditText`, chosen as a target because it reliably participates in the Autofill Framework)
  triggered a real call — confirmed via a temporary `#if PERF_MEASUREMENT`-gated `Console.WriteLine`
  inside `OnFillRequest` (added, observed via `adb logcat`, then fully reverted before committing —
  same "measure for real, then remove the scaffolding" pattern as Phase 11's Argon2id timing
  measurement), which logged `OnFillRequest called` followed by `webDomain= fieldsFound=0
  username=False password=False` — exactly correct: a Wi-Fi network-name field has no web domain
  and isn't a username/password field, so `KeyKeeperAutofillService` correctly found nothing to
  offer, with zero vault access either way.
- **A real, matching web login was *not* live-demonstrated end-to-end, and that gap is itself
  evidenced, not hand-waved**: the intended demo — Chrome navigated to `https://github.com/login`
  (a real page, matching the `LoginItem` created for this phase's testing, `Url =
  "https://github.com"`) — never produced an autofill suggestion. Using the *same*
  `PERF_MEASUREMENT`-instrumented build that had just logged a real call from the Wi-Fi screen
  above, focusing Chrome's username field on that exact GitHub login page produced **zero** log
  output and `adb shell dumpsys autofill` reported "No sessions" — meaning Chrome itself never
  queried the OS Autofill Framework for that page at all, on this specific Chrome build/channel
  (133.0.6943.137) on this emulator image. This was investigated, not just observed once:
  `chrome://flags` was checked for a third-party-autofill-delegation flag
  (`#autofill-third-party-mode-content-provider` was the closest match found; toggling it did not
  change the outcome), and Chrome's own Settings → Google Password Manager screen has no visible
  "use another autofill service" option on this build. The comparative evidence (native field:
  called; Chrome web field, same instrumented build, same session: not called) places this
  specifically as Chrome's own delegation behavior on this emulator/channel, not a defect in
  `KeyKeeperAutofillService` — but it does mean the full "browser suggestion appears → tap → (if
  locked) redirected to KeyKeeper's lock screen → unlock → real value fills in" round trip was
  verified in *pieces* (OS invocation + field analysis on a native target; the authentication
  `PendingIntent`/`Complete()` code path by direct review, not a live trigger) rather than as one
  unbroken live demonstration. **Recommended follow-up**: retry on a physical device or a Chrome
  channel/version where third-party autofill delegation is confirmed enabled.
- **No regression to the ordinary lock/unlock flow**: the vault auto-locked mid-session (inactivity
  timeout) and was unlocked normally afterward — `LockPage.OnUnlocked`'s new
  `HasPendingRequest`-check branch correctly fell through to the original `GoToAsync("//vault")`
  path with no pending autofill request, landing back on `VaultListPage` with the previously
  created items intact, no crash.
- **iOS**: compile-only (`dotnet build KeyKeeper/KeyKeeper.csproj -f net10.0-ios`) — 0 errors,
  9 warnings, unchanged from Phase 9/11 — confirming this phase's shared-code changes (`LockPage`,
  `AppShell`, `VaultListViewModel`/`.xaml`, `MauiProgram.cs`) and the permanent no-op iOS Autofill
  implementations don't regress the iOS target. No Mac/Xcode available, the same limitation as
  every iOS-only piece of this app since Phase 2.

### Build status (Android, Debug)

`dotnet build KeyKeeper/KeyKeeper.csproj -f net10.0-android` — 0 errors, 24 warnings: 18 are the
same pre-existing `NU1608` family (unrelated to this phase); the remaining 6 are `CA1422`
("obsolete since API 33") on `Dataset.Builder`, `Dataset.Builder.SetValue`, `FillResponse.Builder.
SetAuthentication`, and `Intent.GetParcelableExtra(string?)` — all genuinely necessary here, not
oversights: this phase targets API 26 (the Autofill Framework's own minimum), and the API-33+
replacements for these specific calls aren't available down to 26, so using the "old" (still fully
functional, not removed) signatures is the only way to support Android 8.0-12 at all. Every other
new API-26+-only call in this phase's code (`AssistStructure.ViewNode.GetAutofillHints()`/
`InputType`, `AutofillManager`, etc.) is instead covered by explicit `[SupportedOSPlatform
("android26.0")]` annotations — placed on the narrowest member that actually needs it (e.g. only
`AutofillCompletionHandler.Complete()`, not the whole class, so the class stays constructible via DI
on every supported Android version; see that file) — rather than accumulating `CA1416` platform-
compatibility warnings the way an unannotated first draft of this phase briefly did (39 of them,
before this cleanup).

## Phase 11: Release hardening (Android-focused)

Functional development (Phases 0-9) is done. This phase makes the Android build genuinely
releasable: audits every prior phase's security claims against the actual code, turns on R8
obfuscation, measures real device-class performance, produces a real signed APK/AAB, and replaces
the last template placeholders (app icon, splash). iOS is verified only to the extent of "still
compiles" — real Release signing/App Store prep is explicitly deferred, this phase's stated focus
is Android. Autofill (the next functional phase) is untouched.

### Security checklist — every prior phase's claim re-verified against the current code

Each row was re-read from the actual file (not recalled from this document) during this phase:

| Claim | Where | Verified |
|---|---|---|
| Argon2id, m=19456 KiB / t=2 / p=1 (OWASP row) | `Argon2Constants.cs` | Matches; unchanged since Phase 1 |
| AES-256-GCM, fresh random nonce per call, packed `nonce\|\|ciphertext\|\|tag` | `AesGcmEncryptionService.cs` | Matches; unchanged since Phase 1 |
| Master password verifier is HMAC-SHA256 of the derived key, constant-time compared, password/key never persisted | `MasterPasswordVerifier.cs` | Matches; unchanged since Phase 1 |
| Biometric-unlock key wrapping is OS-secure-store-backed (`SecureStorage`/Keystore), Base64 only at that string-API boundary | `WrappedKeyStorage.cs` | Matches; unchanged since Phase 2 |
| `FLAG_SECURE` set once in `MainActivity.OnCreate`, blocks screenshot/screen-record + recents thumbnail app-wide | `MainActivity.cs` | Matches; **also reconfirmed working on this phase's R8-obfuscated Release build** — `docs/phase11-verification/p11_01_vault_list_r8_release.png` is solid black, same signature as every Phase 7-9 screenshot attempt |
| `allowBackup="false"` + `dataExtractionRules` (defense-in-depth) | `AndroidManifest.xml`, `Resources/xml/data_extraction_rules.xml` | Matches; unchanged since Phase 2/7 |
| Root/device-security signals (su binaries, root-manager packages, test-keys tag, non-`user` build type, debugger-attached) | `DeviceSecurityChecker.cs` | Matches; unchanged since Phase 7 |

**Debug/log output never carries sensitive data**: a project-wide search for every logging surface
this codebase could use (`Console.*`, `Debug.*`, `Trace.*`, `Android.Util.Log`, `NSLog`,
`ILogger`/`_logger`) across every `.cs` file returned **zero matches** — this app does not log
anything, anywhere, sensitive or not. The only logging pipeline that exists at all is
`builder.Logging.AddDebug()` in `MauiProgram.cs`, itself gated `#if DEBUG` (compiled out of Release
entirely) and never fed a message referencing a password, key, or TOTP secret regardless. (The
Argon2id timing instrumentation added and removed for this phase's performance measurement, below,
was a temporary `#if PERF_MEASUREMENT`-gated `Console.WriteLine` of an elapsed-milliseconds number
only — never the password or key itself — and was reverted before committing; it does not ship.)

**ProGuard/R8 didn't silently break anything**: see "R8 obfuscation" below for what was actually at
risk and how it was verified, not assumed.

### R8 obfuscation

Before this phase, `KeyKeeper.csproj` set no `AndroidLinkTool` at all for Release, and a Release
`dotnet publish` produced no `mapping.txt` — R8 was not actually shrinking or obfuscating anything,
despite Release builds otherwise looking complete (signed-looking `-Signed.apk`, non-trivial size).
Added explicitly:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release' AND $([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'android'">
  <AndroidLinkTool>r8</AndroidLinkTool>
</PropertyGroup>
```

**What R8 actually touches, and what it doesn't**: R8 (successor to the legacy `proguard` tool,
which this SDK no longer supports as a distinct choice) shrinks and renames the **Java/Kotlin
bytecode** of the Android binding layer this app pulls in (`Xamarin.AndroidX.Biometric` and its
transitive AndroidX/Kotlin dependencies) — it does not touch C# IL at all. The separate mechanism
that *could* affect C# reflection (`System.Text.Json.JsonSerializer.Deserialize<T>` for the five
item payload types in `VaultItemRepository.cs`, and the backup format in `BackupService.cs`/
`VaultBackupFile.cs`) is the .NET IL trimmer (`$(PublishTrimmed)`/`$(TrimMode)`), which this SDK
already defaults to `true`/`partial` for Android Release independent of this phase's change — and
was left at that default rather than disabled, because every `JsonSerializer.Deserialize<T>` call
in this codebase passes a compile-time-known concrete type (`LoginItem`, `CardItem`, `WifiItem`,
`SecureNoteItem`, `LicenseItem`, `VaultBackupPayload`, `VaultBackupHeader`) — exactly the pattern
.NET's trimmer is designed to root correctly (it treats a generic type argument to a
trimmer-annotated API as reachable). `BackupService.cs`'s one runtime-`Type`-based call
(`JsonSerializer.SerializeToElement(i.Payload, i.Payload.GetType())`, used for the backup's
per-item payload) looked riskier on paper, but the same five concrete types are already rooted
elsewhere in the same assembly via the `Deserialize<T>` switch in `VaultItemRepository.cs` — there
is no *additional* type this call could need that isn't already kept.

**Verified, not just reasoned about**: a clean `dotnet publish -c Release -f net10.0-android
-p:AndroidPackageFormat=apk` with `AndroidLinkTool=r8` produced a genuine
`bin/Release/net10.0-android/mapping.txt` (~17 MB, thousands of renamed Java/Kotlin classes) —
concrete proof R8's shrink+obfuscate pass actually ran, not merely configured. Grepping that
mapping for this app's own JNI-registered classes (`MainActivity`, `BiometricAuthService`'s
`AuthenticationCallback`) shows them mapped to **themselves** (`crc64....MainActivity ->
crc64....MainActivity`) — the SDK's own built-in R8 keep rules already protect every JNI entry
point automatically, without any hand-written `-keep` rule from this app. The resulting APK was
then installed on the `pixel_7_-_api_36_0` emulator and driven manually (`docs/phase11-verification/`):
vault setup, lock, master-password unlock, creating a Login item (title/username/password/TOTP
secret) and a Card item, both reappearing correctly in the vault list after a fresh
`VaultItemRepository.List()` reload (i.e. a real encrypt-then-later-decrypt-and-`JsonSerializer.
Deserialize`-back round trip, post-R8, post-trim) — no crash, no `FATAL`/`AndroidRuntime` entry in
`logcat` across the whole session. Every item-payload type shares the identical
`Deserialize<T>`/reflection pattern, so this is treated as representative of all five, not just the
two exercised directly; the `.vault`/CSV backup round trip (the one runtime-`Type` call) was not
separately driven through the on-device Storage-Access-Framework file picker this phase (fragile to
automate reliably, and already covered by the reachability argument above) — a reasonable follow-up
if a future phase wants belt-and-braces confirmation.

### Performance

**Argon2id timing** (the OWASP `m=19456/t=2/p=1` row from Phase 1, unchanged) — measured on-device,
not guessed: a temporary `#if PERF_MEASUREMENT`-gated `Stopwatch` around
`Argon2idKeyDerivationService.DeriveKey`'s `GetBytes` call (added, measured via `adb logcat`, then
fully reverted — see the Security checklist above) on the real Release/R8/AOT-compiled build,
driven through the actual master-password unlock flow on the `pixel_7_-_api_36_0` emulator
(`google_apis_playstore`, x86_64, hardware-accelerated — a reasonable mid-tier-device stand-in
given the same emulator was already this codebase's standard "real device" proxy since Phase 3):

```
Argon2id derive: 457ms (cold, first call)
Argon2id derive: 270ms
Argon2id derive: 282ms
```

Comfortably inside (in fact below) the 0.5-1.5s target band — **no parameter tuning applied**;
weakening `Argon2Constants` below the OWASP-recommended row to chase an even-faster number would be
a security regression this phase has no reason to make. The `p=1` (single-lane) setting also means
there's headroom to raise `Iterations` later if a future security review wants a higher cost
without yet needing multi-core parallelism.

**Large-vault list/search**: `VaultListViewModel.Load()` calls `IVaultItemRepository.List()`, which
decrypts (AES-GCM) and JSON-deserializes every item's payload on every call — the one genuinely
O(n)-per-item-decrypt path in this app; `ApplyFilters()` (search text, folder, tag, favorites) is a
separate, cheap in-memory LINQ filter over that already-decrypted list (see that ViewModel's own
doc comment) and was never a plausible bottleneck. Measured directly against the real
`VaultItemRepository`/`VaultDatabaseService` production code (temporarily, via a throwaway xUnit
`[Fact]` seeding 150 real encrypted `LoginItem`s through a temp SQLite file, timed, then reverted —
same "measure for real, then remove the scaffolding" pattern as the Argon2id measurement above;
desktop CPU, not the emulator, but the same managed AES-GCM/JSON code path that ships):
**`List()` = 55ms for 150 items, in-memory filter = 3ms**. No perceptible lag at this scale;
no optimization made, since none was needed.

### Release build & signing

A `keystore/` directory holds the release-signing keystore and its credentials, **entirely outside
git**: `.gitignore` already excluded `*.jks`/`*.keystore` (pre-existing, since Phase 0), and this
phase added an explicit rule for `keystore/release-local.properties` (the store/key
passwords + alias) alongside it — neither the keystore file nor its passwords are ever meant to
reach the repository, on this machine or any other. `keystore/keykeeper-release.jks` (self-signed,
10000-day validity, RSA-2048, alias `keykeeper`, generated via `keytool -genkeypair`) is this
machine's real release key going forward. **This keystore is the only thing that lets a future APK
update the app already installed by this one** — losing it means every future release needs a new
package identity (see the `ApplicationId` note below); whoever owns this project's release process
should back `keystore/keykeeper-release.jks` and its password up somewhere durable and private
(a password manager attachment, an encrypted archive — never this repo).

Build/sign, from repo root (Windows CI or a release engineer's machine — the exact command this
phase actually ran, credentials read from the gitignored properties file, never typed inline):

```
dotnet publish KeyKeeper/KeyKeeper.csproj -c Release -f net10.0-android \
  -p:AndroidPackageFormat=apk \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningKeyStore=<absolute path to keystore/keykeeper-release.jks> \
  -p:AndroidSigningKeyAlias=keykeeper \
  -p:AndroidSigningKeyPass=<password> \
  -p:AndroidSigningStorePass=<password>
```

(`-p:AndroidPackageFormat=aab` for the Play-Store-submission bundle instead of a sideloadable APK —
both were built and verified this phase.) Output lands in
`KeyKeeper/bin/Release/net10.0-android/com.companyname.keykeeper-Signed.{apk,aab}`.
`apksigner verify --print-certs` against the produced APK confirms it, confirming the certificate
fingerprint matches `keytool -list`'s own report for `keykeeper-release.jks` — i.e. this is a real,
verifiably-signed release artifact, not the SDK's auto-generated debug-like fallback keystore
(which is what every earlier Release build in this repo's history was actually signed with,
unnoticed, since `AndroidSigningKeyStore` was never set before).

**`ApplicationId` is still the MAUI template default, `com.companyname.keykeeper`** — deliberately
*not* changed by this phase, since the real reverse-DNS identifier a Play Store listing will use is
a decision for whoever owns that listing (it must be owned/controlled by them, and — unlike almost
everything else in this phase — is **permanent** once the first real release ships: changing it
later means a new, unrelated app listing, not an update). Flagged here as the one remaining
release-blocking decision that is not this phase's to make unilaterally.

### App icon & splash screen

Both were still the unmodified `dotnet new maui` template placeholder before this phase: `appicon.svg`
a flat `#512BD4` rectangle, and `appiconfg.svg`/`splash.svg` the template's ".NET" wordmark glyph —
present since the repo's initial commit, never revisited by any functional phase. Replaced
`appiconfg.svg` and `splash.svg` with a simple white padlock mark (a rounded-rect body + arc
shackle + a purple `#512BD4` keyhole cut-out matching `appicon.svg`'s background, so it reads as a
punched hole once composited) — `appicon.svg`'s background rect and color were left as-is.
Confirmed by screenshot (`docs/phase11-verification/p11_02_new_icon_splash.png` — the OS splash
screen itself, shown before `MainActivity.OnCreate` sets `FLAG_SECURE`, so unlike every other
screenshot in this app it isn't blocked) that the new glyph renders correctly through the standard
MAUI resizetizer pipeline; the Release+R8 build compiled with 0 errors against it. `ApplicationTitle`
("KeyKeeper") was already correct and untouched.

### iOS: compile-only, unchanged this phase

`dotnet build KeyKeeper/KeyKeeper.csproj -f net10.0-ios` — 0 errors, 9 warnings (the same
pre-existing `NU1608` family as every prior phase) — confirming this phase's Android-only csproj
change (`AndroidLinkTool`, scoped to `$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)'))
== 'android'`) and the shared icon/splash SVG changes didn't regress the iOS target. Real iOS
Release signing (provisioning profiles, an Apple Developer account, App Store Connect) needs
Xcode/a Mac, unavailable in this environment, and was out of scope for this phase by explicit
instruction — deferred, not attempted.

### Tests

`dotnet test KeyKeeper.Core.Tests` — 220/220 passing, unchanged (this phase touched no
`KeyKeeper.Core` production logic; the two temporary measurement scaffolds — the Argon2id
`Stopwatch` and the 150-item perf `[Fact]` — were both added and fully reverted, never committed).

### Manual verification — Android emulator (`pixel_7_-_api_36_0`)

All of the R8/JSON-round-trip and `FLAG_SECURE`-post-R8 verification above was driven for real on
this emulator via `adb`/`uiautomator`, the same methodology established since Phase 3. Evidence in
`docs/phase11-verification/`: `p11_01_vault_list_r8_release.png` (solid black — `FLAG_SECURE`
confirmed still active on the signed Release build) and `p11_02_new_icon_splash.png` (the new
padlock glyph, the one screen this app's own `FLAG_SECURE` doesn't cover).

## Explicitly out of scope so far
- No dedicated tag-management screen (Phase 5) — tags are created inline from an item form's "Yeni
  etiket" field; renaming/deleting a tag entity or `ITagRepository.DeleteUnused` cleanup have no UI
  yet even though the repository fully supports them (see its own tests).
- No on-device/emulator integration tests exercising real SQLCipher key acceptance/rejection
  (see Phase 2's Testing section) — desktop tests cover everything except the actual cipher.
- The iOS `BiometricAuthService` implementation is unverified against real Face ID/Touch ID
  hardware — no Mac/Xcode available in this environment (the Android side *was* verified for
  real, on-emulator — see "Manual verification — Phase 3 UI on the Android emulator" under
  Phase 3).
- No password-strength meter on `SetupPage` (only a minimum-length floor) — `IPasswordStrengthAnalyzer`
  now exists (Phase 6) and is wired into every item-form password field and the standalone
  generator, but not into the master-password field on first-run setup.
- No instant lock-on-background (`App.OnSleep`) — only the inactivity timer and explicit "Kilitle"
  currently lock the vault; backgrounding the app doesn't. Phase 7's iOS `SecureOverlayService` and
  Android's always-on `FLAG_SECURE` hide the *screen content* while backgrounded/in the
  app-switcher, but neither one locks the vault itself — resuming the app still shows the same
  unlocked screen underneath, with no re-authentication prompt.
- No QR-code scanning for TOTP secret entry (Phase 6) — `LoginFormPage`'s TOTP field is manual
  Base32 text entry only; a user has to copy/retype the secret an issuer shows as a QR code.
- No breach-check (HaveIBeenPwned-style) API for password strength (Phase 6, deliberate) —
  `PasswordStrengthAnalyzer` is zxcvbn-only, consistent with this app's offline-first, no-network
  design; see its own doc comment.
- `PasswordGeneratorViewModel`'s clipboard-copy/countdown code duplicates
  `VaultItemFormViewModelBase`'s near-identical implementation (Phase 6) rather than sharing it,
  since the generator page isn't an item form and doesn't derive from that base — not extracted
  into a common helper yet.
- No persistence of the device-security warning acknowledgement (Phase 7, deliberate) — it
  re-evaluates fresh on every cold start, so a genuinely rooted/jailbroken device sees the warning
  again on every launch rather than it being silently remembered forever; see
  `IDeviceSecurityWarningService`'s doc comment.
- No telemetry/reporting of which `DeviceSecurityCheckResult.Signals` fired (Phase 7, deliberate) —
  consistent with this app's offline-first, no-network design; the list exists for a possible
  future in-app debug view, not any external reporting.
- No biometric gate on the device-security warning screen itself (Phase 7) — anyone with the
  device unlocked at the OS level can see and dismiss it, same as any other pre-vault-unlock screen
  in this app.
- Android/iOS jailbreak-detection signal lists (Phase 7) are not exhaustive and are not maintained
  against new root/jailbreak tooling as it appears — see `IDeviceSecurityChecker`'s doc comment for
  why that's an accepted limitation of this whole approach, not a gap specific to this app's list.
- The debugger-attached check (Phase 7) has never been manually verified as a true positive on
  either platform — it's Release-only by design (see its own section above), and no Release build
  with a real debugger attached was exercised on-device this phase; covered by
  `DeviceSecurityWarningServiceTests`' fakes only.
- **Backup restore is replace-only, never merge** (Phase 8, deliberate) — `CommitImportAsync`
  always wipes the vault's existing folders/tags/items before inserting the backup's; see
  `BackupImportPreview`'s doc comment for why a real merge is a separate, harder feature this phase
  didn't attempt.
- No custom `.vault` file-type/UTI registration on either platform (Phase 8) — `FilePicker` is
  opened with no file-type filter, so the user can attempt to pick any file; `VaultBackupFile`'s
  magic-byte check is what turns picking the wrong file into a clear `CorruptFile` message rather
  than a crash, not OS-level filtering.
- No biometric/re-auth gate before *starting* a `.vault`/CSV export beyond re-entering the master
  password (Phase 8) — same trust boundary as everywhere else in this app: whoever can type the
  correct master password can export.
- The inactivity auto-lock timer (Phase 3) is not paused or extended around backup export/import
  (Phase 8, deliberate) — see this phase's manual-verification section for what was actually
  observed and why special-casing it wasn't attempted.
- No dedicated CSV *import* (Phase 8) — only `.vault` files can be restored; CSV is an export-only,
  one-way escape hatch.
- No column/field selection for CSV export (Phase 8) — it's always every column for every item,
  including ones a given item type leaves blank; a "choose which fields" option wasn't asked for.
- No per-item add/remove animations on `VaultListPage`'s `CollectionView` (Phase 9, deliberate) —
  see that phase's Animation section for why a custom `ItemsLayout`/renderer wasn't reached for.
- No pixel-level before/after screenshots for Phase 9's own visual changes — Phase 7's
  `FLAG_SECURE` blocks screenshotting this app's own pages on Android entirely (by design), and no
  Mac/Xcode is available to check iOS; verified via `uiautomator` accessibility dumps instead (see
  Phase 9's Manual verification section).
- The removable-tag "✕" chip affordance (Phase 9) has a smaller-than-44pt touch target by
  deliberate choice — see `TagRemoveLabel`'s doc comment in `Styles.xaml`.
- The five item-form pages' tag-chip `DataTemplate` (Phase 4-9) remains duplicated across all five
  `.xaml` files rather than extracted into a shared custom control — each binds `RemoveTagCommand`
  against its own ViewModel type, and Phase 9 only consolidated the chip's *visual* style
  (`TagChipBorder`/`TagRemoveLabel`), not its markup structure, judging a custom control more
  engineering than a polish phase called for.
- No design-token audit beyond the specific inconsistencies this phase actually found (hardcoded
  reds, the `SubHeadline`-for-captions misuse, non-theme-aware chip backgrounds, the 40px star
  column) — spacing/padding differences between page *kinds* that already differ in purpose (e.g.
  Setup/Lock/SecurityWarning's centered 30/20 layout vs. the item-forms'/tool-pages' 20/16 field
  list) were reviewed and judged intentional, not inconsistencies to unify.
- `ApplicationId` (Phase 11) is still `com.companyname.keykeeper`, the MAUI template default — see
  that phase's "Release build & signing" section for why changing it is deliberately left as an
  open decision, not an oversight.
- No on-device `.vault`/CSV backup round trip driven through the Storage-Access-Framework file
  picker on the R8-obfuscated Release build (Phase 11) — the one runtime-`Type`-based
  `JsonSerializer` call this app makes (`BackupService.cs`) is covered by a reachability argument
  (the same five payload types are already rooted via `VaultItemRepository.cs`'s
  `Deserialize<T>` calls) rather than a driven end-to-end test; see Phase 11's R8 section.
- iOS Release signing/App Store submission prep (provisioning profiles, an Apple Developer account,
  TestFlight) has not been attempted (Phase 11, explicit instruction: Android-focused this phase) —
  iOS remains verified compile-only, the same limitation as every phase since Phase 2.
- No CI/CD pipeline automating the Phase 11 release build/sign steps — `dotnet publish` with the
  signing properties is currently a manual, documented command a release engineer runs by hand.
- iOS autofill (a Credential Provider Extension) is entirely out of scope for Phase 10, by explicit
  instruction — not attempted, not stubbed toward, and a genuinely different mechanism from
  Android's `AutofillService`, not a variant of it. Revisit only if/when Mac/Xcode access exists.
- Native (non-browser) app autofill matching by package name (Phase 10, deliberate) — only a
  browser-reported web domain (`ViewNode.WebDomain`) is matched against a saved `LoginItem.Url`;
  see `AutofillDomainMatcher`'s doc comment for why a package-name fallback wasn't attempted.
- `OnSaveRequest` (Phase 10) is implemented only to satisfy `AutofillService`'s abstract contract —
  no `FillResponse` this app builds ever calls `SetSaveInfo(...)`, so Android has no reason to
  actually invoke it. "Offer to save a new login typed into another app" is a reasonable future
  enhancement, explicitly flagged as optional/not attempted this phase.
- A real matching web-login autofill suggestion was not demonstrated live end-to-end in Chrome on
  this emulator/Chrome-channel this phase (Chrome did not delegate to the OS Autofill Framework for
  a real `github.com/login` page, confirmed via a temporary debug-logged build that *did* receive a
  real call for a native Android Settings field in the same session) — see Phase 10's Manual
  verification section for the full investigation and comparative evidence. The underlying
  mechanism (OS invocation, field/domain analysis, the locked-vault authentication continuation) is
  verified in pieces (native-field on-device trigger + code review + 28 unit tests for the matching
  logic itself), not as one unbroken browser demo. Recommended follow-up: retry on a physical
  device or a Chrome channel confirmed to support third-party autofill delegation.
