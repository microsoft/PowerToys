#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/interop/shared_constants.h>
#include "trace.h"
#include "resource.h"
#include "AwakeConstants.h"
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_helpers.h>

#include <common/utils/elevation.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <common/utils/os-detect.h>
#include <common/utils/winapi_error.h>
#include <common/utils/json.h>

#include <algorithm>
#include <limits>
#include <memory>
#include <cwctype>
#include <optional>
#include <filesystem>
#include <set>

BOOL APIENTRY DllMain(HMODULE /*hModule*/, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        Trace::RegisterProvider();
        break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
        break;
    case DLL_PROCESS_DETACH:
        Trace::UnregisterProvider();
        break;
    }
    return TRUE;
}

const static wchar_t* MODULE_NAME = L"Awake";
const static wchar_t* MODULE_DESC = L"A module that keeps your computer awake on-demand.";

namespace
{
    std::wstring to_lower_copy(std::wstring value)
    {
        std::transform(value.begin(), value.end(), value.begin(), [](wchar_t ch) {
            return static_cast<wchar_t>(std::towlower(ch));
        });
        return value;
    }

    std::wstring mode_to_string(int mode)
    {
        switch (mode)
        {
        case 0:
            return L"passive";
        case 1:
            return L"indefinite";
        case 2:
            return L"timed";
        case 3:
            return L"expirable";
        default:
            return L"unknown";
        }
    }

    std::optional<uint32_t> parse_duration_string(const std::wstring& raw)
    {
        if (raw.empty())
        {
            return std::nullopt;
        }

        std::wstring value = raw;
        double multiplier = 1.0;

        wchar_t suffix = value.back();
        if (!iswdigit(suffix))
        {
            value.pop_back();
            if (suffix == L'h' || suffix == L'H')
            {
                multiplier = 60.0;
            }
            else if (suffix == L'm' || suffix == L'M')
            {
                multiplier = 1.0;
            }
            else
            {
                return std::nullopt;
            }
        }

        try
        {
            double numeric = std::stod(value);
            if (numeric < 0)
            {
                return std::nullopt;
            }

            double totalMinutes = numeric * multiplier;
            if (totalMinutes < 0 || totalMinutes > static_cast<double>(std::numeric_limits<uint32_t>::max()))
            {
                return std::nullopt;
            }

            return static_cast<uint32_t>(totalMinutes);
        }
        catch (...)
        {
            return std::nullopt;
        }
    }

    std::optional<uint32_t> extract_duration_minutes(const json::JsonObject& args)
    {
        if (args.HasKey(L"durationMinutes"))
        {
            auto value = args.GetNamedNumber(L"durationMinutes");
            if (value < 0)
            {
                return std::nullopt;
            }
            return static_cast<uint32_t>(value);
        }

        if (args.HasKey(L"duration"))
        {
            auto asString = args.GetNamedString(L"duration");
            return parse_duration_string(asString.c_str());
        }

        return std::nullopt;
    }
}

class Awake : public PowertoyModuleIface
{
    std::wstring app_name;
    std::wstring app_key;

private:
    bool m_enabled = false;
    PROCESS_INFORMATION p_info = {};
    std::unique_ptr<pt::cli::IModuleCommandProvider> m_cliProvider;

    bool is_process_running()
    {
        return WaitForSingleObject(p_info.hProcess, 0) == WAIT_TIMEOUT;
    }

    void launch_process()
    {
        Logger::trace(L"Launching PowerToys Awake process");
        unsigned long powertoys_pid = GetCurrentProcessId();

        std::wstring executable_args = L"--use-pt-config --pid " + std::to_wstring(powertoys_pid);
        std::wstring application_path = L"PowerToys.Awake.exe";
        std::wstring full_command_path = application_path + L" " + executable_args.data();
        Logger::trace(L"PowerToys Awake launching with parameters: " + executable_args);

        STARTUPINFO info = { sizeof(info) };

        if (!CreateProcess(application_path.c_str(), full_command_path.data(), NULL, NULL, true, NULL, NULL, NULL, &info, &p_info))
        {
            DWORD error = GetLastError();
            std::wstring message = L"PowerToys Awake failed to start with error: ";
            message += std::to_wstring(error);
            Logger::error(message);
        }
    }

public:
    Awake()
    {
        app_name = GET_RESOURCE_STRING(IDS_AWAKE_NAME);
        app_key = AwakeConstants::ModuleKey;
        std::filesystem::path logFilePath(PTSettingsHelper::get_module_save_folder_location(this->app_key));
        logFilePath.append(LogSettings::awakeLogPath);
        Logger::init(LogSettings::launcherLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());
        Logger::info("Launcher object is constructing");
    };

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredAwakeEnabledValue();
    }

    virtual void destroy() override
    {
        delete this;
    }

    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(MODULE_DESC);

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual const wchar_t* get_key() override
    {
        return app_key.c_str();
    }

    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            // If you don't need to do any custom processing of the settings, proceed
            // to persists the values.
            values.save_to_settings_file();
        }
        catch (std::exception&)
        {
            // Improper JSON.
        }
    }

    virtual void enable()
    {
        Trace::EnableAwake(true);
        launch_process();
        m_enabled = true;
    };

    virtual void disable()
    {
        if (m_enabled)
        {
            Trace::EnableAwake(false);
            Logger::trace(L"Disabling Awake...");

            auto exitEvent = CreateEvent(nullptr, false, false, CommonSharedConstants::AWAKE_EXIT_EVENT);
            if (!exitEvent)
            {
                Logger::warn(L"Failed to create exit event for PowerToys Awake. {}", get_last_error_or_default(GetLastError()));
            }
            else
            {
                Logger::trace(L"Signaled exit event for PowerToys Awake.");
                if (!SetEvent(exitEvent))
                {
                    Logger::warn(L"Failed to signal exit event for PowerToys Awake. {}", get_last_error_or_default(GetLastError()));

                    // For some reason, we couldn't process the signal correctly, so we still
                    // need to terminate the Awake process.
                    TerminateProcess(p_info.hProcess, 1);
                }

                ResetEvent(exitEvent);
                CloseHandle(exitEvent);
                CloseHandle(p_info.hProcess);
            }
        }

        m_enabled = false;
    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    pt::cli::IModuleCommandProvider* command_provider() override;

    pt::cli::CommandResult HandleStatus() const;
    pt::cli::CommandResult HandleSet(const json::JsonObject& args);
};

class AwakeCommandProvider final : public pt::cli::IModuleCommandProvider
{
public:
    explicit AwakeCommandProvider(Awake& owner) :
        m_owner(owner)
    {
    }

    std::wstring ModuleKey() const override
    {
        return L"awake";
    }

    std::vector<pt::cli::CommandDescriptor> DescribeCommands() const override
    {
        std::vector<pt::cli::CommandParameter> setParameters{
            { L"mode", false, L"Awake mode: passive | indefinite | timed." },
            { L"durationMinutes", false, L"Total duration in minutes for timed mode." },
            { L"duration", false, L"Duration with unit (e.g. 30m, 2h) for timed mode." },
            { L"displayOn", false, L"Whether to keep the display active (true/false)." },
        };

        pt::cli::CommandDescriptor setDescriptor;
        setDescriptor.action = L"set";
        setDescriptor.description = L"Configure the Awake module.";
        setDescriptor.parameters = std::move(setParameters);

        pt::cli::CommandDescriptor statusDescriptor;
        statusDescriptor.action = L"status";
        statusDescriptor.description = L"Inspect the current Awake mode.";

        return { std::move(setDescriptor), std::move(statusDescriptor) };
    }

    pt::cli::CommandResult Execute(const pt::cli::CommandInvocation& invocation) override
    {
        auto action = to_lower_copy(invocation.action);
        if (action == L"set")
        {
            return m_owner.HandleSet(invocation.args);
        }

        if (action == L"status")
        {
            return m_owner.HandleStatus();
        }

        return pt::cli::CommandResult::Error(L"E_COMMAND_NOT_FOUND", L"Unsupported Awake action.");
    }

private:
    Awake& m_owner;
};

pt::cli::IModuleCommandProvider* Awake::command_provider()
{
    if (!m_cliProvider)
    {
        m_cliProvider = std::make_unique<AwakeCommandProvider>(*this);
    }

    return m_cliProvider.get();
}

pt::cli::CommandResult Awake::HandleStatus() const
{
    auto settings = PTSettingsHelper::load_module_settings(app_key);
    json::JsonObject payload = json::JsonObject();

    if (!settings.HasKey(L"properties"))
    {
        payload.SetNamedValue(L"mode", json::value(L"unknown"));
        payload.SetNamedValue(L"keepDisplayOn", json::value(false));
        return pt::cli::CommandResult::Success(std::move(payload));
    }

    auto properties = settings.GetNamedObject(L"properties");

    const auto modeValue = static_cast<int>(properties.GetNamedNumber(L"mode", 0));
    payload.SetNamedValue(L"mode", json::value(mode_to_string(modeValue)));
    payload.SetNamedValue(L"modeValue", json::value(modeValue));
    payload.SetNamedValue(L"keepDisplayOn", json::value(properties.GetNamedBoolean(L"keepDisplayOn", false)));
    payload.SetNamedValue(L"intervalHours", json::value(static_cast<uint32_t>(properties.GetNamedNumber(L"intervalHours", 0))));
    payload.SetNamedValue(L"intervalMinutes", json::value(static_cast<uint32_t>(properties.GetNamedNumber(L"intervalMinutes", 0))));

    if (properties.HasKey(L"expirationDateTime"))
    {
        payload.SetNamedValue(L"expirationDateTime", json::value(properties.GetNamedString(L"expirationDateTime")));
    }

    return pt::cli::CommandResult::Success(std::move(payload));
}

pt::cli::CommandResult Awake::HandleSet(const json::JsonObject& args)
{
    std::wstring requestedMode = L"indefinite";
    if (args.HasKey(L"mode"))
    {
        requestedMode = to_lower_copy(std::wstring(args.GetNamedString(L"mode").c_str()));
    }

    auto settings = PTSettingsHelper::load_module_settings(app_key);
    json::JsonObject properties = settings.HasKey(L"properties") ? settings.GetNamedObject(L"properties") : json::JsonObject();

    const bool keepDisplayOn = args.GetNamedBoolean(L"displayOn", properties.GetNamedBoolean(L"keepDisplayOn", false));

    int modeValue = 1; // default to indefinite
    if (requestedMode == L"passive")
    {
        modeValue = 0;
    }
    else if (requestedMode == L"indefinite" || requestedMode.empty())
    {
        modeValue = 1;
    }
    else if (requestedMode == L"timed")
    {
        modeValue = 2;
    }
    else
    {
        return pt::cli::CommandResult::Error(L"E_ARGS_INVALID", L"Unsupported mode. Use passive, indefinite, or timed.");
    }

    properties.SetNamedValue(L"keepDisplayOn", json::value(keepDisplayOn));
    properties.SetNamedValue(L"mode", json::value(modeValue));

    if (modeValue == 2)
    {
        auto durationMinutes = extract_duration_minutes(args);
        if (!durationMinutes.has_value() || durationMinutes.value() == 0)
        {
            return pt::cli::CommandResult::Error(L"E_ARGS_INVALID", L"Timed mode requires a non-zero duration.");
        }

        const uint32_t totalMinutes = durationMinutes.value();
        const uint32_t hours = totalMinutes / 60;
        const uint32_t minutes = totalMinutes % 60;
        properties.SetNamedValue(L"intervalHours", json::value(hours));
        properties.SetNamedValue(L"intervalMinutes", json::value(minutes));
    }
    else
    {
        properties.SetNamedValue(L"intervalHours", json::value(0));
        properties.SetNamedValue(L"intervalMinutes", json::value(0));
    }

    settings.SetNamedValue(L"properties", json::value(properties));
    PTSettingsHelper::save_module_settings(app_key, settings);

    return HandleStatus();
}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new Awake();
}
