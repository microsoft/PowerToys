# scripts/pt-shared-events.ps1
# Signal PowerToys modules via Win32 named events.
# Catalog source: PowerToys repo src/common/interop/shared_constants.h
# (Friendly-name mapping was originally surfaced by community frameworks; the values themselves
# are stable PT public IPC names. This file is self-contained — no external repo required.)
# Reason: instead of pressing a hotkey (which is racey, foreground-sensitive, and UIPI-fragile),
# directly SetEvent on the kernel event the module is waiting on. Same code path as the hotkey.

if (-not ('PtEv' -as [type])) {
    Add-Type -TypeDefinition @'
        using System;
        using System.Runtime.InteropServices;
        public static class PtEv {
            [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Unicode)]
            private static extern IntPtr OpenEventW(uint dwAccess, bool bInherit, string lpName);
            [DllImport("kernel32.dll", SetLastError=true)]
            private static extern bool SetEvent(IntPtr h);
            [DllImport("kernel32.dll", SetLastError=true)]
            private static extern bool CloseHandle(IntPtr h);
            private const uint EVENT_MODIFY_STATE = 0x0002;
            private const uint SYNCHRONIZE        = 0x00100000;

            public static bool Signal(string fullName) {
                IntPtr h = OpenEventW(EVENT_MODIFY_STATE | SYNCHRONIZE, false, fullName);
                if (h == IntPtr.Zero) {
                    int err = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(err,
                        "OpenEvent failed for '" + fullName + "' (err=" + err + "). Owning module process may not be running.");
                }
                try { return SetEvent(h); } finally { CloseHandle(h); }
            }

            public static bool Exists(string fullName) {
                IntPtr h = OpenEventW(SYNCHRONIZE, false, fullName);
                if (h == IntPtr.Zero) return false;
                CloseHandle(h); return true;
            }
        }
'@
}

# Friendly-name -> full event name map (per Local\ namespace).
# Source: <PT-repo>\src\common\interop\shared_constants.h
$script:PtSharedEvents = @{
    # ── Hotkey-activated module triggers ──
    'AOT.Pin'                   = 'Local\AlwaysOnTopPinEvent-892e0aa2-cfa8-4cc4-b196-ddeb32314ce8'
    'AOT.IncreaseOpacity'       = 'Local\AlwaysOnTopIncreaseOpacityEvent-a1b2c3d4-e5f6-7890-abcd-ef1234567890'
    'AOT.DecreaseOpacity'       = 'Local\AlwaysOnTopDecreaseOpacityEvent-b2c3d4e5-f6a7-8901-bcde-f12345678901'
    'AdvancedPaste.ShowUI'      = 'Local\PowerToys_AdvancedPaste_ShowUI'
    'CmdPal.Show'               = 'Local\PowerToysCmdPal-ShowEvent-62336fcd-8611-4023-9b30-091a6af4cc5a'
    'ColorPicker.Show'          = 'Local\ShowColorPickerEvent-8c46be2a-3e05-4186-b56b-4ae986ef2525'
    'CropAndLock.Reparent'      = 'Local\PowerToysCropAndLockReparentEvent-6060860a-76a1-44e8-8d0e-6355785e9c36'
    'CropAndLock.Thumbnail'     = 'Local\PowerToysCropAndLockThumbnailEvent-1637be50-da72-46b2-9220-b32b206b2434'
    'CursorWrap.Trigger'        = 'Local\CursorWrapTriggerEvent-1f8452b5-4e6e-45b3-8b09-13f14a5900c9'
    'EnvVars.Show'              = 'Local\PowerToysEnvironmentVariables-ShowEnvironmentVariablesEvent-1021f616-e951-4d64-b231-a8f972159978'
    'EnvVars.ShowAdmin'         = 'Local\PowerToysEnvironmentVariables-EnvironmentVariablesAdminEvent-8c95d2ad-047c-49a2-9e8b-b4656326cfb2'
    'FancyZones.ToggleEditor'   = 'Local\FancyZones-ToggleEditorEvent-1e174338-06a3-472b-874d-073b21c62f14'
    'FindMyMouse.Trigger'       = 'Local\FindMyMouseTriggerEvent-5a9dc5f4-1c74-4f2f-a66f-1b9b6a2f9b23'
    'Hosts.Show'                = 'Local\Hosts-ShowHostsEvent-5a0c0aae-5ff5-40f5-95c2-20e37ed671f0'
    'Hosts.ShowAdmin'           = 'Local\Hosts-ShowHostsAdminEvent-60ff44e2-efd3-43bf-928a-f4d269f98bec'
    'LightSwitch.Toggle'        = 'Local\PowerToys-LightSwitch-ToggleEvent-d8dc2f29-8c94-4ca1-8c5f-3e2b1e3c4f5a'
    'LightSwitch.Light'         = 'Local\PowerToysLightSwitch-LightThemeEvent-50077121-2ffc-4841-9c86-ab1bd3f9baca'
    'LightSwitch.Dark'          = 'Local\PowerToysLightSwitch-DarkThemeEvent-b3a835c0-eaa2-49b0-b8eb-f793e3df3368'
    'MeasureTool.Trigger'       = 'Local\MeasureToolEvent-3d46745f-09b3-4671-a577-236be7abd199'
    'MouseCrosshairs.Trigger'   = 'Local\MouseCrosshairsTriggerEvent-0d4c7f92-0a5c-4f5c-b64b-8a2a2f7e0b21'
    'MouseHighlighter.Trigger'  = 'Local\MouseHighlighterTriggerEvent-1e3c9c3d-3fdf-4f9a-9a52-31c9b3c3a8f4'
    'MouseJump.Show'            = 'Local\MouseJumpEvent-aa0be051-3396-4976-b7ba-1a9cc7d236a5'
    'NewKeyboardManager.Open'   = 'Local\PowerToysOpenNewKeyboardManagerEvent-9c1d2e3f-4b5a-6c7d-8e9f-0a1b2c3d4e5f'
    'Peek.Show'                 = 'Local\ShowPeekEvent'
    'PowerDisplay.Toggle'       = 'Local\PowerToysPowerDisplay-ToggleEvent-5f1a9c3e-7d2b-4e8f-9a6c-3b5d7e9f1a2c'
    'PowerLauncher.Invoke'      = 'Local\PowerToysRunInvokeEvent-30f26ad7-d36d-4c0e-ab02-68bb5ff3c4ab'
    'PowerOcr.Show'             = 'Local\PowerOCREvent-dc864e06-e1af-4ecc-9078-f98bee745e3a'
    'RegistryPreview.Trigger'   = 'Local\RegistryPreviewEvent-4C559468-F75A-4E7F-BC4F-9C9688316687'
    'ShortcutGuide.Trigger'     = 'Local\ShortcutGuide-TriggerEvent-d4275ad3-2531-4d19-9252-c0becbd9b496'
    'TextExtractor.Show'        = 'Local\PowerOCREvent-dc864e06-e1af-4ecc-9078-f98bee745e3a'
    'Workspaces.Hotkey'         = 'Local\PowerToys-Workspaces-HotkeyEvent-2625C3C8-BAC9-4DB3-BCD6-3B4391A26FD0'
    'Workspaces.LaunchEditor'   = 'Local\Workspaces-LaunchEditorEvent-a55ff427-cf62-4994-a2cd-9f72139296bf'
    'ZoomIt.Zoom'               = 'Local\PowerToysZoomIt-ZoomEvent-1e4190d7-94bc-4ad5-adc0-9a8fd07cb393'
    'ZoomIt.Draw'               = 'Local\PowerToysZoomIt-DrawEvent-56338997-404d-4549-bd9a-d132b6766975'
    'ZoomIt.Break'              = 'Local\PowerToysZoomIt-BreakEvent-17f2e63c-4c56-41dd-90a0-2d12f9f50c6b'
    'ZoomIt.LiveZoom'           = 'Local\PowerToysZoomIt-LiveZoomEvent-390bf0c7-616f-47dc-bafe-a2d228add20d'
    'ZoomIt.Snip'               = 'Local\PowerToysZoomIt-SnipEvent-2fd9c211-436d-4f17-a902-2528aaae3e30'
    'ZoomIt.SnipOcr'            = 'Local\PowerToysZoomIt-SnipOcrEvent-a7c3b1d2-9e4f-4a6b-8d5c-1f2e3a4b5c6d'
    'ZoomIt.Record'             = 'Local\PowerToysZoomIt-RecordEvent-74539344-eaad-4711-8e83-23946e424512'

    # ── Termination triggers (clean shutdown without process kill) ──
    'AOT.Terminate'             = 'Local\AlwaysOnTopTerminateEvent-cfdf1eae-791f-4953-8021-2f18f3837eae'
    'Awake.Exit'                = 'Local\PowerToysAwakeExitEvent-c0d5e305-35fc-4fb5-83ec-f6070cfaf7fe'
    'CmdPal.Exit'               = 'Local\PowerToysCmdPal-ExitEvent-eb73f6be-3f22-4b36-aee3-62924ba40bfd'
    'ColorPicker.Terminate'     = 'Local\TerminateColorPickerEvent-3d676258-c4d5-424e-a87a-4be22020e813'
    'CropAndLock.Exit'          = 'Local\PowerToysCropAndLockExitEvent-d995d409-7b70-482b-bad6-e7c8666f375a'
    'FZE.Exit'                  = 'Local\PowerToys-FZE-ExitEvent-ca8c73de-a52c-4274-b691-46e9592d3b43'
    'Hosts.Terminate'           = 'Local\Hosts-TerminateHostsEvent-d5410d5e-45a6-4d11-bbf0-a4ec2d064888'
    'KBM.Terminate'             = 'Local\TerminateKBMSharedEvent-a787c967-55b6-47de-94d9-56f39fed839e'
    'MouseJump.Terminate'       = 'Local\TerminateMouseJumpEvent-252fa337-317f-4c37-a61f-99464c3f9728'
    'Peek.Terminate'            = 'Local\TerminatePeekEvent-267149fe-7ed2-427d-a3ad-9e18203c037c'
    'PowerAccent.Exit'          = 'Local\PowerToysPowerAccentExitEvent-53e93389-d19a-4fbb-9b36-1981c8965e17'
    'PowerOcr.Terminate'        = 'Local\TerminatePowerOCREvent-08e5de9d-15df-4ea8-8840-487c13435a67'
    'PowerDisplay.Terminate'    = 'Local\PowerToysPowerDisplay-TerminateEvent-7b9c2e1f-8a5d-4c3e-9f6b-2a1d8c5e3b7a'
    'Run.Exit'                  = 'Local\PowerToysRunExitEvent-3e38e49d-a762-4ef1-88f2-fd4bc7481516'
    'ShortcutGuide.Exit'        = 'Local\ShortcutGuide-ExitEvent-35697cdd-a3d2-47d6-a246-34efcc73eac0'
    'Settings.Terminate'        = 'Local\PowerToysRunnerTerminateSettingsEvent-c34cb661-2e69-4613-a1f8-4e39c25d7ef6'
    'ZoomIt.Exit'               = 'Local\PowerToysZoomIt-ExitEvent-36641ce6-df02-4eac-abea-a3fbf9138220'
    'GrabAndMove.Exit'          = 'Local\PowerToysGrabAndMove-ExitEvent-b8c4d2e3-5f6a-7b8c-9d0e-1f2a3b4c5d6e'
}

function Invoke-PtSharedEvent {
    <#
    .SYNOPSIS
    Signal a PowerToys named kernel event by friendly name (e.g. 'CmdPal.Show')
    or by full event path (e.g. 'Local\PowerToys_AdvancedPaste_ShowUI').
    Returns $true on success; throws if event doesn't exist or owner not running.
    .EXAMPLE
    Invoke-PtSharedEvent -Name 'CmdPal.Show'
    Invoke-PtSharedEvent -Name 'PowerLauncher.Invoke'
    Invoke-PtSharedEvent -Name 'AOT.Pin'
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name)
    $eventName = if ($script:PtSharedEvents.ContainsKey($Name)) { $script:PtSharedEvents[$Name] } else { $Name }
    return [PtEv]::Signal($eventName)
}

function Test-PtSharedEvent {
    [CmdletBinding()] param([Parameter(Mandatory)][string]$Name)
    $eventName = if ($script:PtSharedEvents.ContainsKey($Name)) { $script:PtSharedEvents[$Name] } else { $Name }
    return [PtEv]::Exists($eventName)
}

function Get-PtSharedEventCatalog {
    $script:PtSharedEvents.GetEnumerator() | Sort-Object Name |
        ForEach-Object { [pscustomobject]@{ Name = $_.Key; Event = $_.Value } }
}
