#include "pch.h"
#include "powertoy_module.h"

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
        winrt::throw_hresult(winrt::hresult(E_POINTER));
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
}
