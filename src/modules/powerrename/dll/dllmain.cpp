#include "stdafx.h"
#include "PowerRenameExt.h"
#include <interface/powertoy_module_interface.h>
#include <settings.h>
#include <trace.h>
#include <common/settings_objects.h>

DWORD g_dwModuleRefCount = 0;
HINSTANCE g_hInst = 0;

extern "C" IMAGE_DOS_HEADER __ImageBase;

class CPowerRenameClassFactory : public IClassFactory
{
public:
    CPowerRenameClassFactory(_In_ REFCLSID clsid) :
        m_refCount(1),
        m_clsid(clsid)
    {
        DllAddRef();
    }

    // IUnknown methods
    IFACEMETHODIMP QueryInterface(_In_ REFIID riid, _COM_Outptr_ void** ppv)
    {
        static const QITAB qit[] =
        {
            QITABENT(CPowerRenameClassFactory, IClassFactory),
            { 0 }
        };
        return QISearch(this, qit, riid, ppv);
    }

    IFACEMETHODIMP_(ULONG) AddRef()
    {
        return InterlockedIncrement(&m_refCount);
    }

    IFACEMETHODIMP_(ULONG) Release()
    {
        LONG refCount = InterlockedDecrement(&m_refCount);
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
            DllAddRef();
        }
        else
        {
            DllRelease();
        }
        return S_OK;
    }

private:
    ~CPowerRenameClassFactory()
    {
        DllRelease();
    }

    long m_refCount;
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
STDAPI DllGetClassObject(_In_ REFCLSID clsid, _In_ REFIID riid, _Outptr_ void **ppv)
{
    *ppv = NULL;
    HRESULT hr = E_OUTOFMEMORY;
    CPowerRenameClassFactory *pClassFactory = new CPowerRenameClassFactory(clsid);
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

void DllAddRef()
{
    g_dwModuleRefCount++;
}

void DllRelease()
{
    g_dwModuleRefCount--;
}


class PowerRenameModule : public PowertoyModuleIface
{
private:
    // Enabled by default
    bool m_enabled = true;

public:
    // Return the display name of the powertoy, this will be cached
    virtual PCWSTR get_name() override
    {
        return L"PowerRename";
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
        settings.set_description(L"A Windows Shell Extension for more advanced bulk renaming using search and replace or regular expressions.");
        settings.set_icon_key(L"pt-power-rename");

        // Link to the GitHub PowerRename sub-page
        settings.set_overview_link(L"https://github.com/microsoft/PowerToys/tree/master/src/modules/powerrename");

        settings.add_bool_toogle(
            L"bool_persist_input",
            L"Restore search, replace and flags values on launch from previous run.",
            CSettings::GetPersistState()
        );

        settings.add_bool_toogle(
            L"bool_mru_enabled",
            L"Enable autocomplete and autosuggest of recently used inputs for search and replace values.",
            CSettings::GetMRUEnabled()
        );

        settings.add_int_spinner(
            L"int_max_mru_size",
            L"Maximum number of items to show in recently used list for autocomplete dropdown.",
            CSettings::GetMaxMRUSize(),
            0,
            20,
            1
        );

        settings.add_bool_toogle(
          L"bool_show_icon_on_menu", 
          L"Show icon on context menu.",
          CSettings::GetShowIconOnMenu()
        );

        settings.add_bool_toogle(
            L"bool_show_extended_menu",
            L"Only show the PowerRename menu item on the extended context menu (SHIFT + Right-click).",
            CSettings::GetExtendedContextMenuOnly()
        );

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Passes JSON with the configuration settings for the powertoy.
    // This is called when the user hits Save on the settings page.
    virtual void set_config(PCWSTR config) override
    {
        try {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config);

            CSettings::SetPersistState(values.get_bool_value(L"bool_persist_input"));
            CSettings::SetMRUEnabled(values.get_bool_value(L"bool_mru_enabled"));
            CSettings::SetMaxMRUSize(values.get_int_value(L"int_max_mru_size"));
            CSettings::SetShowIconOnMenu(values.get_bool_value(L"bool_show_icon_on_menu"));
            CSettings::SetExtendedContextMenuOnly(values.get_bool_value(L"bool_show_extended_menu"));

            values.save_to_settings_file();
        }
        catch (std::exception & ex) {
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
    }
};


extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new PowerRenameModule();
}
