#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include "trace.h"
#include <powerpreview/powerpreviewConstants.h>

#include <functional>

#include <common/utils/modulesRegistry.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/gpo.h>

struct FileExplorerModule
{
    std::wstring settingName;
    std::wstring settingDescription;
    powertoys_gpo::gpo_rule_configured_t (*checkModuleGPOEnabledRuleFunction)();
    registry::ChangeSet registryChanges;
};

// Implement the PowerToy Module Interface and all the required methods.
class PowerPreviewModule : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;
    std::wstring m_moduleName;
    // Contains the non localized key of the powertoy
    std::wstring app_key;
    std::vector<FileExplorerModule> m_fileExplorerModules;

    // Function to warn the user that PowerToys needs to run as administrator for changes to take effect
    void show_update_warning_message();
    void apply_settings(const PowerToysSettings::PowerToyValues& settings);
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
};