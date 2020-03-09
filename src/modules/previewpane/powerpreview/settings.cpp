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

    // Base Settinngs Class Implementation
    FileExplorerPreviewSettings::FileExplorerPreviewSettings(bool state, const std::wstring name, const std::wstring description, LPCWSTR clsid, const std::wstring displayname, RegistryWrapperIface * registryWrapper) :
        m_isPreviewEnabled(state),
        m_name(name),
        m_description(description),
        m_clsid(clsid),
        m_displayName(displayname),
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

    bool FileExplorerPreviewSettings::GetState() const
    {
        return this->m_isPreviewEnabled;
    }

    void FileExplorerPreviewSettings::SetState(bool state)
    {
        this->m_isPreviewEnabled = state;
    }

    void FileExplorerPreviewSettings::LoadState(PowerToysSettings::PowerToyValues& settings)
    {
        auto toggle = settings.get_bool_value(this->GetName());
        if (toggle != std::nullopt)
        {
            this->m_isPreviewEnabled = toggle.value();
        }
    }

    void FileExplorerPreviewSettings::UpdateState(PowerToysSettings::PowerToyValues& values)
    {
        auto toggle = values.get_bool_value(this->GetName());
        if (toggle != std::nullopt)
        {
            if (toggle.value())
            {
                this->EnablePreview();
            }
            else
            {
                this->DisablePreview();
            }
        }
        else
        {
            Trace::PowerPreviewSettingsUpDateFailed(this->GetName().c_str());
        }
    }

    std::wstring FileExplorerPreviewSettings::GetName() const
    {
        return this->m_name;
    }

    void FileExplorerPreviewSettings::SetName(const std::wstring& name)
    {
        this->m_name = name;
    }

    std::wstring FileExplorerPreviewSettings::GetDescription() const
    {
        return this->m_description;
    }

    void FileExplorerPreviewSettings::SetDescription(const std::wstring& description)
    {
        this->m_description = description;
    }

    LPCWSTR FileExplorerPreviewSettings::GetSubKey() const
    {
        return this->m_subKey;
    }

    LPCWSTR FileExplorerPreviewSettings::GetCLSID() const
    {
        return this->m_clsid;
    }

    std::wstring FileExplorerPreviewSettings::GetDisplayName() const
    {
        return this->m_displayName;
    }

    void FileExplorerPreviewSettings::SetDisplayName(const std::wstring& displayName)
    {
        this->m_displayName = displayName;
    }

    void FileExplorerPreviewSettings::EnablePreview()
    {
        // Add registry value to enable preview.
        LONG err = this->m_registryWrapper->SetRegistryValue(HKEY_CURRENT_USER, this->GetSubKey(), this->GetCLSID(), REG_SZ, (LPBYTE)this->GetDisplayName().c_str(), (DWORD)(this->GetDisplayName().length() * sizeof(wchar_t)));

        if (err == ERROR_SUCCESS)
        {
            this->SetState(true);
            Trace::PreviewHandlerEnabled(true, this->GetDisplayName().c_str());
        }
        else
        {
            Trace::PowerPreviewSettingsUpDateFailed(this->GetName().c_str());
        }
    }

    void FileExplorerPreviewSettings::DisablePreview()
    {
        // Delete the registry key to disable preview.
        LONG err = this->m_registryWrapper->DeleteRegistryValue(HKEY_CURRENT_USER, this->GetSubKey(), this->GetCLSID());

        if (err == ERROR_SUCCESS)
        {
            this->SetState(false);
            Trace::PreviewHandlerEnabled(false, this->GetDisplayName().c_str());
        }
        else
        {
            Trace::PowerPreviewSettingsUpDateFailed(this->GetName().c_str());
        }    
    }
}
