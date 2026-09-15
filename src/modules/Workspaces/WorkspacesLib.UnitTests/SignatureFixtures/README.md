# Workspaces signature fixtures

Portable, negative Authenticode fixtures for the Workspaces signature verifier.
These are test tools, not product or installer inputs. No generated binaries,
private keys, or machine-specific paths belong in this folder.

## Requirements

- Windows and PowerShell 7.4 or newer (`pwsh`), not Windows PowerShell 5.1.
- Visual Studio 2022/2026 with the x64 C++ tools and Windows SDK.
- For verification only: a built **Debug** Workspaces launcher supporting
  `--verify-signature`. Release builds do not expose this diagnostic mode.

No extra NuGet packages, signing service, elevation, or certificate installation
is needed. The fixture executable uses the static C++ runtime.

## Generate and verify

Run from the repository root in PowerShell. Use a **new**, dedicated artifact
directory outside the checkout. Relative output paths are resolved against the
caller's working directory; scripts find the repository from their own location.

```powershell
$tools = Join-Path $PWD 'src\modules\Workspaces\WorkspacesLib.UnitTests\SignatureFixtures'
$artifacts = Join-Path (Split-Path $PWD -Parent) ('signature-fixtures-' + [guid]::NewGuid().ToString('N'))

pwsh -NoProfile -File "$tools\Generate-Fixtures.ps1" `
    -OutputDirectory $artifacts -Configuration Debug
if ($LASTEXITCODE -ne 0) { throw 'Fixture generation failed.' }

pwsh -NoProfile -File "$tools\Verify-Fixtures.ps1" -OutputDirectory $artifacts
if ($LASTEXITCODE -ne 0) { throw 'Fixture verification failed.' }
```

`Configuration` selects the standalone fixture's `Debug` or `Release` build;
it never changes the verifier default, `x64\Debug\PowerToys.WorkspacesLauncher.exe`
under this checkout. To use another Debug build, pass `-Verifier` explicitly
to `Verify-Fixtures.ps1`. The generator neither builds nor starts PowerToys.

The generator copies the three native project files to the artifact directory
and invokes the repository's `tools\build\build-common.ps1` MSBuild helper there.
This standalone project has no project/package references, ignores inherited
product build configuration, and is intentionally not in the product solution.
Do not build the project directly in this source folder. No product output,
user fixture collection, or existing registration is overwritten.

Generation refuses an existing output directory, even if empty, and rejects
checkout directories and reparse points. It retains build logs under `build`
and removes staged sources, compiled intermediates, and scratch files in `finally`.
On build failure, inspect `build\build.<configuration>.x64.errors.log`.
`fixtures.json` records relative filenames, build configuration, and SHA-256
hashes. A partial generation has no manifest and must not be used for tests.

## Optional Workspaces capture registration

Signature inspection needs no shortcuts. **Registration is off by default.**
For interactive Workspaces capture testing, opt in explicitly when generating:

```powershell
pwsh -NoProfile -File "$tools\Generate-Fixtures.ps1" `
    -OutputDirectory $artifacts -Configuration Debug -RegisterForWorkspaces
```

As above, generation requires a new output directory. For already-generated
fixtures, preview or perform registration separately:

```powershell
pwsh -NoProfile -File "$tools\Register-Fixtures.ps1" -OutputDirectory $artifacts -WhatIf
pwsh -NoProfile -File "$tools\Register-Fixtures.ps1" -OutputDirectory $artifacts
```

This reuses Windows Script Host shortcut creation and a Shell change notification.
It creates only the five runnable fixtures' `.lnk` files in a dedicated
**current-user** Start menu Programs subfolder named
`PowerToys Signature Fixtures (<output-directory hash>)`. It never registers the
malformed diagnostic, requests elevation, or changes executable bytes or trust.
Different output directories have different shortcut folders. An existing link
must match the generated target, ownership description, empty arguments, and
working directory; otherwise the entire operation refuses to alter any links.

The script polls AppsFolder for up to 45 seconds, reading
`System.Link.TargetParsingPath` and `System.AppUserModel.HostEnvironment`.
Success requires `HostEnvironment=0` for all five targets, which supplies the
desktop metadata Workspaces uses for `canLaunchElevated=true`. Missing metadata
is not treated as zero. Observations are saved to `shell-registration.json`.
If metadata does not appear, registration returns a failure without modifying
workspace JSON; shortcuts may already exist, so retry or remove them explicitly.
The flag is Shell metadata, not an EXE or certificate property.

For manual capture testing:

1. Register the generated fixtures, then open the desired real fixture **normally,
   non-elevated**. Do not launch `diagnostics\NotAnExecutable.exe`.
2. Capture a **new workspace after registration**, containing the desired fixture.
   Existing snapshots retain their old `can-launch-elevated=false` value;
   registration does not update them. Recapture rather than edit JSON.
3. In the new capture, set the fixture to **Run as administrator**, then save.
4. Close the fixture before launching the workspace so "Move existing windows"
   cannot reuse its window and bypass a new launch/signature check.

Keep the fixture non-elevated during capture: the normal capture process may not
be able to query an elevated process. No script starts a fixture or performs
these manual workspace actions.

Remove **only this output directory's generated shortcuts**, before deleting or
moving the artifact directory:

```powershell
pwsh -NoProfile -File "$tools\Register-Fixtures.ps1" -OutputDirectory $artifacts -Remove -WhatIf
pwsh -NoProfile -File "$tools\Register-Fixtures.ps1" -OutputDirectory $artifacts -Remove
```

Removal needs `fixtures.json`, but not intact/matching EXEs, so cleanup still
works if a fixture was deleted or changed. Ownership checks still apply. It
deletes the dedicated shortcut folder only when empty and never removes
unrelated links, other fixture sets, or an existing manually maintained fixture
collection. `-WhatIf` performs no writes, Shell notification, or metadata polling.

The registration helper has dependency-free tests using JSON stand-ins for links
and a mock Shell. These never access the real Start menu, invoke COM, or register
anything. Give the test runner its own new artifact directory:

```powershell
pwsh -NoProfile -File "$tools\Test-Registration.ps1" `
    -OutputDirectory "$artifacts-registration-tests"
```

It tests dry runs, ownership preflight, idempotent add/remove, preservation of
unrelated links, and missing/desktop/UWP host metadata. Results are retained in
`registration-tests.json`; mock shortcut files are cleaned up.

## Cases and assertions

| File | Construction | Expected verifier status |
| --- | --- | --- |
| `Unsigned.exe` | Unmodified, unsigned native GUI app | `unsigned` |
| `SelfSigned.exe` | Intact self-signed code-signing certificate | `certificate-untrusted` |
| `TamperedSignature.exe` | Signed app with one unused RCDATA byte changed | `invalid-signature` |
| `ExpiredCertificate.exe` | Genuinely expired self-signed certificate; no timestamp | `certificate-untrusted` |
| `WrongCertificateUsage.exe` | TLS-server-only EKU; no code-signing EKU | `certificate-untrusted` |
| `diagnostics\NotAnExecutable.exe` | Plain text, not a PE image; never launch | `verification-unavailable` |

Verification checks all six hashes and status categories, inspects the embedded
certificates' validity/EKUs and absence of timestamps, and checks that the tampered
file differs from the signed template only by the intended resource byte.
It records the **observed** `status`, `reason`, and `windowsStatus`, certificate
properties, and per-file PASS/FAIL in `verification-results.json`. Any failed
assertion produces a nonzero exit code; inspect that report for details.

The expired and TLS-only certificates are also untrusted. Windows commonly
returns `CERT_E_UNTRUSTEDROOT` (`0x800B0109`) first for both, **not**
`CERT_E_EXPIRED` or `CERT_E_WRONG_USAGE`. These cases prove certificate properties,
not isolated expiry/EKU HRESULTs or UI warning reasons. Do not install a root,
alter trust policy, or change the clock to force a different result. Primary
failure ordering and local execution/security policies can change observations;
record those differences rather than weakening the assertions or host policy.

The native signer rejects expired certificates. The small test-only CMS helper
therefore re-signs the already-generated Authenticode content: it converts the
legacy content SEQUENCE to a CMS OCTET STRING for hashing, converts it back, and
restores SignedData version 1 from CMS version 3. It checks the CMS signature
mathematically without requiring certificate trust. This helper accepts only the
generated x64 template; it is not a general-purpose PE signing library.

## Safety and cleanup

The five real EXEs are benign `asInvoker` GUI apps that only display a window,
filename, process ID, and elevation state. Generation and verification **never
execute a fixture**, request elevation, edit workspaces, change certificate
stores/registry policy, or install packages. Start menu registration happens
only with `-RegisterForWorkspaces` or an explicit `Register-Fixtures.ps1` call.
Verification starts only the selected Debug launcher's diagnostic mode, which
may write the launcher's ordinary diagnostic logs.

RSA keys and self-signed certificates are created in memory, disposed after
signing, and never exported. Only public certificates embedded in the EXEs
remain. Builds and signing use a scratch directory inside the artifact output.
Regeneration produces new keys, dates, and hashes: cases are reproducible, bytes
are intentionally not deterministic. There is no trusted-success, revocation,
catalog, packaged-app, or isolated explicit-distrust fixture in this set.
A valid signature does not establish that an application is safe.

Keep JSON reports and build logs if needed. If registration was used, run
`Register-Fixtures.ps1 -OutputDirectory $artifacts -Remove` first. Then delete
only the dedicated directory passed to this run:

```powershell
Remove-Item -LiteralPath $artifacts -Recurse -Force
```

There is no certificate, private-key file, or trust-policy cleanup.
Never commit generated EXEs/DLLs, PFX/key/PEM files, build logs, or result JSON.
