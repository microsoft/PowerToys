#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/interop/shared_constants.h>
#include "trace.h"
#include "resource.h"
#include "TextExpanderConstants.h"
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_helpers.h>

#include <common/utils/logger_helper.h>
#include <common/utils/resources.h>
#include <common/utils/winapi_error.h>

#include <shlobj.h>
#include <string>

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

const static wchar_t* MODULE_NAME = L"TextExpander";
const static wchar_t* MODULE_DESC = L"Expands short triggers you type into longer snippets.";

// The engine is a self-contained .NET process living beside the runner. Stored relative, but
// always resolved against the runner's own directory before launch -- see resolve_engine_path.
const static wchar_t* ENGINE_PATH = L"WinUI3Apps\\PowerToys.TextExpander.exe";

// --managed runs the engine without UI of its own; PowerToys owns the UI.
// --instance:powertoys scopes the engine's single-instance mutex and message-window class to
// this module, so the runner can always find and stop the instance it started.
const static wchar_t* ENGINE_ARGS = L"--managed --instance:powertoys";

// Where the Settings app writes this module's configuration. Passed explicitly so the
// dependency is visible on the command line. The engine watches the file, so changes apply live.
static std::wstring settings_argument()
{
    wchar_t* localAppData = nullptr;
    if (FAILED(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &localAppData)) || !localAppData)
    {
        Logger::warn(L"Could not resolve LocalAppData; the engine will fall back to environment settings.");
        if (localAppData)
        {
            CoTaskMemFree(localAppData);
        }
        return L"";
    }

    std::wstring path = std::wstring(localAppData) + L"\\Microsoft\\PowerToys\\" +
                        TextExpanderConstants::ModuleKey + L"\\settings.json";
    CoTaskMemFree(localAppData);

    // Quoted: the path is under a user profile, which routinely contains spaces.
    return L" --settings:\"" + path + L"\"";
}

class TextExpander : public PowertoyModuleIface
{
    std::wstring app_name;
    std::wstring app_key;

private:
    bool m_enabled = false;
    PROCESS_INFORMATION p_info = {};

    bool is_process_running()
    {
        return p_info.hProcess && WaitForSingleObject(p_info.hProcess, 0) == WAIT_TIMEOUT;
    }

    // Closes both handles and clears the record. CreateProcess does not touch
    // PROCESS_INFORMATION on failure, so without this a failed enable() leaves the previous
    // cycle's already-closed handle values in place -- and the next disable() would call
    // TerminateProcess on a handle value Windows may since have recycled into an unrelated
    // process, then close it twice.
    void reset_process_info()
    {
        if (p_info.hThread)
        {
            CloseHandle(p_info.hThread);
        }
        if (p_info.hProcess)
        {
            CloseHandle(p_info.hProcess);
        }
        p_info = {};
    }

    /// <summary>
    /// The engine's absolute path, anchored to the directory holding this DLL.
    ///
    /// Passing ENGINE_PATH straight to CreateProcessW would resolve it against the current
    /// working directory, which other code in the runner process can change. Resolving against
    /// this DLL's own location (as LightSwitch and GrabAndMove do) ensures only the installed
    /// engine is launched.
    /// </summary>
    static std::wstring resolve_engine_path()
    {
        HINSTANCE self = reinterpret_cast<HINSTANCE>(&__ImageBase);

        std::wstring module_path(MAX_PATH, L'\0');
        DWORD length = GetModuleFileNameW(self, module_path.data(),
                                          static_cast<DWORD>(module_path.size()));

        // GetModuleFileNameW truncates and reports the buffer size on overflow rather than
        // failing outright, so a full buffer is indistinguishable from a path that exactly fits.
        // Treat both as failure: a truncated path is worse than none.
        if (length == 0 || length >= module_path.size())
        {
            Logger::error(L"PowerToys TextExpander could not resolve its own module path");
            return {};
        }

        module_path.resize(length);

        size_t separator = module_path.find_last_of(L'\\');
        if (separator == std::wstring::npos)
        {
            Logger::error(L"PowerToys TextExpander module path has no directory component");
            return {};
        }

        return module_path.substr(0, separator + 1) + ENGINE_PATH;
    }

    bool launch_process()
    {
        Logger::trace(L"Launching PowerToys TextExpander process");
        unsigned long powertoys_pid = GetCurrentProcessId();

        std::wstring application_path = resolve_engine_path();
        if (application_path.empty())
        {
            Logger::error(L"PowerToys TextExpander not started: engine path unresolved");
            return false;
        }

        // The engine watches the runner pid and exits with it, so it never outlives the runner
        // while holding a keyboard hook. The path is quoted because it can contain spaces.
        std::wstring full_command_path =
            L"\"" + application_path + L"\" " + std::to_wstring(powertoys_pid) + L" " + ENGINE_ARGS +
            settings_argument();
        Logger::trace(L"PowerToys TextExpander launching: " + full_command_path);

        STARTUPINFOW info = { sizeof(info) };

        if (!CreateProcessW(application_path.c_str(), full_command_path.data(), NULL, NULL, true, NULL, NULL, NULL, &info, &p_info))
        {
            DWORD error = GetLastError();
            std::wstring message = L"PowerToys TextExpander failed to start with error: ";
            message += std::to_wstring(error);
            Logger::error(message);
            p_info = {};
            return false;
        }

        // The primary thread handle is not needed.
        if (p_info.hThread)
        {
            CloseHandle(p_info.hThread);
            p_info.hThread = nullptr;
        }
        return true;
    }

public:
    TextExpander()
    {
        app_name = MODULE_NAME;
        app_key = TextExpanderConstants::ModuleKey;
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", "TextExpander");
        Logger::info("TextExpander object is constructing");
    };

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

    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredTextExpanderEnabledValue();
    }

    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            values.save_to_settings_file();
        }
        catch (std::exception&)
        {
            // Improper JSON.
        }
    }

    virtual void enable()
    {
        if (m_enabled)
        {
            return;
        }

        // Only report enabled if the engine actually started. Claiming otherwise leaves the UI
        // showing a module that expands nothing.
        if (launch_process())
        {
            m_enabled = true;
            Trace::EnableTextExpander(true);
        }
    };

    virtual void disable()
    {
        if (m_enabled)
        {
            Logger::trace(L"Disabling TextExpander... {}", m_enabled);

            // Ask the engine to exit first so an in-flight paste can restore the user's
            // clipboard, then terminate it if it does not exit in time. The terminate fallback
            // also covers failing to create or signal the event.
            bool forceTerminate = true;

            auto exitEvent = CreateEventW(nullptr, false, false, CommonSharedConstants::TEXT_EXPANDER_EXIT_EVENT);
            if (!exitEvent)
            {
                Logger::warn(L"Failed to create exit event for PowerToys TextExpander. {}", get_last_error_or_default(GetLastError()));
            }
            else
            {
                if (!SetEvent(exitEvent))
                {
                    Logger::warn(L"Failed to signal exit event for PowerToys TextExpander. {}", get_last_error_or_default(GetLastError()));
                }
                else
                {
                    Logger::trace(L"Signaled exit event for PowerToys TextExpander.");
                    forceTerminate = WaitForSingleObject(p_info.hProcess, 1500) != WAIT_OBJECT_0;
                    if (forceTerminate)
                    {
                        Logger::warn(L"PowerToys TextExpander did not exit after the exit event; terminating it.");
                    }
                }

                CloseHandle(exitEvent);
            }

            if (forceTerminate && p_info.hProcess)
            {
                if (!TerminateProcess(p_info.hProcess, 1))
                {
                    Logger::warn(L"Failed to terminate PowerToys TextExpander. {}", get_last_error_or_default(GetLastError()));
                }
                else
                {
                    WaitForSingleObject(p_info.hProcess, 500);
                }
            }

            reset_process_info();
        }

        m_enabled = false;
        Trace::EnableTextExpander(false);
    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    virtual bool is_enabled_by_default() const override
    {
        return false;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new TextExpander();
}
