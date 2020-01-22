#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common.h>
#include "trace.h"
#include "settings.h"

using namespace PowerPreviewSettings;

extern "C" IMAGE_DOS_HEADER __ImageBase;

// Implement the PowerToy Module Interface and all the required methods.
class PowerPreviewModule : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;
    ExplrSVGSttngs explrSVGSettings = ExplrSVGSttngs();
    PrevPaneSVGRendrSettings prevPaneSVGSettings = PrevPaneSVGRendrSettings();
    PrevPaneMDRendrSettings prevPaneMDSettings = PrevPaneMDRendrSettings();
    std::wstring moduleName;

    // Load and save Settings.
    void init_settings();

public:
    PowerPreviewModule()
    {
        this->moduleName = GET_RESOURCE_STRING(IDS_MODULE_NAME);

        this->explrSVGSettings = ExplrSVGSttngs();
        this->prevPaneSVGSettings = PrevPaneSVGRendrSettings();
        this->prevPaneMDSettings = PrevPaneMDRendrSettings();

        init_settings();
    };

    virtual void destroy();
    virtual const wchar_t* get_name();
    virtual const wchar_t** get_events();
    virtual bool get_config(_Out_ wchar_t* buffer, _Out_ int* buffer_size);
    virtual void set_config(const wchar_t* config);
    virtual void enable();
    virtual void disable();
    virtual bool is_enabled();
    virtual intptr_t signal_event(const wchar_t* name, intptr_t data);
    virtual void register_system_menu_helper(PowertoySystemMenuIface* helper) override {}
    virtual void signal_system_menu_action(const wchar_t* name) override {}
};