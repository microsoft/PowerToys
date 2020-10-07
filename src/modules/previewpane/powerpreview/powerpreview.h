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
    //contains the non localized key of the powertoy
    std::wstring app_key = L"File Explorer";
    std::vector<FileExplorerPreviewSettings*> m_previewHandlers;
    std::vector<FileExplorerPreviewSettings*> m_thumbnailProviders;

public:
    PowerPreviewModule() :
        m_moduleName(GET_RESOURCE_STRING(IDS_MODULE_NAME)),
        m_previewHandlers(
            { // SVG Preview Handler settings object.
              new FileExplorerPreviewSettings(
                  true,
                  L"svg-previewer-toggle-setting",
                  GET_RESOURCE_STRING(IDS_PREVPANE_SVG_SETTINGS_DESCRIPTION),
                  L"{ddee2b8a-6807-48a6-bb20-2338174ff779}",
                  L"SVG Preview Handler",
                  new RegistryWrapper()),

              // MarkDown Preview Handler Settings Object.
              new FileExplorerPreviewSettings(
                  true,
                  L"md-previewer-toggle-setting",
                  GET_RESOURCE_STRING(IDS_PREVPANE_MD_SETTINGS_DESCRIPTION),
                  L"{45769bcc-e8fd-42d0-947e-02beef77a1f5}",
                  L"Markdown Preview Handler",
                  new RegistryWrapper()) }),
        m_thumbnailProviders(
            { // TODO: MOVE THIS SVG Thumbnail Provider settings object.
              new FileExplorerPreviewSettings(
                  true,
                  L"svg-thumbnail-toggle-setting",
                  GET_RESOURCE_STRING(IDS_SVG_THUMBNAIL_PROVIDER_SETTINGS_DESCRIPTION),
                  L"{36B27788-A8BB-4698-A756-DF9F11F64F84}",
                  L"SVG Thumbnail Provider",
                  new RegistryWrapper()) })
    {
        init_settings();
    };

    virtual void destroy();
    virtual const wchar_t* get_name();
    virtual const wchar_t* get_key();
    virtual bool get_config(_Out_ wchar_t* buffer, _Out_ int* buffer_size);
    virtual void set_config(const wchar_t* config);
    virtual void enable();
    virtual void disable();
    virtual bool is_enabled();
    virtual void init_settings();
};