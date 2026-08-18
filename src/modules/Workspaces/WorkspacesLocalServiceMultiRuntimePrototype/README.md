# LocalService multi-runtime service prototype

This isolated native prototype tests the exact topology:

`LocalSystem PtLsmrUpdater -> direct WindowsApps PtLsmrRuntime services -> shared LocalService primary token`

It has no App Execution Alias, launcher/child split, copied runtime EXE, account creation, or reference to `PtAliasProto`.

## Identity and routing

- Package: `Microsoft.PowerToys.WsLocalSvcMultiRt`, publisher `CN=PowerToys Workspaces LocalService Multi Runtime Prototype Test`, x64 versions `1.0.0.0` and `2.0.0.0`.
- Application: `Runtime`, with `uap10:SupportsMultipleInstances="true"`.
- Updater: persistent LocalSystem `PtLsmrUpdater`.
- Runtime: `PtLsmrRuntime_<SHA256(owner SID)[0..15]>`; its SCM account is always `NT AUTHORITY\LocalService`, `SERVICE_SID_TYPE_UNRESTRICTED`.
- The supplied owner SID is only a validated `S-1-5-21-...` routing key. It is not a logon account. Its deterministic service SID is unique even though both runtime primary tokens would be `S-1-5-19`.
- Runtime stores are `%ProgramData%\Microsoft\PowerToys\WorkspacesLocalServiceMultiRuntimePrototype\<suffix>`, SYSTEM-owned, and allow only SYSTEM, Administrators, and that exact `NT SERVICE\<runtime>` SID.

The packaged runtime implements `ServiceMain`. On successful dispatch it records service/owner SIDs, PID, SessionId, LocalService token SID, service-SID membership, full/family package identity, version, installed location, and executable path in `evidence.txt`.

## Commands

Run from this directory.

```powershell
.\Build.ps1 -Configuration Release -Clean
.\Package.ps1 -Configuration Release -TrustMachine
.\Lifecycle.ps1 -Verb bootstrap                 # elevated only: initial updater install
.\Lifecycle.ps1 -Verb provision-two             # attempts both fixed test SIDs; fails if either fails
.\Lifecycle.ps1 -Verb status
.\Lifecycle.ps1 -Verb upgrade                   # only meaningful after both runtimes are Running
.\Lifecycle.ps1 -Verb cleanup                   # cleans both owners and fails if either cleanup fails
.\Teardown.ps1                                  # optional elevated exact prototype teardown
```

`PtLsmrController.exe` is `asInvoker`; its bounded fixed-size pipe request has no service name, package identity, executable path, or destination path field. The pipe DACL is SYSTEM/Administrators only. The controller verifies `GetNamedPipeServerProcessId` against the live SCM updater PID before sending a request. The updater validates owner SID syntax/policy, derives all names, verifies fixed package identity and WindowsApps leaf, and only accepts v1 provision, v2 upgrade, status, or cleanup.

The updater stages the fixed MSIX from its protected Program Files package cache. It duplicates a verified session-0 LocalService primary token only to launch the protected registrar. The registrar performs the current-user `RegisterPackageByFullNameAsync` call under `S-1-5-19`; updater process code never treats LocalSystem registration as LocalService registration.

The controller retries a bounded 100 times for both `ERROR_PIPE_BUSY` and the updater-startup `ERROR_FILE_NOT_FOUND` race, and binds every successful pipe handle to the SCM updater PID captured before the retry loop. The updater uses overlapped pipe connect/read/write waits; a stop signal cancels the exact outstanding operation and joins the server thread before reporting `STOPPED`. There is no detached worker.

## Lifecycle and servicing

Bootstrap is the only elevated installation action. Later provisioning, status, runtime removal, and v1-to-v2 work are pipe calls serviced by the already-running LocalSystem updater. Upgrade stages/registers v2, stops every owner in its SYSTEM-protected instance list, parses the SCM command line, requires exactly the quoted executable plus the two fixed argument pairs, canonicalizes that executable, and requires it equal the verified v1 WindowsApps runtime before repathing it to the verified v2 path. The controller never invokes UAC or executes the runtime. Upgrade is deliberately not attempted after a decisive concurrency failure.

`Teardown.ps1` first asks the updater to remove only the two deterministic runtime services and stores, then removes only this package name, updater service/install root, store root, and this prototype's test certificates from the relevant current-user and local-machine stores. It never uses wildcards for `PtAliasProto`, services, accounts, or packages. AppX may report a LocalService registration as pending removal without a reboot; the script reports any remaining exact package rather than deleting unrelated state.

## Validation record (Windows 11 build 26200, 2026-08-18)

1. **PASS** — `.\Build.ps1 -Configuration Release -Clean` completed with `/WX`; v1 and v2 were packed, SHA-256 signed, and verified.
2. **PASS** — The LocalSystem updater started. Stop completed promptly and its process exited both while idle and while an elevated `NamedPipeClientStream` was connected but sent no request.
3. **NO-GO, decisive individual gate** — provisioning only `...-1122` created one direct LocalService service, while `...-1123` did not exist. The first stopped immediately with `Win32ExitCode=1309` (`ERROR_NO_IMPERSONATION_TOKEN`), PID 0, and service exit 1309.
4. **NO-GO, two-owner integration** — from a clean state, `Lifecycle.ps1 -Verb provision-two` deliberately attempted both SIDs. One LocalSystem updater remained Running; both deterministic runtime services were created with LocalService and direct image paths:

   ```text
   PtLsmrRuntime_d286468376b3cbc8  owner ...-1122
   PtLsmrRuntime_5356246defed8412  owner ...-1123
   "C:\Program Files\WindowsApps\Microsoft.PowerToys.WsLocalSvcMultiRt_1.0.0.0_x64__tb2xrd195j0e6\PtLsmrRuntime.exe" ...
   ```

   Neither remained Running. Each stopped immediately with `Win32ExitCode=1309` (`ERROR_NO_IMPERSONATION_TOKEN`), PID 0, service exit 1309. SCM Event 7023 gives the exact message: *“An attempt has been made to operate on an impersonation token by a thread that is not currently impersonating a client.”* The v1 package was present at the quoted WindowsApps `ImagePath`, `PtLsmrRuntime.exe` existed there, and `PackageUserInformation` showed the LocalService (`S-1-5-19`) registration as `Installed(pending removal)`. Successful controller replies with the service exit prove this was not an updater-pipe race. No runtime `evidence.txt` exists because the process did not reach `ServiceMain`, so there is no runtime token/session evidence to collect. The exact quoted executable and fixed `--service-name`/`--owner-sid` arguments rule out a malformed ImagePath or arguments; package registration and executable presence rule out missing registration or payload.

   Decimal 1309 must not be confused with `ERROR_CANNOT_IMPERSONATE`, which is
   decimal 1368. Error 1309 means that an internal startup path tried to obtain
   or use the current thread's impersonation token, but the thread was not
   impersonating a client. The prototype does not identify the undocumented
   internal AppModel/SCM function that returns the error. The supported
   architectural interpretation is that a classic `CreateService` registration
   supplies only an account logon token and raw `ImagePath`; it does not supply
   the package-aware service activation metadata created by a manifest-declared
   `desktop6:Service`. Merely executing an EXE from WindowsApps does not turn a
   classic service start into packaged-service activation.
5. **NOT RUN** — v2 repath/restart concurrency is conditional on the preceding gate and is invalid after the direct launch failure.
6. **PASS** — exact `Lifecycle.ps1 -Verb cleanup` followed by `Teardown.ps1` removed all `PtLsmr*` services, package registrations, prototype install/store roots, and the machine test certificate.

## GO / NO-GO

**NO-GO for the requested topology on this machine.** The prerequisite individual direct packaged LocalService runtime fails before same-account concurrency is reached; the two services were only attempted sequentially and neither reached `ServiceMain`. `SupportsMultipleInstances` cannot change a launch failure that occurs before runtime service dispatch. Do not work around this result with an App Execution Alias, launcher, copied binary, different primary account, or per-owner account: each would test a different topology.

## Security assumptions

This is a test-only development harness. The MSIX certificate is self-signed, machine-trusted only when `-TrustMachine` is used, and its private key is deleted after packaging. The WindowsApps ACL is not modified. Production needs an installer-controlled updater binary and a production signer.
