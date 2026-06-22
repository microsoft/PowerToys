#Requires -Version 7.0
# _helpers.ps1 — CmdPal-specific test helpers. Originally a single 1592-line
# file; split in review-#5 batch into topical sub-files under helpers/ for
# readability. This file is now a thin orchestrator that dot-sources them.
#
# Design: dot-sourced (not a module) — these helpers reference $cpHwnd,
# $cpSettings, $cpDataDir, $cpEnabled as script-scope variables set by
# the orchestrator (command-palette-checklist.ps1). When the orchestrator
# dot-sources this file, the script scope is shared, so the references
# resolve to the orchestrator's values. The same applies transitively to
# the helpers/cmdpal-*.ps1 files dot-sourced below.

# Assertion vocabulary first — every helper file below can use Assert-*.
# (Also re-dot-sourced from command-palette-checklist.ps1 BEFORE 01-Bootstrap
# loads, because Test-Case bodies in that file run at registration time.)
. (Join-Path $PSScriptRoot 'helpers\Assertions.ps1')

# Topical helpers (split in review-#5). Order matters: lifecycle/query before
# settings (which reuses Reset-CmdPalToHome); providers before settings-ui
# (which reuses Reset-CmdPalToHome / Use-CmdPalSubPage).
. (Join-Path $PSScriptRoot 'helpers\cmdpal-lifecycle.ps1')
. (Join-Path $PSScriptRoot 'helpers\cmdpal-query.ps1')
. (Join-Path $PSScriptRoot 'helpers\cmdpal-settings.ps1')
. (Join-Path $PSScriptRoot 'helpers\cmdpal-providers.ps1')
. (Join-Path $PSScriptRoot 'helpers\cmdpal-settings-ui.ps1')

