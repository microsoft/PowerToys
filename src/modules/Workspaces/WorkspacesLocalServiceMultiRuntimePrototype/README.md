# LocalSystem dynamic direct-WindowsApps runtime prototype

This isolated native prototype validates:

```text
LocalSystem PtLsmrUpdater
  -> dynamically creates PtLsmrRuntime_<owner SID hash>
  -> SCM directly launches the versioned WindowsApps EXE as LocalSystem
```

There is no launcher, packaged-service manifest declaration, per-owner account,
or runtime EXE copy. The updater is a management-plane service; it is not in the
runtime process chain.

## Important identity distinction

The runtime executable is physically supplied and protected by an MSIX package,
but the dynamically created classic service process has **no package identity**:

```text
GetCurrentPackageFullName -> APPMODEL_ERROR_NO_PACKAGE (15700)
packageIdentityPresent=false
```

It is an ordinary Win32 service process executing an immutable file from the
verified WindowsApps package directory. It cannot rely on package-identity APIs,
manifest capabilities, package virtualization, or AppModel activation.

This is acceptable only if the requirement is “use the protected MSIX payload
without another EXE copy.” It does not satisfy a requirement that the runtime
process itself be a packaged process.

## Identity and isolation

- Updater primary SID: LocalSystem, `S-1-5-18`.
- Runtime primary SID: LocalSystem, `S-1-5-18`.
- Runtime name: `PtLsmrRuntime_<SHA256(owner SID)[0..15]>`.
- Each service receives a distinct `NT SERVICE\<name>` service SID.
- The owner SID is validated routing metadata, not a logon identity.
- Each store is SYSTEM-owned and grants access only to SYSTEM, Administrators,
  and the matching service SID.

Because every runtime is LocalSystem, compromise of any runtime is machine
compromise. Service SIDs still separate store/pipe ACLs and routing mistakes,
but they do not reduce LocalSystem privileges.

## Servicing

The already-installed SYSTEM updater owns package staging and SCM repathing:

1. validate every managed service's exact v1 command line;
2. stop all runtimes;
3. stage v2;
4. verify the exact v2 WindowsApps executable;
5. repoint every SCM `ImagePath`;
6. restart every runtime.

The ordering matters. Staging v2 can remove the v1 package directory, so the
updater must validate/capture v1 SCM paths before staging the replacement.

## Validation record

Validated on Windows 11 build 26200 on 2026-08-18.

1. **PASS** — Release x64 `/WX` build; v1/v2 MSIX pack, sign, and verification.
2. **PASS** — two future-owner SIDs dynamically produced two distinct
   LocalSystem services without manifest-declared names.
3. **PASS** — both services ran concurrently from the same v1 WindowsApps EXE
   in Session 0 with distinct nonzero PIDs and distinct service SIDs.
4. **PASS** — both tokens had primary SID `S-1-5-18` and their own service SID.
5. **EXPECTED** — both processes reported `packageIdentityPresent=false`;
   `GetCurrentPackageFullName` returned 15700.
6. **PASS** — the SYSTEM updater stopped both v1 processes, staged v2, repointed
   both SCM paths, restarted them with new PIDs, and obtained v2 evidence.
7. **PASS** — cleanup left zero matching services, packages, roots, and test
   certificates.

## Corrected 1309 interpretation

The earlier prototype verdict incorrectly attributed error 1309 to SCM/AppModel.
The process had reached `ServiceMain`; the prototype then called
`CheckTokenMembership` with a primary token. That API path returned
`ERROR_NO_IMPERSONATION_TOKEN` before evidence was written.

The helper now enumerates `TokenGroups`, matching the already-correct alias
prototype. After that correction, the real package-related result was 15700,
not 1309.

## Verdict

**GO for dynamic per-SID LocalSystem ordinary services that execute a verified
WindowsApps payload without an extra runtime copy.**

**NO-GO if the runtime must itself have package identity.** For that requirement,
use a fixed manifest-declared packaged service or another supported activation
model.
