#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include "trace.h"
#include "settings.h"
#include "thumbnail_provider.h"
#include "preview_handler.h"
#include "registry_wrapper.h"
#include <powerpreview/powerpreviewConstants.h>

#include <functional>

using namespace PowerPreviewSettings;

// Implement the PowerToy Module Interface and all the required methods.
class PowerPreviewModule : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;
    std::wstring m_moduleName;
    //contains the non localized key of the powertoy
    std::wstring app_key;
    std::vector<std::unique_ptr<FileExplorerPreviewSettings>> m_fileExplorerModules;

    // Function to check if the registry states need to be updated
    bool is_registry_update_required();

    // Function to warn the user that PowerToys needs to run as administrator for changes to take effect
    void show_update_warning_message();

    // Function that checks if a registry method is required and if so checks if the process is elevated and accordingly executes the method or shows a warning
    void registry_and_elevation_check_wrapper(std::function<void()> method);

    // Function that checks if the process is elevated and accordingly executes the method or shows a warning
    void elevation_check_wrapper(std::function<void()> method);

    // Function that updates the registry state to match the toggle states
    void update_registry_to_match_toggles();

public:
    PowerPreviewModule();

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