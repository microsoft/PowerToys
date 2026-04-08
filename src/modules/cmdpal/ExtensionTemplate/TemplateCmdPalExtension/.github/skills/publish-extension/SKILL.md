---
name: publish-extension
description: >-
  Publish your Command Palette extension to the Microsoft Store or WinGet.
  Use when asked to publish, distribute, release, deploy to store,
  create MSIX packages, submit to WinGet, set up CI/CD for releases,
  or automate builds with GitHub Actions.
---

# Publish Your Command Palette Extension

Guide for distributing your Command Palette extension through the Microsoft Store, WinGet, or both.

## When to Use This Skill

- Publishing your extension to the Microsoft Store
- Submitting your extension to WinGet for `winget install` discovery
- Setting up GitHub Actions to automate builds and releases
- Creating MSIX packages for Store submission
- Creating EXE installers for WinGet submission

## Publishing Options

| Channel | Package Format | Discovery | Auto-Updates |
|---------|---------------|-----------|--------------|
| Microsoft Store | MSIX bundle | Store app, `ms-windows-store://` link | Yes |
| WinGet | EXE installer | `winget install`, CmdPal browse | Yes (via manifest) |

**Recommendation**: Publish to both for maximum reach. WinGet enables direct discovery from within Command Palette.

## Workflows

### Microsoft Store Publishing
See [store-publishing.md](references/store-publishing.md) for the complete step-by-step guide.

**Summary:**
1. Register for Partner Center
2. Update `Package.appxmanifest` and `.csproj` with Partner Center identity
3. Build MSIX for x64 and ARM64
4. Create MSIX bundle
5. Submit to Partner Center

### WinGet Publishing
See [winget-publishing.md](references/winget-publishing.md) for the complete step-by-step guide.

**Summary:**
1. Switch project to unpackaged mode
2. Create Inno Setup installer script
3. Build EXE installers
4. Submit manifest via `wingetcreate new`
5. Optionally automate with GitHub Actions

## Prerequisites

- [Visual Studio](https://visualstudio.microsoft.com/) with C# and WinUI workloads
- [Partner Center account](https://partner.microsoft.com/dashboard/home) (for Store publishing)
- [GitHub CLI](https://cli.github.com/) (for WinGet publishing)
- [WingetCreate](https://github.com/microsoft/winget-create) — `winget install Microsoft.WingetCreate`
- [Inno Setup](https://jrsoftware.org/isdl.php) (for WinGet EXE packaging)

## Important Notes

- Your extension's CLSID (the `[Guid("...")]` in your main .cs file) must be unique and consistent across all files
- WinGet manifests must include the `windows-commandpalette-extension` tag for CmdPal discovery
- MSIX packages require both x64 and ARM64 builds for Store submission
- WindowsAppSdk must be listed as a dependency in WinGet manifests
