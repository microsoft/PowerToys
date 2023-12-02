#include "pch.h"
#include "centralized_kb_hook.h"
#include <common/debug_control.h>
#include <common/utils/winapi_error.h>
#include <common/logger/logger.h>
#include <common/interop/shared_constants.h>
#include <modules/keyboardmanager/common/ModifierKey.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <modules/keyboardmanager/common/KeyboardManagerConstants.h>

// For GetProcessIdByName
#include <windows.h>
#include <tlhelp32.h>
#include <iostream>
#include <string>
#include <filesystem>

namespace CentralizedKeyboardHook
{
    

    class RunProgramSpec2
    {
    private:
        // Function to split a wstring based on a delimiter and return a vector of split strings
       
        std::vector<std::wstring> splitwstring(const std::wstring& input, wchar_t delimiter);

        inline auto comparator() const
        {
            return std::make_tuple(winKey, ctrlKey, altKey, shiftKey, actionKey);
        }

        std::vector<std::wstring> RunProgramSpec2::splitwstringOnChar(const std::wstring& input, wchar_t delimiter)
        {
            std::wstringstream ss(input);
            std::wstring item;
            std::vector<std::wstring> splittedStrings;
            while (std::getline(ss, item, delimiter))
            {
                splittedStrings.push_back(item);
            }

            return splittedStrings;
        }

        bool RunProgramSpec2::SetKey(const DWORD input)
        {
            // Since there isn't a key for a common Win key we use the key code defined by us
            if (input == CommonSharedConstants::VK_WIN_BOTH)
            {
                if (winKey == ModifierKey::Both)
                {
                    return false;
                }
                winKey = ModifierKey::Both;
            }
            else if (input == VK_LWIN)
            {
                if (winKey == ModifierKey::Left)
                {
                    return false;
                }
                winKey = ModifierKey::Left;
            }
            else if (input == VK_RWIN)
            {
                if (winKey == ModifierKey::Right)
                {
                    return false;
                }
                winKey = ModifierKey::Right;
            }
            else if (input == VK_LCONTROL)
            {
                if (ctrlKey == ModifierKey::Left)
                {
                    return false;
                }
                ctrlKey = ModifierKey::Left;
            }
            else if (input == VK_RCONTROL)
            {
                if (ctrlKey == ModifierKey::Right)
                {
                    return false;
                }
                ctrlKey = ModifierKey::Right;
            }
            else if (input == VK_CONTROL)
            {
                if (ctrlKey == ModifierKey::Both)
                {
                    return false;
                }
                ctrlKey = ModifierKey::Both;
            }
            else if (input == VK_LMENU)
            {
                if (altKey == ModifierKey::Left)
                {
                    return false;
                }
                altKey = ModifierKey::Left;
            }
            else if (input == VK_RMENU)
            {
                if (altKey == ModifierKey::Right)
                {
                    return false;
                }
                altKey = ModifierKey::Right;
            }
            else if (input == VK_MENU)
            {
                if (altKey == ModifierKey::Both)
                {
                    return false;
                }
                altKey = ModifierKey::Both;
            }
            else if (input == VK_LSHIFT)
            {
                if (shiftKey == ModifierKey::Left)
                {
                    return false;
                }
                shiftKey = ModifierKey::Left;
            }
            else if (input == VK_RSHIFT)
            {
                if (shiftKey == ModifierKey::Right)
                {
                    return false;
                }
                shiftKey = ModifierKey::Right;
            }
            else if (input == VK_SHIFT)
            {
                if (shiftKey == ModifierKey::Both)
                {
                    return false;
                }
                shiftKey = ModifierKey::Both;
            }
            else
            {
                if (actionKey == input)
                {
                    return false;
                }
                actionKey = input;
            }

            return true;
        }

        std::vector<std::wstring> splitwStringOnString(const std::wstring& str, const std::wstring& delimiter, bool ignoreEmpty)
        {
            std::vector<std::wstring> tokens;
            size_t prev = 0, pos = 0;

            while (pos < str.length() && prev < str.length())
            {
                pos = str.find(delimiter, prev);
                if (pos == std::wstring::npos)
                {
                    pos = str.length();
                }

                std::wstring token = str.substr(prev, pos - prev);
                if (!token.empty() || !ignoreEmpty)
                {
                    tokens.push_back(token);
                }

                prev = pos + delimiter.length();

                if (prev == str.length() && !ignoreEmpty)
                {
                    token = str.substr(prev, pos - prev);
                    tokens.push_back(token);
                }
            }

            return tokens;
        }

    public:

        ModifierKey winKey = ModifierKey::Disabled;
        ModifierKey ctrlKey = ModifierKey::Disabled;
        ModifierKey altKey = ModifierKey::Disabled;
        ModifierKey shiftKey = ModifierKey::Disabled;

        std::wstring path = L"";
        std::wstring args = L"";
        std::wstring dir = L"";

        std::vector<DWORD> keys;
        DWORD actionKey = {};

        RunProgramSpec2::RunProgramSpec2(const std::wstring& shortcutVK, const std::wstring& targetAppSpec) :
            winKey(ModifierKey::Disabled), ctrlKey(ModifierKey::Disabled), altKey(ModifierKey::Disabled), shiftKey(ModifierKey::Disabled), actionKey(NULL)
        {
            auto _keys = splitwstringOnChar(shortcutVK, ';');
            for (auto it : _keys)
            {
                auto vkKeyCode = std::stoul(it);
                SetKey(vkKeyCode);
            }

            //auto KBM_RUN_PROGRAM_DELIMITER = L"<|||>";
            //auto targetParts = splitwStringOnString(targetAppSpec, KBM_RUN_PROGRAM_DELIMITER, false);

            path = targetAppSpec;
            //args = targetParts[1];
            //dir = targetParts[2];
        }
    };

    struct HotkeyDescriptor
    {
        Hotkey hotkey;
        std::wstring moduleName;
        std::function<bool()> action;

        bool operator<(const HotkeyDescriptor& other) const
        {
            return hotkey < other.hotkey;
        };
    };

    std::multiset<HotkeyDescriptor> hotkeyDescriptors;
    std::mutex mutex;
    HHOOK hHook{};

    // To store information about handling pressed keys.
    struct PressedKeyDescriptor
    {
        DWORD virtualKey; // Virtual Key code of the key we're keeping track of.
        std::wstring moduleName;
        std::function<bool()> action;
        UINT_PTR idTimer; // Timer ID for calling SET_TIMER with.
        UINT millisecondsToPress; // How much time the key must be pressed.
        bool operator<(const PressedKeyDescriptor& other) const
        {
            // We'll use the virtual key as the real key, since looking for a hit with the key is done in the more time sensitive path (low level keyboard hook).
            return virtualKey < other.virtualKey;
        };
    };
    std::multiset<PressedKeyDescriptor> pressedKeyDescriptors;
    std::mutex pressedKeyMutex;
    long lastKeyInChord = 0;

    // keep track of last pressed key, to detect repeated keys and if there are more keys pressed.
    const DWORD VK_DISABLED = CommonSharedConstants::VK_DISABLED;
    DWORD vkCodePressed = VK_DISABLED;

    // Save the runner window handle for registering timers.
    HWND runnerWindow;

    struct DestroyOnExit
    {
        ~DestroyOnExit()
        {
            Stop();
        }
    } destroyOnExitObj;

    // Handle the pressed key proc
    void PressedKeyTimerProc(
        HWND hwnd,
        UINT /*message*/,
        UINT_PTR idTimer,
        DWORD /*dwTime*/)
    {
        std::multiset<PressedKeyDescriptor> copy;
        {
            // Make a copy, to look for the action to call.
            std::unique_lock lock{ pressedKeyMutex };
            copy = pressedKeyDescriptors;
        }
        for (const auto& it : copy)
        {
            if (it.idTimer == idTimer)
            {
                it.action();
            }
        }

        KillTimer(hwnd, idTimer);
    }

    LRESULT CALLBACK KeyboardHookProc(_In_ int nCode, _In_ WPARAM wParam, _In_ LPARAM lParam)
    {
        if (nCode < 0)
        {
            return CallNextHookEx(hHook, nCode, wParam, lParam);
        }

        const auto& keyPressInfo = *reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);

        if (keyPressInfo.dwExtraInfo == PowertoyModuleIface::CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG)
        {
            // The new keystroke was generated from one of our actions. We should pass it along.
            return CallNextHookEx(hHook, nCode, wParam, lParam);
        }

        // Check if the keys are pressed.
        if (!pressedKeyDescriptors.empty())
        {
            bool wasKeyPressed = vkCodePressed != VK_DISABLED;
            // Hold the lock for the shortest possible duration
            if ((wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
            {
                if (!wasKeyPressed)
                {
                    // If no key was pressed before, let's start a timer to take into account this new key.
                    std::unique_lock lock{ pressedKeyMutex };
                    PressedKeyDescriptor dummy{ .virtualKey = keyPressInfo.vkCode };
                    auto [it, last] = pressedKeyDescriptors.equal_range(dummy);
                    for (; it != last; ++it)
                    {
                        SetTimer(runnerWindow, it->idTimer, it->millisecondsToPress, PressedKeyTimerProc);
                    }
                }
                else if (vkCodePressed != keyPressInfo.vkCode)
                {
                    // If a different key was pressed, let's clear the timers we have started for the previous key.
                    std::unique_lock lock{ pressedKeyMutex };
                    PressedKeyDescriptor dummy{ .virtualKey = vkCodePressed };
                    auto [it, last] = pressedKeyDescriptors.equal_range(dummy);
                    for (; it != last; ++it)
                    {
                        KillTimer(runnerWindow, it->idTimer);
                    }
                }
                vkCodePressed = keyPressInfo.vkCode;
            }
            if (wParam == WM_KEYUP || wParam == WM_SYSKEYUP)
            {
                std::unique_lock lock{ pressedKeyMutex };
                PressedKeyDescriptor dummy{ .virtualKey = keyPressInfo.vkCode };
                auto [it, last] = pressedKeyDescriptors.equal_range(dummy);
                for (; it != last; ++it)
                {
                    KillTimer(runnerWindow, it->idTimer);
                }
                vkCodePressed = 0x100;
            }
        }

        if ((wParam != WM_KEYDOWN) && (wParam != WM_SYSKEYDOWN))
        {
            return CallNextHookEx(hHook, nCode, wParam, lParam);
        }

        LocalKey lHotkey;

        lHotkey.win = (GetAsyncKeyState(VK_LWIN) & 0x8000) || (GetAsyncKeyState(VK_RWIN) & 0x8000);
        lHotkey.control = static_cast<bool>(GetAsyncKeyState(VK_CONTROL) & 0x8000);
        lHotkey.shift = static_cast<bool>(GetAsyncKeyState(VK_SHIFT) & 0x8000);
        lHotkey.alt = static_cast<bool>(GetAsyncKeyState(VK_MENU) & 0x8000);

        lHotkey.l_win = (GetAsyncKeyState(VK_LWIN) & 0x8000);
        lHotkey.l_control = static_cast<bool>(GetAsyncKeyState(VK_LCONTROL) & 0x8000);
        lHotkey.l_shift = static_cast<bool>(GetAsyncKeyState(VK_LSHIFT) & 0x8000);
        lHotkey.l_alt = static_cast<bool>(GetAsyncKeyState(VK_LMENU) & 0x8000);

        lHotkey.r_win = (GetAsyncKeyState(VK_RWIN) & 0x8000);
        lHotkey.r_control = static_cast<bool>(GetAsyncKeyState(VK_RCONTROL) & 0x8000);
        lHotkey.r_shift = static_cast<bool>(GetAsyncKeyState(VK_RSHIFT) & 0x8000);
        lHotkey.r_alt = static_cast<bool>(GetAsyncKeyState(VK_RMENU) & 0x8000);

        lHotkey.key = static_cast<unsigned char>(keyPressInfo.vkCode);

        Hotkey hotkey{
            .win = lHotkey.win,
            .ctrl = lHotkey.control,
            .shift = lHotkey.shift,
            .alt = lHotkey.alt,
            .key = static_cast<UCHAR>(lHotkey.key)
        };

        HandleCreateProcessHotKeysAndChords(lHotkey);

        std::function<bool()> action;
        {
            // Hold the lock for the shortest possible duration
            std::unique_lock lock{ mutex };
            HotkeyDescriptor dummy{ .hotkey = hotkey };
            auto it = hotkeyDescriptors.find(dummy);
            if (it != hotkeyDescriptors.end())
            {
                action = it->action;
            }
        }

        if (action)
        {
            if (action())
            {
                // After invoking the hotkey send a dummy key to prevent Start Menu from activating
                INPUT dummyEvent[1] = {};
                dummyEvent[0].type = INPUT_KEYBOARD;
                dummyEvent[0].ki.wVk = 0xFF;
                dummyEvent[0].ki.dwFlags = KEYEVENTF_KEYUP;
                SendInput(1, dummyEvent, sizeof(INPUT));

                // Swallow the key press
                return 1;
            }
        }

        return CallNextHookEx(hHook, nCode, wParam, lParam);
    }

    bool getConfigInit = false;
    bool runProgramEnabled = true;
    std::vector<RunProgramSpec2> runProgramSpecs;

    void SetRunProgramEnabled(bool enabled)
    {
        runProgramEnabled = enabled;
    }

    void RefreshConfig()
    {
        getConfigInit = false;
        runProgramSpecs.clear();
    }

    void setupConfig()
    {
        if (!getConfigInit)
        {
            PowerToysSettings::PowerToyValues settings = PowerToysSettings::PowerToyValues::load_from_settings_file(KeyboardManagerConstants::ModuleName);
            auto current_config = settings.get_string_value(KeyboardManagerConstants::ActiveConfigurationSettingName);

            if (!current_config)
            {
                return;
            }

            auto jsonData = json::from_file(PTSettingsHelper::get_module_save_folder_location(KeyboardManagerConstants::ModuleName) + L"\\" + *current_config + L".json");

            if (!jsonData)
            {
                return;
            }

            auto keyboardManagerConfig = jsonData->GetNamedObject(KeyboardManagerConstants::RemapShortcutsSettingName);
            /*
            
                        auto newRemapText = it.GetObjectW().GetNamedString(KeyboardManagerConstants::NewTextSettingName, {});
                        auto isRunProgram = it.GetObjectW().GetNamedBoolean(KeyboardManagerConstants::IsRunProgramSettingName, false);

                        if (isRunProgram)
                        {
                            while (!true)
                            {
                                // debugger wait
                                Sleep(1000);
                            }
                            
                            
                            */
            if (keyboardManagerConfig)
            {
                auto global = keyboardManagerConfig.GetNamedArray(KeyboardManagerConstants::GlobalRemapShortcutsSettingName);
                for (const auto& it : global)
                {
                    try
                    {
                        auto originalKeys = it.GetObjectW().GetNamedString(KeyboardManagerConstants::OriginalKeysSettingName);
                        auto isRunProgram = it.GetObjectW().GetNamedBoolean(KeyboardManagerConstants::IsRunProgramSettingName, false);

                        if (isRunProgram)
                        {
                            auto runProgram = it.GetObjectW().GetNamedString(KeyboardManagerConstants::RunProgramSettingName, L"");
                            auto runProgramSpec = RunProgramSpec2(originalKeys.c_str(), runProgram.c_str());
                            runProgramSpecs.push_back(runProgramSpec);
                        }
                    }
                    catch (...)
                    {
                        Logger::error(L"Improper Key Data JSON. Try the next remap.");
                    }
                }
            }

            getConfigInit = true;
        }
    }

    bool isPartOfAnyRunProgramSpec2(DWORD key)
    {
        for (RunProgramSpec2 runProgramSpec : runProgramSpecs)
        {
            if (runProgramSpec.actionKey == key)
            {
                return true;
            }

            /*for (unsigned char c : runProgramSpec.keys)
            {
                if (c == key)
                {
                    return true;
                }
            }*/
        }
        return false;
    }

    bool isPartOfThisRunProgramSpec2(RunProgramSpec2 runProgramSpec, DWORD key)
    {
        for (DWORD c : runProgramSpec.keys)
        {
            if (c == key)
            {
                return true;
            }
        }
        return false;
    }

    void HandleCreateProcessHotKeysAndChords(LocalKey hotkey)
    {        

        if (!runProgramEnabled)
        {
            return;
        }

        if (hotkey.win || hotkey.shift || hotkey.control || hotkey.alt)
        {
            setupConfig();

            if (!isPartOfAnyRunProgramSpec2(hotkey.key))
            {
                lastKeyInChord = 0;
                return;
            }
        }
        else
        {
            return;
        }

        for (RunProgramSpec2 runProgramSpec : runProgramSpecs)
        {
            if (
                (runProgramSpec.winKey == ModifierKey::Disabled || (runProgramSpec.winKey == ModifierKey::Left && hotkey.l_win)) && (runProgramSpec.shiftKey == ModifierKey::Disabled || (runProgramSpec.shiftKey == ModifierKey::Left && hotkey.l_shift)) && (runProgramSpec.altKey == ModifierKey::Disabled || (runProgramSpec.altKey == ModifierKey::Left && hotkey.l_alt)) && (runProgramSpec.ctrlKey == ModifierKey::Disabled || (runProgramSpec.ctrlKey == ModifierKey::Left && hotkey.l_control)))
            {
                auto runProgram = false;

                if (runProgramSpec.actionKey == hotkey.key)
                {
                    runProgram = true;
                }

                /*if (runProgramSpec.keys.size() == 1 && runProgramSpec.keys[0] == hotkey.key)
                {
                    runProgram = true;
                }
                else if (runProgramSpec.keys.size() == 2 && runProgramSpec.keys[0] == lastKeyInChord && runProgramSpec.keys[1] == hotkey.key)
                {
                    runProgram = true;
                }
                else
                {
                    lastKeyInChord = hotkey.key;
                }*/

                if (runProgram)
                {
                    Logger::trace(L"runProgram {}", runProgram);
                    lastKeyInChord = 0;

                    if (runProgramSpec.path.compare(L"RefreshConfig") == 0)
                    {
                        RefreshConfig();
                    }
                    else
                    {
                        auto fileNamePart = GetFileNameFromPath(runProgramSpec.path);
                        DWORD targetPid = 0;

                        if (fileNamePart != L"explorer.exe" && fileNamePart != L"powershell.exe" && fileNamePart != L"cmd.exe")
                        {
                            targetPid = GetProcessIdByName(fileNamePart);
                        }

                        if (targetPid != 0)
                        {
                            // a good place to look for this...
                            // https://github.com/ritchielawrence/cmdow

                            // try by main window.
                            HWND hwnd = find_main_window(targetPid);
                            if (hwnd != NULL)
                            {
                                if (hwnd == GetForegroundWindow())
                                {
                                    ShowWindow(hwnd, SW_MINIMIZE);
                                    return;
                                }
                                else
                                {
                                    ShowWindow(hwnd, SW_RESTORE);

                                    if (!SetForegroundWindow(hwnd))
                                    {
                                        auto errorCode = GetLastError();
                                        Logger::error(L"SetForegroundWindow", errorCode, L"PowerToys - Interop");
                                    }
                                    else
                                    {
                                        return;
                                    }
                                }
                            }

                            // try by console.
                            hwnd = FindWindow(nullptr, nullptr);
                            if (AttachConsole(targetPid))
                            {
                                // Get the console window handle
                                hwnd = GetConsoleWindow();
                                auto showByConsoleSuccess = false;
                                if (hwnd != NULL)
                                {
                                    ShowWindow(hwnd, SW_RESTORE);

                                    if (!SetForegroundWindow(hwnd))
                                    {
                                        auto errorCode = GetLastError();
                                        Logger::error(L"SetForegroundWindow", errorCode, L"PowerToys - Interop");
                                    }
                                    else
                                    {
                                        showByConsoleSuccess = true;
                                    }
                                }

                                // Detach from the console
                                FreeConsole();
                                if (showByConsoleSuccess)
                                {
                                    return;
                                }
                            }

                            // try to just show them all (if they have a title)!.
                            hwnd = FindWindow(nullptr, nullptr);

                            while (hwnd)
                            {
                                DWORD pidForHwnd;
                                GetWindowThreadProcessId(hwnd, &pidForHwnd);
                                if (targetPid == pidForHwnd)
                                {
                                    int length = GetWindowTextLength(hwnd);

                                    if (length > 0)
                                    {
                                        ShowWindow(hwnd, SW_RESTORE);

                                        // hwnd is the window handle with targetPid
                                        if (!SetForegroundWindow(hwnd))
                                        {
                                            auto errorCode = GetLastError();
                                            Logger::error(L"SetForegroundWindow", errorCode, L"PowerToys - Interop");
                                        }
                                    }
                                }
                                hwnd = FindWindowEx(NULL, hwnd, NULL, NULL);
                            }
                        }
                        else
                        {
                            std::wstring executable_and_args = fmt::format(L"\"{}\" {}", runProgramSpec.path, runProgramSpec.args);
                            STARTUPINFO startup_info = { sizeof(startup_info) };
                            PROCESS_INFORMATION process_info = { 0 };

                            auto currentDir = runProgramSpec.dir.c_str();
                            if (runProgramSpec.dir == L"")
                            {
                                currentDir = nullptr;
                            }

                            CreateProcessW(nullptr, executable_and_args.data(), nullptr, nullptr, FALSE, 0, nullptr, currentDir, &startup_info, &process_info);
                        }
                    }
                }
            }
        }
    }

    struct handle_data
    {
        unsigned long process_id;
        HWND window_handle;
    };

    HWND find_main_window(unsigned long process_id)
    {
        handle_data data;
        data.process_id = process_id;
        data.window_handle = 0;
        EnumWindows(enum_windows_callback, reinterpret_cast<LPARAM>(&data));
        return data.window_handle;
    }

    BOOL CALLBACK enum_windows_callback(HWND handle, LPARAM lParam)
    {
        //handle_data& data = *(handle_data*)lParam;
        handle_data& data = *reinterpret_cast<handle_data*>(lParam);
        unsigned long process_id = 0;
        GetWindowThreadProcessId(handle, &process_id);
        if (data.process_id != process_id || !(GetWindow(handle, GW_OWNER) == static_cast<HWND>(0) && IsWindowVisible(handle)))
            return TRUE;
        data.window_handle = handle;
        return FALSE;
    }

    std::wstring GetFileNameFromPath(const std::wstring& fullPath)
    {
        size_t found = fullPath.find_last_of(L"\\");
        if (found != std::wstring::npos)
        {
            return fullPath.substr(found + 1);
        }
        return fullPath;
    }

    DWORD GetProcessIdByName(const std::wstring& processName)
    {
        DWORD pid = 0;
        HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);

        if (snapshot != INVALID_HANDLE_VALUE)
        {
            PROCESSENTRY32 processEntry;
            processEntry.dwSize = sizeof(PROCESSENTRY32);

            if (Process32First(snapshot, &processEntry))
            {
                do
                {
                    if (_wcsicmp(processEntry.szExeFile, processName.c_str()) == 0)
                    {
                        pid = processEntry.th32ProcessID;
                        break;
                    }
                } while (Process32Next(snapshot, &processEntry));
            }

            CloseHandle(snapshot);
        }

        return pid;
    }

    void SetHotkeyAction(const std::wstring& moduleName, const Hotkey& hotkey, std::function<bool()>&& action) noexcept
    {
        Logger::trace(L"Register hotkey action for {}", moduleName);
        std::unique_lock lock{ mutex };
        hotkeyDescriptors.insert({ .hotkey = hotkey, .moduleName = moduleName, .action = std::move(action) });
    }

    void AddPressedKeyAction(const std::wstring& moduleName, const DWORD vk, const UINT milliseconds, std::function<bool()>&& action) noexcept
    {
        // Calculate a unique TimerID.
        auto hash = std::hash<std::wstring>{}(moduleName); // Hash the module as the upper part of the timer ID.
        const UINT upperId = hash & 0xFFFF;
        const UINT lowerId = vk & 0xFFFF; // The key to press can be the lower ID.
        const UINT timerId = upperId << 16 | lowerId;
        std::unique_lock lock{ pressedKeyMutex };
        pressedKeyDescriptors.insert({ .virtualKey = vk, .moduleName = moduleName, .action = std::move(action), .idTimer = timerId, .millisecondsToPress = milliseconds });
    }

    void ClearModuleHotkeys(const std::wstring& moduleName) noexcept
    {
        Logger::trace(L"UnRegister hotkey action for {}", moduleName);
        {
            std::unique_lock lock{ mutex };
            auto it = hotkeyDescriptors.begin();
            while (it != hotkeyDescriptors.end())
            {
                if (it->moduleName == moduleName)
                {
                    it = hotkeyDescriptors.erase(it);
                }
                else
                {
                    ++it;
                }
            }
        }
        {
            std::unique_lock lock{ pressedKeyMutex };
            auto it = pressedKeyDescriptors.begin();
            while (it != pressedKeyDescriptors.end())
            {
                if (it->moduleName == moduleName)
                {
                    it = pressedKeyDescriptors.erase(it);
                }
                else
                {
                    ++it;
                }
            }
        }
    }

    void Start() noexcept
    {
#if defined(DISABLE_LOWLEVEL_HOOKS_WHEN_DEBUGGED)
        const bool hook_disabled = IsDebuggerPresent();
#else
        const bool hook_disabled = false;
#endif
        if (!hook_disabled)
        {
            if (!hHook)
            {
                hHook = SetWindowsHookExW(WH_KEYBOARD_LL, KeyboardHookProc, NULL, NULL);
                if (!hHook)
                {
                    DWORD errorCode = GetLastError();
                    show_last_error_message(L"SetWindowsHookEx", errorCode, L"centralized_kb_hook");
                }
            }
        }
    }

    void Stop() noexcept
    {
        if (hHook && UnhookWindowsHookEx(hHook))
        {
            hHook = NULL;
        }
    }

    void RegisterWindow(HWND hwnd) noexcept
    {
        runnerWindow = hwnd;
    }
}
