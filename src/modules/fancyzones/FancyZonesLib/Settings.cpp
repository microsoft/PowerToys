#include "pch.h"
#include "Settings.h"

#include <common/logger/call_tracer.h>
#include <common/logger/logger.h>
#include <common/SettingsAPI/FileWatcher.h>
#include <common/utils/resources.h>
#include <common/utils/string_utils.h>

#include <FancyZonesLib/FancyZonesWinHookEventIDs.h>
#include <FancyZonesLib/SettingsObserver.h>
#include <FancyZonesLib/trace.h>

// Non-Localizable strings
namespace NonLocalizable
{
    // FancyZones settings descriptions are localized, but underlying toggle (spinner, color picker) names are not.
    const wchar_t ShiftDragID[] = L"fancyzones_shiftDrag";
    const wchar_t MouseSwitchID[] = L"fancyzones_mouseSwitch";
    const wchar_t OverrideSnapHotKeysID[] = L"fancyzones_overrideSnapHotkeys";
    const wchar_t MoveWindowAcrossMonitorsID[] = L"fancyzones_moveWindowAcrossMonitors";
    const wchar_t MoveWindowsBasedOnPositionID[] = L"fancyzones_moveWindowsBasedOnPosition";
    const wchar_t OverlappingZonesAlgorithmID[] = L"fancyzones_overlappingZonesAlgorithm";
    const wchar_t DisplayChangeMoveWindowsID[] = L"fancyzones_displayChange_moveWindows";
    const wchar_t ZoneSetChangeMoveWindowsID[] = L"fancyzones_zoneSetChange_moveWindows";
    const wchar_t AppLastZoneMoveWindowsID[] = L"fancyzones_appLastZone_moveWindows";
    const wchar_t OpenWindowOnActiveMonitorID[] = L"fancyzones_openWindowOnActiveMonitor";
    const wchar_t RestoreSizeID[] = L"fancyzones_restoreSize";
    const wchar_t QuickLayoutSwitch[] = L"fancyzones_quickLayoutSwitch";
    const wchar_t FlashZonesOnQuickSwitch[] = L"fancyzones_flashZonesOnQuickSwitch";
    const wchar_t UseCursorPosEditorStartupScreenID[] = L"use_cursorpos_editor_startupscreen";
    const wchar_t ShowOnAllMonitorsID[] = L"fancyzones_show_on_all_monitors";
    const wchar_t SpanZonesAcrossMonitorsID[] = L"fancyzones_span_zones_across_monitors";
    const wchar_t MakeDraggedWindowTransparentID[] = L"fancyzones_makeDraggedWindowTransparent";
    const wchar_t AllowPopupWindowSnapID[] = L"fancyzones_allowPopupWindowSnap";
    const wchar_t AllowChildWindowSnapID[] = L"fancyzones_allowChildWindowSnap";
    const wchar_t DisableRoundCornersOnSnapping[] = L"fancyzones_disableRoundCornersOnSnap";

    const wchar_t SystemThemeID[] = L"fancyzones_systemTheme";
    const wchar_t ZoneColorID[] = L"fancyzones_zoneColor";
    const wchar_t ZoneBorderColorID[] = L"fancyzones_zoneBorderColor";
    const wchar_t ZoneHighlightColorID[] = L"fancyzones_zoneHighlightColor";
    const wchar_t ZoneNumberColorID[] = L"fancyzones_zoneNumberColor";
    const wchar_t EditorHotkeyID[] = L"fancyzones_editor_hotkey";
    const wchar_t WindowSwitchingToggleID[] = L"fancyzones_windowSwitching";
    const wchar_t NextTabHotkeyID[] = L"fancyzones_nextTab_hotkey";
    const wchar_t PrevTabHotkeyID[] = L"fancyzones_prevTab_hotkey";
    const wchar_t ExcludedAppsID[] = L"fancyzones_excluded_apps";
    const wchar_t ZoneHighlightOpacityID[] = L"fancyzones_highlight_opacity";
    const wchar_t ShowZoneNumberID[] = L"fancyzones_showZoneNumber";
}

FancyZonesSettings::FancyZonesSettings()
{
    const std::wstring& settingsFileName = GetSettingsFileName();
    m_settingsFileWatcher = std::make_unique<FileWatcher>(settingsFileName, [&]() {
        PostMessageW(HWND_BROADCAST, WM_PRIV_SETTINGS_CHANGED, NULL, NULL);
    });
}

FancyZonesSettings& FancyZonesSettings::instance()
{
    static FancyZonesSettings instance;
    return instance;
}

void FancyZonesSettings::AddObserver(SettingsObserver& observer)
{
    m_observers.insert(&observer);
}

void FancyZonesSettings::RemoveObserver(SettingsObserver& observer)
{
    auto iter = m_observers.find(&observer);
    if (iter != m_observers.end())
    {
        m_observers.erase(iter);
    }
}

void FancyZonesSettings::SetBoolFlag(const PowerToysSettings::PowerToyValues& values, const wchar_t* id, SettingId notificationId, bool& out)
{
    if (const auto val = values.get_bool_value(id))
    {
        if (out != *val)
        {
            out = *val;
            NotifyObservers(notificationId);
        }
    }
}

void FancyZonesSettings::LoadSettings()
{
    _TRACER_;

    try
    {
        auto jsonOpt = json::from_file(GetSettingsFileName());
        if (!jsonOpt)
        {
            Logger::warn(L"Failed to read from settings file");
            return;
        }

        PowerToysSettings::PowerToyValues values = PowerToysSettings::PowerToyValues::from_json_string(jsonOpt->Stringify(), NonLocalizable::ModuleKey);

        // flags
        SetBoolFlag(values, NonLocalizable::ShiftDragID, SettingId::ShiftDrag, m_settings.shiftDrag);
        SetBoolFlag(values, NonLocalizable::MouseSwitchID, SettingId::MouseSwitch, m_settings.mouseSwitch);
        SetBoolFlag(values, NonLocalizable::OverrideSnapHotKeysID, SettingId::OverrideSnapHotkeys, m_settings.overrideSnapHotkeys);
        SetBoolFlag(values, NonLocalizable::MoveWindowAcrossMonitorsID, SettingId::MoveWindowAcrossMonitors, m_settings.moveWindowAcrossMonitors);
        SetBoolFlag(values, NonLocalizable::MoveWindowsBasedOnPositionID, SettingId::MoveWindowsBasedOnPosition, m_settings.moveWindowsBasedOnPosition);
        SetBoolFlag(values, NonLocalizable::DisplayChangeMoveWindowsID, SettingId::DisplayChangeMoveWindows, m_settings.displayChange_moveWindows);
        SetBoolFlag(values, NonLocalizable::ZoneSetChangeMoveWindowsID, SettingId::ZoneSetChangeMoveWindows, m_settings.zoneSetChange_moveWindows);
        SetBoolFlag(values, NonLocalizable::AppLastZoneMoveWindowsID, SettingId::AppLastZoneMoveWindows, m_settings.appLastZone_moveWindows);
        SetBoolFlag(values, NonLocalizable::OpenWindowOnActiveMonitorID, SettingId::OpenWindowOnActiveMonitor, m_settings.openWindowOnActiveMonitor);
        SetBoolFlag(values, NonLocalizable::RestoreSizeID, SettingId::RestoreWindowSize, m_settings.restoreSize);
        SetBoolFlag(values, NonLocalizable::QuickLayoutSwitch, SettingId::QuickLayoutSwitch, m_settings.quickLayoutSwitch);
        SetBoolFlag(values, NonLocalizable::FlashZonesOnQuickSwitch, SettingId::FlashZonesOnQuickSwitch, m_settings.flashZonesOnQuickSwitch);
        SetBoolFlag(values, NonLocalizable::UseCursorPosEditorStartupScreenID, SettingId::LaunchEditorOnScreenWhereCursorPlaced, m_settings.use_cursorpos_editor_startupscreen);
        SetBoolFlag(values, NonLocalizable::ShowOnAllMonitorsID, SettingId::ShowOnAllMonitors, m_settings.showZonesOnAllMonitors);
        SetBoolFlag(values, NonLocalizable::SpanZonesAcrossMonitorsID, SettingId::SpanZonesAcrossMonitors, m_settings.spanZonesAcrossMonitors);
        SetBoolFlag(values, NonLocalizable::MakeDraggedWindowTransparentID, SettingId::MakeDraggedWindowsTransparent, m_settings.makeDraggedWindowTransparent);
        SetBoolFlag(values, NonLocalizable::WindowSwitchingToggleID, SettingId::WindowSwitching, m_settings.windowSwitching);
        SetBoolFlag(values, NonLocalizable::SystemThemeID, SettingId::SystemTheme, m_settings.systemTheme);
        SetBoolFlag(values, NonLocalizable::ShowZoneNumberID, SettingId::ShowZoneNumber, m_settings.showZoneNumber);
        SetBoolFlag(values, NonLocalizable::AllowPopupWindowSnapID, SettingId::AllowSnapPopupWindows, m_settings.allowSnapPopupWindows);
        SetBoolFlag(values, NonLocalizable::AllowChildWindowSnapID, SettingId::AllowSnapChildWindows, m_settings.allowSnapChildWindows);
        SetBoolFlag(values, NonLocalizable::DisableRoundCornersOnSnapping, SettingId::DisableRoundCornersOnSnapping, m_settings.disableRoundCorners);

        // colors
        if (auto val = values.get_string_value(NonLocalizable::ZoneColorID))
        {
            if (m_settings.zoneColor != *val)
            {
                m_settings.zoneColor = std::move(*val);
                NotifyObservers(SettingId::ZoneColor);
            }
        }

        if (auto val = values.get_string_value(NonLocalizable::ZoneBorderColorID))
        {
            if (m_settings.zoneBorderColor != *val)
            {
                m_settings.zoneBorderColor = std::move(*val);
                NotifyObservers(SettingId::ZoneBorderColor);
            }
        }

        if (auto val = values.get_string_value(NonLocalizable::ZoneHighlightColorID))
        {
            if (m_settings.zoneHighlightColor != *val)
            {
                m_settings.zoneHighlightColor = std::move(*val);
                NotifyObservers(SettingId::ZoneHighlightColor);
            }
        }

        if (auto val = values.get_string_value(NonLocalizable::ZoneNumberColorID))
        {
            if (m_settings.zoneNumberColor != *val)
            {
                m_settings.zoneNumberColor = std::move(*val);
                NotifyObservers(SettingId::ZoneNumberColor);
            }
        }

        if (auto val = values.get_int_value(NonLocalizable::ZoneHighlightOpacityID))
        {
            if (m_settings.zoneHighlightOpacity != *val)
            {
                m_settings.zoneHighlightOpacity = std::move(*val);
                NotifyObservers(SettingId::ZoneHighlightOpacity);
            }
        }

        // hotkeys
        if (const auto val = values.get_json(NonLocalizable::EditorHotkeyID))
        {
            auto hotkey = PowerToysSettings::HotkeyObject::from_json(*val);
            if (m_settings.editorHotkey.get_modifiers() != hotkey.get_modifiers() || m_settings.editorHotkey.get_key() != hotkey.get_key() || m_settings.editorHotkey.get_code() != hotkey.get_code())
            {
                m_settings.editorHotkey = std::move(hotkey);
                NotifyObservers(SettingId::EditorHotkey);
            }
        }

        if (const auto val = values.get_json(NonLocalizable::NextTabHotkeyID))
        {
            auto hotkey = PowerToysSettings::HotkeyObject::from_json(*val);
            if (m_settings.nextTabHotkey.get_modifiers() != hotkey.get_modifiers() || m_settings.nextTabHotkey.get_key() != hotkey.get_key() || m_settings.nextTabHotkey.get_code() != hotkey.get_code())
            {
                m_settings.nextTabHotkey = PowerToysSettings::HotkeyObject::from_json(*val);
                NotifyObservers(SettingId::NextTabHotkey);
            }
        }

        if (const auto val = values.get_json(NonLocalizable::PrevTabHotkeyID))
        {
            auto hotkey = PowerToysSettings::HotkeyObject::from_json(*val);
            if (m_settings.prevTabHotkey.get_modifiers() != hotkey.get_modifiers() || m_settings.prevTabHotkey.get_key() != hotkey.get_key() || m_settings.prevTabHotkey.get_code() != hotkey.get_code())
            {
                m_settings.prevTabHotkey = PowerToysSettings::HotkeyObject::from_json(*val);
                NotifyObservers(SettingId::PrevTabHotkey);
            }
        }

        // excluded apps
        if (auto val = values.get_string_value(NonLocalizable::ExcludedAppsID))
        {
            std::wstring apps = std::move(*val);
            std::vector<std::wstring> excludedApps;
            auto excludedUppercase = apps;
            CharUpperBuffW(excludedUppercase.data(), static_cast<DWORD>(excludedUppercase.length()));
            std::wstring_view view(excludedUppercase);
            view = left_trim<wchar_t>(trim<wchar_t>(view));

            while (!view.empty())
            {
                auto pos = (std::min)(view.find_first_of(L"\r\n"), view.length());
                excludedApps.emplace_back(view.substr(0, pos));
                view.remove_prefix(pos);
                view = left_trim<wchar_t>(trim<wchar_t>(view));
            }

            if (m_settings.excludedAppsArray != excludedApps)
            {
                m_settings.excludedApps = apps;
                m_settings.excludedAppsArray = excludedApps;
                NotifyObservers(SettingId::ExcludedApps);
            }
        }

        // algorithms
        if (auto val = values.get_int_value(NonLocalizable::OverlappingZonesAlgorithmID))
        {
            // Avoid undefined behavior
            if (*val >= 0 || *val < static_cast<int>(OverlappingZonesAlgorithm::EnumElements))
            {
                auto algorithm = (OverlappingZonesAlgorithm)*val;
                if (m_settings.overlappingZonesAlgorithm != algorithm)
                {
                    m_settings.overlappingZonesAlgorithm = algorithm;
                    NotifyObservers(SettingId::OverlappingZonesAlgorithm);
                }
            }
        }
    }
    catch (const winrt::hresult_error& e)
    {
        // Failure to load settings does not break FancyZones functionality. Display error message and continue with default settings.
        Logger::error(L"Failed to read settings. {}", e.message());
        MessageBox(NULL,
                   GET_RESOURCE_STRING(IDS_FANCYZONES_SETTINGS_LOAD_ERROR).c_str(),
                   GET_RESOURCE_STRING(IDS_POWERTOYS_FANCYZONES).c_str(),
                   MB_OK);
    }
}

void FancyZonesSettings::NotifyObservers(SettingId id) const
{
    for (auto observer : m_observers)
    {
        if (observer->WantsToBeNotified(id))
        {
            observer->SettingsUpdate(id);
        }
    }
}
