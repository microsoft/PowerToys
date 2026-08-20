PowerToys Workspaces packaged-updater / virtual-runtime prototype
Portable cross-machine validation bundle

Requirements
------------
- x64 Windows 11
- An elevated 64-bit PowerShell session
- No existing PtPuvr prototype installation

The bundle contains a test-only self-signed certificate. The runner temporarily
adds that exact certificate to LocalMachine\TrustedPeople, validates all bundle
file hashes, runs the complete two-runtime test, removes all prototype services,
packages, stores, and ACL-bearing package directories, and then removes the
certificate if it was not already trusted before the test.

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
- updater.breakawayDescendantPackageIdentityPresent = true
- updater.breakawayStageHresult = 0x80070520
- updater.packagedAddResults contains two 0x80070520 results
- updater.deploymentHelperPackageIdentityPresent = false
- runtimes contains two concurrently validated virtual-account services
- verdict = PASS

This test deliberately adds a read/execute ACE to each exact staged runtime
package-version directory, never to the WindowsApps root. A different result on
another OS build is valuable evidence; preserve validation-result.json and the
full console output.
