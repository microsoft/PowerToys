#include "pch.h"

#include "../modules/interface/powertoy_module_interface.h"

namespace CentralizedKeyboardHook
{
    using Hotkey = PowertoyModuleIface::Hotkey;
    using HotkeyDisposition = PowertoyModuleIface::HotkeyDisposition;

    // Start() and Stop() must be called on the runner's main thread, which runs the queued
    // hotkey actions. Hotkeys can be registered before Start(). Call Stop() before unloading
    // modules: it drops the queued actions and joins the hook thread.
    void Start() noexcept;
    void Stop() noexcept;

    // shouldHandle runs on the keyboard hook thread and must return in microseconds.
    // invoke runs later on the runner's main thread, in FIFO order with the other actions.
    void SetHotkeyAction(const std::wstring& moduleName, const Hotkey& hotkey, std::function<HotkeyDisposition()>&& shouldHandle, std::function<void()>&& invoke) noexcept;

    // action runs on the runner's main thread once the key was held for the given time.
    void AddPressedKeyAction(const std::wstring& moduleName, const DWORD vk, const UINT milliseconds, std::function<void()>&& action) noexcept;
    void ClearPressedKeyActions(const std::wstring& moduleName) noexcept;
    void ClearModuleHotkeys(const std::wstring& moduleName) noexcept;
};