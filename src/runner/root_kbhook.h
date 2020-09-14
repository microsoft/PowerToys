#include "pch.h"

#include "../modules/interface/powertoy_module_interface.h"

namespace RootKeyboardHook
{
    using Hotkey = PowertoyModuleIface::Hotkey;

    void Start() noexcept;
    void Stop() noexcept;
    void SetHotkeyAction(const Hotkey& hotkey, std::function<void()>&& action) noexcept;
    void ClearHotkeyAction(const Hotkey& hotkey) noexcept;
};
