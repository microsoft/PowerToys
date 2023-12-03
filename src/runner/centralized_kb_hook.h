#include "pch.h"

#include "../modules/interface/powertoy_module_interface.h"

namespace CentralizedKeyboardHook
{
    struct LocalKey
    {
        bool win;
        bool control;
        bool shift;
        bool alt;

        bool l_win;
        bool l_control;
        bool l_shift;
        bool l_alt;

        bool r_win;
        bool r_control;
        bool r_shift;
        bool r_alt;

        DWORD key;
    };

    using Hotkey = PowertoyModuleIface::Hotkey;

    void Start() noexcept;
    void Stop() noexcept;
    void SetHotkeyAction(const std::wstring& moduleName, const Hotkey& hotkey, std::function<bool()>&& action) noexcept;
    void HandleCreateProcessHotKeysAndChords(LocalKey hotkey);
    void AddPressedKeyAction(const std::wstring& moduleName, const DWORD vk, const UINT milliseconds, std::function<bool()>&& action) noexcept;
    void ClearModuleHotkeys(const std::wstring& moduleName) noexcept;
    void RegisterWindow(HWND hwnd) noexcept;
    void RefreshConfig();
    void SetRunProgramEnabled(bool enabled);
    DWORD GetProcessIdByName(const std::wstring& processName);
    std::wstring GetFileNameFromPath(const std::wstring& fullPath);
    HWND find_main_window(unsigned long process_id);
    BOOL CALLBACK enum_windows_callback(HWND handle, LPARAM lParam);
};
