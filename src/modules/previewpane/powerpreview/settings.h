#pragma once
#include <string>
#include "Generated Files/resource.h"
#include <settings_objects.h>
#include "registry_wrapper_interface.h"

namespace PowerPreviewSettings
{
    // PowerToy Windows Explorer File Preview Settings.
    class FileExplorerPreviewSettings
    {
    private:
        bool m_toggleSettingEnabled;
        std::wstring m_toggleSettingName;
        std::wstring m_toggleSettingDescription;
        std::wstring m_registryValueData;
        LPCWSTR m_clsid;

    protected:
        std::unique_ptr<RegistryWrapperIface> m_registryWrapper;

    public:
        FileExplorerPreviewSettings(bool toggleSettingEnabled, const std::wstring& toggleSettingName, const std::wstring& toggleSettingDescription, LPCWSTR clsid, const std::wstring& registryValueData, std::unique_ptr<RegistryWrapperIface>);

        virtual bool GetToggleSettingState() const;
        virtual void UpdateToggleSettingState(bool state);
        virtual std::wstring GetToggleSettingName() const;
        virtual std::wstring GetToggleSettingDescription() const;
        virtual LPCWSTR GetCLSID() const;
        virtual std::wstring GetRegistryValueData() const;
        virtual void LoadState(PowerToysSettings::PowerToyValues& settings);
        virtual bool UpdateState(PowerToysSettings::PowerToyValues& settings, bool enabled, bool isElevated);
        virtual LONG Enable() = 0;
        virtual LONG Disable() = 0;
        virtual bool CheckRegistryState() = 0;
    };
}
