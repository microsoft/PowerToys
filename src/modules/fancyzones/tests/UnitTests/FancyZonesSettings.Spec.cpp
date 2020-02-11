#include "pch.h"
#include <filesystem>

#include <lib/Settings.h>
#include <lib/FancyZones.h>
#include <common/settings_helpers.h>

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
        Assert::AreEqual(expected.displayChange_moveWindows, actual.displayChange_moveWindows);
        Assert::AreEqual(expected.virtualDesktopChange_moveWindows, actual.virtualDesktopChange_moveWindows);
        Assert::AreEqual(expected.zoneSetChange_flashZones, actual.zoneSetChange_flashZones);
        Assert::AreEqual(expected.zoneSetChange_moveWindows, actual.zoneSetChange_moveWindows);
        Assert::AreEqual(expected.overrideSnapHotkeys, actual.overrideSnapHotkeys);
        Assert::AreEqual(expected.appLastZone_moveWindows, actual.appLastZone_moveWindows);
        Assert::AreEqual(expected.use_cursorpos_editor_startupscreen, actual.use_cursorpos_editor_startupscreen);
        Assert::AreEqual(expected.zoneHightlightColor.c_str(), actual.zoneHightlightColor.c_str());
        Assert::AreEqual(expected.zoneHighlightOpacity, actual.zoneHighlightOpacity);
        Assert::AreEqual(expected.excludedApps.c_str(), actual.excludedApps.c_str());
        Assert::AreEqual(expected.excludedAppsArray.size(), actual.excludedAppsArray.size());
        for (int i = 0; i < expected.excludedAppsArray.size(); i++)
        {
            Assert::AreEqual(expected.excludedAppsArray[i].c_str(), actual.excludedAppsArray[i].c_str());
        }

        compareHotkeyObjects(expected.editorHotkey, actual.editorHotkey);
    }

    TEST_CLASS(FancyZonesSettingsCreationUnitTest)
    {
        HINSTANCE m_hInst;
        PCWSTR m_moduleName = L"FancyZonesTest";
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
            std::filesystem::remove(m_tmpName);
        }

        TEST_METHOD(CreateWithHinstanceDefault)
        {
            auto actual = MakeFancyZonesSettings({}, m_moduleName);
            Assert::IsTrue(actual != nullptr);

            auto actualSettings = actual->GetSettings();
            compareSettings(m_defaultSettings, actualSettings);
        }

        TEST_METHOD(CreateWithHinstanceNullptr)
        {
            auto actual = MakeFancyZonesSettings(nullptr, m_moduleName);
            Assert::IsTrue(actual != nullptr);

            auto actualSettings = actual->GetSettings();
            compareSettings(m_defaultSettings, actualSettings);
        }

        TEST_METHOD(CreateWithNameEmpty)
        {
            auto actual = MakeFancyZonesSettings(m_hInst, L"");
            Assert::IsTrue(actual != nullptr);

            auto actualSettings = actual->GetSettings();
            compareSettings(m_defaultSettings, actualSettings);
        }

        TEST_METHOD(Create)
        {
            //prepare data
            const Settings expected {
                .shiftDrag = false,
                .displayChange_moveWindows = true,
                .virtualDesktopChange_moveWindows = true,
                .zoneSetChange_flashZones = true,
                .zoneSetChange_moveWindows = true,
                .overrideSnapHotkeys = false,
                .appLastZone_moveWindows = false,
                .use_cursorpos_editor_startupscreen = true,
                .zoneHightlightColor = L"#00FFD7",
                .zoneHighlightOpacity = 45,
                .editorHotkey = PowerToysSettings::HotkeyObject::from_settings(false, true, true, false, VK_OEM_3),
                .excludedApps = L"app",
                .excludedAppsArray = { L"APP" },
            };

            PowerToysSettings::PowerToyValues values(m_moduleName);
            values.add_property(L"fancyzones_shiftDrag", expected.shiftDrag);
            values.add_property(L"fancyzones_displayChange_moveWindows", expected.displayChange_moveWindows);
            values.add_property(L"fancyzones_virtualDesktopChange_moveWindows", expected.virtualDesktopChange_moveWindows);
            values.add_property(L"fancyzones_zoneSetChange_flashZones", expected.zoneSetChange_flashZones);
            values.add_property(L"fancyzones_zoneSetChange_moveWindows", expected.zoneSetChange_moveWindows);
            values.add_property(L"fancyzones_overrideSnapHotkeys", expected.overrideSnapHotkeys);
            values.add_property(L"fancyzones_appLastZone_moveWindows", expected.appLastZone_moveWindows);
            values.add_property(L"use_cursorpos_editor_startupscreen", expected.use_cursorpos_editor_startupscreen);
            values.add_property(L"fancyzones_zoneHighlightColor", expected.zoneHightlightColor);
            values.add_property(L"fancyzones_highlight_opacity", expected.zoneHighlightOpacity);
            values.add_property(L"fancyzones_editor_hotkey", expected.editorHotkey.get_json());
            values.add_property(L"fancyzones_excluded_apps", expected.excludedApps);

            values.save_to_settings_file();

            auto actual = MakeFancyZonesSettings(m_hInst, m_moduleName);
            Assert::IsTrue(actual != nullptr);

            auto actualSettings = actual->GetSettings();
            compareSettings(expected, actualSettings);
        }

        TEST_METHOD(CreateWithMultipleApps)
        {
            //prepare data
            const Settings expected {
                .shiftDrag = false,
                .displayChange_moveWindows = true,
                .virtualDesktopChange_moveWindows = true,
                .zoneSetChange_flashZones = true,
                .zoneSetChange_moveWindows = true,
                .overrideSnapHotkeys = false,
                .appLastZone_moveWindows = false,
                .use_cursorpos_editor_startupscreen = true,
                .zoneHightlightColor = L"#00FFD7",
                .zoneHighlightOpacity = 45,
                .editorHotkey = PowerToysSettings::HotkeyObject::from_settings(false, true, true, false, VK_OEM_3),
                .excludedApps = L"app\r\napp1\r\napp2\r\nanother app",
                .excludedAppsArray = { L"APP", L"APP1", L"APP2", L"ANOTHER APP" },
            };

            PowerToysSettings::PowerToyValues values(m_moduleName);
            values.add_property(L"fancyzones_shiftDrag", expected.shiftDrag);
            values.add_property(L"fancyzones_displayChange_moveWindows", expected.displayChange_moveWindows);
            values.add_property(L"fancyzones_virtualDesktopChange_moveWindows", expected.virtualDesktopChange_moveWindows);
            values.add_property(L"fancyzones_zoneSetChange_flashZones", expected.zoneSetChange_flashZones);
            values.add_property(L"fancyzones_zoneSetChange_moveWindows", expected.zoneSetChange_moveWindows);
            values.add_property(L"fancyzones_overrideSnapHotkeys", expected.overrideSnapHotkeys);
            values.add_property(L"fancyzones_appLastZone_moveWindows", expected.appLastZone_moveWindows);
            values.add_property(L"use_cursorpos_editor_startupscreen", expected.use_cursorpos_editor_startupscreen);
            values.add_property(L"fancyzones_zoneHighlightColor", expected.zoneHightlightColor);
            values.add_property(L"fancyzones_highlight_opacity", expected.zoneHighlightOpacity);
            values.add_property(L"fancyzones_editor_hotkey", expected.editorHotkey.get_json());
            values.add_property(L"fancyzones_excluded_apps", expected.excludedApps);

            values.save_to_settings_file();

            auto actual = MakeFancyZonesSettings(m_hInst, m_moduleName);
            Assert::IsTrue(actual != nullptr);

            auto actualSettings = actual->GetSettings();
            compareSettings(expected, actualSettings);
        }

        TEST_METHOD(CreateWithBoolValuesMissed)
        {
            const Settings expected {
                .shiftDrag = m_defaultSettings.shiftDrag,
                .displayChange_moveWindows = m_defaultSettings.displayChange_moveWindows,
                .virtualDesktopChange_moveWindows = m_defaultSettings.virtualDesktopChange_moveWindows,
                .zoneSetChange_flashZones = m_defaultSettings.zoneSetChange_flashZones,
                .zoneSetChange_moveWindows = m_defaultSettings.zoneSetChange_moveWindows,
                .overrideSnapHotkeys = m_defaultSettings.overrideSnapHotkeys,
                .appLastZone_moveWindows = m_defaultSettings.appLastZone_moveWindows,
                .use_cursorpos_editor_startupscreen = m_defaultSettings.use_cursorpos_editor_startupscreen,
                .zoneHightlightColor = L"#00FFD7",
                .zoneHighlightOpacity = 45,
                .editorHotkey = PowerToysSettings::HotkeyObject::from_settings(false, true, true, false, VK_OEM_3),
                .excludedApps = L"app",
                .excludedAppsArray = { L"APP" },
            };

            PowerToysSettings::PowerToyValues values(m_moduleName);
            values.add_property(L"fancyzones_zoneHighlightColor", expected.zoneHightlightColor);
            values.add_property(L"fancyzones_highlight_opacity", expected.zoneHighlightOpacity);
            values.add_property(L"fancyzones_editor_hotkey", expected.editorHotkey.get_json());
            values.add_property(L"fancyzones_excluded_apps", expected.excludedApps);

            values.save_to_settings_file();

            auto actual = MakeFancyZonesSettings(m_hInst, m_moduleName);
            Assert::IsTrue(actual != nullptr);

            auto actualSettings = actual->GetSettings();
            compareSettings(expected, actualSettings);
        }

        TEST_METHOD(CreateColorMissed)
        {
            //prepare data
            const Settings expected {
                .shiftDrag = false,
                .displayChange_moveWindows = true,
                .virtualDesktopChange_moveWindows = true,
                .zoneSetChange_flashZones = true,
                .zoneSetChange_moveWindows = true,
                .overrideSnapHotkeys = false,
                .appLastZone_moveWindows = false,
                .use_cursorpos_editor_startupscreen = true,
                .zoneHightlightColor = m_defaultSettings.zoneHightlightColor,
                .zoneHighlightOpacity = 45,
                .editorHotkey = PowerToysSettings::HotkeyObject::from_settings(false, true, true, false, VK_OEM_3),
                .excludedApps = L"app",
                .excludedAppsArray = { L"APP" },
            };

            PowerToysSettings::PowerToyValues values(m_moduleName);
            values.add_property(L"fancyzones_shiftDrag", expected.shiftDrag);
            values.add_property(L"fancyzones_displayChange_moveWindows", expected.displayChange_moveWindows);
            values.add_property(L"fancyzones_virtualDesktopChange_moveWindows", expected.virtualDesktopChange_moveWindows);
            values.add_property(L"fancyzones_zoneSetChange_flashZones", expected.zoneSetChange_flashZones);
            values.add_property(L"fancyzones_zoneSetChange_moveWindows", expected.zoneSetChange_moveWindows);
            values.add_property(L"fancyzones_overrideSnapHotkeys", expected.overrideSnapHotkeys);
            values.add_property(L"fancyzones_appLastZone_moveWindows", expected.appLastZone_moveWindows);
            values.add_property(L"use_cursorpos_editor_startupscreen", expected.use_cursorpos_editor_startupscreen);
            values.add_property(L"fancyzones_highlight_opacity", expected.zoneHighlightOpacity);
            values.add_property(L"fancyzones_editor_hotkey", expected.editorHotkey.get_json());
            values.add_property(L"fancyzones_excluded_apps", expected.excludedApps);

            values.save_to_settings_file();

            auto actual = MakeFancyZonesSettings(m_hInst, m_moduleName);
            Assert::IsTrue(actual != nullptr);

            auto actualSettings = actual->GetSettings();
            compareSettings(expected, actualSettings);
        }

        TEST_METHOD(CreateOpacityMissed)
        {
            //prepare data
            const Settings expected {
                .shiftDrag = false,
                .displayChange_moveWindows = true,
                .virtualDesktopChange_moveWindows = true,
                .zoneSetChange_flashZones = true,
                .zoneSetChange_moveWindows = true,
                .overrideSnapHotkeys = false,
                .appLastZone_moveWindows = false,
                .use_cursorpos_editor_startupscreen = true,
                .zoneHightlightColor = L"#00FFD7",
                .zoneHighlightOpacity = m_defaultSettings.zoneHighlightOpacity,
                .editorHotkey = PowerToysSettings::HotkeyObject::from_settings(false, true, true, false, VK_OEM_3),
                .excludedApps = L"app",
                .excludedAppsArray = { L"APP" },
            };

            PowerToysSettings::PowerToyValues values(m_moduleName);
            values.add_property(L"fancyzones_shiftDrag", expected.shiftDrag);
            values.add_property(L"fancyzones_displayChange_moveWindows", expected.displayChange_moveWindows);
            values.add_property(L"fancyzones_virtualDesktopChange_moveWindows", expected.virtualDesktopChange_moveWindows);
            values.add_property(L"fancyzones_zoneSetChange_flashZones", expected.zoneSetChange_flashZones);
            values.add_property(L"fancyzones_zoneSetChange_moveWindows", expected.zoneSetChange_moveWindows);
            values.add_property(L"fancyzones_overrideSnapHotkeys", expected.overrideSnapHotkeys);
            values.add_property(L"fancyzones_appLastZone_moveWindows", expected.appLastZone_moveWindows);
            values.add_property(L"use_cursorpos_editor_startupscreen", expected.use_cursorpos_editor_startupscreen);
            values.add_property(L"fancyzones_zoneHighlightColor", expected.zoneHightlightColor);
            values.add_property(L"fancyzones_editor_hotkey", expected.editorHotkey.get_json());
            values.add_property(L"fancyzones_excluded_apps", expected.excludedApps);

            values.save_to_settings_file();

            auto actual = MakeFancyZonesSettings(m_hInst, m_moduleName);
            Assert::IsTrue(actual != nullptr);

            auto actualSettings = actual->GetSettings();
            compareSettings(expected, actualSettings);
        }

        TEST_METHOD(CreateHotkeyMissed)
        {
            //prepare data
            const Settings expected = Settings{
                .shiftDrag = false,
                .displayChange_moveWindows = true,
                .virtualDesktopChange_moveWindows = true,
                .zoneSetChange_flashZones = true,
                .zoneSetChange_moveWindows = true,
                .overrideSnapHotkeys = false,
                .appLastZone_moveWindows = false,
                .use_cursorpos_editor_startupscreen = true,
                .zoneHightlightColor = L"#00FFD7",
                .zoneHighlightOpacity = 45,
                .editorHotkey = m_defaultSettings.editorHotkey,
                .excludedApps = L"app",
                .excludedAppsArray = { L"APP" },
            };

            PowerToysSettings::PowerToyValues values(m_moduleName);
            values.add_property(L"fancyzones_shiftDrag", expected.shiftDrag);
            values.add_property(L"fancyzones_displayChange_moveWindows", expected.displayChange_moveWindows);
            values.add_property(L"fancyzones_virtualDesktopChange_moveWindows", expected.virtualDesktopChange_moveWindows);
            values.add_property(L"fancyzones_zoneSetChange_flashZones", expected.zoneSetChange_flashZones);
            values.add_property(L"fancyzones_zoneSetChange_moveWindows", expected.zoneSetChange_moveWindows);
            values.add_property(L"fancyzones_overrideSnapHotkeys", expected.overrideSnapHotkeys);
            values.add_property(L"fancyzones_appLastZone_moveWindows", expected.appLastZone_moveWindows);
            values.add_property(L"use_cursorpos_editor_startupscreen", expected.use_cursorpos_editor_startupscreen);
            values.add_property(L"fancyzones_zoneHighlightColor", expected.zoneHightlightColor);
            values.add_property(L"fancyzones_highlight_opacity", expected.zoneHighlightOpacity);
            values.add_property(L"fancyzones_excluded_apps", expected.excludedApps);

            values.save_to_settings_file();

            auto actual = MakeFancyZonesSettings(m_hInst, m_moduleName);
            Assert::IsTrue(actual != nullptr);

            auto actualSettings = actual->GetSettings();
            compareSettings(expected, actualSettings);
        }

        TEST_METHOD(CreateAppsMissed)
        {
            //prepare data
            const Settings expected = Settings{
                .shiftDrag = false,
                .displayChange_moveWindows = true,
                .virtualDesktopChange_moveWindows = true,
                .zoneSetChange_flashZones = true,
                .zoneSetChange_moveWindows = true,
                .overrideSnapHotkeys = false,
                .appLastZone_moveWindows = false,
                .use_cursorpos_editor_startupscreen = true,
                .zoneHightlightColor = L"#00FFD7",
                .zoneHighlightOpacity = 45,
                .editorHotkey = PowerToysSettings::HotkeyObject::from_settings(false, true, true, false, VK_OEM_3),
                .excludedApps = m_defaultSettings.excludedApps,
                .excludedAppsArray = m_defaultSettings.excludedAppsArray,
            };

            PowerToysSettings::PowerToyValues values(m_moduleName);
            values.add_property(L"fancyzones_shiftDrag", expected.shiftDrag);
            values.add_property(L"fancyzones_displayChange_moveWindows", expected.displayChange_moveWindows);
            values.add_property(L"fancyzones_virtualDesktopChange_moveWindows", expected.virtualDesktopChange_moveWindows);
            values.add_property(L"fancyzones_zoneSetChange_flashZones", expected.zoneSetChange_flashZones);
            values.add_property(L"fancyzones_zoneSetChange_moveWindows", expected.zoneSetChange_moveWindows);
            values.add_property(L"fancyzones_overrideSnapHotkeys", expected.overrideSnapHotkeys);
            values.add_property(L"fancyzones_appLastZone_moveWindows", expected.appLastZone_moveWindows);
            values.add_property(L"use_cursorpos_editor_startupscreen", expected.use_cursorpos_editor_startupscreen);
            values.add_property(L"fancyzones_zoneHighlightColor", expected.zoneHightlightColor);
            values.add_property(L"fancyzones_highlight_opacity", expected.zoneHighlightOpacity);
            values.add_property(L"fancyzones_editor_hotkey", expected.editorHotkey.get_json());

            values.save_to_settings_file();

            auto actual = MakeFancyZonesSettings(m_hInst, m_moduleName);
            Assert::IsTrue(actual != nullptr);

            auto actualSettings = actual->GetSettings();
            compareSettings(expected, actualSettings);
        }

        TEST_METHOD(CreateWithEmptyJson)
        {
            json::to_file(m_tmpName, json::JsonObject());
            auto actual = MakeFancyZonesSettings(m_hInst, m_moduleName);
            Assert::IsTrue(actual != nullptr);

            auto actualSettings = actual->GetSettings();
            compareSettings(m_defaultSettings, actualSettings);
        }
    };

    TEST_CLASS(FancyZonesSettingsCallbackUnitTests)
    {
        winrt::com_ptr<IFancyZonesSettings> m_settings = nullptr;
        PCWSTR m_moduleName = L"FancyZonesTest";

        struct FZCallback : public winrt::implements<FZCallback, IFancyZonesCallback>
        {
        public:
            FZCallback(bool* callFlag) :
                m_callFlag(callFlag)
            {
                *m_callFlag = false;
            }

            IFACEMETHODIMP_(bool) InMoveSize() noexcept { return false; }
            IFACEMETHODIMP_(void) MoveSizeStart(HWND window, HMONITOR monitor, POINT const& ptScreen) noexcept {}
            IFACEMETHODIMP_(void) MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen) noexcept {}
            IFACEMETHODIMP_(void) MoveSizeEnd(HWND window, POINT const& ptScreen) noexcept {}
            IFACEMETHODIMP_(void) VirtualDesktopChanged() noexcept {}
            IFACEMETHODIMP_(void) VirtualDesktopInitialize() noexcept {}
            IFACEMETHODIMP_(void) WindowCreated(HWND window) noexcept {}
            IFACEMETHODIMP_(bool) OnKeyDown(PKBDLLHOOKSTRUCT info) noexcept { return false; }

            IFACEMETHODIMP_(void) ToggleEditor() noexcept
            {
                Assert::IsNotNull(m_callFlag);
                *m_callFlag = true;
            }

            IFACEMETHODIMP_(void) SettingsChanged() noexcept
            {
                Assert::IsNotNull(m_callFlag);
                *m_callFlag = true;
            }

        private:
            bool* m_callFlag = nullptr;
        };

        TEST_METHOD_INITIALIZE(Init)
        {
            HINSTANCE hInst = (HINSTANCE)GetModuleHandleW(nullptr);
            const Settings expected{
                .shiftDrag = false,
                .displayChange_moveWindows = true,
                .virtualDesktopChange_moveWindows = true,
                .zoneSetChange_flashZones = true,
                .zoneSetChange_moveWindows = true,
                .overrideSnapHotkeys = false,
                .appLastZone_moveWindows = false,
                .use_cursorpos_editor_startupscreen = true,
                .zoneHightlightColor = L"#00FFD7",
                .zoneHighlightOpacity = 45,
                .editorHotkey = PowerToysSettings::HotkeyObject::from_settings(false, true, true, false, VK_OEM_3),
                .excludedApps = L"app",
                .excludedAppsArray = { L"APP" },
            };

            PowerToysSettings::PowerToyValues values(m_moduleName);
            values.add_property(L"fancyzones_shiftDrag", expected.shiftDrag);
            values.add_property(L"fancyzones_displayChange_moveWindows", expected.displayChange_moveWindows);
            values.add_property(L"fancyzones_virtualDesktopChange_moveWindows", expected.virtualDesktopChange_moveWindows);
            values.add_property(L"fancyzones_zoneSetChange_flashZones", expected.zoneSetChange_flashZones);
            values.add_property(L"fancyzones_zoneSetChange_moveWindows", expected.zoneSetChange_moveWindows);
            values.add_property(L"fancyzones_overrideSnapHotkeys", expected.overrideSnapHotkeys);
            values.add_property(L"fancyzones_appLastZone_moveWindows", expected.appLastZone_moveWindows);
            values.add_property(L"use_cursorpos_editor_startupscreen", expected.use_cursorpos_editor_startupscreen);
            values.add_property(L"fancyzones_zoneHighlightColor", expected.zoneHightlightColor);
            values.add_property(L"fancyzones_highlight_opacity", expected.zoneHighlightOpacity);
            values.add_property(L"fancyzones_editor_hotkey", expected.editorHotkey.get_json());
            values.add_property(L"fancyzones_excluded_apps", expected.excludedApps);

            values.save_to_settings_file();

            m_settings = MakeFancyZonesSettings(hInst, m_moduleName);
            Assert::IsTrue(m_settings != nullptr);
        }

        TEST_METHOD_CLEANUP(Cleanup)
        {
            const auto settingsFile = PTSettingsHelper::get_module_save_folder_location(m_moduleName) + L"\\settings.json";
            std::filesystem::remove(settingsFile);
        }

        TEST_METHOD(CallbackSetConfig)
        {
            bool flag = false;
            FZCallback callback(&flag);

            json::JsonObject json{};
            json.SetNamedValue(L"name", json::JsonValue::CreateStringValue(L"name"));

            m_settings->SetCallback(&callback);
            m_settings->SetConfig(json.Stringify().c_str());

            Assert::IsTrue(flag);
        }

        TEST_METHOD(CallbackCallCustomAction)
        {
            bool flag = false;
            FZCallback callback(&flag);

            json::JsonObject action{};
            action.SetNamedValue(L"action_name", json::JsonValue::CreateStringValue(L"ToggledFZEditor"));

            m_settings->SetCallback(&callback);
            m_settings->CallCustomAction(action.Stringify().c_str());

            Assert::IsTrue(flag);
        }

        TEST_METHOD(CallbackCallCustomActionNotToggle)
        {
            bool flag = false;
            FZCallback callback(&flag);

            json::JsonObject action{};
            action.SetNamedValue(L"action_name", json::JsonValue::CreateStringValue(L"NOT_ToggledFZEditor"));

            m_settings->SetCallback(&callback);
            m_settings->CallCustomAction(action.Stringify().c_str());

            Assert::IsFalse(flag);
        }

        TEST_METHOD(CallbackGetConfig)
        {
            bool flag = false;
            FZCallback callback(&flag);

            m_settings->SetCallback(&callback);

            int bufSize = 0;
            m_settings->GetConfig(L"", &bufSize);

            Assert::IsFalse(flag);
        }

        TEST_METHOD(CallbackGetSettings)
        {
            bool flag = false;
            FZCallback callback(&flag);

            m_settings->SetCallback(&callback);
            m_settings->GetSettings();

            Assert::IsFalse(flag);
        }
    };

    TEST_CLASS(FancyZonesSettingsUnitTests)
    {
        winrt::com_ptr<IFancyZonesSettings> m_settings = nullptr;
        PowerToysSettings::Settings* m_ptSettings = nullptr;
        PCWSTR m_moduleName = L"FancyZonesTest";

        std::wstring serializedPowerToySettings(const Settings& settings)
        {
            PowerToysSettings::Settings ptSettings(HINSTANCE{}, m_moduleName);
            ptSettings.set_description(IDS_SETTING_DESCRIPTION);
            ptSettings.set_icon_key(L"pt-fancy-zones");
            ptSettings.set_overview_link(L"https://github.com/microsoft/PowerToys/blob/master/src/modules/fancyzones/README.md");
            ptSettings.set_video_link(L"https://youtu.be/rTtGzZYAXgY");

            ptSettings.add_custom_action(
                L"ToggledFZEditor", // action name.
                IDS_SETTING_LAUNCH_EDITOR_LABEL,
                IDS_SETTING_LAUNCH_EDITOR_BUTTON,
                IDS_SETTING_LAUNCH_EDITOR_DESCRIPTION);
            ptSettings.add_hotkey(L"fancyzones_editor_hotkey", IDS_SETTING_LAUNCH_EDITOR_HOTKEY_LABEL, settings.editorHotkey);
            ptSettings.add_bool_toogle(L"fancyzones_shiftDrag", IDS_SETTING_DESCRIPTION_SHIFTDRAG, settings.shiftDrag);
            ptSettings.add_bool_toogle(L"fancyzones_overrideSnapHotkeys", IDS_SETTING_DESCRIPTION_OVERRIDE_SNAP_HOTKEYS, settings.overrideSnapHotkeys);
            ptSettings.add_bool_toogle(L"fancyzones_zoneSetChange_flashZones", IDS_SETTING_DESCRIPTION_ZONESETCHANGE_FLASHZONES, settings.zoneSetChange_flashZones);
            ptSettings.add_bool_toogle(L"fancyzones_displayChange_moveWindows", IDS_SETTING_DESCRIPTION_DISPLAYCHANGE_MOVEWINDOWS, settings.displayChange_moveWindows);
            ptSettings.add_bool_toogle(L"fancyzones_zoneSetChange_moveWindows", IDS_SETTING_DESCRIPTION_ZONESETCHANGE_MOVEWINDOWS, settings.zoneSetChange_moveWindows);
            ptSettings.add_bool_toogle(L"fancyzones_virtualDesktopChange_moveWindows", IDS_SETTING_DESCRIPTION_VIRTUALDESKTOPCHANGE_MOVEWINDOWS, settings.virtualDesktopChange_moveWindows);
            ptSettings.add_bool_toogle(L"fancyzones_appLastZone_moveWindows", IDS_SETTING_DESCRIPTION_APPLASTZONE_MOVEWINDOWS, settings.appLastZone_moveWindows);
            ptSettings.add_bool_toogle(L"use_cursorpos_editor_startupscreen", IDS_SETTING_DESCRIPTION_USE_CURSORPOS_EDITOR_STARTUPSCREEN, settings.use_cursorpos_editor_startupscreen);
            ptSettings.add_int_spinner(L"fancyzones_highlight_opacity", IDS_SETTINGS_HIGHLIGHT_OPACITY, settings.zoneHighlightOpacity, 0, 100, 1);
            ptSettings.add_color_picker(L"fancyzones_zoneHighlightColor", IDS_SETTING_DESCRIPTION_ZONEHIGHLIGHTCOLOR, settings.zoneHightlightColor);
            ptSettings.add_multiline_string(L"fancyzones_excluded_apps", IDS_SETTING_EXCLCUDED_APPS_DESCRIPTION, settings.excludedApps);

            return ptSettings.serialize();
        }

        TEST_METHOD_INITIALIZE(Init)
        {
            HINSTANCE hInst = (HINSTANCE)GetModuleHandleW(nullptr);

            //init m_settings
            const Settings expected{
                .shiftDrag = false,
                .displayChange_moveWindows = true,
                .virtualDesktopChange_moveWindows = true,
                .zoneSetChange_flashZones = true,
                .zoneSetChange_moveWindows = true,
                .overrideSnapHotkeys = false,
                .appLastZone_moveWindows = false,
                .use_cursorpos_editor_startupscreen = true,
                .zoneHightlightColor = L"#00FFD7",
                .zoneHighlightOpacity = 45,
                .editorHotkey = PowerToysSettings::HotkeyObject::from_settings(false, true, true, false, VK_OEM_3),
                .excludedApps = L"app",
                .excludedAppsArray = { L"APP" },
            };

            PowerToysSettings::PowerToyValues values(m_moduleName);
            values.add_property(L"fancyzones_shiftDrag", expected.shiftDrag);
            values.add_property(L"fancyzones_displayChange_moveWindows", expected.displayChange_moveWindows);
            values.add_property(L"fancyzones_virtualDesktopChange_moveWindows", expected.virtualDesktopChange_moveWindows);
            values.add_property(L"fancyzones_zoneSetChange_flashZones", expected.zoneSetChange_flashZones);
            values.add_property(L"fancyzones_zoneSetChange_moveWindows", expected.zoneSetChange_moveWindows);
            values.add_property(L"fancyzones_overrideSnapHotkeys", expected.overrideSnapHotkeys);
            values.add_property(L"fancyzones_appLastZone_moveWindows", expected.appLastZone_moveWindows);
            values.add_property(L"use_cursorpos_editor_startupscreen", expected.use_cursorpos_editor_startupscreen);
            values.add_property(L"fancyzones_zoneHighlightColor", expected.zoneHightlightColor);
            values.add_property(L"fancyzones_highlight_opacity", expected.zoneHighlightOpacity);
            values.add_property(L"fancyzones_editor_hotkey", expected.editorHotkey.get_json());
            values.add_property(L"fancyzones_excluded_apps", expected.excludedApps);

            values.save_to_settings_file();

            m_settings = MakeFancyZonesSettings(hInst, m_moduleName);
            Assert::IsTrue(m_settings != nullptr);

            //init m_ptSettings
            m_ptSettings = new PowerToysSettings::Settings(hInst, m_moduleName);
            m_ptSettings->set_description(IDS_SETTING_DESCRIPTION);
            m_ptSettings->set_icon_key(L"pt-fancy-zones");
            m_ptSettings->set_overview_link(L"https://github.com/microsoft/PowerToys/blob/master/src/modules/fancyzones/README.md");
            m_ptSettings->set_video_link(L"https://youtu.be/rTtGzZYAXgY");

            m_ptSettings->add_custom_action(
                L"ToggledFZEditor", // action name.
                IDS_SETTING_LAUNCH_EDITOR_LABEL,
                IDS_SETTING_LAUNCH_EDITOR_BUTTON,
                IDS_SETTING_LAUNCH_EDITOR_DESCRIPTION);
            m_ptSettings->add_hotkey(L"fancyzones_editor_hotkey", IDS_SETTING_LAUNCH_EDITOR_HOTKEY_LABEL, expected.editorHotkey);
            m_ptSettings->add_bool_toogle(L"fancyzones_shiftDrag", IDS_SETTING_DESCRIPTION_SHIFTDRAG, expected.shiftDrag);
            m_ptSettings->add_bool_toogle(L"fancyzones_overrideSnapHotkeys", IDS_SETTING_DESCRIPTION_OVERRIDE_SNAP_HOTKEYS, expected.overrideSnapHotkeys);
            m_ptSettings->add_bool_toogle(L"fancyzones_zoneSetChange_flashZones", IDS_SETTING_DESCRIPTION_ZONESETCHANGE_FLASHZONES, expected.zoneSetChange_flashZones);
            m_ptSettings->add_bool_toogle(L"fancyzones_displayChange_moveWindows", IDS_SETTING_DESCRIPTION_DISPLAYCHANGE_MOVEWINDOWS, expected.displayChange_moveWindows);
            m_ptSettings->add_bool_toogle(L"fancyzones_zoneSetChange_moveWindows", IDS_SETTING_DESCRIPTION_ZONESETCHANGE_MOVEWINDOWS, expected.zoneSetChange_moveWindows);
            m_ptSettings->add_bool_toogle(L"fancyzones_virtualDesktopChange_moveWindows", IDS_SETTING_DESCRIPTION_VIRTUALDESKTOPCHANGE_MOVEWINDOWS, expected.virtualDesktopChange_moveWindows);
            m_ptSettings->add_bool_toogle(L"fancyzones_appLastZone_moveWindows", IDS_SETTING_DESCRIPTION_APPLASTZONE_MOVEWINDOWS, expected.appLastZone_moveWindows);
            m_ptSettings->add_bool_toogle(L"use_cursorpos_editor_startupscreen", IDS_SETTING_DESCRIPTION_USE_CURSORPOS_EDITOR_STARTUPSCREEN, expected.use_cursorpos_editor_startupscreen);
            m_ptSettings->add_int_spinner(L"fancyzones_highlight_opacity", IDS_SETTINGS_HIGHLIGHT_OPACITY, expected.zoneHighlightOpacity, 0, 100, 1);
            m_ptSettings->add_color_picker(L"fancyzones_zoneHighlightColor", IDS_SETTING_DESCRIPTION_ZONEHIGHLIGHTCOLOR, expected.zoneHightlightColor);
            m_ptSettings->add_multiline_string(L"fancyzones_excluded_apps", IDS_SETTING_EXCLCUDED_APPS_DESCRIPTION, expected.excludedApps);
        }

        TEST_METHOD_CLEANUP(Cleanup)
        {
            const auto settingsFile = PTSettingsHelper::get_module_save_folder_location(m_moduleName) + L"\\settings.json";
            std::filesystem::remove(settingsFile);
        }

        TEST_METHOD(GetConfig)
        {
            const int expectedSize = static_cast<int>(m_ptSettings->serialize().size()) + 1;

            int actualBufferSize = expectedSize;
            PWSTR actualBuffer = new wchar_t[actualBufferSize];

            Assert::IsTrue(m_settings->GetConfig(actualBuffer, &actualBufferSize));
            Assert::AreEqual(expectedSize, actualBufferSize);

            Assert::AreEqual(m_ptSettings->serialize().c_str(), actualBuffer);
        }

        TEST_METHOD(GetConfigSmallBuffer)
        {
            const auto serialized = m_ptSettings->serialize();
            const int size = static_cast<int>(serialized.size());
            const int expectedSize = size + 1;

            int actualBufferSize = size - 1;
            PWSTR actualBuffer = new wchar_t[actualBufferSize];

            Assert::IsFalse(m_settings->GetConfig(actualBuffer, &actualBufferSize));
            Assert::AreEqual(expectedSize, actualBufferSize);
            Assert::AreNotEqual(serialized.c_str(), actualBuffer);
        }

        TEST_METHOD(GetConfigNullBuffer)
        {
            const auto serialized = m_ptSettings->serialize();
            const int expectedSize = static_cast<int>(serialized.size()) + 1;

            int actualBufferSize = 0;

            Assert::IsFalse(m_settings->GetConfig(nullptr, &actualBufferSize));
            Assert::AreEqual(expectedSize, actualBufferSize);
        }

        TEST_METHOD(SetConfig)
        {
            //cleanup file before call set config
            const auto settingsFile = PTSettingsHelper::get_module_save_folder_location(m_moduleName) + L"\\settings.json";
            std::filesystem::remove(settingsFile);

            const Settings expected {
                .shiftDrag = true,
                .displayChange_moveWindows = true,
                .virtualDesktopChange_moveWindows = true,
                .zoneSetChange_flashZones = false,
                .zoneSetChange_moveWindows = true,
                .overrideSnapHotkeys = false,
                .appLastZone_moveWindows = true,
                .use_cursorpos_editor_startupscreen = true,
                .zoneHightlightColor = L"#00AABB",
                .zoneHighlightOpacity = 45,
                .editorHotkey = PowerToysSettings::HotkeyObject::from_settings(false, false, false, false, VK_OEM_3),
                .excludedApps = L"app\r\napp2",
                .excludedAppsArray = { L"APP", L"APP2" },
            };

            auto config = serializedPowerToySettings(expected);
            m_settings->SetConfig(config.c_str());

            auto actual = m_settings->GetSettings();
            compareSettings(expected, actual);

            Assert::IsTrue(std::filesystem::exists(settingsFile));
        }
    };
}