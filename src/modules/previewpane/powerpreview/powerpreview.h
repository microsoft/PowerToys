#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common.h>
#include "trace.h"
#include "settings.h"
#include "registry_wrapper.h"

using namespace PowerPreviewSettings;

extern "C" IMAGE_DOS_HEADER __ImageBase;

// Implement the PowerToy Module Interface and all the required methods.
class PowerPreviewModule : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;
    std::wstring m_moduleName;
    std::vector<FileExplorerPreviewSettings> m_previewHandlers;

public:
    PowerPreviewModule() :
        m_moduleName(GET_RESOURCE_STRING(IDS_MODULE_NAME)),
        m_previewHandlers(
            { // SVG Preview Hanlder settings object.
              FileExplorerPreviewSettings(
                  false,
                  GET_RESOURCE_STRING(IDS_PREVPANE_SVG_BOOL_TOGGLE_CONTROLL),
                  GET_RESOURCE_STRING(IDS_PREVPANE_SVG_SETTINGS_DESCRIPTION),
                  L"{ddee2b8a-6807-48a6-bb20-2338174ff779}",
                  GET_RESOURCE_STRING(IDS_PREVPANE_SVG_SETTINGS_DISPLAYNAME),
                  new RegistryWrapper()),

              // MarkDown Preview Handler Settings Object.
              FileExplorerPreviewSettings(
                  false,
                  GET_RESOURCE_STRING(IDS_PREVPANE_MD_BOOL_TOGGLE_CONTROLL),
                  GET_RESOURCE_STRING(IDS_PREVPANE_MD_SETTINGS_DESCRIPTION),
                  L"{45769bcc-e8fd-42d0-947e-02beef77a1f5}",
                  GET_RESOURCE_STRING(IDS_PREVPANE_MD_SETTINGS_DISPLAYNAME),
                  new RegistryWrapper())
            })
    {
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
    virtual void init_settings();
    virtual intptr_t signal_event(const wchar_t* name, intptr_t data);
    virtual void register_system_menu_helper(PowertoySystemMenuIface* helper) override {}
    virtual void signal_system_menu_action(const wchar_t* name) override {}
};