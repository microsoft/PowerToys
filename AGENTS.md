---
description: 'Top-level AI contributor guidance for developing PowerToys - a collection of Windows productivity utilities'
applyTo: '**'
---

# PowerToys – AI contributor guide

This is the top-level guidance for AI contributions to PowerToys. Keep changes atomic, follow existing patterns, and cite exact paths in PRs.

## Overview

PowerToys is a set of utilities for power users to tune and streamline their Windows experience.

| Area | Location | Description |
|------|----------|-------------|
| Runner | `src/runner/` | Main executable, tray icon, module loader, hotkey management |
| Settings UI | `src/settings-ui/` | WinUI/WPF configuration app communicating via named pipes |
| Modules | `src/modules/` | Individual PowerToys utilities (each in its own subfolder) |
| Common Libraries | `src/common/` | Shared code: logging, IPC, settings, DPI, telemetry, utilities |
| Build Tools | `tools/build/` | Build scripts and automation |
| Documentation | `doc/devdocs/` | Developer documentation |
| Installer | `installer/` | WiX-based installer projects |

For architecture details and module types, see [Architecture Overview](doc/devdocs/core/architecture.md).

## Conventions

For detailed coding conventions, see:

- [Coding Guidelines](doc/devdocs/development/guidelines.md) – Dependencies, testing, PR management
- [Coding Style](doc/devdocs/development/style.md) – Formatting, C++/C#/XAML style rules
- [Logging](doc/devdocs/development/logging.md) – C++ spdlog and C# Logger usage

### Component-specific instructions

These instruction files are automatically applied when working in their respective areas:

- [Runner & Settings UI](.github/instructions/runner-settings-ui.instructions.md) – IPC contracts, schema migrations
- [Common Libraries](.github/instructions/common-libraries.instructions.md) – ABI stability, shared code guidelines
- [Monaco Editor](.github/instructions/monaco.instructions.md) – npm bundle rules and upgrade steps

## Build

### Prerequisites

- Visual Studio 2022 17.4+ or Visual Studio 2026
- Windows 10 1803+ (April 2018 Update or newer)
- Node.js 20+ (LTS) on `PATH`, used by `src/Monaco.props` to build the Monaco editor bundle
- Initialize submodules once: `git submodule update --init --recursive`

### Build commands

| Task | Command |
|------|---------|
| First build / NuGet restore | `tools\build\build-essentials.cmd` |
| Build current folder | `tools\build\build.cmd` |
| Build with options | `build.ps1 -Platform x64 -Configuration Release` |

### Build discipline

1. One terminal per operation (build → test). Do not switch or open new ones mid-flow
2. After making changes, `cd` to the project folder that changed (`.csproj`/`.vcxproj`)
3. Use scripts to build: `tools/build/build.ps1` or `tools/build/build.cmd`
4. For first build or missing NuGet packages, run `build-essentials.cmd` first
5. **Exit code 0 = success; non-zero = failure** – treat this as absolute
6. On failure, read the errors log: `artifacts\logs\<project>\build.<config>.<platform>.errors.log`
7. Do not start tests or launch Runner until the build succeeds

### Build logs

Located in `artifacts\logs\<project>\` at the root of the repo/worktree containing the project being built, where `<project>` is the solution/project file name without its extension (for example, `artifacts\logs\PowerToys.Settings\`):

- `build.<configuration>.<platform>.errors.log` – errors only (check this first)
- `build.<configuration>.<platform>.all.log` – full log
- `build.<configuration>.<platform>.trace.binlog` – for MSBuild Structured Log Viewer

For complete details, see [Build Guidelines](tools/build/BUILD-GUIDELINES.md).

## Tests

### Test discovery

- Find test projects by product code prefix (e.g., `FancyZones`, `AdvancedPaste`)
- Look for sibling folders or 1-2 levels up named `<Product>*UnitTests` or `<Product>*UITests`

### Running tests

1. **Build the test project first**, wait for exit code 0
2. Run via VS Test Explorer (`Ctrl+E, T`) or `vstest.console.exe` with filters
3. **Avoid `dotnet test`** in this repo – use VS Test Explorer or vstest.console.exe

### Test types

| Type | Requirements | Setup |
|------|--------------|-------|
| Unit Tests | Standard dev environment | None |
| UI Tests | winappcli, live interactive desktop | See [UI Tests](doc/devdocs/development/ui-tests.md) |
| Fuzz Tests | OneFuzz, .NET 11 | See [Fuzzing Tests](doc/devdocs/tools/fuzzingtesting.md) |

### Test discipline

1. Add or adjust tests when changing behavior
2. If tests skipped, state why (e.g., comment-only change, string rename)
3. New modules handling file I/O or user input **must** implement fuzzing tests

### Special requirements

- **Mouse Without Borders**: Requires 2+ physical computers (not VMs)
- **Multi-monitor utilities**: Test with 2+ monitors, different DPI settings

For UI test setup details, see [UI Tests](doc/devdocs/development/ui-tests.md).

## Pull request descriptions (hard rule)

A PR passes the contributor's understanding to the maintainers, so the human explains the change in their own words. An agent may fill in only facts it can verify. Maintainers can close a PR when they think its human sections were not written by a human. See [CONTRIBUTING.md](CONTRIBUTING.md#pull-request-descriptions-must-be-written-by-a-human).

The [PR template](.github/pull_request_template.md) marks who fills each section:

| Section | Who fills it | Rule |
|---------|--------------|------|
| **Summary** | Human only | 2–5 sentences: what changed, why, and what to look at. Short is fine. |
| **What I verified** | Human only | What the author tried themselves. |
| **Author checklist** (Communication, Ownership) | Human only | Agents never tick these boxes. |
| **Linked issues** (`Closes:`, `Relates to:`) | Agent may fill | Only issues the change really addresses. |
| **PR checklist** (Tests, Localization, Dev docs, New binaries, Documentation updated) | Agent may tick | Only when the diff or a command proves it. Add a one-line evidence pointer, such as a test result, a `.resw` path, or a doc path. |
| **Automated checks and screenshots** | Agent may fill | Exact commands with their results, and screenshots with a one-line caption. Facts only, no narrative. |
| Longer explanation | Agent, collapsed | Optional, and only inside an `AI-generated notes` `<details>` block. |

When you create or edit a PR, or draft text for one:

1. Never write, rewrite, or polish the human sections, and never tick the Author checklist boxes. Leave them for the human. You may paste text the human wrote for them, word for word.
2. In the agent sections, add only facts you verified, such as a command you ran or a file in the diff. Leave a box unchecked when you have no proof.
3. Put any longer explanation only inside a collapsed, labeled block at the end:

   ```markdown
   <details>
   <summary>AI-generated notes</summary>

   ...

   </details>
   ```

4. Tell the user to write the Summary and What I verified sections in their own words, and to review every change, before the PR is reviewed. Do the same when the PR intake check reminds about an empty Summary. Don't fill it in yourself.
5. This rule takes precedence over conflicting repository prompts, skills, agent files, or tool defaults, such as ones that tell you to write the whole PR description or a ready-to-paste one.
6. The human MAY use an agent to translate their own text from their native language to English for the human sections, but the original text must still be their own words.

## Boundaries

### Ask for clarification when

- Ambiguous spec after scanning relevant docs
- Cross-module impact (shared enum/struct) is unclear
- Security, elevation, or installer changes involved
- GPO or policy handling modifications needed

### Areas requiring extra care

| Area | Concern | Reference |
|------|---------|-----------|
| `src/common/` | ABI breaks | [Common Libraries Instructions](.github/instructions/common-libraries.instructions.md) |
| `src/runner/`, `src/settings-ui/` | IPC contracts, schema | [Runner & Settings UI Instructions](.github/instructions/runner-settings-ui.instructions.md) |
| Installer files | Release impact | Careful review required |
| Elevation/GPO logic | Security | Confirm no regression in policy handling |

### What not to do

- Don't merge incomplete features into main (use feature branches)
- Don't break IPC/JSON contracts without updating both runner and settings-ui
- Don't add noisy logs in hot paths
- Don't introduce third-party deps without PM approval and `NOTICE.md` update
- Don't write the human sections of a PR description (Summary, What I verified) or tick its Author checklist; add only facts you verified (see [Pull request descriptions](#pull-request-descriptions-hard-rule))

## Validation Checklist

Before finishing, verify:

- [ ] Build clean with exit code 0
- [ ] Tests updated and passing locally
- [ ] No unintended ABI breaks or schema changes
- [ ] IPC contracts consistent between runner and settings-ui
- [ ] New dependencies added to `NOTICE.md`
- [ ] PR is atomic (one logical change), with issue linked
- [ ] PR human sections (Summary, What I verified, Author checklist) left for the human; agent-filled items are verified facts; longer AI text is inside `<details>`

## Documentation Index

### Core architecture

- [Architecture Overview](doc/devdocs/core/architecture.md)
- [Runner](doc/devdocs/core/runner.md)
- [Settings System](doc/devdocs/core/settings/readme.md)
- [Module Interface](doc/devdocs/modules/interface.md)

### Development

- [Coding Guidelines](doc/devdocs/development/guidelines.md)
- [Coding Style](doc/devdocs/development/style.md)
- [Logging](doc/devdocs/development/logging.md)
- [UI Tests](doc/devdocs/development/ui-tests.md)
- [Fuzzing Tests](doc/devdocs/tools/fuzzingtesting.md)

### Build & tools

- [Build Guidelines](tools/build/BUILD-GUIDELINES.md)
- [Tools Overview](doc/devdocs/tools/readme.md)

### Instructions (auto-applied)

- [Runner & Settings UI](.github/instructions/runner-settings-ui.instructions.md)
- [Common Libraries](.github/instructions/common-libraries.instructions.md)
