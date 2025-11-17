#include "pch.h"
#include <filesystem>
#include <fstream>

#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/FancyZones.h>
#include <FancyZonesLib/ModuleConstants.h>
#include <common/SettingsAPI/settings_helpers.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    void compareHotkeyObjects(const PowerToysSettings::HotkeyObject& expected, const PowerToysSettings::HotkeyObject& actual)
    {
        Assert::AreEqual(expected.alt_pressed(), actual.alt_pressed());
        Assert::AreEqual(expected.ctrl_pressed(), actual.ctrl_pressed());
        Assert::AreEqual(expected.shift_pressed(), actual.shift_pressed());
        Assert::AreEqual(expected.win_pressed(), actual.win_pressed());

        //NOTE: key_from_code may create different values
        //Assert::AreEqual(expected.get_key(), actual.get_key());
        Assert::AreEqual(expected.get_code(), actual.get_code());
        Assert::AreEqual(expected.get_modifiers(), actual.get_modifiers());
        Assert::AreEqual(expected.get_modifiers_repeat(), actual.get_modifiers_repeat());
    }

    void compareSettings(const Settings& expected, const Settings& actual)
    {
        Assert::AreEqual(expected.shiftDrag, actual.shiftDrag);
        Assert::AreEqual(expected.mouseSwitch, actual.mouseSwitch);
        Assert::AreEqual(expected.displayOrWorkAreaChange_moveWindows, actual.displayOrWorkAreaChange_moveWindows);
        Assert::AreEqual(expected.zoneSetChange_flashZones, actual.zoneSetChange_flashZones);
        Assert::AreEqual(expected.zoneSetChange_moveWindows, actual.zoneSetChange_moveWindows);
        Assert::AreEqual(expected.overrideSnapHotkeys, actual.overrideSnapHotkeys);
        Assert::AreEqual(expected.moveWindowAcrossMonitors, actual.moveWindowAcrossMonitors);
        Assert::AreEqual(expected.moveWindowsBasedOnPosition, actual.moveWindowsBasedOnPosition);
        Assert::AreEqual(expected.appLastZone_moveWindows, actual.appLastZone_moveWindows);
        Assert::AreEqual(expected.openWindowOnActiveMonitor, actual.openWindowOnActiveMonitor);
        Assert::AreEqual(expected.restoreSize, actual.restoreSize);
        Assert::AreEqual(expected.use_cursorpos_editor_startupscreen, actual.use_cursorpos_editor_startupscreen);
        Assert::AreEqual(expected.showZonesOnAllMonitors, actual.showZonesOnAllMonitors);
        Assert::AreEqual(expected.spanZonesAcrossMonitors, actual.spanZonesAcrossMonitors);
        Assert::AreEqual(expected.makeDraggedWindowTransparent, actual.makeDraggedWindowTransparent);
        Assert::AreEqual(expected.windowSwitching, actual.windowSwitching);
        Assert::AreEqual(expected.zoneColor.c_str(), actual.zoneColor.c_str());
        Assert::AreEqual(expected.zoneBorderColor.c_str(), actual.zoneBorderColor.c_str());
        Assert::AreEqual(expected.zoneHighlightColor.c_str(), actual.zoneHighlightColor.c_str());
        Assert::AreEqual(expected.zoneHighlightOpacity, actual.zoneHighlightOpacity);
        Assert::AreEqual(expected.excludedApps.c_str(), actual.excludedApps.c_str());
        Assert::AreEqual(expected.excludedAppsArray.size(), actual.excludedAppsArray.size());
        for (int i = 0; i < expected.excludedAppsArray.size(); i++)
        {
            Assert::AreEqual(expected.excludedAppsArray[i].c_str(), actual.excludedAppsArray[i].c_str());
        }

        compareHotkeyObjects(expected.editorHotkey, actual.editorHotkey);
        compareHotkeyObjects(expected.nextTabHotkey, actual.nextTabHotkey);
        compareHotkeyObjects(expected.prevTabHotkey, actual.prevTabHotkey);
    }

    TEST_CLASS (FancyZonesSettingsUnitTest)
    {
        const Settings m_defaultSettings;

        TEST_METHOD_INITIALIZE(Init)
        {
            // reset to defaults
            PowerToysSettings::PowerToyValues values(NonLocalizable::ModuleKey, NonLocalizable::ModuleKey);
            values.add_property(L"fancyzones_shiftDrag", m_defaultSettings.shiftDrag);
            values.add_property(L"fancyzones_mouseSwitch", m_defaultSettings.mouseSwitch);
            values.add_property(L"fancyzones_displayOrWorkAreaChange_moveWindows", m_defaultSettings.displayOrWorkAreaChange_moveWindows);
            values.add_property(L"fancyzones_zoneSetChange_flashZones", m_defaultSettings.zoneSetChange_flashZones);
            values.add_property(L"fancyzones_zoneSetChange_moveWindows", m_defaultSettings.zoneSetChange_moveWindows);
            values.add_property(L"fancyzones_overrideSnapHotkeys", m_defaultSettings.overrideSnapHotkeys);
            values.add_property(L"fancyzones_moveWindowAcrossMonitors", m_defaultSettings.moveWindowAcrossMonitors);
            values.add_property(L"fancyzones_moveWindowsBasedOnPosition", m_defaultSettings.moveWindowsBasedOnPosition);
            values.add_property(L"fancyzones_appLastZone_moveWindows", m_defaultSettings.appLastZone_moveWindows);
            values.add_property(L"fancyzones_openWindowOnActiveMonitor", m_defaultSettings.openWindowOnActiveMonitor);
            values.add_property(L"fancyzones_restoreSize", m_defaultSettings.restoreSize);
            values.add_property(L"use_cursorpos_editor_startupscreen", m_defaultSettings.use_cursorpos_editor_startupscreen);
            values.add_property(L"fancyzones_show_on_all_monitors", m_defaultSettings.showZonesOnAllMonitors);
            values.add_property(L"fancyzones_multi_monitor_mode", m_defaultSettings.spanZonesAcrossMonitors);
            values.add_property(L"fancyzones_makeDraggedWindowTransparent", m_defaultSettings.makeDraggedWindowTransparent);
            values.add_property(L"fancyzones_zoneColor", m_defaultSettings.zoneColor);
            values.add_property(L"fancyzones_zoneBorderColor", m_defaultSettings.zoneBorderColor);
            values.add_property(L"fancyzones_zoneHighlightColor", m_defaultSettings.zoneHighlightColor);
            values.add_property(L"fancyzones_highlight_opacity", m_defaultSettings.zoneHighlightOpacity);
            values.add_property(L"fancyzones_editor_hotkey", m_defaultSettings.editorHotkey.get_json());
            values.add_property(L"fancyzones_windowSwitching", m_defaultSettings.windowSwitching);
            values.add_property(L"fancyzones_nextTab_hotkey", m_defaultSettings.nextTabHotkey.get_json());
            values.add_property(L"fancyzones_prevTab_hotkey", m_defaultSettings.prevTabHotkey.get_json());
            values.add_property(L"fancyzones_excluded_apps", m_defaultSettings.excludedApps);

            json::to_file(FancyZonesSettings::GetSettingsFileName(), values.get_raw_json());
            FancyZonesSettings::instance().LoadSettings();
        }
        
        TEST_METHOD_CLEANUP(Cleanup)
        {
            std::filesystem::remove(FancyZonesSettings::GetSettingsFileName());
        }

        TEST_METHOD (Parse)
        {
            //prepare data
            const Settings expected{
                .excludedApps = L"app\r\napp1\r\napp2\r\nanother app",
                .excludedAppsArray = { L"APP", L"APP1", L"APP2", L"ANOTHER APP" },
            };

            PowerToysSettings::PowerToyValues values(NonLocalizable::ModuleKey, NonLocalizable::ModuleKey);
            values.add_property(L"fancyzones_shiftDrag", expected.shiftDrag);
            values.add_property(L"fancyzones_mouseSwitch", expected.mouseSwitch);
            values.add_property(L"fancyzones_displayOrWorkAreaChange_moveWindows", expected.displayOrWorkAreaChange_moveWindows);
            values.add_property(L"fancyzones_zoneSetChange_flashZones", expected.zoneSetChange_flashZones);
            values.add_property(L"fancyzones_zoneSetChange_moveWindows", expected.zoneSetChange_moveWindows);
            values.add_property(L"fancyzones_overrideSnapHotkeys", expected.overrideSnapHotkeys);
            values.add_property(L"fancyzones_moveWindowAcrossMonitors", expected.moveWindowAcrossMonitors);
            values.add_property(L"fancyzones_moveWindowsBasedOnPosition", expected.moveWindowsBasedOnPosition);
            values.add_property(L"fancyzones_appLastZone_moveWindows", expected.appLastZone_moveWindows);
            values.add_property(L"fancyzones_openWindowOnActiveMonitor", expected.openWindowOnActiveMonitor);
            values.add_property(L"fancyzones_restoreSize", expected.restoreSize);
            values.add_property(L"use_cursorpos_editor_startupscreen", expected.use_cursorpos_editor_startupscreen);
            values.add_property(L"fancyzones_show_on_all_monitors", expected.showZonesOnAllMonitors);
            values.add_property(L"fancyzones_multi_monitor_mode", expected.spanZonesAcrossMonitors);
            values.add_property(L"fancyzones_makeDraggedWindowTransparent", expected.makeDraggedWindowTransparent);
            values.add_property(L"fancyzones_zoneColor", expected.zoneColor);
            values.add_property(L"fancyzones_zoneBorderColor", expected.zoneBorderColor);
            values.add_property(L"fancyzones_zoneHighlightColor", expected.zoneHighlightColor);
            values.add_property(L"fancyzones_highlight_opacity", expected.zoneHighlightOpacity);
            values.add_property(L"fancyzones_editor_hotkey", expected.editorHotkey.get_json());
            values.add_property(L"fancyzones_windowSwitching", expected.windowSwitching);
            values.add_property(L"fancyzones_nextTab_hotkey", expected.nextTabHotkey.get_json());
            values.add_property(L"fancyzones_prevTab_hotkey", expected.prevTabHotkey.get_json());
            values.add_property(L"fancyzones_excluded_apps", expected.excludedApps);

            json::to_file(FancyZonesSettings::GetSettingsFileName(), values.get_raw_json());
            
            FancyZonesSettings::instance().LoadSettings();
            auto actual = FancyZonesSettings::settings();
            compareSettings(expected, actual);
        }

        TEST_METHOD (ParseInvalid)
        {
            PowerToysSettings::PowerToyValues values(NonLocalizable::ModuleKey, NonLocalizable::ModuleKey);
            values.add_property(L"non_fancyzones_value", false);

            json::to_file(FancyZonesSettings::GetSettingsFileName(), values.get_raw_json());

            FancyZonesSettings::instance().LoadSettings();
            auto actual = FancyZonesSettings::settings();
            compareSettings(m_defaultSettings, actual);
        }

        TEST_METHOD (ParseEmpty)
        {
            FancyZonesSettings::instance().LoadSettings();
            auto actual = FancyZonesSettings::settings();
            compareSettings(m_defaultSettings, actual);
        }
    };
}
