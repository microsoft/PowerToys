#pragma once
#include <interface/powertoy_module_interface.h>
#include <string>
#include <memory>
#include <mutex>
#include <vector>
#include <functional>

#include <common/utils/json.h>

struct PowertoyModuleDeleter
{
    void operator()(PowertoyModuleIface* module) const
    {
        if (module)
        {
            module->destroy();
        }
    }
};

struct PowertoyModuleDLLDeleter
{
    using pointer = HMODULE;
    void operator()(HMODULE handle) const
    {
        FreeLibrary(handle);
    }
};

class PowertoyModule
{
public:
    PowertoyModule(PowertoyModuleIface* module, HMODULE handle);

    inline PowertoyModuleIface* operator->()
    {
        return module.get();
    }

    json::JsonObject json_config() const;

    void update_hotkeys();

private:
    std::unique_ptr<HMODULE, PowertoyModuleDLLDeleter> handle;
    std::unique_ptr<PowertoyModuleIface, PowertoyModuleDeleter> module;
};

PowertoyModule load_powertoy(const std::wstring_view filename);
std::map<std::wstring, PowertoyModule>& modules();
