#include "pch.h"

#include "pipe_server.h"

#include <interface/powertoy_module_interface.h>
#include <common/logger/logger.h>
#include <common/utils/logger_helper.h>
#include <common/utils/winapi_error.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/process_path.h>

#include <shellapi.h>
#include <tlhelp32.h>

#include <format>
#include <memory>
#include <condition_variable>
#include <deque>
#include <mutex>
#include <thread>
#include <vector>

using namespace SoundManager;

extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace
{
    constexpr wchar_t MODULE_KEY[] = L"Sound Manager";
    constexpr wchar_t MODULE_NAME[] = L"Sound Manager";
    constexpr wchar_t MODULE_DESCRIPTION[] =
        L"Sound Manager is a quick way to control your system volume, per-app volumes and audio "
        L"devices from a single window.";
    constexpr wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    constexpr wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"activation_shortcut";
    constexpr wchar_t JSON_KEY_MUTE_SHORTCUTS[] = L"mute-shortcuts";
    constexpr wchar_t JSON_KEY_VALUE[] = L"value";
    constexpr wchar_t JSON_KEY_PROGRAM[] = L"program";
    constexpr wchar_t JSON_KEY_NAME[] = L"name";
    constexpr wchar_t JSON_KEY_SHORTCUT[] = L"shortcut";
    constexpr wchar_t JSON_KEY_WIN[] = L"win";
    constexpr wchar_t JSON_KEY_CTRL[] = L"ctrl";
    constexpr wchar_t JSON_KEY_SHIFT[] = L"shift";
    constexpr wchar_t JSON_KEY_ALT[] = L"alt";
    constexpr wchar_t JSON_KEY_CODE[] = L"code";

    constexpr wchar_t APP_PROCESS_NAME[] = L"PowerToys.SoundManager.exe";

    struct WindowSearch
    {
        DWORD processId;
        HWND window;
    };

    struct MuteShortcut
    {
        // The full path of the executable. That is what tells one program from another.
        std::wstring program;

        std::wstring name;

        PowertoyModuleIface::Hotkey hotkey;
    };

    struct MuteRequest
    {
        std::wstring program;
        std::wstring name;
    };
}

class SoundManagerModuleInterface : public PowertoyModuleIface
{
private:
    bool m_enabled = false;
    HANDLE m_hProcess = nullptr;
    DWORD m_appProcessId = 0;
    Hotkey m_activationHotkey = { .win = true, .ctrl = true, .shift = true, .alt = false, .key = 'V' };
    std::vector<MuteShortcut> m_muteShortcuts;
    std::mutex m_muteShortcutsMutex;
    std::wstring m_pipeName;
    std::unique_ptr<PipeServer> m_pipeServer;

    // The thread that carries out the shortcuts. It has an audio engine of its own.
    std::thread m_muteThread;
    std::mutex m_muteMutex;
    std::condition_variable m_muteSignal;
    std::deque<MuteRequest> m_muteQueue;
    bool m_muteStopping = false;

    bool IsProcessRunning() const
    {
        return m_hProcess != nullptr && WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
    }

    // Finds the visible window of the Sound Manager; the program has hidden ones of its own.
    HWND FindAppWindow(DWORD processId) const
    {
        WindowSearch search{ processId, nullptr };

        const auto enum_windows = [](HWND hwnd, LPARAM param) -> BOOL {
            auto* data = reinterpret_cast<WindowSearch*>(param);

            DWORD windowProcessId = 0;
            GetWindowThreadProcessId(hwnd, &windowProcessId);
            if (windowProcessId != data->processId || !IsWindowVisible(hwnd))
            {
                return TRUE;
            }

            data->window = hwnd;
            return FALSE;
        };

        EnumWindows(enum_windows, reinterpret_cast<LPARAM>(&search));
        return search.window;
    }

    // Looks for a Sound Manager that this module did not start.
    DWORD FindAppProcessId() const
    {
        const HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == INVALID_HANDLE_VALUE)
        {
            return 0;
        }

        DWORD processId = 0;

        PROCESSENTRY32W entry = { sizeof(entry) };
        if (Process32FirstW(snapshot, &entry))
        {
            do
            {
                if (CompareStringOrdinal(entry.szExeFile, -1, APP_PROCESS_NAME, -1, TRUE) == CSTR_EQUAL)
                {
                    processId = entry.th32ProcessID;
                    break;
                }
            }
            while (Process32NextW(snapshot, &entry));
        }

        CloseHandle(snapshot);
        return processId;
    }

    // Brings the window of the Sound Manager to the front. The runner is a background process, so SetForegroundWindow is refused, and the input queues are joined.
    bool BringProcessToFront(DWORD processId) const
    {
        const HWND window = FindAppWindow(processId);
        if (window == nullptr)
        {
            return false;
        }

        if (IsIconic(window))
        {
            ShowWindow(window, SW_RESTORE);
        }

        const DWORD foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), nullptr);
        const DWORD currentThread = GetCurrentThreadId();

        if (foregroundThread != 0 && foregroundThread != currentThread)
        {
            AttachThreadInput(currentThread, foregroundThread, TRUE);
        }

        BringWindowToTop(window);
        SetForegroundWindow(window);

        if (foregroundThread != 0 && foregroundThread != currentThread)
        {
            AttachThreadInput(currentThread, foregroundThread, FALSE);
        }

        return true;
    }

    void LaunchProcess(DWORD existingProcessId)
    {
        Logger::trace(L"Starting Sound Manager process");

        const std::wstring arguments = std::format(L"{} {}", GetCurrentProcessId(), m_pipeName);

        std::wstring path = get_module_folderpath();
        path += L"\\WinUI3Apps\\PowerToys.SoundManager.exe";

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI;
        sei.lpFile = path.c_str();
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = arguments.data();

        if (!ShellExecuteExW(&sei))
        {
            Logger::error(L"Sound Manager failed to start. {}", get_last_error_or_default(GetLastError()));
            return;
        }

        Logger::trace(L"Successfully started the Sound Manager process");

        if (existingProcessId != 0)
        {
            // The copy that was started hands the activation to the one that is already running.
            CloseHandle(sei.hProcess);

            const HANDLE running = OpenProcess(
                SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_TERMINATE,
                FALSE,
                existingProcessId);
            TrackAppProcess(running, existingProcessId);
            return;
        }

        TrackAppProcess(sei.hProcess, GetProcessId(sei.hProcess));
    }

    void TrackAppProcess(HANDLE process, DWORD processId)
    {
        if (m_hProcess != nullptr)
        {
            CloseHandle(m_hProcess);
        }

        m_hProcess = process;
        m_appProcessId = process == nullptr ? 0 : processId;
    }

    void ShowMixerWindow()
    {
        if (IsProcessRunning() && BringProcessToFront(m_appProcessId))
        {
            return;
        }

        const DWORD processId = FindAppProcessId();
        if (processId != 0 && BringProcessToFront(processId))
        {
            return;
        }

        LaunchProcess(processId);
    }

    void ParseSettings(PowerToysSettings::PowerToyValues settings)
    {
        std::lock_guard lock(m_muteShortcutsMutex);

        const auto settingsObject = settings.get_raw_json();
        if (!settingsObject.GetView().Size() || !settingsObject.HasKey(JSON_KEY_PROPERTIES))
        {
            return;
        }

        const auto properties = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);

        Hotkey activationHotkey = m_activationHotkey;
        if (properties.HasKey(JSON_KEY_ACTIVATION_SHORTCUT))
        {
            const auto hotkey = PowerToysSettings::HotkeyObject::from_json(properties.GetNamedObject(JSON_KEY_ACTIVATION_SHORTCUT));

            activationHotkey.win = hotkey.win_pressed();
            activationHotkey.ctrl = hotkey.ctrl_pressed();
            activationHotkey.shift = hotkey.shift_pressed();
            activationHotkey.alt = hotkey.alt_pressed();
            activationHotkey.key = static_cast<unsigned char>(hotkey.get_code());
            activationHotkey.isShown = true;
        }

        std::vector<MuteShortcut> shortcuts;

        if (properties.HasKey(JSON_KEY_MUTE_SHORTCUTS))
        {
            const auto entries = properties.GetNamedObject(JSON_KEY_MUTE_SHORTCUTS).GetNamedArray(JSON_KEY_VALUE);

            for (const auto& entry : entries)
            {
                const auto object = entry.GetObjectW();

                const std::wstring program = std::wstring(object.GetNamedString(JSON_KEY_PROGRAM));
                if (program.empty())
                {
                    continue;
                }

                MuteShortcut shortcut;
                shortcut.program = program;
                shortcut.name = std::wstring(object.GetNamedString(JSON_KEY_NAME, program));
                shortcut.hotkey = ParseHotkey(object.GetNamedObject(JSON_KEY_SHORTCUT));

                shortcuts.push_back(shortcut);
            }
        }

        m_activationHotkey = activationHotkey;
        m_muteShortcuts = std::move(shortcuts);
    }

    // Reads a shortcut out of the settings.
    static Hotkey ParseHotkey(const json::JsonObject& object)
    {
        Hotkey hotkey;

        try
        {
            hotkey.win = object.GetNamedBoolean(JSON_KEY_WIN);
            hotkey.ctrl = object.GetNamedBoolean(JSON_KEY_CTRL);
            hotkey.shift = object.GetNamedBoolean(JSON_KEY_SHIFT);
            hotkey.alt = object.GetNamedBoolean(JSON_KEY_ALT);
            hotkey.key = static_cast<unsigned char>(object.GetNamedNumber(JSON_KEY_CODE));
        }
        catch (...)
        {
            Logger::warn(L"Sound Manager could not read a shortcut out of the settings");
            return {};
        }

        hotkey.isShown = true;
        return hotkey;
    }

    void LoadSettings()
    {
        try
        {
            ParseSettings(PowerToysSettings::PowerToyValues::load_from_settings_file(MODULE_KEY));
        }
        catch (...)
        {
            Logger::warn(L"Sound Manager could not read its settings, using the default ones");
        }
    }

    void StartMuteWorker()
    {
        {
            std::lock_guard lock(m_muteMutex);
            m_muteStopping = false;
            m_muteQueue.clear();
        }

        m_muteThread = std::thread([this] {
            CoInitializeEx(nullptr, COINIT_MULTITHREADED);

            {
                AudioEngine engine;
                engine.Initialize();

                std::unique_lock lock(m_muteMutex);
                while (!m_muteStopping)
                {
                    m_muteSignal.wait(lock, [this] { return m_muteStopping || !m_muteQueue.empty(); });

                    if (m_muteStopping)
                    {
                        break;
                    }

                    const MuteRequest request = m_muteQueue.front();
                    m_muteQueue.pop_front();
                    lock.unlock();

                    if (engine.ToggleApplicationMute(request.program))
                    {
                        Logger::trace(L"Sound Manager silenced {} with the shortcut of its own", request.name);
                    }
                    else
                    {
                        Logger::trace(L"Sound Manager found no sound of {} to silence", request.name);
                    }

                    lock.lock();
                }
            }

            // The engine is gone by now, so everything it held is released while the apartment is still there.
            CoUninitialize();
        });
    }

    void StopMuteWorker()
    {
        {
            std::lock_guard lock(m_muteMutex);
            m_muteStopping = true;
        }

        m_muteSignal.notify_all();

        if (m_muteThread.joinable())
        {
            m_muteThread.join();
        }
    }

    void QueueMute(const std::wstring& program, const std::wstring& name)
    {
        {
            std::lock_guard lock(m_muteMutex);
            m_muteQueue.push_back({ program, name });
        }

        m_muteSignal.notify_one();
    }

public:
    SoundManagerModuleInterface()
    {
        LoggerHelpers::init_logger(MODULE_KEY, L"ModuleInterface", LogSettings::soundManagerLoggerName);

        // The pipe name carries the runner process id so that a development build and an installed copy of PowerToys never talk to each other.
        m_pipeName = std::format(L"PowerToys-SoundManager-{}", GetCurrentProcessId());

        LoadSettings();
    }

    virtual void destroy() override
    {
        Logger::trace(L"SoundManagerModuleInterface::destroy()");

        disable();
        delete this;
    }

    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    virtual const wchar_t* get_key() override
    {
        return MODULE_KEY;
    }

    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredSoundManagerEnabledValue();
    }

    virtual bool is_enabled_by_default() const override
    {
        return false;
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(MODULE_DESCRIPTION);
        settings.set_overview_link(L"https://aka.ms/powertoys");

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            ParseSettings(PowerToysSettings::PowerToyValues::from_json_string(config, MODULE_KEY));
        }
        catch (...)
        {
            Logger::warn(L"Sound Manager could not read the updated settings");
        }
    }

    virtual void call_custom_action(const wchar_t* action) override
    {
        if (action == nullptr || !m_enabled)
        {
            return;
        }

        try
        {
            PowerToysSettings::CustomActionObject action_object =
                PowerToysSettings::CustomActionObject::from_json_string(action);
            if (action_object.get_name() == L"open-sound-manager")
            {
                ShowMixerWindow();
            }
        }
        catch (...)
        {
            Logger::warn(L"Sound Manager received an action it does not understand");
        }
    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    virtual void enable() override
    {
        Logger::trace(L"SoundManagerModuleInterface::enable()");

        if (m_enabled)
        {
            return;
        }

        m_pipeServer = std::make_unique<PipeServer>(m_pipeName);
        m_pipeServer->Start();

        StartMuteWorker();

        m_enabled = true;
        Logger::trace(L"Sound Manager enabled");
    }

    virtual void disable() override
    {
        Logger::trace(L"SoundManagerModuleInterface::disable()");

        if (!m_enabled)
        {
            return;
        }

        if (m_pipeServer)
        {
            m_pipeServer->Stop();
            m_pipeServer.reset();
        }

        StopMuteWorker();

        if (m_hProcess != nullptr)
        {
            // The window is closed straight away. Waiting for it would hold up the runner, which is the thread that switches the module off.
            TerminateProcess(m_hProcess, 1);
            CloseHandle(m_hProcess);
            m_hProcess = nullptr;
            m_appProcessId = 0;
        }

        m_enabled = false;
        Logger::trace(L"Sound Manager disabled");
    }

    virtual size_t get_hotkeys(Hotkey* hotkeys, size_t buffer_size) override
    {
        std::lock_guard lock(m_muteShortcutsMutex);

        // The first one opens the window, the ones after it are the shortcuts of the programs.
        const size_t count = 1 + m_muteShortcuts.size();

        if (hotkeys && buffer_size >= count)
        {
            hotkeys[0] = m_activationHotkey;

            for (size_t i = 0; i < m_muteShortcuts.size(); i++)
            {
                hotkeys[i + 1] = m_muteShortcuts[i].hotkey;
            }
        }

        return count;
    }

    virtual bool on_hotkey(size_t hotkeyId) override
    {
        if (!m_enabled)
        {
            return false;
        }

        if (hotkeyId == 0)
        {
            ShowMixerWindow();
            return true;
        }

        const size_t index = hotkeyId - 1;

        MuteShortcut shortcut;
        {
            std::lock_guard lock(m_muteShortcutsMutex);
            if (index >= m_muteShortcuts.size())
            {
                return false;
            }

            shortcut = m_muteShortcuts[index];
        }

        QueueMute(shortcut.program, shortcut.name);
        return true;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new SoundManagerModuleInterface();
}
