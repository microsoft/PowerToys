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
    FileExplorerPreviewSettings::FileExplorerPreviewSettings(bool state, const std::wstring name, const std::wstring description, LPCWSTR clsid, const std::wstring displayname) :
        m_isPreviewEnabled(state),
        m_name(name),
        m_description(description),
        m_clsid(clsid),
        m_displayName(displayname) {}

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

    LONG FileExplorerPreviewSettings::SetRegistryValue() const
    {
        HKEY hKey = HKEY_CURRENT_USER;
        const REGSAM WRITE_PERMISSION = KEY_WRITE;
        DWORD options = 0;
        HKEY OpenResult;

        LONG err = RegOpenKeyExW(hKey, this->GetSubKey(), options, WRITE_PERMISSION, &OpenResult);

        if (err == ERROR_SUCCESS)
        {
            err = RegSetValueExW(
                OpenResult,
                this->GetCLSID(),
                0,
                REG_SZ,
                (LPBYTE)this->GetDisplayName().c_str(),
                this->GetDisplayName().length() * sizeof(wchar_t));
            RegCloseKey(OpenResult);
            if (err != ERROR_SUCCESS)
            {
                return err;
            }
        }
        else
        {
            return err;
        }
    }

    LONG FileExplorerPreviewSettings::DeleteRegistryValue() const
    {
        HKEY hKey = HKEY_CURRENT_USER;
        const REGSAM WRITE_PERMISSION = KEY_WRITE;
        DWORD options = 0;
        HKEY OpenResult;

        LONG err = RegOpenKeyExW(hKey, this->GetSubKey(), options, WRITE_PERMISSION, &OpenResult);
        if (err == ERROR_SUCCESS)
        {
            err = RegDeleteKeyValueW(
                OpenResult,
                NULL,
                this->GetCLSID());
            RegCloseKey(OpenResult);

            if (err != ERROR_SUCCESS)
            {
                return err;
            }
        }
        else
        {
            return err;
        }
    }

    bool FileExplorerPreviewSettings::GetRegistryValue() const
    {
        HKEY OpenResult;
        LONG err = RegOpenKeyExW(
            HKEY_CURRENT_USER,
            this->GetSubKey(),
            0,
            KEY_READ,
            &OpenResult);

        if (err == ERROR_SUCCESS)
        {
            DWORD dataType;
            err = RegGetValueW(
                OpenResult,
                NULL,
                this->GetCLSID(),
                RRF_RT_ANY,
                &dataType,
                NULL,
                0);
            RegCloseKey(OpenResult);
            if (err != ERROR_SUCCESS)
            {
                return false;
            }
        }
        else
        {
            return false;
        }
        return true;
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
        if (this->SetRegistryValue() == ERROR_SUCCESS)
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
        if (this->DeleteRegistryValue() == ERROR_SUCCESS)
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
