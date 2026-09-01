// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"

#include "AutoHideCursorState.h"
#include "SystemCursorHider.h"
#include "resource.h"
#include "trace.h"

#include "../../../common/SettingsAPI/settings_objects.h"
#include "../../../common/logger/logger.h"
#include "../../../common/utils/gpo.h"
#include "../../../common/utils/logger_helper.h"
#include "../../../common/utils/process_path.h"
#include "../../../interface/powertoy_module_interface.h"

#include <mutex>
#include <string>
#include <thread>

extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace
{
    constexpr wchar_t moduleName[] = L"AutoHideCursor";
    constexpr wchar_t moduleDescription[] = L"<no description>";
    constexpr wchar_t workerExecutableName[] = L"PowerToys.AutoHideCursor.exe";
    constexpr wchar_t jsonProperties[] = L"properties";
    constexpr wchar_t jsonValue[] = L"value";
    constexpr wchar_t jsonHideOnTyping[] = L"hide_on_typing";
    constexpr wchar_t jsonHideOnIdle[] = L"hide_on_idle";
    constexpr wchar_t jsonIdleDelayMs[] = L"idle_delay_ms";

    class AutoHideCursorModule : public PowertoyModuleIface
    {
    public:
        AutoHideCursorModule()
        {
            LoggerHelpers::init_logger(moduleName, L"ModuleInterface", LogSettings::autoHideCursorLoggerName);
            LoadSettings();
        }

        void destroy() override
        {
            disable();
            delete this;
        }

        const wchar_t* get_name() override
        {
            return moduleName;
        }

        const wchar_t* get_key() override
        {
            return moduleName;
        }

        powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
        {
            return powertoys_gpo::getConfiguredAutoHideCursorEnabledValue();
        }

        bool get_config(wchar_t* buffer, int* bufferSize) override
        {
            const auto settingsSnapshot = GetSettingsSnapshot();
            PowerToysSettings::Settings settings(
                reinterpret_cast<HINSTANCE>(&__ImageBase),
                get_name());
            settings.set_description(IDS_AUTO_HIDE_CURSOR_NAME);
            settings.set_icon_key(L"pt-auto-hide-cursor");
            settings.add_bool_toggle(
                jsonHideOnTyping,
                IDS_AUTO_HIDE_CURSOR_HIDE_ON_TYPING,
                settingsSnapshot.hideOnTyping);
            settings.add_bool_toggle(
                jsonHideOnIdle,
                IDS_AUTO_HIDE_CURSOR_HIDE_ON_IDLE,
                settingsSnapshot.hideOnIdle);
            settings.add_int_spinner(
                jsonIdleDelayMs,
                IDS_AUTO_HIDE_CURSOR_IDLE_DELAY,
                static_cast<int>(settingsSnapshot.idleDelayMs),
                auto_hide_cursor::minimumIdleDelayMs,
                auto_hide_cursor::maximumIdleDelayMs,
                1000);
            return settings.serialize_to_buffer(buffer, bufferSize);
        }

        void call_custom_action(const wchar_t*) override
        {
        }

        void set_config(const wchar_t* config) override
        {
            try
            {
                auto values = PowerToysSettings::PowerToyValues::from_json_string(config, get_key());
                ParseSettings(values);
                if (m_enabled && m_restartEvent)
                {
                    SetEvent(m_restartEvent);
                }
            }
            catch (const std::exception&)
            {
                Logger::error("Invalid JSON when parsing Auto Hide Cursor settings.");
            }
        }

        void enable() override
        {
            if (m_enabled.exchange(true))
            {
                return;
            }

            m_terminateEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
            m_restartEvent = CreateEventW(nullptr, FALSE, FALSE, nullptr);
            if (!m_terminateEvent || !m_restartEvent)
            {
                Logger::error(L"Failed to create Auto Hide Cursor supervisor events. Error: {}", GetLastError());
                CloseSupervisorEvents();
                m_enabled = false;
                return;
            }

            m_supervisorThread = std::thread([this]() { SupervisorLoop(); });
            Trace::EnableAutoHideCursor(true);
        }

        void disable() override
        {
            if (!m_enabled.exchange(false))
            {
                return;
            }

            if (m_terminateEvent)
            {
                SetEvent(m_terminateEvent);
            }

            if (m_supervisorThread.joinable())
            {
                m_supervisorThread.join();
            }

            CloseSupervisorEvents();
            if (!auto_hide_cursor::RestoreSystemCursors())
            {
                Logger::error(L"Failed to restore system cursors while disabling Auto Hide Cursor. Error: {}", GetLastError());
            }

            Trace::EnableAutoHideCursor(false);
        }

        bool is_enabled() override
        {
            return m_enabled;
        }

        bool is_enabled_by_default() const override
        {
            return false;
        }

    private:
        auto_hide_cursor::Configuration GetSettingsSnapshot()
        {
            std::scoped_lock lock{ m_settingsMutex };
            return m_settings;
        }

        void LoadSettings()
        {
            try
            {
                auto settings = PowerToysSettings::PowerToyValues::load_from_settings_file(get_key());
                ParseSettings(settings);
            }
            catch (const std::exception&)
            {
                Logger::error("Invalid JSON when loading Auto Hide Cursor settings.");
            }
        }

        void ParseSettings(PowerToysSettings::PowerToyValues& settings)
        {
            auto updatedSettings = GetSettingsSnapshot();
            const auto root = settings.get_raw_json();
            if (root.GetView().Size() == 0)
            {
                return;
            }

            const auto properties = root.GetNamedObject(jsonProperties);
            if (properties.HasKey(jsonHideOnTyping))
            {
                updatedSettings.hideOnTyping =
                    properties.GetNamedObject(jsonHideOnTyping).GetNamedBoolean(jsonValue);
            }
            if (properties.HasKey(jsonHideOnIdle))
            {
                updatedSettings.hideOnIdle =
                    properties.GetNamedObject(jsonHideOnIdle).GetNamedBoolean(jsonValue);
            }
            if (properties.HasKey(jsonIdleDelayMs))
            {
                const auto idleDelay =
                    properties.GetNamedObject(jsonIdleDelayMs).GetNamedNumber(jsonValue);
                if (idleDelay >= 0 && idleDelay <= UINT32_MAX)
                {
                    updatedSettings.idleDelayMs = static_cast<std::uint32_t>(idleDelay);
                }
            }

            updatedSettings = auto_hide_cursor::State::NormalizeConfiguration(updatedSettings);
            std::scoped_lock lock{ m_settingsMutex };
            m_settings = updatedSettings;
        }

        void SupervisorLoop()
        {
            unsigned int workerGeneration = 0;
            while (m_enabled)
            {
                const auto settingsSnapshot = GetSettingsSnapshot();
                if (!settingsSnapshot.hideOnTyping && !settingsSnapshot.hideOnIdle)
                {
                    const HANDLE events[] = { m_terminateEvent, m_restartEvent };
                    const auto waitResult = WaitForMultipleObjects(
                        static_cast<DWORD>(std::size(events)),
                        events,
                        FALSE,
                        INFINITE);
                    if (waitResult == WAIT_OBJECT_0)
                    {
                        break;
                    }
                    continue;
                }

                const auto stopEventName =
                    L"Local\\PowerToysAutoHideCursorStop-" +
                    std::to_wstring(GetCurrentProcessId()) +
                    L"-" +
                    std::to_wstring(++workerGeneration);
                const auto stopEvent = CreateEventW(nullptr, TRUE, FALSE, stopEventName.c_str());
                if (!stopEvent)
                {
                    Logger::error(L"Failed to create the Auto Hide Cursor worker stop event. Error: {}", GetLastError());
                    if (WaitForSingleObject(m_terminateEvent, 1000) == WAIT_OBJECT_0)
                    {
                        break;
                    }
                    continue;
                }

                PROCESS_INFORMATION processInfo{};
                if (!LaunchWorker(settingsSnapshot, stopEventName, processInfo))
                {
                    CloseHandle(stopEvent);
                    if (WaitForSingleObject(m_terminateEvent, 1000) == WAIT_OBJECT_0)
                    {
                        break;
                    }
                    continue;
                }

                const HANDLE waitHandles[] = { m_terminateEvent, m_restartEvent, processInfo.hProcess };
                const auto waitResult = WaitForMultipleObjects(
                    static_cast<DWORD>(std::size(waitHandles)),
                    waitHandles,
                    FALSE,
                    INFINITE);

                const bool stopping = waitResult == WAIT_OBJECT_0;
                const bool restarting = waitResult == WAIT_OBJECT_0 + 1;
                if (stopping || restarting)
                {
                    SetEvent(stopEvent);
                    if (WaitForSingleObject(processInfo.hProcess, 3000) == WAIT_TIMEOUT)
                    {
                        Logger::warn("Auto Hide Cursor worker did not stop in time; terminating it.");
                        TerminateProcess(processInfo.hProcess, ERROR_CANCELLED);
                        WaitForSingleObject(processInfo.hProcess, 1000);
                    }
                }
                else if (waitResult == WAIT_OBJECT_0 + 2)
                {
                    DWORD exitCode = ERROR_GEN_FAILURE;
                    GetExitCodeProcess(processInfo.hProcess, &exitCode);
                    Logger::warn(L"Auto Hide Cursor worker exited unexpectedly with code {}.", exitCode);
                }
                else
                {
                    Logger::error(L"Failed while waiting for the Auto Hide Cursor worker. Error: {}", GetLastError());
                    SetEvent(stopEvent);
                    if (WaitForSingleObject(processInfo.hProcess, 1000) == WAIT_TIMEOUT)
                    {
                        TerminateProcess(processInfo.hProcess, ERROR_CANCELLED);
                        WaitForSingleObject(processInfo.hProcess, 1000);
                    }
                }

                CloseHandle(processInfo.hProcess);
                CloseHandle(stopEvent);
                if (!auto_hide_cursor::RestoreSystemCursors())
                {
                    Logger::error(L"Failed to restore system cursors after the worker exited. Error: {}", GetLastError());
                }

                if (stopping)
                {
                    break;
                }

                if (!restarting && WaitForSingleObject(m_terminateEvent, 1000) == WAIT_OBJECT_0)
                {
                    break;
                }
            }
        }

        bool LaunchWorker(
            const auto_hide_cursor::Configuration& configuration,
            const std::wstring& stopEventName,
            PROCESS_INFORMATION& processInfo)
        {
            auto moduleFolder = get_module_folderpath();
            auto workerPath = moduleFolder + L"\\" + workerExecutableName;
            auto commandLine =
                L"\"" + workerPath + L"\"" +
                L" --parent-pid " + std::to_wstring(GetCurrentProcessId()) +
                L" --stop-event \"" + stopEventName + L"\"" +
                L" --hide-on-typing " + std::to_wstring(configuration.hideOnTyping ? 1 : 0) +
                L" --hide-on-idle " + std::to_wstring(configuration.hideOnIdle ? 1 : 0) +
                L" --idle-delay-ms " + std::to_wstring(configuration.idleDelayMs);

            STARTUPINFOW startupInfo{};
            startupInfo.cb = sizeof(startupInfo);
            if (!CreateProcessW(
                    workerPath.c_str(),
                    commandLine.data(),
                    nullptr,
                    nullptr,
                    FALSE,
                    CREATE_NO_WINDOW,
                    nullptr,
                    moduleFolder.c_str(),
                    &startupInfo,
                    &processInfo))
            {
                Logger::error(L"Failed to start Auto Hide Cursor worker. Error: {}", GetLastError());
                return false;
            }

            CloseHandle(processInfo.hThread);
            processInfo.hThread = nullptr;
            return true;
        }

        void CloseSupervisorEvents() noexcept
        {
            if (m_restartEvent)
            {
                CloseHandle(m_restartEvent);
                m_restartEvent = nullptr;
            }
            if (m_terminateEvent)
            {
                CloseHandle(m_terminateEvent);
                m_terminateEvent = nullptr;
            }
        }

        std::atomic_bool m_enabled = false;
        std::mutex m_settingsMutex;
        auto_hide_cursor::Configuration m_settings;
        HANDLE m_terminateEvent = nullptr;
        HANDLE m_restartEvent = nullptr;
        std::thread m_supervisorThread;
    };
}

BOOL APIENTRY DllMain(HMODULE, DWORD reason, LPVOID)
{
    switch (reason)
    {
    case DLL_PROCESS_ATTACH:
        Trace::RegisterProvider();
        break;
    case DLL_PROCESS_DETACH:
        Trace::UnregisterProvider();
        break;
    }

    return TRUE;
}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new AutoHideCursorModule();
}
