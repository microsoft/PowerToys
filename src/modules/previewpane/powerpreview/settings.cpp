#include "pch.h"
#include <common.h>
#include "settings.h"
#include "trace.h"

namespace PowerPreviewSettings
{
	extern "C" IMAGE_DOS_HEADER __ImageBase;

	// Base Settinngs Class Implementation
	FileExplorerPreviewSettings::FileExplorerPreviewSettings(bool state, std::wstring name, std::wstring description)
	{
        this->m_isPreviewEnabled = std::move(state);
		this->m_name = std::move(name);
        this->m_description = std::move(description);
	}

	FileExplorerPreviewSettings::FileExplorerPreviewSettings(bool state)
    {
        this->m_isPreviewEnabled = std::move(state);
    }

	FileExplorerPreviewSettings::FileExplorerPreviewSettings()
	{
		this->m_isPreviewEnabled = false;
		this->m_name = L"_UNDEFINED_";
		this->m_description = L"_UNDEFINED_";
	}

	bool FileExplorerPreviewSettings::GetState()
	{
		return this->m_isPreviewEnabled;
	}

	void FileExplorerPreviewSettings::SetState(bool state)
	{
        this->m_isPreviewEnabled = std::move(state);
	}

	void FileExplorerPreviewSettings::LoadState(PowerToysSettings::PowerToyValues &settings)
	{
		auto toggle = settings.get_bool_value(this->GetName());
		if(toggle != std::nullopt)
		{
			this->m_isPreviewEnabled = toggle.value();
		}
	}

	void FileExplorerPreviewSettings::UpdateState(PowerToysSettings::PowerToyValues &values)
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



	std::wstring FileExplorerPreviewSettings::GetName()
	{
		return this->m_name;
	}

	void FileExplorerPreviewSettings::SetName(std::wstring name)
	{
		this->m_name = std::move(name);
	}

	std::wstring FileExplorerPreviewSettings::GetDescription()
	{
		return this->m_description;
	}

	void FileExplorerPreviewSettings::SetDescription(std::wstring description)
	{
        this->m_description = std::move(description);
	}


	// Explorer SVG Icons Preview Settings Implemention
    ExplrSVGSttngs::ExplrSVGSttngs() 
		:FileExplorerPreviewSettings(false)
	{
		this->m_name = GET_RESOURCE_STRING(IDS_EXPLR_SVG_BOOL_TOGGLE_CONTROLL);
		this->m_description = GET_RESOURCE_STRING(IDS_EXPLR_SVG_SETTINGS_DESCRIPTION);
	}

	void ExplrSVGSttngs::EnablePreview()
	{
		Trace::ExplorerSVGRenderEnabled();
	}

	void ExplrSVGSttngs::DisabledPreview()
	{
		Trace::ExplorerSVGRenderDisabled();
	}

	// Preview Pane SVG Render Settings
    PrevPaneSVGRendrSettings::PrevPaneSVGRendrSettings() 
		:FileExplorerPreviewSettings(false)
	{
		this->m_name = GET_RESOURCE_STRING(IDS_PREVPANE_SVG_BOOL_TOGGLE_CONTROLL);
		this->m_description = GET_RESOURCE_STRING(IDS_PREVPANE_SVG_SETTINGS_DESCRIPTION);
	}

	void PrevPaneSVGRendrSettings::EnablePreview()
	{
		Trace::ExplorerSVGRenderEnabled();
	}

	void PrevPaneSVGRendrSettings::DisabledPreview()
	{
		Trace::ExplorerSVGRenderDisabled();
	}

	// Preview Pane Mark Down Render Settings
	PrevPaneMDRendrSettings::PrevPaneMDRendrSettings() 
		:FileExplorerPreviewSettings(false)
	{
		this->m_name = GET_RESOURCE_STRING(IDS_PREVPANE_MD_BOOL_TOGGLE_CONTROLL);
		this->m_description = GET_RESOURCE_STRING(IDS_PREVPANE_MD_SETTINGS_DESCRIPTION);
	}

	void PrevPaneMDRendrSettings::EnablePreview()
	{
		Trace::ExplorerSVGRenderEnabled();
	}

	void PrevPaneMDRendrSettings::DisabledPreview()
	{
		Trace::ExplorerSVGRenderDisabled();
	}

}