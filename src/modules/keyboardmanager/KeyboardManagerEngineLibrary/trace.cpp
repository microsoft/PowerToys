#include "pch.h"
#include "trace.h"
#include <common/interop/keyboard_layout.h>

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    "Microsoft.PowerToys",
    // {38e8889b-9731-53f5-e901-e8a7c1753074}
    (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
    TraceLoggingOptionProjectTelemetry());

void Trace::RegisterProvider() noexcept
{
    TraceLoggingRegister(g_hProvider);
}

void Trace::UnregisterProvider() noexcept
{
    TraceLoggingUnregister(g_hProvider);
}

// Log if a key to key remap has been invoked today.
void Trace::DailyKeyToKeyRemapInvoked() noexcept
{
        TraceLoggingWrite(
            g_hProvider,
            "KeyboardManager_DailyKeyToKeyRemapInvoked",
            ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
            TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

// Log if a key to shortcut remap has been invoked today.
void Trace::DailyKeyToShortcutRemapInvoked() noexcept
{
        TraceLoggingWrite(
            g_hProvider,
            "KeyboardManager_DailyKeyToShortcutRemapInvoked",
            ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
            TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

// Log if a shortcut to key remap has been invoked today.
void Trace::DailyShortcutToKeyRemapInvoked() noexcept
{
        TraceLoggingWrite(
            g_hProvider,
            "KeyboardManager_DailyShortcutToKeyRemapInvoked",
            ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
            TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

// Log if a shortcut to shortcut remap has been invoked today.
void Trace::DailyShortcutToShortcutRemapInvoked() noexcept
{
        TraceLoggingWrite(
            g_hProvider,
            "KeyboardManager_DailyShortcutToShortcutRemapInvoked",
            ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
            TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

// Log if an app specific shortcut to key remap has been invoked today.
void Trace::DailyAppSpecificShortcutToKeyRemapInvoked() noexcept
{
        TraceLoggingWrite(
            g_hProvider,
            "KeyboardManager_DailyAppSpecificShortcutToKeyRemapInvoked",
            ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
            TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

// Log if an app specific shortcut to shortcut remap has been invoked today.
void Trace::DailyAppSpecificShortcutToShortcutRemapInvoked() noexcept
{
        TraceLoggingWrite(
            g_hProvider,
            "KeyboardManager_DailyAppSpecificShortcutToShortcutRemapInvoked",
            ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
            TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

// Log if a key remap has been invoked (not being used currently, due to being garrulous)
void Trace::KeyRemapInvoked(bool isKeyToKey) noexcept
{
    if (isKeyToKey)
    {
        TraceLoggingWrite(
            g_hProvider,
            "KeyboardManager_KeyToKeyRemapInvoked",
            ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
            TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
    }
    else
    {
        TraceLoggingWrite(
            g_hProvider,
            "KeyboardManager_KeyToShortcutRemapInvoked",
            ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
            TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
    }
}

// Log if a shortcut remap has been invoked (not being used currently, due to being garrulous)
void Trace::ShortcutRemapInvoked(bool isShortcutToShortcut, bool isAppSpecific) noexcept
{
    if (isAppSpecific)
    {
        if (isShortcutToShortcut)
        {
            TraceLoggingWrite(
                g_hProvider,
                "KeyboardManager_AppSpecificShortcutToShortcutRemapInvoked",
                ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
                TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
        }
        else
        {
            TraceLoggingWrite(
                g_hProvider,
                "KeyboardManager_AppSpecificShortcutToKeyRemapInvoked",
                ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
                TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
        }
    }
    else
    {
        if (isShortcutToShortcut)
        {
            TraceLoggingWrite(
                g_hProvider,
                "KeyboardManager_OSLevelShortcutToShortcutRemapInvoked",
                ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
                TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
        }
        else
        {
            TraceLoggingWrite(
                g_hProvider,
                "KeyboardManager_OSLevelShortcutToKeyRemapInvoked",
                ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
                TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
        }
    }
}

// Function to return a human readable string for the shortcut
std::wstring GetShortcutHumanReadableString(Shortcut const & shortcut, LayoutMap& keyboardMap)
{
    std::wstring humanReadableShortcut = L"";
    if (shortcut.winKey != ModifierKey::Disabled)
    {
        humanReadableShortcut += keyboardMap.GetKeyName(shortcut.GetWinKey(ModifierKey::Both)) + L" + ";
    }
    if (shortcut.ctrlKey != ModifierKey::Disabled)
    {
        humanReadableShortcut += keyboardMap.GetKeyName(shortcut.GetCtrlKey()) + L" + ";
    }
    if (shortcut.altKey != ModifierKey::Disabled)
    {
        humanReadableShortcut += keyboardMap.GetKeyName(shortcut.GetAltKey()) + L" + ";
    }
    if (shortcut.shiftKey != ModifierKey::Disabled)
    {
        humanReadableShortcut += keyboardMap.GetKeyName(shortcut.GetShiftKey()) + L" + ";
    }
    if (shortcut.actionKey != NULL)
    {
        humanReadableShortcut += keyboardMap.GetKeyName(shortcut.actionKey);
        if (shortcut.secondKey != NULL)
        {
            humanReadableShortcut += L" , " + keyboardMap.GetKeyName(shortcut.secondKey);
        }
    }
    return humanReadableShortcut;
}


// Log the current remappings of key and shortcuts when keyboard manager engine loads the settings.
void Trace::SendKeyAndShortcutRemapLoadedConfiguration(State& remappings) noexcept
{
    LayoutMap keyboardMap;
    for (auto const& keyRemap : remappings.singleKeyReMap)
    {
        if (keyRemap.second.index() == 0) // 0 - Remapping to key
        {
            DWORD keyRemappedTo = std::get<DWORD>(keyRemap.second);
            TraceLoggingWrite(
                g_hProvider,
                "KeyboardManager_KeyRemapConfigurationLoaded",
                ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
                TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
                TraceLoggingInt64(keyRemap.first, "KeyRemapFrom"),
                TraceLoggingInt64(keyRemappedTo, "KeyRemapTo"),
                TraceLoggingWideString(keyboardMap.GetKeyName(keyRemap.first).c_str(), "HumanRemapFrom"),
                TraceLoggingWideString(keyboardMap.GetKeyName(keyRemappedTo).c_str(), "HumanRemapTo")
            );
        }
        else if (keyRemap.second.index() == 1) // 1 - Remapping to shortcut
        {
            Shortcut shortcutRemappedTo = std::get<Shortcut>(keyRemap.second);
            TraceLoggingWrite(
                g_hProvider,
                "KeyboardManager_KeyRemapConfigurationLoaded",
                ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
                TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
                TraceLoggingInt64(keyRemap.first, "KeyRemapFrom"),
                TraceLoggingInt64(shortcutRemappedTo.actionKey, "KeyRemapTo"),
                TraceLoggingInt8(static_cast<INT8>(shortcutRemappedTo.winKey), "ModifierRemapToWin"),
                TraceLoggingInt8(static_cast<INT8>(shortcutRemappedTo.ctrlKey), "ModifierRemapToCtrl"),
                TraceLoggingInt8(static_cast<INT8>(shortcutRemappedTo.altKey), "ModifierRemapToAlt"),
                TraceLoggingInt8(static_cast<INT8>(shortcutRemappedTo.shiftKey), "ModifierRemapToShift"),
                TraceLoggingWideString(keyboardMap.GetKeyName(keyRemap.first).c_str(), "HumanRemapFrom"),
                TraceLoggingWideString(GetShortcutHumanReadableString(shortcutRemappedTo, keyboardMap).c_str(), "HumanRemapTo")
            );
        }
    }

    for (auto const& shortcutRemap : remappings.osLevelShortcutReMap)
    {
        Shortcut shortcutRemappedFrom = shortcutRemap.first;
        if (shortcutRemap.second.targetShortcut.index() == 0) // 0 - Remapping to key
        {
            DWORD keyRemappedTo = std::get<DWORD>(shortcutRemap.second.targetShortcut);
            TraceLoggingWrite(
                g_hProvider,
                "KeyboardManager_ShortcutRemapConfigurationLoaded",
                ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
                TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
                TraceLoggingInt64(shortcutRemappedFrom.actionKey, "KeyRemapFrom"),
                TraceLoggingInt8(static_cast<INT8>(shortcutRemappedFrom.winKey), "ModifierRemapFromWin"),
                TraceLoggingInt8(static_cast<INT8>(shortcutRemappedFrom.ctrlKey), "ModifierRemapFromCtrl"),
                TraceLoggingInt8(static_cast<INT8>(shortcutRemappedFrom.altKey), "ModifierRemapFromAlt"),
                TraceLoggingInt8(static_cast<INT8>(shortcutRemappedFrom.shiftKey), "ModifierRemapFromShift"),
                TraceLoggingBool(shortcutRemappedFrom.HasChord(), "KeyRemapFromHasChord"),
                TraceLoggingInt64(shortcutRemappedFrom.secondKey, "KeyRemapFromChordSecondKey"),
                TraceLoggingInt64(keyRemappedTo, "KeyRemapTo"),
                TraceLoggingWideString(GetShortcutHumanReadableString(shortcutRemappedFrom, keyboardMap).c_str(), "HumanRemapFrom"),
                TraceLoggingWideString(keyboardMap.GetKeyName(keyRemappedTo).c_str(), "HumanRemapTo"));
        }
        else if (shortcutRemap.second.targetShortcut.index() == 1) // 1 - Remapping to shortcut
        {
            Shortcut shortcutRemappedTo = std::get<Shortcut>(shortcutRemap.second.targetShortcut);
            if (shortcutRemappedTo.IsRunProgram() || shortcutRemappedTo.IsOpenURI())
            {
                // Don't include Start app or Open URI mappings in this telemetry.
                continue;
            }
            TraceLoggingWrite(
                g_hProvider,
                "KeyboardManager_ShortcutRemapConfigurationLoaded",
                ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
                TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
                TraceLoggingInt64(shortcutRemappedFrom.actionKey, "KeyRemapFrom"),
                TraceLoggingInt8(static_cast<INT8>(shortcutRemappedFrom.winKey), "ModifierRemapFromWin"),
                TraceLoggingInt8(static_cast<INT8>(shortcutRemappedFrom.ctrlKey), "ModifierRemapFromCtrl"),
                TraceLoggingInt8(static_cast<INT8>(shortcutRemappedFrom.altKey), "ModifierRemapFromAlt"),
                TraceLoggingInt8(static_cast<INT8>(shortcutRemappedFrom.shiftKey), "ModifierRemapFromShift"),
                TraceLoggingBool(shortcutRemappedFrom.HasChord(), "KeyRemapFromHasChord"),
                TraceLoggingInt64(shortcutRemappedFrom.secondKey, "KeyRemapFromChordSecondKey"),
                TraceLoggingInt64(shortcutRemappedTo.actionKey, "KeyRemapTo"),
                TraceLoggingInt8(static_cast<INT8>(shortcutRemappedTo.winKey), "ModifierRemapToWin"),
                TraceLoggingInt8(static_cast<INT8>(shortcutRemappedTo.ctrlKey), "ModifierRemapToCtrl"),
                TraceLoggingInt8(static_cast<INT8>(shortcutRemappedTo.altKey), "ModifierRemapToAlt"),
                TraceLoggingInt8(static_cast<INT8>(shortcutRemappedTo.shiftKey), "ModifierRemapToShift"),
                TraceLoggingWideString(GetShortcutHumanReadableString(shortcutRemappedFrom, keyboardMap).c_str(), "HumanRemapFrom"),
                TraceLoggingWideString(GetShortcutHumanReadableString(shortcutRemappedTo, keyboardMap).c_str(), "HumanRemapTo")
            );
        }
    }

    for (auto const& appShortcutRemap : remappings.appSpecificShortcutReMap)
    {
        std::wstring appName = appShortcutRemap.first;
        for (auto const& shortcutRemap : appShortcutRemap.second)
        {
            Shortcut shortcutRemappedFrom = shortcutRemap.first;
            if (shortcutRemap.second.targetShortcut.index() == 0) // 0 - Remapping to key
            {
                DWORD keyRemappedTo = std::get<DWORD>(shortcutRemap.second.targetShortcut);
                TraceLoggingWrite(
                    g_hProvider,
                    "KeyboardManager_AppSpecificShortcutRemapConfigurationLoaded",
                    ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
                    TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
                    TraceLoggingInt64(shortcutRemappedFrom.actionKey, "KeyRemapFrom"),
                    TraceLoggingInt8(static_cast<INT8>(shortcutRemappedFrom.winKey), "ModifierRemapFromWin"),
                    TraceLoggingInt8(static_cast<INT8>(shortcutRemappedFrom.ctrlKey), "ModifierRemapFromCtrl"),
                    TraceLoggingInt8(static_cast<INT8>(shortcutRemappedFrom.altKey), "ModifierRemapFromAlt"),
                    TraceLoggingInt8(static_cast<INT8>(shortcutRemappedFrom.shiftKey), "ModifierRemapFromShift"),
                    TraceLoggingBool(shortcutRemappedFrom.HasChord(), "KeyRemapFromHasChord"),
                    TraceLoggingInt64(shortcutRemappedFrom.secondKey, "KeyRemapFromChordSecondKey"),
                    TraceLoggingInt64(keyRemappedTo, "KeyRemapTo"),
                    TraceLoggingWideString(GetShortcutHumanReadableString(shortcutRemappedFrom, keyboardMap).c_str(), "HumanRemapFrom"),
                    TraceLoggingWideString(keyboardMap.GetKeyName(keyRemappedTo).c_str(), "HumanRemapTo"),
                    TraceLoggingWideString(appName.c_str(), "TargetApp")
                );
            }
            else if (shortcutRemap.second.targetShortcut.index() == 1) // 1 - Remapping to shortcut
            {
                Shortcut shortcutRemappedTo = std::get<Shortcut>(shortcutRemap.second.targetShortcut);
                if (shortcutRemappedTo.IsRunProgram() || shortcutRemappedTo.IsOpenURI())
                {
                    // Don't include Start app or Open URI mappings in this telemetry.
                    continue;
                }
                TraceLoggingWrite(
                    g_hProvider,
                    "KeyboardManager_AppSpecificShortcutRemapConfigurationLoaded",
                    ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
                    TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
                    TraceLoggingInt64(shortcutRemappedFrom.actionKey, "KeyRemapFrom"),
                    TraceLoggingInt8(static_cast<INT8>(shortcutRemappedFrom.winKey), "ModifierRemapFromWin"),
                    TraceLoggingInt8(static_cast<INT8>(shortcutRemappedFrom.ctrlKey), "ModifierRemapFromCtrl"),
                    TraceLoggingInt8(static_cast<INT8>(shortcutRemappedFrom.altKey), "ModifierRemapFromAlt"),
                    TraceLoggingInt8(static_cast<INT8>(shortcutRemappedFrom.shiftKey), "ModifierRemapFromShift"),
                    TraceLoggingBool(shortcutRemappedFrom.HasChord(), "KeyRemapFromHasChord"),
                    TraceLoggingInt64(shortcutRemappedFrom.secondKey, "KeyRemapFromChordSecondKey"),
                    TraceLoggingInt64(shortcutRemappedTo.actionKey, "KeyRemapTo"),
                    TraceLoggingInt8(static_cast<INT8>(shortcutRemappedTo.winKey), "ModifierRemapToWin"),
                    TraceLoggingInt8(static_cast<INT8>(shortcutRemappedTo.ctrlKey), "ModifierRemapToCtrl"),
                    TraceLoggingInt8(static_cast<INT8>(shortcutRemappedTo.altKey), "ModifierRemapToAlt"),
                    TraceLoggingInt8(static_cast<INT8>(shortcutRemappedTo.shiftKey), "ModifierRemapToShift"),
                    TraceLoggingWideString(GetShortcutHumanReadableString(shortcutRemappedFrom, keyboardMap).c_str(), "HumanRemapFrom"),
                    TraceLoggingWideString(GetShortcutHumanReadableString(shortcutRemappedTo, keyboardMap).c_str(), "HumanRemapTo"),
                    TraceLoggingWideString(appName.c_str(), "TargetApp")
                );
            }
        }
    }
}

// Log an error while trying to send remappings telemetry.
void Trace::ErrorSendingKeyAndShortcutRemapLoadedConfiguration() noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "KeyboardManager_ErrorSendingKeyAndShortcutRemapLoadedConfiguration",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}


// Log if an error occurs in KBM
void Trace::Error(const DWORD errorCode, std::wstring errorMessage, std::wstring methodName) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "KeyboardManager_Error",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(methodName.c_str(), "MethodName"),
        TraceLoggingValue(errorCode, "ErrorCode"),
        TraceLoggingValue(errorMessage.c_str(), "ErrorMessage"));
}
