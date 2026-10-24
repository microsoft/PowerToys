// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"

#include "CalibrationOverlay.h"
#include "CalibrationStore.h"
#include "EdgeClaims.h"
#include "ModuleConstants.h"
#include "MouseHookThread.h"
#include "Generated Files/resource.h"
#include "trace.h"

#include "../../../common/SettingsAPI/settings_objects.h"
#include "../../../common/interop/shared_constants.h"
#include "../../../common/logger/logger.h"
#include "../../../common/utils/gpo.h"
#include "../../../common/utils/logger_helper.h"
#include "../../interface/powertoy_module_interface.h"

#include <mutex>
#include <system_error>
#include <thread>

extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace
{
    using namespace seamless_cursor;

    // Custom actions sent by the Settings UI buttons.
    constexpr wchar_t calibrateAction[] = L"calibrate";
    constexpr wchar_t resetCalibrationAction[] = L"reset_calibration";

    constexpr wchar_t jsonStopAtEmptyEdges[] = L"stop_at_empty_edges";
    constexpr wchar_t jsonMapCursorWrap[] = L"map_cursor_wrap";

    class SeamlessCursorModule : public PowertoyModuleIface
    {
    public:
        SeamlessCursorModule() :
            m_overlay({ [this]() { m_hookThread.Suspend(); },
                        [this](std::optional<std::vector<BorderCalibration>> calibrations) {
                            if (calibrations)
                            {
                                m_hookThread.Preview(std::move(*calibrations));
                            }
                            else
                            {
                                m_hookThread.Suspend();
                            }
                        },
                        // Reloading also drops an unsaved preview and resumes remapping.
                        [this](bool) { m_hookThread.ReloadCalibration(); },
                        [this](bool stop) { SetStopAtEmptyEdges(stop); } })
        {
            LoggerHelpers::init_logger(moduleKey, L"ModuleInterface", LogSettings::seamlessCursorLoggerName);
            try
            {
                auto values = PowerToysSettings::PowerToyValues::load_from_settings_file(moduleKey);
                ApplySettings(values);
            }
            catch (...)
            {
                // Windows.Data.Json reports type mismatches as hresult_error, which is not a std::exception.
                Logger::error("Invalid JSON when loading Seamless Multi-Screen Cursor settings.");
            }
        }

        void destroy() override
        {
            disable();
            delete this;
        }

        const wchar_t* get_name() override
        {
            return moduleKey;
        }

        const wchar_t* get_key() override
        {
            return moduleKey;
        }

        powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
        {
            return powertoys_gpo::getConfiguredSeamlessCursorEnabledValue();
        }

        bool get_config(wchar_t* buffer, int* bufferSize) override
        {
            PowerToysSettings::Settings settings(reinterpret_cast<HINSTANCE>(&__ImageBase), get_name());
            settings.set_description(IDS_SEAMLESS_CURSOR_NAME);
            settings.set_icon_key(L"pt-seamless-cursor");
            std::scoped_lock lock{ m_settingsMutex };
            settings.add_bool_toggle(jsonStopAtEmptyEdges, IDS_SETTING_STOP_AT_EMPTY_EDGES, m_options.stopWhereNothingIsAcross);
            settings.add_bool_toggle(jsonMapCursorWrap, IDS_SETTING_MAP_CURSOR_WRAP, m_mapCursorWrap);
            return settings.serialize_to_buffer(buffer, bufferSize);
        }

        void call_custom_action(const wchar_t* action) override
        {
            try
            {
                auto actionObject = PowerToysSettings::CustomActionObject::from_json_string(action);
                const auto name = actionObject.get_name();
                if (!m_enabled)
                {
                    return;
                }

                if (name == calibrateAction)
                {
                    m_overlay.Show();
                }
                else if (name == resetCalibrationAction && DeleteCalibrations())
                {
                    m_hookThread.ReloadCalibration();
                }
            }
            catch (...)
            {
                Logger::error("Invalid custom action JSON for Seamless Multi-Screen Cursor.");
            }
        }

        void set_config(const wchar_t* config) override
        {
            try
            {
                auto values = PowerToysSettings::PowerToyValues::from_json_string(config, get_key());
                ApplySettings(values);
            }
            catch (...)
            {
                Logger::error("Invalid JSON when parsing Seamless Multi-Screen Cursor settings.");
            }
        }

        void enable() override
        {
            if (m_enabled || !m_hookThread.Start())
            {
                return;
            }

            m_enabled = true;
            StartCalibrateListener();
            Trace::EnableSeamlessCursor(true);
        }

        void disable() override
        {
            if (!m_enabled.exchange(false))
            {
                return;
            }

            StopCalibrateListener();
            m_overlay.Close();
            m_hookThread.Stop();
            Trace::EnableSeamlessCursor(false);
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
        void ApplySettings(const PowerToysSettings::PowerToyValues& values)
        {
            std::scoped_lock lock{ m_settingsMutex };

            // A missing, null or non-boolean value keeps the current one, so a bad entry cannot hold up the others.
            const bool stopWhereNothingIsAcross = values.get_bool_value(jsonStopAtEmptyEdges).value_or(m_options.stopWhereNothingIsAcross);
            m_mapCursorWrap = values.get_bool_value(jsonMapCursorWrap).value_or(m_mapCursorWrap);
            EnableWrapMapping(m_mapCursorWrap);

            // New options rebuild the engine on the hook thread, which holds up mouse input for a moment.
            if (stopWhereNothingIsAcross != m_options.stopWhereNothingIsAcross)
            {
                m_options.stopWhereNothingIsAcross = stopWhereNothingIsAcross;
                m_hookThread.SetOptions(m_options);
                m_overlay.SetEngineOptions(m_options);
            }
        }

        // From the overlay's switch: the same setting as in Settings, so it is saved to the settings file, from where the
        // Settings page picks it up.
        void SetStopAtEmptyEdges(bool stop)
        {
            std::scoped_lock lock{ m_settingsMutex };
            if (stop == m_options.stopWhereNothingIsAcross)
            {
                return;
            }

            m_options.stopWhereNothingIsAcross = stop;
            m_hookThread.SetOptions(m_options);
            m_overlay.SetEngineOptions(m_options);
            try
            {
                PowerToysSettings::PowerToyValues values(moduleKey, moduleKey);
                values.add_property(jsonStopAtEmptyEdges, m_options.stopWhereNothingIsAcross);
                values.add_property(jsonMapCursorWrap, m_mapCursorWrap);
                values.save_to_settings_file();
            }
            catch (...)
            {
                Logger::error("Failed to save the Seamless Multi-Screen Cursor settings.");
            }
        }

        // Command Palette opens the calibration through a named event, like the other Mouse Utilities.
        void StartCalibrateListener()
        {
            m_calibrateEvent.attach(CreateEventW(nullptr, FALSE, FALSE, CommonSharedConstants::SEAMLESS_CURSOR_CALIBRATE_EVENT));
            m_stopListenerEvent.attach(CreateEventW(nullptr, TRUE, FALSE, nullptr));
            if (!m_calibrateEvent || !m_stopListenerEvent)
            {
                Logger::error(L"Failed to create the Seamless Multi-Screen Cursor calibrate events. Error: {}", GetLastError());
                return;
            }

            try
            {
                m_calibrateListener = std::thread([this]() {
                    const HANDLE events[] = { m_stopListenerEvent.get(), m_calibrateEvent.get() };
                    while (WaitForMultipleObjects(static_cast<DWORD>(std::size(events)), events, FALSE, INFINITE) == WAIT_OBJECT_0 + 1)
                    {
                        m_overlay.Show();
                    }
                });
            }
            catch (const std::system_error& error)
            {
                Logger::error("Failed to start the Seamless Multi-Screen Cursor calibrate listener: {}", error.what());
            }
        }

        void StopCalibrateListener()
        {
            if (m_stopListenerEvent)
            {
                SetEvent(m_stopListenerEvent.get());
            }

            if (m_calibrateListener.joinable())
            {
                m_calibrateListener.join();
            }

            m_calibrateEvent.close();
            m_stopListenerEvent.close();
        }

        std::atomic_bool m_enabled = false;
        std::mutex m_settingsMutex; // Settings come from the runner and from the overlay thread.
        EngineOptions m_options;
        bool m_mapCursorWrap = true;
        winrt::handle m_calibrateEvent;
        winrt::handle m_stopListenerEvent;
        std::thread m_calibrateListener;

        // Declared before the overlay: the overlay callbacks use it, and the overlay is destroyed first.
        MouseHookThread m_hookThread;
        CalibrationOverlay m_overlay;
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
    return new SeamlessCursorModule();
}
