# Workspaces service-account App Execution Alias prototype

This isolated, test-only native prototype validates:

`SCM -> protected ordinary launcher PE -> service-account App Execution Alias -> packaged worker`

It is not referenced by PowerToys product projects or runtime.

## Architecture

- **PtAliasProtoLauncher.exe**: normal SCM service with `ServiceMain`, status/control handling, a bounded authenticated named-pipe server, exact staged-package registration through `PackageManager.RegisterPackageByFullNameAsync`, alias launch, package-token verification, readiness evidence, update/rollback, tamper retry, and worker ownership through a kill-on-close job.
- **PtAliasProtoWorker.exe**: x64 full-trust packaged desktop worker exposed as the fixed `PtAliasProtoWorker.exe` alias. It records package full name/version/family, account SID, service SID membership, PID, readiness, and launch count.
- **PtAliasProtoController.exe**: elevation-aware native provisioner/client. Privileged verbs enforce elevation; owner/admin pipe client verbs can run as-invoker. It creates deterministic per-owner `PtAliasProto*` account/service names, profiles, rights, hardened directories, SCM credentials, rotation/repair, intentional 1069 breakage, status, tamper, registration cleanup, and exact teardown.
- **PtAliasProtoSessionBroker.exe**: negative cross-session prototype. A LocalSystem service validates the owner session, moves a LocalSystem helper and a duplicated dedicated-account token into that session, creates a private window station/desktop, and attempts activation from a dedicated-account bridge. It proves ordinary cross-session process creation works but AppModel rejects the own-profile alias with error 1920.
- **Common**: RAII handles/secrets, package policy, protected fixed-layout state/evidence, SID/token helpers, ACLs, and binary protocol.
- **Store**: `%ProgramData%\Microsoft\PowerToys\PtAliasProto\<8-hex-owner-hash>`. SYSTEM, Administrators, and the service account have Full Control; the owner has read/execute only. The product-controlled ancestor and instance directory are protected and SYSTEM-owned.
- **Launcher**: `%ProgramFiles%\PowerToys\PtAliasProto\<hash>\PtAliasProtoLauncher.exe`. SYSTEM/Administrators have Full Control; the service account has inherited read/execute only.

The package identity is fixed to `Microsoft.PowerToys.PtAliasProto`, publisher `CN=PowerToys PtAliasProto Test`, x64, versions `1.0.0.0` and `2.0.0.0`. The family is derived with Windows package identity APIs. Caller full names are hints, never authority.

## Commands

Run from this directory.

```powershell
# Non-elevated build and native policy/protocol self-test
.\Build.ps1 -Configuration Release
.\artifacts\bin\x64\Release\PtAliasProtoSelfTest.exe

# Elevated: create a test-only cert, trust its public cert machine-wide,
# build/sign v1+v2, and stage v1 without registering the interactive user
.\Package.ps1 -Configuration Release -TrustMachine -StageVersion v1

# Elevated lifecycle
.\Lifecycle.ps1 -Verb install
.\Lifecycle.ps1 -Verb test
.\Lifecycle.ps1 -Verb update
.\Lifecycle.ps1 -Verb invalid-update
.\Lifecycle.ps1 -Verb tamper
.\Lifecycle.ps1 -Verb break-1069
.\Lifecycle.ps1 -Verb repair
.\Reboot-Before.ps1             # then reboot manually
.\Reboot-After.ps1
.\Lifecycle.ps1 -Verb uninstall
```

Use `-OwnerSid S-1-...` on lifecycle commands for a different owner. Two instances:

```powershell
.\Lifecycle.ps1 -Verb two-owner -SecondOwnerSid 'S-1-5-21-...'
.\Lifecycle.ps1 -Verb uninstall -OwnerSid 'first SID'
.\Lifecycle.ps1 -Verb uninstall -OwnerSid 'second SID'
```

No password or logon token is required for the second owner. The controller creates independent random credentials and never emits them.

Cross-session negative validation:

```powershell
.\Package.ps1 -Configuration Release -SkipBuild -TrustMachine -StageVersion v1
.\CrossSession.ps1 -OtherOwnerSid 'S-1-5-21-...'
```

The script passes when it reproduces the final AppModel error 1920 after ordinary cross-session process creation has succeeded, then removes services, accounts, packages, and the test certificate.

## Pass/fail matrix

| Requirement | Command/evidence | Pass condition |
|---|---|---|
| Real service-account registration | `install`, protected log | service reaches Running; worker evidence exists after `RegisterPackageByFullNameAsync` |
| Account/package/service SID | `test` | nonzero worker PID, exact full name/family/account SID, `serviceSidPresent=1` |
| Protected binary IPC | every controller client verb | pipe DACL plus impersonated caller is owner SID or Administrators; malformed lengths/commands fail |
| v1 worker in use -> v2 | `update` | v1 healthy first; old worker stops; same alias launches exact v2 |
| Invalid update fail-safe | `invalid-update` | valid fixed-identity v3 hint is rejected because it is not staged; prior worker remains healthy |
| Alias tamper | `tamper` | unpackaged `whoami.exe` leaf is rejected by token identity, killed, exact leaf deleted, package re-registered, one retry succeeds |
| SCM 1069 | `break-1069` | account password changes without SCM; `StartService` returns 1069 |
| Credential repair | `repair` | new random password is applied to account and SCM, service/worker return healthy |
| Reboot | `Reboot-Before.ps1`, `Reboot-After.ps1` | manual reboot only; exact v2 identity and worker recover |
| Uninstall | `uninstall` | service, account, alias leaf, launcher directory, and per-owner store are removed; profile deletion is retried for 30 seconds and is explicitly reported as reboot-pending if AppX still holds the hive |
| Multi-owner | `two-owner` / manual matrix | **Design blocker found:** only one account can actively launch this packaged application in service session 0. A second installation receives `CreateProcess=5`, rolls back its account/service/store transaction, and succeeds only after the first packaged worker stops. The failure remains with `uap10:SupportsMultipleInstances="true"` and when bypassing the alias to launch the installed packaged EXE directly, so it is not an SCM `ImagePath`, alias-leaf, or default single-instance-manifest restriction. |
| Cross-session E2 | `CrossSession.ps1 -OtherOwnerSid <sid>` | **NO-GO confirmed in isolation:** with both packaged workers stopped, the same duplicated token can launch an ordinary PE in the owner session. A LocalSystem helper and dedicated-account bridge also enter that session, but the bridge's absolute own-profile alias returns error 1920. Changing `TokenSessionId` does not create a supported interactive AppX session for the service-logon account. |

## Security assumptions

- Controller/package lifecycle runs elevated; the service account is a regular local non-admin account.
- BCrypt generates a 40-character high-entropy password. Password buffers are passed directly to NetAPI/SCM and securely zeroed; they are never placed in arguments, files, registry values, or logs.
- Account rights include service logon and deny interactive, RDP, network, and batch logon.
- `SERVICE_SID_TYPE_UNRESTRICTED` supplies the unique service SID checked in the packaged worker token.
- The service account receives no service `SERVICE_CHANGE_CONFIG`/`WRITE_DAC` grant.
- The launcher verifies at startup that its account cannot open its own service with `SERVICE_CHANGE_CONFIG`.
- The owner can read but cannot alter state. The service account can write it, so both the elevated controller and launcher rederive deterministic account/service names, bind the state path to the owner SID, verify live account/service SIDs, and revalidate package full names before using identity-bearing fields.
- `Package.ps1` removes its test signing private key from `CurrentUser\My` after packaging. An owner therefore cannot mint another package satisfying the fixed family/publisher policy; production would use Microsoft's controlled signer.
- The WindowsApps directory ACL is never changed. Recovery deletes only the exact alias leaf.
- Cleanup derives and verifies exact `PtAliasProto*` names and the eight-character instance leaf; it never removes a profile/root directory broadly.

## Environment matrix / known limitations

The architecture intentionally depends on OS policy and therefore needs separate runs on:

- Windows 10 22H2, Windows 11 current, and Server/Desktop Experience where supported.
- Microsoft Store enabled/disabled, AppX Deployment Service enabled/disabled.
- App Execution Alias policy enabled/disabled.
- Developer Mode on/off.
- domain GPOs that deny local service logon or package deployment.
- WDAC/AppLocker environments that block locally built or test-signed binaries.
- reboot and multi-owner/domain-owner cases.

AppX can keep a deleted service account's profile hive loaded after its package registration is removed. The native uninstall path retries `DeleteProfileW` for 30 seconds, deletes the service/account/store regardless, and emits `PROFILE_CLEANUP_PENDING` when Windows requires a reboot. The lifecycle harness treats package/profile pending markers as deferred, non-green results instead of claiming complete cleanup. After reboot, run the exact native `cleanup-profile --owner-sid ... --account-sid ...` command printed by the harness; no writable cleanup marker is trusted.

The elevated scripts and binaries are a development harness, not a production trust anchor. Run them only from an ACL-protected developer checkout. Product code must be installed and signed by the PowerToys installer before elevation; it must not execute controller or launcher binaries from a user-writable build tree.

## Prototype verdict

The single-owner lifecycle is viable: service-account registration, alias launch, package-token verification, v1-to-v2 update, tamper recovery, password rotation, 1069 recovery, protected-store writes, and SCM restart all work.

The original per-PowerToys-user architecture is **NO-GO**. Multiple per-user services all run in session 0, and Windows permits only one active packaged process for this package application identity across those service-account tokens. Moving a duplicated service-logon token into the owner's interactive session does not rescue the topology: ordinary PE creation succeeds, but App Execution Alias activation remains unsupported. Even after a LocalSystem helper creates a private desktop and launches a dedicated-account bridge in the target session, `CreateProcessW` on that account's absolute own-profile alias returns error 1920. A real interactive logon owned by the dedicated account would require removing deny-logon protections and introducing password/session/desktop lifecycle, which defeats the intended security model.

## Validation record

Validated on Windows 11 25H2 build 26200:

- PASS: Release x64 build with warnings-as-errors and native self-test.
- PASS: real regular-local-account service, `RegisterPackageByFullNameAsync`, service-owned profile/alias, and package identity.
- PASS: worker token retained the exact local-account SID and unique service SID.
- PASS: v1 worker stopped, v2 staged/registered, same alias launched v2.
- PASS: valid fixed-family v3 hint rejected because it was not staged; v2 worker remained healthy.
- PASS: ordinary-PE alias replacement was rejected by process package-token verification and repaired.
- PASS: SCM 1069 reproduced by desynchronizing account/SCM credentials, then repaired with a fresh password.
- PASS: medium-integrity owner could not overwrite evidence, create a store file, or overwrite the protected launcher.
- PASS: service stop/start recovered the registered v2 worker; reboot checkpoint scripts are ready, but this shared machine was not rebooted.
- PASS with OS-deferred cleanup: service, account, rights, packages, launcher, and store were removed. AppX kept service-account profile hives loaded; native `DeleteProfileW` reported them as reboot-pending.
- FAIL / design blocker: two local-account services could not run the packaged worker concurrently, for either mixed versions or the same version.
- FAIL remained after `uap10:SupportsMultipleInstances="true"` and after replacing alias launch with direct package-install-path launch; both still returned error 5 for the second account.
- FAIL / E2 closed: with no other packaged worker running, duplicated dedicated-account token plus `TokenSessionId=2` launched an ordinary PE, but direct alias and packaged-EXE launches returned error 5. A Session-2 LocalSystem host and same-account bridge eliminated caller-session, desktop, alias-path, named-object, and concurrent-instance variables; the final alias activation returned error 1920.

The certificate is development-only and not timestamped. `-TrustMachine` adds only its public certificate to LocalMachine TrustedPeople; remove it after testing if required by local policy. Direct SCM `ImagePath` to the alias is deliberately not implemented because that path already fails with 1920. Elevated lifecycle execution is intentionally not auto-UAC-launched.
