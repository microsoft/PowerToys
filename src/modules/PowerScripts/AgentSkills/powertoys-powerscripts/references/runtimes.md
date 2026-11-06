# Runtime and environment

`.ps1` selects PowerShell; `.py` selects globally configured Python.
No per-script Python environment or sandbox override.

| Requirement | Choose/check |
|---|---|
| Windows cmdlets/APIs, COM, registry, `.exe` tools | PowerShell or supported Windows Python; verify sandbox/API permissions |
| Portable text/JSON/files | PowerShell or Python standard library; prefer existing dependencies |
| Linux utilities/libraries, `/proc`, distro services | Linux Python; distinguish user distro from managed image |

PowerShell uses 7 when available, otherwise 5.1. Verify 7-only features/modules.
Use `param(...)`; no profile dependency, `Read-Host`, GUI prompt, or custom entry function.

## Python modes

Read `python.mode`, `python.windows.interpreterPath`, `python.wsl.distribution`,
`python.wsl.containerImage`, `python.timeoutSeconds`, and global `mxc`.
Modes are case-insensitive; legacy `managedLinux` means Linux/WSL.
Missing Python settings mean disabled.

| Mode | Actual environment |
|---|---|
| Disabled | Hidden from normal discovery; do not enable or substitute implicitly |
| Windows | Configured/auto-detected native interpreter; not WSL packages/tools |
| `wsl`, MXC off | Selected user distro, or its default; that distro's `python3`/dependencies |
| `wsl`, MXC on | Managed WSLC image, not user-distro packages; needs prepared/supported MXC, filesystem protection, cached/configured image with `/bin/sh`, `python3`, and task dependencies |

Check Windows-only imports/DLLs/COM versus Linux-only APIs/services, dependency
availability in the actual environment, and filesystem/network/desktop/privilege
and timeout requirements. Prefer portable APIs and argument lists over shell strings.
Support/readiness probes do not prove a task's imports work; executing imports or
installing dependencies needs consent. Network permission does not grant host
VPN/desktop access; guest root is not Windows administrator.

If incompatible, explain alternatives. Do not change settings or invent metadata
overrides. Advanced `x-execute` recipes do not select built-in Python environments;
explicit `wsl.exe` recipes are refused with MXC enabled.

## Paths and lifetime

- Consumers supply Windows paths. The Host translates primary files and
  `format: "file-path"` options for Linux; ordinary strings are not translated.
  Do not prepend `/mnt/c` or nest `wsl.exe`. User-WSL file options reject UNC/device paths.
- Use received paths and `tempfile`/supplied `TMPDIR`. Managed Linux outputs must
  be existing workspace files, not symlinks or guest-only/outside paths.
  User-WSL output uses a Windows-backed temporary workspace; the Host maps
  mounted-drive paths back. Never write into packages or selected originals.
- Managed containers are warm/shared, not fresh private machines per invocation.
  Use unique temporary names; no persistent globals, daemons, assumed clean state,
  or automatic runtime/WSL restarts.
