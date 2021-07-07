#include "pch.h"
#include <filesystem>
#include <fstream>

#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/FancyZones.h>
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
        Assert::AreEqual(expected.displayChange_moveWindows, actual.displayChange_moveWindows);
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
    }

    TEST_CLASS (FancyZonesSettingsCreationUnitTest)
    {
        HINSTANCE m_hInst;
        PCWSTR m_moduleName = L"FancyZonesUnitTests";
        PCWSTR m_moduleKey = L"FancyZonesUnitTests";
        std::wstring m_tmpName;

        const PowerToysSettings::HotkeyObject m_defaultHotkeyObject = PowerToysSettings::HotkeyObject::from_settings(true, false, false, false, VK_OEM_3);
        const Settings m_defaultSettings;

        TEST_METHOD_INITIALIZE(Init)
            {
                m_hInst = (HINSTANCE)GetModuleHandleW(nullptr);
                m_tmpName = PTSettingsHelper::get_module_save_folder_location(m_moduleName) + L"\\settings.json";
            }
            TEST_METHOD_CLEANUP(Cleanup)
                {
                    std::filesystem::remove_all(PTSettingsHelper::get_module_save_folder_location(m_moduleName));
                }

                TEST_METHOD (CreateWithHinstanceDefault)
                {
                    auto actual = MakeFancyZonesSettings({}, m_moduleName, m_moduleKey);
                    Assert::IsTrue(actual != nullptr);

                    auto actualSettings = actual->GetSettings();
                    compareSettings(m_defaultSettings, *actualSettings);
                }

                TEST_METHOD (CreateWithHinstanceNullptr)
                {
                    auto actual = MakeFancyZonesSettings(nullptr, m_moduleName, m_moduleKey);
                    Assert::IsTrue(actual != nullptr);

                    auto actualSettings = actual->GetSettings();
                    compareSettings(m_defaultSettings, *actualSettings);
                }

                TEST_METHOD (CreateWithNameEmpty)
                {
                    auto actual = MakeFancyZonesSettings(m_hInst, L"", m_moduleKey);
                    Assert::IsTrue(actual != nullptr);

                    auto actualSettings = actual->GetSettings();
                    compareSettings(m_defaultSettings, *actualSettings);
                }

                TEST_METHOD (Create)
                {
                    //prepare data
                    const Settings expected;

                    PowerToysSettings::PowerToyValues values(m_moduleName, m_moduleKey);
                    values.add_property(L"fancyzones_shiftDrag", expected.shiftDrag);
                    values.add_property(L"fancyzones_mouseSwitch", expected.mouseSwitch);
                    values.add_property(L"fancyzones_displayChange_moveWindows", expected.displayChange_moveWindows);
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
                    values.add_property(L"fancyzones_excluded_apps", expected.excludedApps);

                    values.save_to_settings_file();

                    auto actual = MakeFancyZonesSettings(m_hInst, m_moduleName, m_moduleKey);
                    Assert::IsTrue(actual != nullptr);

                    auto actualSettings = actual->GetSettings();
                    compareSettings(expected, *actualSettings);
                }

                TEST_METHOD (CreateWithMultipleApps)
                {
                    //prepare data
                    const Settings expected{
                        .excludedApps = L"app\r\napp1\r\napp2\r\nanother app",
                        .excludedAppsArray = { L"APP", L"APP1", L"APP2", L"ANOTHER APP" },
                    };

                    PowerToysSettings::PowerToyValues values(m_moduleName, m_moduleKey);
                    values.add_property(L"fancyzones_shiftDrag", expected.shiftDrag);
                    values.add_property(L"fancyzones_mouseSwitch", expected.mouseSwitch);
                    values.add_property(L"fancyzones_displayChange_moveWindows", expected.displayChange_moveWindows);
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
                    values.add_property(L"fancyzones_excluded_apps", expected.excludedApps);

                    values.save_to_settings_file();

                    auto actual = MakeFancyZonesSettings(m_hInst, m_moduleName, m_moduleKey);
                    Assert::IsTrue(actual != nullptr);

                    auto actualSettings = actual->GetSettings();
                    compareSettings(expected, *actualSettings);
                }

                TEST_METHOD (CreateWithBoolValuesMissed)
                {
                    const Settings expected{
                        .shiftDrag = m_defaultSettings.shiftDrag,
                        .mouseSwitch = m_defaultSettings.mouseSwitch,
                        .displayChange_moveWindows = m_defaultSettings.displayChange_moveWindows,
                        .zoneSetChange_flashZones = m_defaultSettings.zoneSetChange_flashZones,
                        .zoneSetChange_moveWindows = m_defaultSettings.zoneSetChange_moveWindows,
                        .overrideSnapHotkeys = m_defaultSettings.overrideSnapHotkeys,
                        .moveWindowAcrossMonitors = m_defaultSettings.moveWindowAcrossMonitors,
                        .moveWindowsBasedOnPosition = m_defaultSettings.moveWindowsBasedOnPosition,
                        .appLastZone_moveWindows = m_defaultSettings.appLastZone_moveWindows,
                        .openWindowOnActiveMonitor = m_defaultSettings.openWindowOnActiveMonitor,
                        .restoreSize = m_defaultSettings.restoreSize,
                        .use_cursorpos_editor_startupscreen = m_defaultSettings.use_cursorpos_editor_startupscreen,
                        .showZonesOnAllMonitors = m_defaultSettings.showZonesOnAllMonitors,
                        .spanZonesAcrossMonitors = m_defaultSettings.spanZonesAcrossMonitors,
                        .makeDraggedWindowTransparent = m_defaultSettings.makeDraggedWindowTransparent,
                        .zoneColor = L"FAFAFA",
                        .zoneBorderColor = L"CCDDEE",
                        .zoneHighlightColor = L"#00FFD7",
                        .zoneHighlightOpacity = 45,
                        .editorHotkey = PowerToysSettings::HotkeyObject::from_settings(false, true, true, false, VK_OEM_3),
                        .excludedApps = L"app",
                        .excludedAppsArray = { L"APP" },
                    };

                    PowerToysSettings::PowerToyValues values(m_moduleName, m_moduleKey);
                    values.add_property(L"fancyzones_zoneColor", expected.zoneColor);
                    values.add_property(L"fancyzones_zoneBorderColor", expected.zoneBorderColor);
                    values.add_property(L"fancyzones_zoneHighlightColor", expected.zoneHighlightColor);
                    values.add_property(L"fancyzones_highlight_opacity", expected.zoneHighlightOpacity);
                    values.add_property(L"fancyzones_editor_hotkey", expected.editorHotkey.get_json());
                    values.add_property(L"fancyzones_excluded_apps", expected.excludedApps);

                    values.save_to_settings_file();

                    auto actual = MakeFancyZonesSettings(m_hInst, m_moduleName, m_moduleKey);
                    Assert::IsTrue(actual != nullptr);

                    auto actualSettings = actual->GetSettings();
                    compareSettings(expected, *actualSettings);
                }

                TEST_METHOD (CreateColorMissed)
                {
                    //prepare data
                    const Settings expected;

                    PowerToysSettings::PowerToyValues values(m_moduleName, m_moduleKey);
                    values.add_property(L"fancyzones_shiftDrag", expected.shiftDrag);
                    values.add_property(L"fancyzones_mouseSwitch", expected.mouseSwitch);
                    values.add_property(L"fancyzones_displayChange_moveWindows", expected.displayChange_moveWindows);
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
                    values.add_property(L"fancyzones_highlight_opacity", expected.zoneHighlightOpacity);
                    values.add_property(L"fancyzones_editor_hotkey", expected.editorHotkey.get_json());
                    values.add_property(L"fancyzones_excluded_apps", expected.excludedApps);

                    values.save_to_settings_file();

                    auto actual = MakeFancyZonesSettings(m_hInst, m_moduleName, m_moduleKey);
                    Assert::IsTrue(actual != nullptr);

                    auto actualSettings = actual->GetSettings();
                    compareSettings(expected, *actualSettings);
                }

                TEST_METHOD (CreateOpacityMissed)
                {
                    //prepare data
                    const Settings expected;

                    PowerToysSettings::PowerToyValues values(m_moduleName, m_moduleKey);
                    values.add_property(L"fancyzones_shiftDrag", expected.shiftDrag);
                    values.add_property(L"fancyzones_mouseSwitch", expected.mouseSwitch);
                    values.add_property(L"fancyzones_displayChange_moveWindows", expected.displayChange_moveWindows);
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
                    values.add_property(L"fancyzones_zoneHighlightColor", expected.zoneHighlightColor);
                    values.add_property(L"fancyzones_editor_hotkey", expected.editorHotkey.get_json());
                    values.add_property(L"fancyzones_excluded_apps", expected.excludedApps);

                    values.save_to_settings_file();

                    auto actual = MakeFancyZonesSettings(m_hInst, m_moduleName, m_moduleKey);
                    Assert::IsTrue(actual != nullptr);

                    auto actualSettings = actual->GetSettings();
                    compareSettings(expected, *actualSettings);
                }

                TEST_METHOD (CreateHotkeyMissed)
                {
                    //prepare data
                    const Settings expected;

                    PowerToysSettings::PowerToyValues values(m_moduleName, m_moduleKey);
                    values.add_property(L"fancyzones_shiftDrag", expected.shiftDrag);
                    values.add_property(L"fancyzones_mouseSwitch", expected.mouseSwitch);
                    values.add_property(L"fancyzones_displayChange_moveWindows", expected.displayChange_moveWindows);
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
                    values.add_property(L"fancyzones_excluded_apps", expected.excludedApps);

                    values.save_to_settings_file();

                    auto actual = MakeFancyZonesSettings(m_hInst, m_moduleName, m_moduleKey);
                    Assert::IsTrue(actual != nullptr);

                    auto actualSettings = actual->GetSettings();
                    compareSettings(expected, *actualSettings);
                }

                TEST_METHOD (CreateAppsMissed)
                {
                    //prepare data
                    const Settings expected;

                    PowerToysSettings::PowerToyValues values(m_moduleName, m_moduleKey);
                    values.add_property(L"fancyzones_shiftDrag", expected.shiftDrag);
                    values.add_property(L"fancyzones_mouseSwitch", expected.mouseSwitch);
                    values.add_property(L"fancyzones_displayChange_moveWindows", expected.displayChange_moveWindows);
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

                    values.save_to_settings_file();

                    auto actual = MakeFancyZonesSettings(m_hInst, m_moduleName, m_moduleKey);
                    Assert::IsTrue(actual != nullptr);

                    auto actualSettings = actual->GetSettings();
                    compareSettings(expected, *actualSettings);
                }

                TEST_METHOD (CreateWithEmptyJson)
                {
                    json::to_file(m_tmpName, json::JsonObject());
                    auto actual = MakeFancyZonesSettings(m_hInst, m_moduleName, m_moduleKey);
                    Assert::IsTrue(actual != nullptr);

                    auto actualSettings = actual->GetSettings();
                    compareSettings(m_defaultSettings, *actualSettings);
                }

                TEST_METHOD (CreateWithCorruptedJson)
                {
                    std::wofstream{ m_tmpName.data(), std::ios::binary } << L"{ \"version\": \"1.0\", \"name\": \"";

                    auto actual = MakeFancyZonesSettings(m_hInst, m_moduleName, m_moduleKey);

                    Assert::IsTrue(actual != nullptr);
                    auto actualSettings = actual->GetSettings();
                    compareSettings(m_defaultSettings, *actualSettings);
                }

                TEST_METHOD (CreateWithCyrillicSymbolsInJson)
                {
                    std::wofstream{ m_tmpName.data(), std::ios::binary } << L"{ \"version\": \"1.0\", \"name\": \"ФансиЗонс\"}";
                    auto actual = MakeFancyZonesSettings(m_hInst, m_moduleName, m_moduleKey);
                    Assert::IsTrue(actual != nullptr);

                    auto actualSettings = actual->GetSettings();
                    compareSettings(m_defaultSettings, *actualSettings);
                }
    };

    TEST_CLASS (FancyZonesSettingsUnitTests)
    {
        winrt::com_ptr<IFancyZonesSettings> m_settings = nullptr;
        PCWSTR m_moduleName = L"FancyZonesUnitTests";
        PCWSTR m_moduleKey = L"FancyZonesUnitTests";

        std::wstring serializedPowerToySettings(const Settings& settings)
        {
            PowerToysSettings::Settings ptSettings(HINSTANCE{}, m_moduleName);
            ptSettings.set_description(IDS_SETTING_DESCRIPTION);
            ptSettings.set_icon_key(L"pt-fancy-zones");
            ptSettings.set_overview_link(L"https://aka.ms/PowerToysOverview_FancyZones");
            ptSettings.set_video_link(L"https://youtu.be/rTtGzZYAXgY");

            ptSettings.add_custom_action(
                L"ToggledFZEditor", // action name.
                IDS_SETTING_LAUNCH_EDITOR_LABEL,
                IDS_SETTING_LAUNCH_EDITOR_BUTTON,
                IDS_SETTING_LAUNCH_EDITOR_DESCRIPTION);
            ptSettings.add_hotkey(L"fancyzones_editor_hotkey", IDS_SETTING_LAUNCH_EDITOR_HOTKEY_LABEL, settings.editorHotkey);
            ptSettings.add_bool_toggle(L"fancyzones_shiftDrag", IDS_SETTING_DESCRIPTION_SHIFTDRAG, settings.shiftDrag);
            ptSettings.add_bool_toggle(L"fancyzones_mouseSwitch", IDS_SETTING_DESCRIPTION_MOUSESWITCH, settings.mouseSwitch);
            ptSettings.add_bool_toggle(L"fancyzones_overrideSnapHotkeys", IDS_SETTING_DESCRIPTION_OVERRIDE_SNAP_HOTKEYS, settings.overrideSnapHotkeys);
            ptSettings.add_bool_toggle(L"fancyzones_moveWindowAcrossMonitors", IDS_SETTING_DESCRIPTION_MOVE_WINDOW_ACROSS_MONITORS, settings.moveWindowAcrossMonitors);
            ptSettings.add_bool_toggle(L"fancyzones_moveWindowsBasedOnPosition", IDS_SETTING_DESCRIPTION_MOVE_WINDOWS_BASED_ON_POSITION, settings.moveWindowsBasedOnPosition);
            ptSettings.add_bool_toggle(L"fancyzones_zoneSetChange_flashZones", IDS_SETTING_DESCRIPTION_ZONESETCHANGE_FLASHZONES, settings.zoneSetChange_flashZones);
            ptSettings.add_bool_toggle(L"fancyzones_displayChange_moveWindows", IDS_SETTING_DESCRIPTION_DISPLAYCHANGE_MOVEWINDOWS, settings.displayChange_moveWindows);
            ptSettings.add_bool_toggle(L"fancyzones_zoneSetChange_moveWindows", IDS_SETTING_DESCRIPTION_ZONESETCHANGE_MOVEWINDOWS, settings.zoneSetChange_moveWindows);
            ptSettings.add_bool_toggle(L"fancyzones_appLastZone_moveWindows", IDS_SETTING_DESCRIPTION_APPLASTZONE_MOVEWINDOWS, settings.appLastZone_moveWindows);
            ptSettings.add_bool_toggle(L"fancyzones_openWindowOnActiveMonitor", IDS_SETTING_DESCRIPTION_OPEN_WINDOW_ON_ACTIVE_MONITOR, settings.openWindowOnActiveMonitor);
            ptSettings.add_bool_toggle(L"fancyzones_restoreSize", IDS_SETTING_DESCRIPTION_RESTORESIZE, settings.restoreSize);
            ptSettings.add_bool_toggle(L"use_cursorpos_editor_startupscreen", IDS_SETTING_DESCRIPTION_USE_CURSORPOS_EDITOR_STARTUPSCREEN, settings.use_cursorpos_editor_startupscreen);
            ptSettings.add_bool_toggle(L"fancyzones_show_on_all_monitors", IDS_SETTING_DESCRIPTION_SHOW_FANCY_ZONES_ON_ALL_MONITORS, settings.showZonesOnAllMonitors);
            ptSettings.add_bool_toggle(L"fancyzones_multi_monitor_mode", IDS_SETTING_DESCRIPTION_SPAN_ZONES_ACROSS_MONITORS, settings.spanZonesAcrossMonitors);
            ptSettings.add_bool_toggle(L"fancyzones_makeDraggedWindowTransparent", IDS_SETTING_DESCRIPTION_MAKE_DRAGGED_WINDOW_TRANSPARENT, settings.makeDraggedWindowTransparent);
            ptSettings.add_int_spinner(L"fancyzones_highlight_opacity", IDS_SETTINGS_HIGHLIGHT_OPACITY, settings.zoneHighlightOpacity, 0, 100, 1);
            ptSettings.add_color_picker(L"fancyzones_zoneColor", IDS_SETTING_DESCRIPTION_ZONECOLOR, settings.zoneColor);
            ptSettings.add_color_picker(L"fancyzones_zoneBorderColor", IDS_SETTING_DESCRIPTION_ZONE_BORDER_COLOR, settings.zoneBorderColor);
            ptSettings.add_color_picker(L"fancyzones_zoneHighlightColor", IDS_SETTING_DESCRIPTION_ZONEHIGHLIGHTCOLOR, settings.zoneHighlightColor);
            ptSettings.add_multiline_string(L"fancyzones_excluded_apps", IDS_SETTING_EXCLUDED_APPS_DESCRIPTION, settings.excludedApps);

            return ptSettings.serialize();
        }

        TEST_METHOD_INITIALIZE(Init)
            {
                HINSTANCE hInst = (HINSTANCE)GetModuleHandleW(nullptr);

                m_settings = MakeFancyZonesSettings(hInst, m_moduleName, m_moduleKey);
                Assert::IsTrue(m_settings != nullptr);
            }

            TEST_METHOD_CLEANUP(Cleanup)
                {
                    std::filesystem::remove_all(PTSettingsHelper::get_module_save_folder_location(m_moduleName));
                }

                TEST_METHOD (GetConfig)
                {
                    int expectedSize = 0;
                    m_settings->GetConfig(nullptr, &expectedSize);
                    Assert::AreNotEqual(0, expectedSize);

                    int actualBufferSize = expectedSize;
                    PWSTR actualBuffer = new wchar_t[actualBufferSize];

                    Assert::IsTrue(m_settings->GetConfig(actualBuffer, &actualBufferSize));
                    Assert::AreEqual(expectedSize, actualBufferSize);
                }

                TEST_METHOD (GetConfigSmallBuffer)
                {
                    int size = 0;
                    m_settings->GetConfig(nullptr, &size);
                    Assert::AreNotEqual(0, size);

                    int actualBufferSize = size - 1;
                    PWSTR actualBuffer = new wchar_t[actualBufferSize];

                    Assert::IsFalse(m_settings->GetConfig(actualBuffer, &actualBufferSize));
                    Assert::AreEqual(size, actualBufferSize);
                }

                TEST_METHOD (GetConfigNullBuffer)
                {
                    int expectedSize = 0;
                    m_settings->GetConfig(nullptr, &expectedSize);
                    Assert::AreNotEqual(0, expectedSize);

                    int actualBufferSize = 0;

                    Assert::IsFalse(m_settings->GetConfig(nullptr, &actualBufferSize));
                    Assert::AreEqual(expectedSize, actualBufferSize);
                }

                TEST_METHOD (SetConfig)
                {
                    //cleanup file before call set config
                    const auto settingsFile = PTSettingsHelper::get_module_save_folder_location(m_moduleName) + L"\\settings.json";
                    std::filesystem::remove(settingsFile);

                    const Settings expected{
                        .shiftDrag = true,
                        .mouseSwitch = true,
                        .displayChange_moveWindows = true,
                        .zoneSetChange_flashZones = false,
                        .zoneSetChange_moveWindows = true,
                        .overrideSnapHotkeys = false,
                        .moveWindowAcrossMonitors = false,
                        .appLastZone_moveWindows = true,
                        .openWindowOnActiveMonitor = false,
                        .restoreSize = false,
                        .use_cursorpos_editor_startupscreen = true,
                        .showZonesOnAllMonitors = false,
                        .spanZonesAcrossMonitors = false,
                        .makeDraggedWindowTransparent = true,
                        .zoneColor = L"#FAFAFA",
                        .zoneBorderColor = L"CCDDEE",
                        .zoneHighlightColor = L"#00AABB",
                        .zoneHighlightOpacity = 45,
                        .editorHotkey = PowerToysSettings::HotkeyObject::from_settings(false, false, false, false, VK_OEM_3),
                        .excludedApps = L"app\r\napp2",
                        .excludedAppsArray = { L"APP", L"APP2" },
                    };

                    auto config = serializedPowerToySettings(expected);
                    m_settings->SetConfig(config.c_str());

                    auto actual = m_settings->GetSettings();
                    compareSettings(expected, *actual);

                    Assert::IsTrue(std::filesystem::exists(settingsFile));
                }
    };
}
