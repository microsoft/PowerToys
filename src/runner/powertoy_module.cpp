#include "pch.h"
#include "powertoy_module.h"
#include "centralized_kb_hook.h"

std::map<std::wstring, PowertoyModule>& modules()
{
    static std::map<std::wstring, PowertoyModule> modules;
    return modules;
}

PowertoyModule load_powertoy(const std::wstring_view filename)
{
    auto handle = winrt::check_pointer(LoadLibraryW(filename.data()));
    auto create = reinterpret_cast<powertoy_create_func>(GetProcAddress(handle, "powertoy_create"));
    if (!create)
    {
        FreeLibrary(handle);
        winrt::throw_last_error();
    }
    auto module = create();
    if (!module)
    {
        FreeLibrary(handle);
        winrt::throw_last_error();
    }
    return PowertoyModule(module, handle);
}

json::JsonObject PowertoyModule::json_config() const
{
    int size = 0;
    module->get_config(nullptr, &size);
    std::wstring result;
    result.resize(size - 1);
    module->get_config(result.data(), &size);
    return json::JsonObject::Parse(result);
}

PowertoyModule::PowertoyModule(PowertoyModuleIface* module, HMODULE handle) :
    handle(handle), module(module)
{
    if (!module)
    {
        throw std::runtime_error("Module not initialized");
    }

    CentralizedKeyboardHook::ClearModuleHotkeys(module->get_name());

    int hotkeyCount = module->get_hotkeys(nullptr, 0);
    std::vector<PowertoyModuleIface::Hotkey> hotkeys(hotkeyCount);
    module->get_hotkeys(hotkeys.data(), hotkeyCount);
    
    for (int i = 0; i < hotkeyCount; i++)
    {
        CentralizedKeyboardHook::SetHotkeyAction(module->get_name(), hotkeys[i], [module, i] {
            module->on_hotkey(i);
            return true;
        });
    }
}
