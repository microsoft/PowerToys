#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common.h>
#include "trace.h"
#include "settings.h"
#include "thumbnail_provider.h"
#include "preview_handler.h"
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
    std::vector<FileExplorerPreviewSettings*> m_fileExplorerModules;

    // Function to check if the registry states need to be updated
    bool is_registry_update_required();

    // Function to warn the user that PowerToys needs to run as administrator for changes to take effect
    void show_update_warning_message();

    // Function that checks if a registry method is required and if so checks if the process is elevated and accordingly executes the method or shows a warning
    void elevation_check_wrapper(std::function<void()> method);

public:
    PowerPreviewModule() :
        m_moduleName(GET_RESOURCE_STRING(IDS_MODULE_NAME)),
        m_fileExplorerModules(
            { // SVG Preview Handler settings object.
              new PreviewHandlerSettings(
                  true,
                  L"svg-previewer-toggle-setting",
                  GET_RESOURCE_STRING(IDS_PREVPANE_SVG_SETTINGS_DESCRIPTION),
                  L"{ddee2b8a-6807-48a6-bb20-2338174ff779}",
                  L"SVG Preview Handler",
                  new RegistryWrapper()),

              // MarkDown Preview Handler Settings Object.
              new PreviewHandlerSettings(
                  true,
                  L"md-previewer-toggle-setting",
                  GET_RESOURCE_STRING(IDS_PREVPANE_MD_SETTINGS_DESCRIPTION),
                  L"{45769bcc-e8fd-42d0-947e-02beef77a1f5}",
                  L"Markdown Preview Handler",
                  new RegistryWrapper()),
              //SVG Thumbnail Provider settings object.
              new ThumbnailProviderSettings(
                  true,
                  L"svg-thumbnail-toggle-setting",
                  GET_RESOURCE_STRING(IDS_SVG_THUMBNAIL_PROVIDER_SETTINGS_DESCRIPTION),
                  L"{36B27788-A8BB-4698-A756-DF9F11F64F84}",
                  L"SVG Thumbnail Provider",
                  new RegistryWrapper(),
                  L".svg\\shellex\\{E357FCCD-A995-4576-B01F-234630154E96}") })
    {
        init_settings();
    };

    virtual void destroy();
    virtual const wchar_t* get_name();
    virtual bool get_config(_Out_ wchar_t* buffer, _Out_ int* buffer_size);
    virtual void set_config(const wchar_t* config);
    virtual void enable();
    virtual void disable();
    virtual bool is_enabled();
    virtual void init_settings();
};