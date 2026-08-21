PowerToys Workspaces MSIX-delivered raw-updater / virtual-runtime prototype
Portable cross-machine validation bundle

Requirements
------------
- x64 Windows 11
- An elevated 64-bit PowerShell session
- No existing PtPuvr prototype installation

The updater and both runtimes are signed MSIX payloads staged into WindowsApps,
but all services are dynamically registered classic SCM services. No manifest
declares desktop6:Service. The resulting LocalSystem updater and per-owner
virtual-account runtimes have no package identity.

The bundle contains a test-only self-signed certificate. The runner validates
every bundled file hash, temporarily adds that exact certificate to
LocalMachine\TrustedPeople, and exercises:

- two byte-identical updater-v5 bundle bootstraps;
- direct runtime-track-1 stage by the raw v5 updater;
- updater v5 -> v6 upgrade;
- updater v6 -> v5 rollback;
- final updater v5 -> v6 upgrade;
- direct runtime-track-2 stage by the raw v6 updater;
- two concurrently running virtual-account runtimes;
- exact service, package, store, and certificate cleanup.

Run
---
1. Extract the ZIP to a local directory.
2. Open PowerShell as Administrator.
3. Run:

   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Run-PortableValidation.ps1

Expected final line
-------------------
PORTABLE VALIDATION PASS: <path>\artifacts\validation-result.json

Important evidence in validation-result.json
--------------------------------------------
- updater.artifactType = msix-staged-raw-scm
- updater.standaloneVersion = 6.0.0.0
- updater.packageIdentityPresent = false
- updater.packageIdentityError = 15700
- updater.deploymentMode = direct-unpackaged-package-manager
- updater.deploymentHelperPresent = false
- updater.transitions includes idempotent v5 bootstrap, v5->v6, v6->v5, and
  final v5->v6
- updater.directStageResults contains two 0x0 results whose callerProcessId
  matches the updater PID for the corresponding transition
- runtimes contains two concurrently validated virtual-account services
- verdict = PASS

This test deliberately adds a read/execute ACE to each exact staged runtime
package-version directory, never to the WindowsApps root. It also exposes a
same-family updater servicing risk: staging a new updater version may retire
the old WindowsApps path before SCM is repointed. The prototype proves the
mechanism and rollback, not production transaction atomicity.

A different result on another Windows build is valuable evidence. Preserve
validation-result.json and the full console output.
