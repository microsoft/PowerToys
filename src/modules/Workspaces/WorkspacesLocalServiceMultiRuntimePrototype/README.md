# LocalSystem dynamic multi-runtime variant

This isolated native prototype tests:

```text
LocalSystem PtLsmrUpdater
  -> CreateService per owner SID
  -> SCM directly starts the WindowsApps PtLsmrRuntime.exe as LocalSystem
```

There is no per-owner launcher/child split. `PtLsmrUpdater` provisions, repaths,
starts, stops, and deletes services, but it does not remain in a runtime launch
chain. Each `PtLsmrRuntime_<SID hash>` is itself the SCM service executable and
implements `ServiceMain`.

The branch intentionally reuses the package identity, service names, and storage
roots from the preceding LocalService prototype and therefore must not coexist
with it on one machine.

## Identity and routing

- Package: `Microsoft.PowerToys.WsLocalSvcMultiRt`, x64 versions `1.0.0.0` and
  `2.0.0.0`.
- Manifest: ordinary `runFullTrust` application with
  `SupportsMultipleInstances="true"`; it has no `desktop6:Service`.
- Updater: persistent `PtLsmrUpdater`, LocalSystem (`S-1-5-18`).
- Runtime: dynamically named `PtLsmrRuntime_<SHA256(owner SID)[0..15]>`,
  LocalSystem (`S-1-5-18`), `SERVICE_SID_TYPE_UNRESTRICTED`.
- The owner SID is validated routing metadata, not the runtime's primary token.
- Each runtime would receive a distinct `NT SERVICE\<service name>` SID for its
  protected store even though all runtime primary tokens are LocalSystem.

The package is staged by the updater. The final test deliberately does not try
to register it as a normal app for SYSTEM: an initial
`RegisterPackageByFullNameAsync` attempt under `S-1-5-18` returned Win32 87
(`ERROR_INVALID_PARAMETER`). Staging still places the verified payload at its
exact WindowsApps path, which is sufficient to test raw SCM process creation.

## Commands

```powershell
.\Build.ps1 -Configuration Release -Clean
.\Package.ps1 -Configuration Release -TrustMachine
.\Lifecycle.ps1 -Verb bootstrap
.\Lifecycle.ps1 -Verb provision-two
.\Lifecycle.ps1 -Verb status
.\Lifecycle.ps1 -Verb cleanup
.\Teardown.ps1
```

Bootstrap is the only elevated controller action. Later requests go to the
already-installed SYSTEM updater through an administrator-only named pipe. The
controller cannot supply a service name, executable path, package identity, or
destination path; the updater derives and validates all of them.

When the last managed owner is removed, the updater calls the current-user
`RemovePackageAsync` overload as SYSTEM. This clears the `S-1-5-18: Staged`
reference; `RemoveForAllUsers` alone only removed registrations and left the
SYSTEM staging reference.

## Validation record

Validated on Windows 11 build 26200 on 2026-08-18.

1. **PASS** — Release x64 `/WX` build completed. v1 and v2 MSIX packages were
   packed, SHA-256 signed, and verified.
2. **EXPECTED REJECTION** — staging v1 as SYSTEM succeeded, but attempting
   `RegisterPackageByFullNameAsync` as SYSTEM returned Win32 87. The package
   state was `S-1-5-18: Staged`; no runtime service had yet been created.
3. **NO-GO, decisive stage-only gate** — provisioning only owner `...-1122`
   created:

   ```text
   service:   PtLsmrRuntime_d286468376b3cbc8
   StartName: LocalSystem
   ImagePath: "C:\Program Files\WindowsApps\
              Microsoft.PowerToys.WsLocalSvcMultiRt_1.0.0.0_x64__tb2xrd195j0e6\
              PtLsmrRuntime.exe" --service-name ... --owner-sid ...
   ```

   SCM immediately stopped it with `Win32ExitCode=1309`
   (`ERROR_NO_IMPERSONATION_TOKEN`), PID 0, and service exit 1309. Event 7023:

   > An attempt has been made to operate on an impersonation token by a thread
   > that is not currently impersonating a client.

   No `evidence.txt` was created, proving that the process did not reach
   `ServiceMain`. The package payload and exact EXE existed at the quoted path.
4. **NOT RUN** — a second owner and v2 update are invalid after the individual
   process-creation prerequisite failed.
5. **PASS** — exact cleanup left zero `PtLsmr*` services, matching packages,
   prototype roots, and test certificates.

## Verdict

**LocalSystem for both updater and runtime does not fix the direct-WindowsApps
dynamic-service failure.** It produces the same 1309, at the same pre-process
boundary, as the LocalService runtime test.

This rules out LocalService privilege or `S-1-5-19` registration as the root
cause. A classic `CreateService` entry still supplies only an account and raw
`ImagePath`; changing that account to LocalSystem does not add the package-owned
activation metadata produced by a signed `desktop6:Service` declaration.

Therefore:

- dynamic per-SID classic services remain viable with a protected payload
  outside WindowsApps;
- direct WindowsApps execution remains viable through fixed manifest-declared
  packaged services;
- dynamic per-SID services pointing directly into WindowsApps remain **NO-GO**,
  including when both updater and runtime use LocalSystem.
