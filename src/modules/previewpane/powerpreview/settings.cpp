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
    FileExplorerPreviewSettings::FileExplorerPreviewSettings(bool state, const std::wstring name, const std::wstring description, LPCWSTR clsid, const std::wstring displayname) 
		: 
		m_isPreviewEnabled(state),
        m_name(name),
        m_description(description),
		m_clsid(clsid),
		m_displayName(displayname){}

	FileExplorerPreviewSettings::FileExplorerPreviewSettings()
		:
        m_isPreviewEnabled(false),
        m_name(L"_UNDEFINED_"),
        m_description(L"_UNDEFINED_"),
		m_clsid(L"_UNDEFINED_"),
		m_displayName(L"_UNDEFINED_"){}

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
		if(toggle != std::nullopt)
		{
			this->m_isPreviewEnabled = toggle.value();
		}
	}

	void FileExplorerPreviewSettings::UpdateState(PowerToysSettings::PowerToyValues& values)
	{
        auto toggle = values.get_bool_value(this->GetName());
		if(toggle != std::nullopt)
		{
			this->m_isPreviewEnabled  = toggle.value();
			if (this->m_isPreviewEnabled)
			{
				this->EnablePreview();
			}
			else
			{
				this->DisabledPreview();
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

		LONG err = RegOpenKeyEx(hKey, this->GetSubKey(), options, WRITE_PERMISSION, &OpenResult); 

		if (err == ERROR_SUCCESS)
		{
			err = RegSetValueExW(
					OpenResult,
					this->GetCLSID(),
					0,
					REG_SZ,
					(LPBYTE)this->GetDisplayName().c_str(),
					this->GetDisplayName().length() * sizeof(TCHAR));

			if (err != ERROR_SUCCESS)
			{
				return err;
			}
		}
		else
		{
			return err;
		}
        RegCloseKey(OpenResult);
	}

	LONG FileExplorerPreviewSettings::RemvRegistryValue() const
	{
		HKEY hKey = HKEY_CURRENT_USER;
		const REGSAM WRITE_PERMISSION = KEY_WRITE;
		DWORD options = 0;
		HKEY OpenResult;

		LONG err = RegOpenKeyEx(hKey, this->GetSubKey(), options, WRITE_PERMISSION, &OpenResult); 
		if (err == ERROR_SUCCESS)
		{
			err = RegDeleteKeyValueW(
				OpenResult,
				NULL,
				this->GetCLSID());

			if (err != ERROR_SUCCESS)
			{
				return err;
			}
		}
		else
		{
			return err;
		}
        RegCloseKey(OpenResult);
	}

	bool FileExplorerPreviewSettings::GetRegistryValue() const
	{
		HKEY OpenResult;
		LONG err = RegOpenKeyEx(
			HKEY_CURRENT_USER,
			this->GetSubKey(), 
			0, 
			KEY_READ,
			&OpenResult);

		if (err != ERROR_SUCCESS)
		{
			return false;
		}
		else
		{
			DWORD dataType;
			WCHAR value[255];
			PVOID pvData = value;
			DWORD size = sizeof(value);

			err = RegGetValueW(
					OpenResult, 
					NULL,
					this->GetCLSID(),
					RRF_RT_ANY,
					&dataType,
					pvData,
					&size);
			if (err != ERROR_SUCCESS)
			{
				return false;
			}
		}
		return true;
	}

	std::wstring FileExplorerPreviewSettings::GetName() const
	{
		return this->m_name;
	}

	void FileExplorerPreviewSettings::SetName(const std::wstring &name)
	{
		this->m_name = name;
	}

	std::wstring FileExplorerPreviewSettings::GetDescription() const
	{
		return this->m_description;
	}

	void FileExplorerPreviewSettings::SetDescription(const std::wstring &description)
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

	void FileExplorerPreviewSettings::SetDisplayName(const std::wstring &displayName)
	{
		this->m_displayName = displayName;
	}

	// Preview Pane SVG Render Settings
    PrevPaneSVGRendrSettings::PrevPaneSVGRendrSettings() 
		:
		FileExplorerPreviewSettings(
			false,
            GET_RESOURCE_STRING(IDS_PREVPANE_SVG_BOOL_TOGGLE_CONTROLL),
            GET_RESOURCE_STRING(IDS_PREVPANE_SVG_SETTINGS_DESCRIPTION),
			L"{ddee2b8a-6807-48a6-bb20-2338174ff779}",
			GET_RESOURCE_STRING(IDS_PREVPANE_SVG_SETTINGS_DISPLAYNAME)){}

	void PrevPaneSVGRendrSettings::EnablePreview()
	{
		if(this->SetRegistryValue() == ERROR_SUCCESS)
		{
			Trace::ExplorerSVGRenderEnabled();
		}
		else
		{
			Trace::PowerPreviewSettingsUpDateFailed(this->GetName().c_str());
			this->SetState(false);
		}
	}

	void PrevPaneSVGRendrSettings::DisabledPreview()
	{
		if(this->RemvRegistryValue() == ERROR_SUCCESS)
		{
			Trace::ExplorerSVGRenderDisabled();
		}
		else
		{
			Trace::PowerPreviewSettingsUpDateFailed(this->GetName().c_str());
			this->SetState(true);
		}
	}

	// Preview Pane Mark Down Render Settings
	PrevPaneMDRendrSettings::PrevPaneMDRendrSettings() 
		:
		FileExplorerPreviewSettings(
			false,
            GET_RESOURCE_STRING(IDS_PREVPANE_MD_BOOL_TOGGLE_CONTROLL),
            GET_RESOURCE_STRING(IDS_PREVPANE_MD_SETTINGS_DESCRIPTION),
			L"{45769bcc-e8fd-42d0-947e-02beef77a1f5}",
			GET_RESOURCE_STRING(IDS_PREVPANE_MD_SETTINGS_DISPLAYNAME)){}

	void PrevPaneMDRendrSettings::EnablePreview()
	{
		if(this->SetRegistryValue() == ERROR_SUCCESS)
		{
			Trace::ExplorerSVGRenderEnabled();
		}
		else
		{
			Trace::PowerPreviewSettingsUpDateFailed(this->GetName().c_str());
			this->SetState(false);
		}
	}

	void PrevPaneMDRendrSettings::DisabledPreview()
	{
		if(this->RemvRegistryValue() == ERROR_SUCCESS)
		{
			Trace::ExplorerSVGRenderDisabled();
		}
		else
		{
			Trace::PowerPreviewSettingsUpDateFailed(this->GetName().c_str());
			this->SetState(true);
		}
	}

}