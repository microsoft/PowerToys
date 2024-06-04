#pragma once

#include <cstdint>

namespace CommonSharedConstants
{
    // Flag that can be set on an input event so that it is ignored by Keyboard Manager
    const uintptr_t KEYBOARDMANAGER_INJECTED_FLAG = 0x1;

    // Fake key code to represent VK_WIN.
    inline const DWORD VK_WIN_BOTH = 0x104;

    const wchar_t APPDATA_PATH[] = L"Microsoft\\PowerToys";

    // Path to the event used by PowerLauncher
    const wchar_t POWER_LAUNCHER_SHARED_EVENT[] = L"Local\\PowerToysRunInvokeEvent-30f26ad7-d36d-4c0e-ab02-68bb5ff3c4ab";

    const wchar_t POWER_LAUNCHER_CENTRALIZED_HOOK_SHARED_EVENT[] = L"Local\\PowerToysRunCentralizedHookInvokeEvent-30f26ad7-d36d-4c0e-ab02-68bb5ff3c4ab";

    const wchar_t RUN_SEND_SETTINGS_TELEMETRY_EVENT[] = L"Local\\PowerToysRunInvokeEvent-638ec522-0018-4b96-837d-6bd88e06f0d6";

    const wchar_t RUN_EXIT_EVENT[] = L"Local\\PowerToysRunExitEvent-3e38e49d-a762-4ef1-88f2-fd4bc7481516";
    
    const wchar_t FZE_EXIT_EVENT[] = L"Local\\PowerToys-FZE-ExitEvent-ca8c73de-a52c-4274-b691-46e9592d3b43";

    const wchar_t COLOR_PICKER_SEND_SETTINGS_TELEMETRY_EVENT[] = L"Local\\ColorPickerSettingsTelemetryEvent-6c7071d8-4014-46ec-b687-913bd8a422f1";

    // Path to the event used to show Advanced Paste UI
    const wchar_t SHOW_ADVANCED_PASTE_SHARED_EVENT[] = L"Local\\ShowAdvancedPasteEvent-9a46be2a-3e05-4186-b56b-4ae986ef2526";

    const wchar_t ADVANCED_PASTE_MARKDOWN_EVENT[] = L"Local\\AdvancedPasteJsonEvent-a18c0798-3ee6-4fc5-bb9f-114c57ac0d47";

    const wchar_t ADVANCED_PASTE_JSON_EVENT[] = L"Local\\AdvancedPasteJsonEvent-9ed021ab-b711-4cf3-9f33-135a698a9d21";

    // Path to the event used to show Color Picker
    const wchar_t SHOW_COLOR_PICKER_SHARED_EVENT[] = L"Local\\ShowColorPickerEvent-8c46be2a-3e05-4186-b56b-4ae986ef2525";

    const wchar_t SHORTCUT_GUIDE_TRIGGER_EVENT[] = L"Local\\ShortcutGuide-TriggerEvent-d4275ad3-2531-4d19-9252-c0becbd9b496";

    const wchar_t SHORTCUT_GUIDE_EXIT_EVENT[] = L"Local\\ShortcutGuide-ExitEvent-35697cdd-a3d2-47d6-a246-34efcc73eac0";

    const wchar_t FANCY_ZONES_EDITOR_TOGGLE_EVENT[] = L"Local\\FancyZones-ToggleEditorEvent-1e174338-06a3-472b-874d-073b21c62f14";

    const wchar_t SHOW_HOSTS_EVENT[] = L"Local\\Hosts-ShowHostsEvent-5a0c0aae-5ff5-40f5-95c2-20e37ed671f0";

    const wchar_t SHOW_HOSTS_ADMIN_EVENT[] = L"Local\\Hosts-ShowHostsAdminEvent-60ff44e2-efd3-43bf-928a-f4d269f98bec";

    // Path to the event used by Awake
    const wchar_t AWAKE_EXIT_EVENT[] = L"Local\\PowerToysAwakeExitEvent-c0d5e305-35fc-4fb5-83ec-f6070cfaf7fe";
    
    // Path to the event used by AlwaysOnTop
    const wchar_t ALWAYS_ON_TOP_PIN_EVENT[] = L"Local\\AlwaysOnTopPinEvent-892e0aa2-cfa8-4cc4-b196-ddeb32314ce8";

    // Path to the event used by PowerAccent
    const wchar_t POWERACCENT_EXIT_EVENT[] = L"Local\\PowerToysPowerAccentExitEvent-53e93389-d19a-4fbb-9b36-1981c8965e17";

    // Path to the event used by PowerOCR
    const wchar_t SHOW_POWEROCR_SHARED_EVENT[] = L"Local\\PowerOCREvent-dc864e06-e1af-4ecc-9078-f98bee745e3a";

    // Path to the events used by Mouse Jump
    const wchar_t MOUSE_JUMP_SHOW_PREVIEW_EVENT[] = L"Local\\MouseJumpEvent-aa0be051-3396-4976-b7ba-1a9cc7d236a5";

    // Path to the event used by RegistryPreview
    const wchar_t REGISTRY_PREVIEW_TRIGGER_EVENT[] = L"Local\\RegistryPreviewEvent-4C559468-F75A-4E7F-BC4F-9C9688316687";

    // Path to the event used by MeasureTool
    const wchar_t MEASURE_TOOL_TRIGGER_EVENT[] = L"Local\\MeasureToolEvent-3d46745f-09b3-4671-a577-236be7abd199";

    // Path to the event used by GcodePreviewHandler
    const wchar_t GCODE_PREVIEW_RESIZE_EVENT[] = L"Local\\PowerToysGcodePreviewResizeEvent-6ff1f9bd-ccbd-4b24-a79f-40a34fb0317d";

    // Path to the event used by QoiPreviewHandler
    const wchar_t QOI_PREVIEW_RESIZE_EVENT[] = L"Local\\PowerToysQoiPreviewResizeEvent-579518d1-8c8b-494f-8143-04f43d761ead";

    // Path to the event used by DevFilesPreviewHandler
    const wchar_t DEV_FILES_PREVIEW_RESIZE_EVENT[] = L"Local\\PowerToysDevFilesPreviewResizeEvent-5707a22c-2cac-4ea2-82f0-27c03ef0b5f3";

    // Path to the event used by MarkdownPreviewHandler
    const wchar_t MARKDOWN_PREVIEW_RESIZE_EVENT[] = L"Local\\PowerToysMarkdownPreviewResizeEvent-54c9ab69-11f3-49e9-a98f-53221cfef3ec";

    // Path to the event used by MarkdownPreviewHandler
    const wchar_t PDF_PREVIEW_RESIZE_EVENT[] = L"Local\\PowerToysPdfPreviewResizeEvent-5a2f162a-f728-45fe-8bda-ef3d5e434ce7";

    // Path to the event used by MarkdownPreviewHandler
    const wchar_t SVG_PREVIEW_RESIZE_EVENT[] = L"Local\\PowerToysSvgPreviewResizeEvent-0701a4fc-d5a1-4ee7-b885-f83982c62a0d";

    // Path to the event used to show Peek
    const wchar_t SHOW_PEEK_SHARED_EVENT[] = L"Local\\ShowPeekEvent";

    // Path to the events used by CropAndLock
    const wchar_t CROP_AND_LOCK_REPARENT_EVENT[] = L"Local\\PowerToysCropAndLockReparentEvent-6060860a-76a1-44e8-8d0e-6355785e9c36";
    const wchar_t CROP_AND_LOCK_THUMBNAIL_EVENT[] = L"Local\\PowerToysCropAndLockThumbnailEvent-1637be50-da72-46b2-9220-b32b206b2434";
    const wchar_t CROP_AND_LOCK_EXIT_EVENT[] = L"Local\\PowerToysCropAndLockExitEvent-d995d409-7b70-482b-bad6-e7c8666f375a";

    // Path to the events used by EnvironmentVariables
    const wchar_t SHOW_ENVIRONMENT_VARIABLES_EVENT[] = L"Local\\PowerToysEnvironmentVariables-ShowEnvironmentVariablesEvent-1021f616-e951-4d64-b231-a8f972159978";
    const wchar_t SHOW_ENVIRONMENT_VARIABLES_ADMIN_EVENT[] = L"Local\\PowerToysEnvironmentVariables-EnvironmentVariablesAdminEvent-8c95d2ad-047c-49a2-9e8b-b4656326cfb2";

    // Max DWORD for key code to disable keys.
    const DWORD VK_DISABLED = 0x100;
}
