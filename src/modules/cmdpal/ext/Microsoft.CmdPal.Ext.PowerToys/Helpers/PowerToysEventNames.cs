// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToysExtension.Helpers;

/// <summary>
/// Mirrors the event names exposed by PowerToys' shared constants so the extension can raise them.
/// Keep these values in sync with src/common/interop/shared_constants.h in the PowerToys repo.
/// </summary>
internal static class PowerToysEventNames
{
    internal const string AlwaysOnTopPin = "Local\\AlwaysOnTopPinEvent-892e0aa2-cfa8-4cc4-b196-ddeb32314ce8";
    internal const string ColorPickerShow = "Local\\ShowColorPickerEvent-8c46be2a-3e05-4186-b56b-4ae986ef2525";
    internal const string CommandPaletteShow = "Local\\PowerToysCmdPal-ShowEvent-62336fcd-8611-4023-9b30-091a6af4cc5a";
    internal const string AwakeExitEvent = "Local\\PowerToysAwakeExitEvent-c0d5e305-35fc-4fb5-83ec-f6070cfaf7fe";
    internal const string EnvironmentVariablesShow = "Local\\PowerToysEnvironmentVariables-ShowEnvironmentVariablesEvent-1021f616-e951-4d64-b231-a8f972159978";
    internal const string EnvironmentVariablesShowAdmin = "Local\\PowerToysEnvironmentVariables-EnvironmentVariablesAdminEvent-8c95d2ad-047c-49a2-9e8b-b4656326cfb2";
    internal const string FancyZonesToggleEditor = "Local\\FancyZones-ToggleEditorEvent-1e174338-06a3-472b-874d-073b21c62f14";
    internal const string HostsShow = "Local\\Hosts-ShowHostsEvent-5a0c0aae-5ff5-40f5-95c2-20e37ed671f0";
    internal const string HostsShowAdmin = "Local\\Hosts-ShowHostsAdminEvent-60ff44e2-efd3-43bf-928a-f4d269f98bec";
    internal const string MeasureToolTrigger = "Local\\MeasureToolEvent-3d46745f-09b3-4671-a577-236be7abd199";
    internal const string PeekShow = "Local\\ShowPeekEvent";
    internal const string PowerOcrShow = "Local\\PowerOCREvent-dc864e06-e1af-4ecc-9078-f98bee745e3a";
    internal const string PowerToysRunInvoke = "Local\\PowerToysRunInvokeEvent-30f26ad7-d36d-4c0e-ab02-68bb5ff3c4ab";
    internal const string RegistryPreviewTrigger = "Local\\RegistryPreviewEvent-4C559468-F75A-4E7F-BC4F-9C9688316687";
    internal const string ShortcutGuideTrigger = "Local\\ShortcutGuide-TriggerEvent-d4275ad3-2531-4d19-9252-c0becbd9b496";
    internal const string WorkspacesLaunchEditor = "Local\\Workspaces-LaunchEditorEvent-a55ff427-cf62-4994-a2cd-9f72139296bf";
    internal const string WorkspacesHotkey = "Local\\PowerToys-Workspaces-HotkeyEvent-2625C3C8-BAC9-4DB3-BCD6-3B4391A26FD0";
    internal const string CropAndLockThumbnail = "Local\\PowerToysCropAndLockThumbnailEvent-1637be50-da72-46b2-9220-b32b206b2434";
    internal const string CropAndLockReparent = "Local\\PowerToysCropAndLockReparentEvent-6060860a-76a1-44e8-8d0e-6355785e9c36";
}
