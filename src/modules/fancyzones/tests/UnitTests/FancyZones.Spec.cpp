#include "pch.h"

#include <filesystem>

#include <lib/FancyZones.h>
#include <lib/Settings.h>
#include <common/common.h>

#include "util.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    TEST_CLASS(FancyZonesUnitTests)
    {
        HINSTANCE m_hInst;
        winrt::com_ptr<IFancyZonesSettings> m_settings;

        TEST_METHOD_INITIALIZE(Init)
        {
            m_hInst = (HINSTANCE)GetModuleHandleW(nullptr);
            m_settings = MakeFancyZonesSettings(m_hInst, L"FancyZonesUnitTests");
            Assert::IsTrue(m_settings != nullptr);
        }

        TEST_METHOD(Create)
        {
            auto actual = MakeFancyZones(m_hInst, m_settings);
            Assert::IsNotNull(actual.get());
        }
        TEST_METHOD(CreateWithEmptyHinstance)
        {
            auto actual = MakeFancyZones({}, m_settings);
            Assert::IsNotNull(actual.get());
        }

        TEST_METHOD(CreateWithNullHinstance)
        {
            auto actual = MakeFancyZones(nullptr, m_settings);
            Assert::IsNotNull(actual.get());
        }

        TEST_METHOD(CreateWithNullSettings)
        {
            auto actual = MakeFancyZones(m_hInst, nullptr);
            Assert::IsNull(actual.get());
        }

        TEST_METHOD(Run)
        {
            auto actual = MakeFancyZones(m_hInst, m_settings);

            std::vector<std::thread> threads;
            std::atomic<int> counter = 0;
            const int expectedCount = 10;

            auto runFunc = [&]() {
                actual->Run();
                counter++;
            };

            for (int i = 0; i < expectedCount; i++)
            {
                threads.push_back(std::thread(runFunc));
            }

            for (auto& thread : threads)
            {
                thread.join();
            }

            Assert::AreEqual(expectedCount, counter.load());
        }

        TEST_METHOD(Destroy)
        {
            auto actual = MakeFancyZones(m_hInst, m_settings);

            std::vector<std::thread> threads;
            std::atomic<int> counter = 0;
            const int expectedCount = 10;

            auto destroyFunc = [&]() {
                actual->Destroy();
                counter++;
            };

            for (int i = 0; i < expectedCount; i++)
            {
                threads.push_back(std::thread(destroyFunc));
            }

            for (auto& thread : threads)
            {
                thread.join();
            }

            Assert::AreEqual(expectedCount, counter.load());
        }

        /*
        TEST_METHOD(RunDestroy)
        {
            auto actual = MakeFancyZones(m_hInst, m_settings);

            std::vector<std::thread> threads;
            std::atomic<int> counter = 0;
            const int expectedCount = 20;

            auto func = [&]() {
                auto idHash = std::hash<std::thread::id>()(std::this_thread::get_id());
                bool run = (idHash % 2 == 0);
                run ? actual->Run() : actual->Destroy();
                counter++;
            };

            for (int i = 0; i < expectedCount; i++)
            {
                threads.push_back(std::thread(func));
            }

            for (auto& thread : threads)
            {
                thread.join();
            }

            Assert::AreEqual(expectedCount, counter.load());
        }
        */
    };

    TEST_CLASS(FancyZonesIZoneWindowHostUnitTests)
    {
        HINSTANCE m_hInst{};
        std::wstring m_settingsLocation = L"FancyZonesUnitTests";
        winrt::com_ptr<IFancyZonesSettings> m_settings = nullptr;
        winrt::com_ptr<IZoneWindowHost> m_zoneWindowHost = nullptr;

        std::wstring serializedPowerToySettings(const Settings& settings)
        {
            PowerToysSettings::Settings ptSettings(HINSTANCE{}, L"FancyZonesUnitTests");

            ptSettings.add_hotkey(L"fancyzones_editor_hotkey", IDS_SETTING_LAUNCH_EDITOR_HOTKEY_LABEL, settings.editorHotkey);
            ptSettings.add_bool_toogle(L"fancyzones_shiftDrag", IDS_SETTING_DESCRIPTION_SHIFTDRAG, settings.shiftDrag);
            ptSettings.add_bool_toogle(L"fancyzones_overrideSnapHotkeys", IDS_SETTING_DESCRIPTION_OVERRIDE_SNAP_HOTKEYS, settings.overrideSnapHotkeys);
            ptSettings.add_bool_toogle(L"fancyzones_zoneSetChange_flashZones", IDS_SETTING_DESCRIPTION_ZONESETCHANGE_FLASHZONES, settings.zoneSetChange_flashZones);
            ptSettings.add_bool_toogle(L"fancyzones_displayChange_moveWindows", IDS_SETTING_DESCRIPTION_DISPLAYCHANGE_MOVEWINDOWS, settings.displayChange_moveWindows);
            ptSettings.add_bool_toogle(L"fancyzones_zoneSetChange_moveWindows", IDS_SETTING_DESCRIPTION_ZONESETCHANGE_MOVEWINDOWS, settings.zoneSetChange_moveWindows);
            ptSettings.add_bool_toogle(L"fancyzones_virtualDesktopChange_moveWindows", IDS_SETTING_DESCRIPTION_VIRTUALDESKTOPCHANGE_MOVEWINDOWS, settings.virtualDesktopChange_moveWindows);
            ptSettings.add_bool_toogle(L"fancyzones_appLastZone_moveWindows", IDS_SETTING_DESCRIPTION_APPLASTZONE_MOVEWINDOWS, settings.appLastZone_moveWindows);
            ptSettings.add_bool_toogle(L"use_cursorpos_editor_startupscreen", IDS_SETTING_DESCRIPTION_USE_CURSORPOS_EDITOR_STARTUPSCREEN, settings.use_cursorpos_editor_startupscreen);
            ptSettings.add_bool_toogle(L"fancyzones_show_on_all_monitors", IDS_SETTING_DESCRIPTION_SHOW_FANCY_ZONES_ON_ALL_MONITORS, settings.showZonesOnAllMonitors);
            ptSettings.add_int_spinner(L"fancyzones_highlight_opacity", IDS_SETTINGS_HIGHLIGHT_OPACITY, settings.zoneHighlightOpacity, 0, 100, 1);
            ptSettings.add_color_picker(L"fancyzones_zoneHighlightColor", IDS_SETTING_DESCRIPTION_ZONEHIGHLIGHTCOLOR, settings.zoneHightlightColor);
            ptSettings.add_multiline_string(L"fancyzones_excluded_apps", IDS_SETTING_EXCLCUDED_APPS_DESCRIPTION, settings.excludedApps);

            return ptSettings.serialize();
        }

        TEST_METHOD_INITIALIZE(Init)
        {
            m_hInst = (HINSTANCE)GetModuleHandleW(nullptr);
            m_settings = MakeFancyZonesSettings(m_hInst, m_settingsLocation.c_str());
            Assert::IsTrue(m_settings != nullptr);

            auto fancyZones = MakeFancyZones(m_hInst, m_settings);
            Assert::IsTrue(fancyZones != nullptr);

            m_zoneWindowHost = fancyZones.as<IZoneWindowHost>();
            Assert::IsTrue(m_zoneWindowHost != nullptr);
        }

        TEST_METHOD_CLEANUP(Cleanup)
        {
            auto settingsFolder = PTSettingsHelper::get_module_save_folder_location(m_settingsLocation);
            const auto settingsFile = settingsFolder + L"\\settings.json";
            std::filesystem::remove(settingsFile);
            std::filesystem::remove(settingsFolder);
        }

        TEST_METHOD(GetZoneHighlightColor)
        {
            const auto expected = RGB(171, 175, 238);
            const Settings settings{
                .shiftDrag = true,
                .displayChange_moveWindows = true,
                .virtualDesktopChange_moveWindows = true,
                .zoneSetChange_flashZones = false,
                .zoneSetChange_moveWindows = true,
                .overrideSnapHotkeys = false,
                .appLastZone_moveWindows = true,
                .use_cursorpos_editor_startupscreen = true,
                .showZonesOnAllMonitors = false,
                .zoneHightlightColor = L"#abafee",
                .zoneHighlightOpacity = 45,
                .editorHotkey = PowerToysSettings::HotkeyObject::from_settings(false, false, false, false, VK_OEM_3),
                .excludedApps = L"app\r\napp2",
                .excludedAppsArray = { L"APP", L"APP2" },
            };

            auto config = serializedPowerToySettings(settings);
            m_settings->SetConfig(config.c_str());

            const auto actual = m_zoneWindowHost->GetZoneHighlightColor();
            Assert::AreEqual(expected, actual);
        }

        TEST_METHOD(GetZoneHighlightOpacity)
        {
            const auto expected = 88;
            const Settings settings{
                .shiftDrag = true,
                .displayChange_moveWindows = true,
                .virtualDesktopChange_moveWindows = true,
                .zoneSetChange_flashZones = false,
                .zoneSetChange_moveWindows = true,
                .overrideSnapHotkeys = false,
                .appLastZone_moveWindows = true,
                .use_cursorpos_editor_startupscreen = true,
                .showZonesOnAllMonitors = false,
                .zoneHightlightColor = L"#abafee",
                .zoneHighlightOpacity = expected,
                .editorHotkey = PowerToysSettings::HotkeyObject::from_settings(false, false, false, false, VK_OEM_3),
                .excludedApps = L"app\r\napp2",
                .excludedAppsArray = { L"APP", L"APP2" },
            };

            auto config = serializedPowerToySettings(settings);
            m_settings->SetConfig(config.c_str());

            const auto actual = m_zoneWindowHost->GetZoneHighlightOpacity();
            Assert::AreEqual(expected, actual);
        }

        TEST_METHOD(GetCurrentMonitorZoneSetEmpty)
        {
            const auto* actual = m_zoneWindowHost->GetParentZoneWindow(Mocks::Monitor());
            Assert::IsNull(actual);
        }

        TEST_METHOD(GetCurrentMonitorZoneSetNullMonitor)
        {
            const auto* actual = m_zoneWindowHost->GetParentZoneWindow(nullptr);
            Assert::IsNull(actual);
        }
    };

    TEST_CLASS(FancyZonesIFancyZonesCallbackUnitTests)
    {
        HINSTANCE m_hInst{};
        std::wstring m_settingsLocation = L"FancyZonesUnitTests";
        winrt::com_ptr<IFancyZonesSettings> m_settings = nullptr;
        winrt::com_ptr<IFancyZonesCallback> m_fzCallback = nullptr;

        JSONHelpers::FancyZonesData& m_fancyZonesData = JSONHelpers::FancyZonesDataInstance();

        std::wstring serializedPowerToySettings(const Settings& settings)
        {
            PowerToysSettings::Settings ptSettings(HINSTANCE{}, L"FancyZonesUnitTests");

            ptSettings.add_hotkey(L"fancyzones_editor_hotkey", IDS_SETTING_LAUNCH_EDITOR_HOTKEY_LABEL, settings.editorHotkey);
            ptSettings.add_bool_toogle(L"fancyzones_shiftDrag", IDS_SETTING_DESCRIPTION_SHIFTDRAG, settings.shiftDrag);
            ptSettings.add_bool_toogle(L"fancyzones_overrideSnapHotkeys", IDS_SETTING_DESCRIPTION_OVERRIDE_SNAP_HOTKEYS, settings.overrideSnapHotkeys);
            ptSettings.add_bool_toogle(L"fancyzones_zoneSetChange_flashZones", IDS_SETTING_DESCRIPTION_ZONESETCHANGE_FLASHZONES, settings.zoneSetChange_flashZones);
            ptSettings.add_bool_toogle(L"fancyzones_displayChange_moveWindows", IDS_SETTING_DESCRIPTION_DISPLAYCHANGE_MOVEWINDOWS, settings.displayChange_moveWindows);
            ptSettings.add_bool_toogle(L"fancyzones_zoneSetChange_moveWindows", IDS_SETTING_DESCRIPTION_ZONESETCHANGE_MOVEWINDOWS, settings.zoneSetChange_moveWindows);
            ptSettings.add_bool_toogle(L"fancyzones_virtualDesktopChange_moveWindows", IDS_SETTING_DESCRIPTION_VIRTUALDESKTOPCHANGE_MOVEWINDOWS, settings.virtualDesktopChange_moveWindows);
            ptSettings.add_bool_toogle(L"fancyzones_appLastZone_moveWindows", IDS_SETTING_DESCRIPTION_APPLASTZONE_MOVEWINDOWS, settings.appLastZone_moveWindows);
            ptSettings.add_bool_toogle(L"use_cursorpos_editor_startupscreen", IDS_SETTING_DESCRIPTION_USE_CURSORPOS_EDITOR_STARTUPSCREEN, settings.use_cursorpos_editor_startupscreen);
            ptSettings.add_bool_toogle(L"fancyzones_show_on_all_monitors", IDS_SETTING_DESCRIPTION_SHOW_FANCY_ZONES_ON_ALL_MONITORS, settings.showZonesOnAllMonitors);
            ptSettings.add_int_spinner(L"fancyzones_highlight_opacity", IDS_SETTINGS_HIGHLIGHT_OPACITY, settings.zoneHighlightOpacity, 0, 100, 1);
            ptSettings.add_color_picker(L"fancyzones_zoneHighlightColor", IDS_SETTING_DESCRIPTION_ZONEHIGHLIGHTCOLOR, settings.zoneHightlightColor);
            ptSettings.add_multiline_string(L"fancyzones_excluded_apps", IDS_SETTING_EXCLCUDED_APPS_DESCRIPTION, settings.excludedApps);

            return ptSettings.serialize();
        }

        void sendKeyboardInput(WORD code, bool release = false)
        {
            INPUT ip;
            ip.type = INPUT_KEYBOARD;
            ip.ki.wScan = 0; // hardware scan code for key
            ip.ki.time = 0;
            ip.ki.dwExtraInfo = 0;
            ip.ki.wVk = code;
            ip.ki.dwFlags = release ? KEYEVENTF_KEYUP : 0;
            SendInput(1, &ip, sizeof(INPUT));
        }

        TEST_METHOD_INITIALIZE(Init)
        {
            m_hInst = (HINSTANCE)GetModuleHandleW(nullptr);
            m_settings = MakeFancyZonesSettings(m_hInst, m_settingsLocation.c_str());
            Assert::IsTrue(m_settings != nullptr);

            auto fancyZones = MakeFancyZones(m_hInst, m_settings);
            Assert::IsTrue(fancyZones != nullptr);

            m_fzCallback = fancyZones.as<IFancyZonesCallback>();
            Assert::IsTrue(m_fzCallback != nullptr);

            m_fancyZonesData.clear_data();
        }

        TEST_METHOD_CLEANUP(Cleanup)
        {
            sendKeyboardInput(VK_SHIFT, true);
            sendKeyboardInput(VK_LWIN, true);
            sendKeyboardInput(VK_CONTROL, true);

            auto settingsFolder = PTSettingsHelper::get_module_save_folder_location(m_settingsLocation);
            const auto settingsFile = settingsFolder + L"\\settings.json";
            std::filesystem::remove(settingsFile);
            std::filesystem::remove(settingsFolder);
        }

        TEST_METHOD(OnKeyDownNothingPressed)
        {
            for (DWORD code = '0'; code <= '9'; code++)
            {
                tagKBDLLHOOKSTRUCT input{};
                input.vkCode = code;
                Assert::IsFalse(m_fzCallback->OnKeyDown(&input));
            }

            {
                tagKBDLLHOOKSTRUCT input{};
                input.vkCode = VK_LEFT;
                Assert::IsFalse(m_fzCallback->OnKeyDown(&input));
            }

            {
                tagKBDLLHOOKSTRUCT input{};
                input.vkCode = VK_RIGHT;
                Assert::IsFalse(m_fzCallback->OnKeyDown(&input));
            }
        }

        TEST_METHOD(OnKeyDownShiftPressed)
        {
            sendKeyboardInput(VK_SHIFT);

            for (DWORD code = '0'; code <= '9'; code++)
            {
                tagKBDLLHOOKSTRUCT input{};
                input.vkCode = code;
                Assert::IsFalse(m_fzCallback->OnKeyDown(&input));
            }

            {
                tagKBDLLHOOKSTRUCT input{};
                input.vkCode = VK_LEFT;
                Assert::IsFalse(m_fzCallback->OnKeyDown(&input));
            }

            {
                tagKBDLLHOOKSTRUCT input{};
                input.vkCode = VK_RIGHT;
                Assert::IsFalse(m_fzCallback->OnKeyDown(&input));
            }
        }

        TEST_METHOD(OnKeyDownWinPressed)
        {
            sendKeyboardInput(VK_LWIN);

            for (DWORD code = '0'; code <= '9'; code++)
            {
                tagKBDLLHOOKSTRUCT input{};
                input.vkCode = code;
                Assert::IsFalse(m_fzCallback->OnKeyDown(&input));
            }

            {
                tagKBDLLHOOKSTRUCT input{};
                input.vkCode = VK_LEFT;
                Assert::IsFalse(m_fzCallback->OnKeyDown(&input));
            }

            {
                tagKBDLLHOOKSTRUCT input{};
                input.vkCode = VK_RIGHT;
                Assert::IsFalse(m_fzCallback->OnKeyDown(&input));
            }
        }

        TEST_METHOD(OnKeyDownWinShiftPressed)
        {
            sendKeyboardInput(VK_LWIN);
            sendKeyboardInput(VK_SHIFT);

            for (DWORD code = '0'; code <= '9'; code++)
            {
                tagKBDLLHOOKSTRUCT input{};
                input.vkCode = code;
                Assert::IsFalse(m_fzCallback->OnKeyDown(&input));
            }

            {
                tagKBDLLHOOKSTRUCT input{};
                input.vkCode = VK_LEFT;
                Assert::IsFalse(m_fzCallback->OnKeyDown(&input));
            }

            {
                tagKBDLLHOOKSTRUCT input{};
                input.vkCode = VK_RIGHT;
                Assert::IsFalse(m_fzCallback->OnKeyDown(&input));
            }
        }

        TEST_METHOD(OnKeyDownWinCtrlPressed)
        {
            sendKeyboardInput(VK_LWIN);
            sendKeyboardInput(VK_CONTROL);

            const Settings settings{
                .overrideSnapHotkeys = false,
            };

            auto config = serializedPowerToySettings(settings);
            m_settings->SetConfig(config.c_str());

            for (DWORD code = '0'; code <= '9'; code++)
            {
                tagKBDLLHOOKSTRUCT input{};
                input.vkCode = code;
                Assert::IsTrue(m_fzCallback->OnKeyDown(&input));
            }

            {
                tagKBDLLHOOKSTRUCT input{};
                input.vkCode = VK_LEFT;
                Assert::IsFalse(m_fzCallback->OnKeyDown(&input));
            }

            {
                tagKBDLLHOOKSTRUCT input{};
                input.vkCode = VK_RIGHT;
                Assert::IsFalse(m_fzCallback->OnKeyDown(&input));
            }
        }
    };
}