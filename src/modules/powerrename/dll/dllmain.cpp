#include "stdafx.h"
#include "PowerRenameExt.h"
#include <interface/powertoy_module_interface.h>
#include <settings.h>
#include <trace.h>
#include <common/settings_objects.h>
#include <common/common.h>
#include "resource.h"
#include <atomic>

std::atomic<DWORD> g_dwModuleRefCount = 0;
HINSTANCE g_hInst = 0;

extern "C" IMAGE_DOS_HEADER __ImageBase;

class CPowerRenameClassFactory : public IClassFactory
{
public:
    CPowerRenameClassFactory(_In_ REFCLSID clsid) :
        m_refCount(1),
        m_clsid(clsid)
    {
        ModuleAddRef();
    }

    // IUnknown methods
    IFACEMETHODIMP QueryInterface(_In_ REFIID riid, _COM_Outptr_ void** ppv)
    {
        static const QITAB qit[] = {
            QITABENT(CPowerRenameClassFactory, IClassFactory),
            { 0 }
        };
        return QISearch(this, qit, riid, ppv);
    }

    IFACEMETHODIMP_(ULONG)
    AddRef()
    {
        return ++m_refCount;
    }

    IFACEMETHODIMP_(ULONG)
    Release()
    {
        LONG refCount = --m_refCount;
        if (refCount == 0)
        {
            delete this;
        }
        return refCount;
    }

    // IClassFactory methods
    IFACEMETHODIMP CreateInstance(_In_opt_ IUnknown* punkOuter, _In_ REFIID riid, _Outptr_ void** ppv)
    {
        *ppv = NULL;
        HRESULT hr;
        if (punkOuter)
        {
            hr = CLASS_E_NOAGGREGATION;
        }
        else
        {
            if (m_clsid == CLSID_PowerRenameMenu)
            {
                hr = CPowerRenameMenu::s_CreateInstance(punkOuter, riid, ppv);
            }
            else
            {
                hr = CLASS_E_CLASSNOTAVAILABLE;
            }
        }
        return hr;
    }

    IFACEMETHODIMP LockServer(BOOL bLock)
    {
        if (bLock)
        {
            ModuleAddRef();
        }
        else
        {
            ModuleRelease();
        }
        return S_OK;
    }

private:
    ~CPowerRenameClassFactory()
    {
        ModuleRelease();
    }

    std::atomic<long> m_refCount;
    CLSID m_clsid;
};

BOOL WINAPI DllMain(HINSTANCE hInstance, DWORD dwReason, void*)
{
    switch (dwReason)
    {
    case DLL_PROCESS_ATTACH:
        g_hInst = hInstance;
        Trace::RegisterProvider();
        break;

    case DLL_PROCESS_DETACH:
        Trace::UnregisterProvider();
        break;
    }
    return TRUE;
}

//
// Checks if there are any external references to this module
//
STDAPI DllCanUnloadNow(void)
{
    return (g_dwModuleRefCount == 0) ? S_OK : S_FALSE;
}

//
// DLL export for creating COM objects
//
STDAPI DllGetClassObject(_In_ REFCLSID clsid, _In_ REFIID riid, _Outptr_ void** ppv)
{
    *ppv = NULL;
    HRESULT hr = E_OUTOFMEMORY;
    CPowerRenameClassFactory* pClassFactory = new CPowerRenameClassFactory(clsid);
    if (pClassFactory)
    {
        hr = pClassFactory->QueryInterface(riid, ppv);
        pClassFactory->Release();
    }
    return hr;
}

STDAPI DllRegisterServer()
{
    return S_OK;
}

STDAPI DllUnregisterServer()
{
    return S_OK;
}

void ModuleAddRef()
{
    g_dwModuleRefCount++;
}

void ModuleRelease()
{
    g_dwModuleRefCount--;
}

class PowerRenameModule : public PowertoyModuleIface
{
private:
    // Enabled by default
    bool m_enabled = true;
    std::wstring app_name;

public:
    // Return the display name of the powertoy, this will be cached
    virtual PCWSTR get_name() override
    {
        return app_name.c_str();
    }

    // Enable the powertoy
    virtual void enable()
    {
        m_enabled = true;
        save_settings();
    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
        save_settings();
    }

    // Returns if the powertoy is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Return array of the names of all events that this powertoy listens for, with
    // nullptr as the last element of the array. Nullptr can also be retured for empty list.
    virtual PCWSTR* get_events() override
    {
        return nullptr;
    }

    // Return JSON with the configuration options.
    // These are the settings shown on the settings page along with their current values.
    virtual bool get_config(_Out_ PWSTR buffer, _Out_ int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(GET_RESOURCE_STRING(IDS_SETTINGS_DESCRIPTION));
        settings.set_icon_key(L"pt-power-rename");

        // Link to the GitHub PowerRename sub-page
        settings.set_overview_link(GET_RESOURCE_STRING(IDS_OVERVIEW_LINK));

        settings.add_bool_toogle(
            L"bool_persist_input",
            GET_RESOURCE_STRING(IDS_RESTORE_SEARCH),
            CSettings::GetPersistState());

        settings.add_bool_toogle(
            L"bool_mru_enabled",
            GET_RESOURCE_STRING(IDS_ENABLE_AUTO),
            CSettings::GetMRUEnabled());

        settings.add_int_spinner(
            L"int_max_mru_size",
            GET_RESOURCE_STRING(IDS_MAX_ITEMS),
            CSettings::GetMaxMRUSize(),
            0,
            20,
            1);

        settings.add_bool_toogle(
            L"bool_show_icon_on_menu",
            GET_RESOURCE_STRING(IDS_ICON_CONTEXT_MENU),
            CSettings::GetShowIconOnMenu());

        settings.add_bool_toogle(
            L"bool_show_extended_menu",
            GET_RESOURCE_STRING(IDS_EXTENDED_MENU_INFO),
            CSettings::GetExtendedContextMenuOnly());

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Passes JSON with the configuration settings for the powertoy.
    // This is called when the user hits Save on the settings page.
    virtual void set_config(PCWSTR config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config);

            CSettings::SetPersistState(values.get_bool_value(L"bool_persist_input").value());
            CSettings::SetMRUEnabled(values.get_bool_value(L"bool_mru_enabled").value());
            CSettings::SetMaxMRUSize(values.get_int_value(L"int_max_mru_size").value());
            CSettings::SetShowIconOnMenu(values.get_bool_value(L"bool_show_icon_on_menu").value());
            CSettings::SetExtendedContextMenuOnly(values.get_bool_value(L"bool_show_extended_menu").value());

            Trace::SettingsChanged();
        }
        catch (std::exception)
        {
            // Improper JSON.
        }
    }

    // Signal from the Settings editor to call a custom action.
    // This can be used to spawn more complex editors.
    virtual void call_custom_action(const wchar_t* action) override
    {
    }

    // Handle incoming event, data is event-specific
    virtual intptr_t signal_event(const wchar_t* name, intptr_t data) override
    {
        return 0;
    }

    virtual void register_system_menu_helper(PowertoySystemMenuIface* helper) override {}
    virtual void signal_system_menu_action(const wchar_t* name) override {}

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        delete this;
    }

    void init_settings()
    {
        m_enabled = CSettings::GetEnabled();
        Trace::EnablePowerRename(m_enabled);
    }

    void save_settings()
    {
        CSettings::SetEnabled(m_enabled);
        Trace::EnablePowerRename(m_enabled);
    }

    PowerRenameModule()
    {
        init_settings();
        app_name = GET_RESOURCE_STRING(IDS_POWERRENAME);
    }

    ~PowerRenameModule(){};
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new PowerRenameModule();
}
