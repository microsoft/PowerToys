#include "pch.h"
#include <common.h>
#include "settings.h"
#include "trace.h"
#include <iostream>
#include <atlstr.h>

using namespace std;

namespace PowerPreviewSettings
{
    extern "C" IMAGE_DOS_HEADER __ImageBase;

    // Relative(HKLM/HKCU) sub key path of Preview Handlers list in registry.
    static LPCWSTR preview_handlers_subkey = L"Software\\Microsoft\\Windows\\CurrentVersion\\PreviewHandlers";

    // Base Settinngs Class Implementation
    FileExplorerPreviewSettings::FileExplorerPreviewSettings(bool toggleSettingEnabled, const std::wstring& toggleSettingName, const std::wstring& toggleSettingDescription, LPCWSTR clsid, const std::wstring& registryValueData, RegistryWrapperIface* registryWrapper) :
        m_toggleSettingEnabled(toggleSettingEnabled),
        m_toggleSettingName(toggleSettingName),
        m_toggleSettingDescription(toggleSettingDescription),
        m_clsid(clsid),
        m_registryValueData(registryValueData),
        m_registryWrapper(registryWrapper)
    {
    }

    FileExplorerPreviewSettings::~FileExplorerPreviewSettings() 
    {
        if (this->m_registryWrapper != NULL)
        {
            delete this->m_registryWrapper;
        }
    }

    bool FileExplorerPreviewSettings::GetToggleSettingState() const
    {
        return this->m_toggleSettingEnabled;
    }

    void FileExplorerPreviewSettings::UpdateToggleSettingState(bool state)
    {
        this->m_toggleSettingEnabled = state;
    }

    std::wstring FileExplorerPreviewSettings::GetToggleSettingName() const
    {
        return this->m_toggleSettingName;
    }

    std::wstring FileExplorerPreviewSettings::GetToggleSettingDescription() const
    {
        return this->m_toggleSettingDescription;
    }

    LPCWSTR FileExplorerPreviewSettings::GetCLSID() const
    {
        return this->m_clsid;
    }

    std::wstring FileExplorerPreviewSettings::GetRegistryValueData() const
    {
        return this->m_registryValueData;
    }

    // Load intital state of the Preview Handler. If no inital state present initialize setting with default value.
    void FileExplorerPreviewSettings::LoadState(PowerToysSettings::PowerToyValues& settings)
    {
        auto toggle = settings.get_bool_value(this->GetToggleSettingName());
        if (toggle)
        {
            // If no exisiting setting found leave the default intitialization value.
            this->UpdateToggleSettingState(*toggle);
        }
    }

    // Manage change in state of Preview Handler settings.
    void FileExplorerPreviewSettings::UpdateState(PowerToysSettings::PowerToyValues& settings, bool enabled)
    {
        auto toggle = settings.get_bool_value(this->GetToggleSettingName());
        if (toggle)
        {
            auto lastState = this->GetToggleSettingState();
            auto newState = *toggle;
            if (lastState != newState)
            {
                this->UpdateToggleSettingState(newState);

                // If global setting is enable. Add or remove the preview handler otherwise just change the UI and save the updated config.
                if (enabled)
                {
                    LONG err;
                    if (lastState)
                    {
                        err = this->DisablePreview();
                    }
                    else
                    {
                        err = this->EnablePreview();
                    }

                    if (err == ERROR_SUCCESS)
                    {
                        Trace::PowerPreviewSettingsUpdated(this->GetToggleSettingName().c_str(), lastState, newState, enabled);
                    }
                    else
                    {
                        Trace::PowerPreviewSettingsUpdateFailed(this->GetToggleSettingName().c_str(), lastState, newState, enabled);
                    }
                }
                else
                {
                    Trace::PowerPreviewSettingsUpdated(this->GetToggleSettingName().c_str(), lastState, newState, enabled);
                }
            }
        }
    }

    LONG FileExplorerPreviewSettings::EnablePreview()
    {
        // Add registry value to enable preview.
        return this->m_registryWrapper->SetRegistryValue(HKEY_CURRENT_USER, preview_handlers_subkey, this->GetCLSID(), REG_SZ, (LPBYTE)this->GetRegistryValueData().c_str(), (DWORD)(this->GetRegistryValueData().length() * sizeof(wchar_t)));
    }

    LONG FileExplorerPreviewSettings::DisablePreview()
    {
        // Delete the registry key to disable preview.
        return this->m_registryWrapper->DeleteRegistryValue(HKEY_CURRENT_USER, preview_handlers_subkey, this->GetCLSID());
    }
}
