#Requires -Version 7.0
# 12-TerminalProfiles-Stub.tests.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.
# ── Box L1033-L1034: Windows Terminal profile launches wt.exe ───────
# Read profile names from WT settings.json, query CmdPal for one we know
# exists, invoke (this WILL spawn wt.exe), wait briefly for the process
# to start, kill it in cleanup. We pick "Windows PowerShell" because
# it's universal across Windows installs.
Invoke-AAATest -Tag direct -Id 'CmdPal_TerminalProfiles_OpenProfileLaunchesWtExe' `
    -Name "Box L1033: Windows Terminal '<profile>' invocation spawns wt.exe (FUNCTIONAL e2e)" `
    -Ignore -IgnoreReason 'WindowsTerminalProfiles provider is enabled in ProviderSettings (verified by SettingsSchema bucket) but does NOT surface profile entries in inline CmdPal home queries — typing profile names like "Windows PowerShell" / "Azure Cloud Shell" / "wt" / "terminal" returns 0 results from this provider (other providers may shadow with same names). Investigated 2026-05-15: the provider may require a dedicated keyword or sub-page navigation we have not yet discovered. Likely needs CmdPal source-code inspection to find the right query semantics.' `
    -Act { } -Assert { }


# ── ★ 0.99.0 NEW (regression guard): rapid typing does not crash CmdPal ─
# 0.99.0 fixed a typing-induced crash; this guards the regression by
# issuing 50 rapid set-value operations with random strings and asserting
