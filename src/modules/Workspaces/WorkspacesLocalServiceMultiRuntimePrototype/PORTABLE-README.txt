PowerToys Workspaces protected runtime control-plane prototype
Portable cross-machine validation bundle

Requirements
------------
- x64 Windows 11
- An elevated 64-bit PowerShell 7 (`pwsh.exe`) session
- No existing PtPuvr control-plane prototype installation or prototype test users
- A local extraction directory; do not run the bundle from a network share

The bundle contains a signed WiX companion MSI, signed stable host, signed
versioned updater engines, signed user client, signed policy PEs, signed
release-manifest PEs, runtime artifacts, and three test-only certificates.
All files listed in portable-manifest.json are hash-checked with path
containment and reparse-point rejection before execution.

Run
---
1. Extract the ZIP to a local directory.
2. Open PowerShell as Administrator.
3. Obtain the source commit and portable-manifest SHA-256 from the exporter
   through an independent channel. Do not derive the expected values from the
   extracted ZIP.
4. Run:

   pwsh.exe -NoProfile -ExecutionPolicy Bypass -File .\Run-PortableValidation.ps1 `
     -ExpectedSourceCommit <40-hex-commit> `
     -ExpectedPortableManifestSha256 <64-hex-sha256>

Before reading bundled certificate metadata or launching Lifecycle.ps1, the
runner verifies the portable manifest against both external anchors and then
verifies every contained path, length, and SHA-256. It also requires package
and build provenance to name the same clean source commit. The runner then
snapshots this target machine's exact presence of each test certificate.
Lifecycle.ps1 temporarily trusts only the exact test leaves required for the
prototype. Teardown restores that target-specific certificate state and
removes the MSI, host and runtime services, protected roots, and prototype
local users.

Expected final line
-------------------
PORTABLE VALIDATION PASS: <path>\artifacts\validation-result.json

The result covers MSI-owned LocalSystem host bootstrap, normal-user pipe
authorization through a random registry-published per-start endpoint,
immediate direct-client rejection, a four-instance listener pool with
per-SID starvation resistance, token-derived owner identity, two isolated
leases and runtime services,
bounded no-follow inbox intake with signed exact artifact lengths,
live zero-byte and max-plus-one manifest/runtime/engine rejection,
same-version/hash-generation acquisition semantics, signed release metadata
rejection cases, engine self-servicing and crash recovery, stop-aware
kill-on-close qualification recovery, deterministic mutable-state and journal
.new recovery, runtime transaction recovery and floors, repair
preservation, blocked feature removal, blocked teardown with retained trust,
last-lease cleanup, raw MSI commit-cleanup outcome/root assertions, and exact
final teardown.

This is a validation bundle, not a production installer or trust channel.
Executing the runner itself still requires obtaining it through an appropriate
reviewed path; self-hashing cannot make an untrusted script trusted. Its test
certificates are generated per package run and must never be trusted outside
the validation window. If teardown is blocked while a lease or protected
state remains, it intentionally retains that trust so the signed installed
client can release the owner; it restores certificate state only after exact
product/service/root removal succeeds. Export-PortableArtifacts.ps1 requires
a clean committed worktree, rebuilds and packages from that HEAD, records
source/file provenance, and prints the two values that must be conveyed out
of band.
