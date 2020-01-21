#include "pch.h"
#include <common.h>
#include "settings.h"
#include "trace.h"

namespace PowerPreviewSettings
{
	extern "C" IMAGE_DOS_HEADER __ImageBase;
	// Base Settinngs Class Implementation
	FileExplorerPreviewSettings::FileExplorerPreviewSettings(bool _state, std::wstring _Name, std::wstring _Description)
	{
		this->IsPreviewEnabled = false;
		this->Name = _Name;
		this->Description = _Description;
	}

	FileExplorerPreviewSettings::FileExplorerPreviewSettings()
	{
		this->IsPreviewEnabled = false;
		this->Name = L"_UNDEFINED_";
		this->Description = L"_UNDEFINED_";
	}

	bool FileExplorerPreviewSettings::GetState()
	{
		return this->IsPreviewEnabled;
	}

	void FileExplorerPreviewSettings::SetState(bool _State)
	{
		this->IsPreviewEnabled = _State;
	}

	void FileExplorerPreviewSettings::LoadState(PowerToysSettings::PowerToyValues settings)
	{
		auto toggle = settings.get_bool_value(this->GetName());
		if(toggle != std::nullopt)
		{
			this->IsPreviewEnabled = toggle.value();
		}
	}

	void FileExplorerPreviewSettings::UpdateState(PowerToysSettings::PowerToyValues values)
	{
		auto toggle = values.get_bool_value(this->GetName());
		if(toggle != std::nullopt)
		{
			this->IsPreviewEnabled  = toggle.value();
			if (this->IsPreviewEnabled)
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
		return this->Name;
	}

	void FileExplorerPreviewSettings::SetName(std::wstring _Name)
	{
		this->Name = _Name;
	}

	std::wstring FileExplorerPreviewSettings::GetDescription()
	{
		return this->Description;
	}

	void FileExplorerPreviewSettings::SetDescription(std::wstring _Description)
	{
		this->Description = _Description;
	}


	// Explorer SVG Icons Preview Settings Implemention
	ExplrSVGSttngs::ExplrSVGSttngs()
	{
		this->IsPreviewEnabled = false;
		this->Name = GET_RESOURCE_STRING(IDS_EXPLR_SVG_BOOL_TOGGLE_CONTROLL);
		this->Description = GET_RESOURCE_STRING(IDS_EXPLR_SVG_SETTINGS_DESCRIPTION);
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
	{
		this->IsPreviewEnabled = false;
		this->Name = GET_RESOURCE_STRING(IDS_PREVPANE_SVG_BOOL_TOGGLE_CONTROLL);
		this->Description = GET_RESOURCE_STRING(IDS_PREVPANE_SVG_SETTINGS_DESCRIPTION);
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
	{
		this->IsPreviewEnabled = false;
		this->Name = GET_RESOURCE_STRING(IDS_PREVPANE_MD_BOOL_TOGGLE_CONTROLL);
		this->Description = GET_RESOURCE_STRING(IDS_PREVPANE_MD_SETTINGS_DESCRIPTION);
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