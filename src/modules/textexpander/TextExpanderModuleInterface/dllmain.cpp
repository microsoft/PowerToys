#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/interop/shared_constants.h>
#include "trace.h"
#include "TextExpanderConstants.h"
#include <common/logger/logger.h>

#include <common/utils/logger_helper.h>
#include <common/utils/resources.h>
#include <common/utils/winapi_error.h>

#include <shlobj.h>
#include <algorithm>
#include <string>
#include <vector>

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
// this module, so the runner can always find and stop the engines it started, this session's or earlier.
const static wchar_t* ENGINE_ARGS = L"--managed --instance:powertoys";

// The window of an engine started with --instance:powertoys, the message that asks it to exit, and its file name, as in
// the engine's Program.cs. The window and the message are as old as the engine, so an engine from any build answers.
const static wchar_t* ENGINE_WINDOW_CLASS = L"PowerToys.TextExpander.MessageWindow.powertoys";
const static wchar_t* ENGINE_SHUTDOWN_MESSAGE = L"PowerToys.TextExpander.RequestShutdown";
const static wchar_t* ENGINE_FILE_NAME = L"PowerToys.TextExpander.exe";

// How long an engine asked to exit gets before it's terminated: time to put back a clipboard a paste borrowed.
// Match Program.WorkerDrainTimeoutMs so an in-flight paste can settle and restore the clipboard.
const static DWORD ENGINE_EXIT_TIMEOUT_MS = 10000;

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
    std::wstring app_key;

private:
    bool m_enabled = false;
    PROCESS_INFORMATION p_info = {};

    bool create_process_from_shell(const std::wstring& application_path, std::wstring& command_line, PROCESS_INFORMATION* process_info)
    {
        HWND shell_window = GetShellWindow();
        if (!shell_window)
        {
            Logger::error(L"PowerToys Text Expander could not find the desktop shell to launch its medium-integrity engine");
            return false;
        }

        DWORD shell_pid = 0;
        GetWindowThreadProcessId(shell_window, &shell_pid);
        HANDLE shell_process = shell_pid ? OpenProcess(PROCESS_CREATE_PROCESS, FALSE, shell_pid) : nullptr;
        if (!shell_process)
        {
            Logger::error(L"PowerToys Text Expander could not open the desktop shell process");
            return false;
        }

        SIZE_T attribute_list_size = 0;
        InitializeProcThreadAttributeList(nullptr, 1, 0, &attribute_list_size);
        if (GetLastError() != ERROR_INSUFFICIENT_BUFFER || attribute_list_size == 0)
        {
            CloseHandle(shell_process);
            Logger::error(L"PowerToys Text Expander could not size process attributes");
            return false;
        }

        std::vector<char> attribute_buffer(attribute_list_size);
        auto attribute_list = reinterpret_cast<PPROC_THREAD_ATTRIBUTE_LIST>(attribute_buffer.data());
        if (!InitializeProcThreadAttributeList(attribute_list, 1, 0, &attribute_list_size))
        {
            CloseHandle(shell_process);
            Logger::error(L"PowerToys Text Expander could not initialize process attributes");
            return false;
        }

        HANDLE parent_process = shell_process;
        bool attributes_set = UpdateProcThreadAttribute(
            attribute_list,
            0,
            PROC_THREAD_ATTRIBUTE_PARENT_PROCESS,
            &parent_process,
            sizeof(parent_process),
            nullptr,
            nullptr);

        STARTUPINFOEXW startup_info = {};
        startup_info.StartupInfo.cb = sizeof(startup_info);
        startup_info.lpAttributeList = attribute_list;

        bool created = attributes_set && CreateProcessW(
            application_path.c_str(),
            command_line.data(),
            nullptr,
            nullptr,
            FALSE,
            EXTENDED_STARTUPINFO_PRESENT,
            nullptr,
            nullptr,
            &startup_info.StartupInfo,
            process_info);

        DWORD error = created ? ERROR_SUCCESS : GetLastError();
        DeleteProcThreadAttributeList(attribute_list);
        CloseHandle(shell_process);
        SetLastError(error);
        return created;
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

    // Whether the engine must not inherit the runner's token: it is elevated, or can't be read.
    // is_process_elevated in common/utils/elevation.h reads the same flag, but including that
    // header would pull WIL into this module.
    static bool runner_may_be_elevated()
    {
        HANDLE token = nullptr;
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token))
        {
            return true;
        }

        TOKEN_ELEVATION elevation = {};
        DWORD size = 0;
        bool read = GetTokenInformation(token, TokenElevation, &elevation, sizeof(elevation), &size);
        CloseHandle(token);
        return !read || elevation.TokenIsElevated != 0;
    }

    // Whether process runs a Text Expander engine: a window of the engine's class could belong to any process.
    static bool is_engine_process(HANDLE process)
    {
        std::wstring path(32768, L'\0');
        DWORD size = static_cast<DWORD>(path.size());
        if (!QueryFullProcessImageNameW(process, 0, path.data(), &size))
        {
            return false;
        }

        path.resize(size);
        size_t separator = path.find_last_of(L'\\');
        return _wcsicmp(path.c_str() + (separator == std::wstring::npos ? 0 : separator + 1), ENGINE_FILE_NAME) == 0;
    }

    // Asks every engine PowerToys started in this session to exit, except keep_pid, and terminates any still running
    // after ENGINE_EXIT_TIMEOUT_MS. One left by an earlier PowerToys session, such as a build from before an update, went
    // on expanding with Text Expander off: a runner that starts with it off never stops an engine, and the exit event
    // wakes only one engine. Left running, it would also hold the single-instance mutex this runner's engine needs.
    static void stop_other_engines(DWORD keep_pid)
    {
        const UINT shutdown_message = RegisterWindowMessageW(ENGINE_SHUTDOWN_MESSAGE);
        std::vector<DWORD> pids;
        std::vector<HANDLE> engines;
        for (HWND window = FindWindowExW(nullptr, nullptr, ENGINE_WINDOW_CLASS, nullptr); window;
             window = FindWindowExW(nullptr, window, ENGINE_WINDOW_CLASS, nullptr))
        {
            DWORD pid = 0;
            GetWindowThreadProcessId(window, &pid);
            if (pid == 0 || pid == keep_pid || std::find(pids.begin(), pids.end(), pid) != pids.end())
            {
                continue;
            }

            HANDLE process = OpenProcess(SYNCHRONIZE | PROCESS_TERMINATE | PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
            if (!process)
            {
                Logger::warn(L"Could not open a Text Expander engine this runner didn't start (process {}). {}", pid, get_last_error_or_default(GetLastError()));
                continue;
            }

            if (!is_engine_process(process))
            {
                CloseHandle(process);
                continue;
            }

            // The engine exits as it does for the runner's own request, putting back a clipboard a paste borrowed.
            Logger::info(L"Asking a Text Expander engine this runner didn't start to exit (process {}).", pid);
            if (shutdown_message == 0 || !PostMessageW(window, shutdown_message, 0, 0))
            {
                Logger::warn(L"Could not ask Text Expander engine {} to exit. {}", pid, get_last_error_or_default(GetLastError()));
            }

            pids.push_back(pid);
            engines.push_back(process);
        }

        const ULONGLONG deadline = GetTickCount64() + ENGINE_EXIT_TIMEOUT_MS;
        for (size_t i = 0; i < engines.size(); ++i)
        {
            const ULONGLONG now = GetTickCount64();
            if (WaitForSingleObject(engines[i], now < deadline ? static_cast<DWORD>(deadline - now) : 0) != WAIT_OBJECT_0)
            {
                Logger::warn(L"Text Expander engine {} did not exit when asked; terminating it.", pids[i]);
                if (TerminateProcess(engines[i], 1))
                {
                    WaitForSingleObject(engines[i], 500);
                }
                else
                {
                    Logger::warn(L"Failed to terminate Text Expander engine {}. {}", pids[i], get_last_error_or_default(GetLastError()));
                }
            }

            CloseHandle(engines[i]);
        }
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

        // A runner that isn't elevated has a token the engine can simply inherit, which also works
        // at sign-in, before Explorer has created the shell window. An elevated runner's token must
        // not be passed on, so the engine is then started from the shell, and not at all without one.
        bool from_shell = runner_may_be_elevated();
        bool created = false;
        if (from_shell)
        {
            created = create_process_from_shell(application_path, full_command_path, &p_info);
        }
        else
        {
            STARTUPINFOW startup_info = { sizeof(startup_info) };
            created = CreateProcessW(application_path.c_str(), full_command_path.data(), nullptr, nullptr, FALSE, 0, nullptr, nullptr, &startup_info, &p_info);
        }

        if (!created)
        {
            DWORD error = GetLastError();
            std::wstring message = from_shell ? L"PowerToys Text Expander failed to start as the desktop user with error: " :
                                                L"PowerToys Text Expander failed to start with error: ";
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
        app_key = TextExpanderConstants::ModuleKey;
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", "TextExpander");
        Logger::info("TextExpander object is constructing");

        // Before the runner decides whether to enable this module: with it off, nothing else would stop them.
        stop_other_engines(0);
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

        // An engine still running now isn't this runner's, and would keep the one it starts from running.
        stop_other_engines(0);

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

            // Any other engine first, so the exit event, which wakes only one, reaches this runner's.
            stop_other_engines(p_info.dwProcessId);

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
                    forceTerminate = WaitForSingleObject(p_info.hProcess, ENGINE_EXIT_TIMEOUT_MS) != WAIT_OBJECT_0;
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
