#include "pch.h"

#include "../modules/interface/powertoy_module_interface.h"

namespace CentralizedKeyboardHook
{
    using Hotkey = PowertoyModuleIface::Hotkey;

    void Start() noexcept;
    void Stop() noexcept;
    void SetHotkeyAction(const std::wstring& moduleName, const Hotkey& hotkey, std::function<bool()>&& action) noexcept;
    void AddPressedKeyAction(const std::wstring& moduleName, const DWORD vk, const UINT milliseconds, std::function<bool()>&& action) noexcept;
    void ClearModuleHotkeys(const std::wstring& moduleName) noexcept;
    void RegisterWindow(HWND hwnd) noexcept;
};
