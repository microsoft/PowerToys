# Choose the language and actual execution environment

Resolve the Host and read effective settings before writing environment-specific
code. The file extension selects built-in PowerShell (`.ps1`) or Python (`.py`);
the global Python settings select where Python runs. There is no per-script
Python environment or sandbox override.

## Runtime decision

| Task requirement | Candidate | Verify before committing |
|---|---|---|
| Windows cmdlets, Windows APIs, registry, COM, a Windows-only executable | PowerShell, or Windows Python with the necessary supported library | API availability and permission under the configured Windows sandbox. Do not assume host/desktop/admin access. |
| Portable text, JSON, or file processing | Python standard library or PowerShell | Consumer contract, available interpreter, path handling, and encoding. Prefer existing dependencies over installing new ones. |
| Linux commands, `/proc`, Linux libraries or packages | Python in Linux mode | Whether the task requires the selected user distribution or can run in the configured managed image. Windows Python cannot supply these features. |
| A Windows Python package or Win32/COM binding | Windows Python | Package availability in the actual configured interpreter; a similarly named WSL package is not sufficient. |

PowerShell prefers PowerShell 7 when available and otherwise Windows PowerShell
5.1. Do not use 7-only language features or modules unless availability is
established. Scripts use normal `param(...)`, not a custom entry function.
They run non-interactively; do not depend on profile initialization or
`Read-Host`/GUI prompts. Consumers collect declared parameters instead.

## Python environment matrix

Read `python.mode`, `python.windows.interpreterPath`,
`python.wsl.distribution`, `python.wsl.containerImage`, and
`python.timeoutSeconds` from configuration. Treat mode names case-insensitively;
older `managedLinux` values represent Linux/WSL mode. Missing Python configuration
means disabled, not permission to pick an interpreter.

| Effective setting | Actual environment | Consequences |
|---|---|---|
| Disabled | Python cannot execute; normal discovery hides Python scripts | Explain the requirement and guide the user to Settings. Do not enable Python or substitute another environment automatically. |
| Windows | Configured/auto-detected native Windows Python | Windows modules and paths; WSL packages and Linux tools are not automatically available. Global Windows sandbox restrictions still apply. |
| Linux (`wsl`), global MXC off | Selected user WSL distribution (or its default when not specified) | Uses that distribution's `python3`, packages, tools, and filesystem. It is not Windows Python or a managed image. |
| Linux (`wsl`), global MXC on | PowerToys-managed WSLC container from the configured image | Requires supported/prepared MXC, filesystem protection, a configured/cached image with `/bin/sh` and `python3`, and dependencies inside that image/package. Does not use packages or configuration from the user's distribution. |

The Host's `mxc-support --json` probes platform capability, not all task
dependencies. Linux container status checks Python readiness, not whether a
particular script/library will work. Do not download/setup an image, import
unreviewed packages, run dependency-install commands, or change global settings
as an implicit part of script creation. Imports can have side effects too:
diagnostics involving interpreter/package execution need appropriate consent.

## Identify Windows/WSL-specific assumptions

Before creating Python code, explicitly check:

1. Does it import Windows-only modules, load DLLs, use COM/registry, invoke
   `.exe` tools, or affect the Windows desktop/VPN?
2. Does it require Linux-only modules, shell utilities, a named distribution's
   installed packages/services, `/home`, `/proc`, or Linux permissions?
3. Can the algorithm use portable Python (`pathlib`, `tempfile`, `subprocess`
   argument lists) instead? Avoid shell expansion and hardcoded user paths.
4. Are dependencies already in the selected interpreter/distribution/image?
   An environment mismatch is a blocker, not permission to disable isolation.

If requirements cannot be satisfied in the current global environment, describe
the alternatives and what the user would have to configure. Do not invent a
descriptor field to override the global choice. Advanced `x-execute` recipes
are not a workaround for policy: explicit `wsl.exe` execution is refused with
MXC enabled, and recipes do not use built-in Python environment selection.

## Paths, outputs, and lifetime

Consumers send Windows paths. The Host validates then translates declared
primary files and `format: "file-path"` parameters for Linux execution.
Ordinary string options are not path-translated; declare actual auxiliary files
as file parameters. Do not manually prepend `/mnt/c` or run a nested `wsl.exe`.
User-distribution WSL file parameters do not support UNC/device paths.

Use the received paths and `tempfile`/the supplied `TMPDIR` for output.
For managed Linux, return only existing workspace files: guest-only paths,
symlinks, and outside-workspace outputs are refused. User-distribution file
output uses a Windows-backed temporary workspace and mounted-drive paths are
mapped back by the Host. Never write into the installed package or depend on
editing the original selection.

The managed Linux container is warm/shared across scripts and consumers, not a
fresh private machine each run. Avoid persistent globals, background daemons,
fixed temporary names, and assumptions that a previous run left a clean state.
Do not automatically restart WSL or the managed runtime when something fails.

## Feasibility gate

Check filesystem, network, desktop/UI, privilege, dependency, and timeout needs
against effective policy. Capabilities in metadata describe intent, not grants.
Allowing network access inside Linux or a Windows sandbox does not mean a host
VPN can be connected. Linux guest root is not Windows administrator. Never
promise a host-side action merely because a provider command exists.
