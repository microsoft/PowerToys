#Requires -Version 7.0
# 90-SkippedRegistry.ps1 — extracted from command-palette-checklist.ps1 during Phase 2b split.
# Dot-sourced from the orchestrator so it shares script scope ($cpHwnd,
# $cpSettings, $cpEnabled, $cpDataDir). See _helpers.ps1 for the
# CmdPal-specific helper functions these tests call into.
$skipped = @(
    # ─── YELLOW: PROTOTYPE — implementable, needs ~30min probing each ──
    @('CmdPal_Files_ContextMenu_OpenAndCopyPathAndShowInFolder',
      'Box L1025-L1027: drive context menu (Ctrl+K) and exercise Open / Copy Path / Show in folder',
      'NEEDS-FEATURE: investigated 2026-05-20 — the CommandsDropdown for the Files notepad.exe entry in CmdPal 0.10.11181.0 contains only two items: ''Search apps'' and ''Settings''. ''Show in folder'' / ''Copy path'' / ''Open with...'' do NOT exist in this version''s Files context menu. The CommandsDropdown is fully accessible (not virtualized) — there are simply no such items to drive. Revisit when the Files provider adds per-file actions.'),
    @('CmdPal_Pin_PinCommandToDockViaContextMenu',
      '★ 0.99.0: Pin command to Dock via context menu (title/subtitle dialog)',
      'PROTOTYPE: dialog-driving prototype. The Pin to Dock entry IS reachable (verified by CmdPal_Pin_PinToDockDialogAppearsAfterMoreMenuClick which clicks it and asserts the dialog popup appears). This entry would extend that to fill in the title/subtitle and assert the new DockSettings.StartBands entry on disk. Implementable now — recipe: PinToDockDialogAppearsAfterMoreMenuClick + drive the dialog''s text boxes via Set-UiaText + invoke the dialog''s confirm button + read settings.json.'),
    @('CmdPal_Registry_NavigateAndCopyKeyPath',
      'Box L1037: Registry deep-navigate (HKLM\SOFTWARE...) and Copy key path',
      'NEEDS-INVESTIGATION: root-key listing already covered by CmdPal_Registry_AliasOpensRootKeys. The deep walk part is implementable (sub-page navigation by selecting a key + invoking). The Copy-key-path action lives in the More menu — unclear if CmdPal 0.10 exposes it (Files context menu doesn''t have analogous "Copy path"). Probe Registry sub-page''s CommandsDropdown to confirm availability before implementing.'),
    @('CmdPal_TerminalProfiles_OpenAdminProfile',
      'Box L1034: Windows Terminal admin profile launches with elevation prompt',
      'PROTOTYPE: non-admin profile spawn covered by CmdPal_TerminalProfiles_OpenProfileLaunchesWtExe. Admin variant needs UAC dialog handling — out of scope for non-elevated test runs (would interact with secure desktop).'),
    @('CmdPal_TerminalProfiles_PinningWithPerProfileIcons',
      '★ 0.99.0: Pin Windows Terminal profile to dock with per-profile icon',
      'PROTOTYPE: combines TerminalProfiles_Open + Pin_PinCommandToDock + screenshot/icon-path assertion. Two prior YELLOWs need to land first.'),
    @('CmdPal_Bookmarks_AddAndOpen',
      'Box L1044-L1045: Bookmark add and open',
      'PROTOTYPE: drive bookmark-add dialog + alias-invoke. Bookmarks settings file (in AppX LocalState) verifiable as side-channel.'),
    @('CmdPal_Calculator_HistoryClearAndDeleteViaUI',
      '★ 0.99.0: Calculator history clear/delete via Calc sub-page More menu',
      'NEEDS-FEATURE: investigated 2026-05-20 — the Calc sub-page More menu in CmdPal 0.10.11181.0 contains result-formatting items (Copy, Paste, Replace query, 0xA hex, 0b1010 binary, 0o12 octal) but NO Clear/Delete history entries. History management actions don''t exist in this version''s More menu. File-persistence is verified by CmdPal_Calculator_HistoryFilePersistsOnDisk. Revisit when calc adds UI-driven history management.'),

    # ─── ORANGE: NEEDS-ENV — needs special environment, not just code ──
    @('CmdPal_Hotkey_ActivationUnderDifferentPtElevationModes',
      'Box L1017-L1019: Hotkey activation works under different PT elevation modes',
      'NEEDS-ENV: needs test rig that can start PT in admin / non-admin / standard mode + SendInput hotkey. CmdPal.Show event is verified; actual hotkey path is the runner''s LLKH (same as other modules).'),
    @('CmdPal_WinGet_SearchAndInstall',
      'Box L1031: WinGet search and install',
      'NEEDS-ENV: needs network + would install a real package. Implementable behind opt-in flag with a guaranteed-safe package (e.g. wingetcreate).'),
    @('CmdPal_Service_StartAndStopAndRestart',
      'Box L1038: Windows Service start/stop/restart',
      'NEEDS-ENV: needs admin elevation + an opt-in safe target service (e.g. AppMgmt or Windows Search). Add ".\\winget-test-services.json" config file once we author one.'),
    @('CmdPal_Master_DisableCmdPalInPtSettings',
      'Box L1046: Disable CmdPal in PT — hotkey does nothing',
      'NEEDS-ENV: requires writing master PT settings.json (enabled.CmdPal=false) + restarting the RUNNER (not just AppX) + verifying CmdPal.Show event is gone. Risky if test crashes mid-disable; needs a robust restore-on-exit handler.'),

    # ─── RED-DESTRUCTIVE — would actually destroy data; permanent skip ──
    @('CmdPal_AppX_UninstallRemovesPackage',
      'Box L1015: Uninstall removes CmdPal AppX (destructive)',
      'DESTRUCTIVE: would uninstall PowerToys itself. Opt-in only via a separate teardown script run AFTER all other tests on a sacrificial machine.'),
    @('CmdPal_System_ShutdownAndEmptyRecycleBin',
      'Box L1040-L1043: Windows System — shutdown / empty recycle bin / network adapter',
      'DESTRUCTIVE: shutdown = catastrophic; empty bin destroys files. The safe variant is covered by CmdPal_System_ReturnsShutdownCommandWithCorrectPrimary. Network-adapter paste would be safe to assert via clipboard but not yet wired.'),

    # ─── RED-COVERED — already covered by an active test ──────────────
    @('CmdPal_WebSearch_ActuallyOpensBrowserTab',
      'Box L1032: Web Search executes (actually opens browser)',
      'COVERED: actively invoking would open a browser tab. Wiring covered by CmdPal_WebSearch_PrimaryActionOpensDefaultBrowser which asserts Primary action label without invoking.'),

    # ─── RED-FIXTURE — needs an authored extension fixture ────────────
    @('CmdPal_Extensions_PlainTextAndImageViewerContentTypes',
      '★ 0.99.0: Plain-text and image viewer content types',
      'NEEDS-FIXTURE: requires authoring a custom extension that emits each content type. Hard to test without that fixture in the repo.'),
    @('CmdPal_Extensions_BadExtensionDoesNotBreakOthers',
      '★ 0.99.0: One bad extension does not break others (load isolation)',
      'NEEDS-FIXTURE: requires authoring a deliberately-broken extension fixture. Verify all built-in providers still appear after the bad one is installed.')
)
