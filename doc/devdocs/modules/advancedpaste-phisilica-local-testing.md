# Advanced Paste – Phi Silica local testing

How to build, register, and test **Phi Silica** in **Advanced Paste (AP)** on a dev machine,
plus the few things that actually break it.

## How it fits together

AP ships as an **unpackaged, self-contained WinUI 3 exe** (`PowerToys.AdvancedPaste.exe`).
The Windows AI `LanguageModel` (Phi Silica) API is a **Limited Access Feature (LAF)**. For it
to work, all of these must line up:

1. **Package identity** — AP runs with identity granted by the sparse MSIX
   `Microsoft.PowerToys.SparseApp`.
2. **Matching LAF creds** — the token/attestation baked into the exe match the registered
   sparse package's publisher.
3. **AI metadata deployed** — the `Microsoft.Windows.AI*.winmd` files ship next to the exe;
   the AI runtime resolves them **at runtime**.
4. **Model ready** — supported hardware and the on-device model downloaded
   (`GetReadyState() == Ready`).

Two identities — the baked token must match the registered package's publisher:

| Build | Publisher Id | LAF creds |
|-------|--------------|-----------|
| **Dev** | `djwsxzxb4ksa8` | dev default in [`src/PhiSilicaLaf.props`](../../../src/PhiSilicaLaf.props) |
| **Prod** | `8wekyb3d8bbwe` | secret, injected only by `.pipelines/v2/release.yml` |

Non-secret pairing check: the exe's baked **Attestation** must equal the registered package's
**PublisherId**.

## Build + register (dev loop)

```powershell
$repo = "X:\GitHub\PowerToys"; $Plat = "ARM64"; $Cfg = "Debug"   # or x64 / Release

# Build AP only (C#; reuses existing C++ outputs):
dotnet restore "$repo\src\modules\AdvancedPaste\AdvancedPaste\AdvancedPaste.csproj" /p:Platform=$Plat
& "$repo\tools\build\build.cmd" -Path "$repo\src\modules\AdvancedPaste\AdvancedPaste" `
    -Platform $Plat -Configuration $Cfg /p:BuildProjectReferences=false

# Register the dev sparse package (creates + trusts a dev cert, grants identity):
pwsh -ExecutionPolicy Bypass -File "$repo\src\PackageIdentity\BuildSparsePackage.ps1" `
    -Platform $Plat -Configuration $Cfg -DevRegister
# Expect: PublisherId djwsxzxb4ksa8, IsDevelopmentMode True
```

## Check the API

`PowerToys.AdvancedPaste.exe` is a **GUI-subsystem** app — run directly in a console it prints
nothing and returns no exit code. **Redirect** stdout/stderr and wait:

```powershell
$exe = "$repo\$Plat\$Cfg\WinUI3Apps\PowerToys.AdvancedPaste.exe"
$o = "$env:TEMP\ap.out"; $e = "$env:TEMP\ap.err"
$p = Start-Process $exe '--check-phi-silica' -Wait -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput $o -RedirectStandardError $e
"exit=$($p.ExitCode)  stdout=$((Get-Content $o -Raw).Trim())"
Get-Content $e -Raw   # stderr: [phi-silica] LAF unlock status: <…>; ReadyState: <…>
```

| `--check-phi-silica` | `--prepare-phi-silica` (downloads the model) |
|----------------------|----------------------------------------------|
| `0` Available · `1` NotReady · `2` NotSupported / unlock failed | `0` Ready · `1` Failed · `2` NotSupported |

`--check` only reads state; use `--prepare` to trigger the model download (`EnsureReadyAsync`).
On failure it prints the `HRESULT` to stderr.

Confirm the running AP has identity:

```powershell
$apPid = (Get-Process PowerToys.AdvancedPaste -EA SilentlyContinue | Select-Object -First 1).Id
if ($apPid) { & "$repo\src\PackageIdentity\Check-ProcessIdentity.ps1" -ProcessId $apPid }
# Expect a PFN ending in the publisher id that matches the baked attestation
```

## What actually breaks it

- **Missing `.winmd` (most important).** The Windows AI runtime resolves
  `Microsoft.Windows.AI*.winmd` from the app folder at runtime. If they aren't deployed,
  `GetReadyState()` returns `NotReady` and `EnsureReadyAsync()` fails with
  `RO_E_METADATA_NAME_NOT_FOUND` (`0x8000000F`) — even though identity, token, and the AI DLLs
  are all correct. The build emits these winmd into `WinUI3Apps\`; the **installer must harvest
  them** (`*.winmd` is in the inclusion list of
  [`generateAllFileComponents.ps1`](../../../installer/PowerToysSetupVNext/generateAllFileComponents.ps1)).
  Classic symptom: "works from the build output but not from the installer" → check that the
  installed `WinUI3Apps\` contains `Microsoft.Windows.AI*.winmd`.
- **Dev/prod mismatch.** A dev-cred exe running against a prod sparse package (or vice versa)
  makes the LAF unlock silently return `Unavailable`. Keep the exe and the registered package
  the same flavor, and verify with the attestation == publisherId check above.
- **Forgot to redirect.** `--check-phi-silica` in a console prints nothing — that's the
  GUI-subsystem quirk, not a result.

## Cleanup

```powershell
pwsh -ExecutionPolicy Bypass -File "$repo\src\PackageIdentity\BuildSparsePackage.ps1" -Unregister
```

⚠️ This removes any `Microsoft.PowerToys.SparseApp` registration, **including a prod one** from
an installer — reinstall/repair PowerToys to restore it.
