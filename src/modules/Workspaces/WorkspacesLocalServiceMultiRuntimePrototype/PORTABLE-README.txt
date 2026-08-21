PowerToys Workspaces protected ordinary-PE runtime prototype
Portable cross-machine validation bundle

Requirements
------------
- x64 Windows 11
- An elevated 64-bit PowerShell session
- No existing PtPuvr prototype installation

The bundle contains primary and foreign test-only code-signing certificates.
The runner checks every artifact hash, temporarily trusts both exact
certificates in the machine store so the foreign-candidate rejection reaches
the signer-pin check, runs the full signed-PE lifecycle, verifies the JSON
result, and restores exact prior machine-trust presence for both thumbprints.
It removes prototype services, protected roots, and stores without deleting
unrelated or pre-existing certificates that merely share a subject.

Run
---
1. Extract the ZIP to a local directory.
2. Open PowerShell as Administrator.
3. Run:

   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Run-PortableValidation.ps1

Expected final line
-------------------
PORTABLE VALIDATION PASS: <path>\artifacts\validation-result.json

The result covers LocalSystem bootstrap, two distinct virtual-account
runtimes, protected Program Files/ProgramData ACLs, trusted-foreign signer
pin rejection, anti-downgrade, update, readiness rollback, deterministic
crash and ordinary cleanup-failure recovery, the 32-owner inventory limit,
staging cleanup, exact SCM/runtime evidence, and final teardown.
