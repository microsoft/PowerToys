// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.KeyboardManager.UITests;

internal static class KeyboardManagerTestConstants
{
    public const string EditorProcessName = "PowerToys.KeyboardManagerEditorUI";
    public const string ModuleName = "Keyboard Manager";
    public const string SettingsChangedEventName = "PowerToys_KeyboardManager_Event_Settings";
    public const ulong SingleKeyInjectedFlag = 0x11;
    public const ulong ShortcutInjectedFlag = 0x101;
    public const int DisabledKey = 0x100;
    public const int LoadProbeBaseKey = 0x7C;
    public const int LoadProbeKeyCount = 12;
    public const int LoadProbeSourceKey = LoadProbeBaseKey;
    public const int LoadProbeTargetKey = LoadProbeBaseKey + 1;
}
