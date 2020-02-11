#include "stdafx.h"
#include "resource.h"
#include "ImageResizerExt_i.h"
#include "dllmain.h"
#include <interface/powertoy_module_interface.h>
#include <common/settings_objects.h>
#include "Settings.h"
#include "trace.h"
#include <common/common.h>

CImageResizerExtModule _AtlModule;
HINSTANCE g_hInst_imageResizer = 0;

extern "C" BOOL WINAPI DllMain(HINSTANCE hInstance, DWORD dwReason, LPVOID lpReserved)
{
    switch (dwReason)
    {
    case DLL_PROCESS_ATTACH:
        g_hInst_imageResizer = hInstance;
        Trace::RegisterProvider();
        break;
    case DLL_PROCESS_DETACH:
        Trace::UnregisterProvider();
        break;
    }
    return _AtlModule.DllMain(dwReason, lpReserved);
}

class ImageResizerModule : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;
    std::wstring app_name;

    // Load initial settings from the persisted values.
    void init_settings();

public:
    // Constructor
    ImageResizerModule()
    {
        init_settings();
        app_name = GET_RESOURCE_STRING(IDS_IMAGERESIZER);
    };

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        delete this;
    }

    // Return the display name of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_name() override
    {
        return app_name.c_str();
    }

    // Return array of the names of all events that this powertoy listens for, with
    // nullptr as the last element of the array. Nullptr can also be retured for empty
    // list.
    virtual const wchar_t** get_events() override
    {
        static const wchar_t* events[] = { nullptr };
        // Available events:
        // - ll_keyboard
        // - win_hook_event
        //
        // static const wchar_t* events[] = { ll_keyboard,
        //                                   win_hook_event,
        //                                   nullptr };

        return events;
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(GET_RESOURCE_STRING(IDS_SETTINGS_DESCRIPTION));
        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Signal from the Settings editor to call a custom action.
    // This can be used to spawn more complex editors.
    virtual void call_custom_action(const wchar_t* action) override {}

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override {}

    // Enable the powertoy
    virtual void enable()
    {
        m_enabled = true;
        CSettings::SetEnabled(m_enabled);
        Trace::EnableImageResizer(m_enabled);
    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
        CSettings::SetEnabled(m_enabled);
        Trace::EnableImageResizer(m_enabled);
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Handle incoming event, data is event-specific
    virtual intptr_t signal_event(const wchar_t* name, intptr_t data) override
    {
        return 0;
    }

    virtual void register_system_menu_helper(PowertoySystemMenuIface* helper) override {}
    virtual void signal_system_menu_action(const wchar_t* name) override {}
};

// Load the settings file.
void ImageResizerModule::init_settings() {}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new ImageResizerModule();
}