#pragma once
#include <string>
#include "resource.h"
#include <settings_objects.h>

namespace PowerPreviewSettings
{
	// PowerToy Winodws Explore File Preview Settings.
	class FileExplorerPreviewSettings{
	protected:
		bool m_isPreviewEnabled;
        std::wstring m_name;
        std::wstring m_description;

	public:
		FileExplorerPreviewSettings(bool _state, std::wstring _Name, std::wstring _Description);
		FileExplorerPreviewSettings();

		virtual bool GetState();
		virtual void SetState(bool _State);
		virtual void LoadState(PowerToysSettings::PowerToyValues settings);
		virtual void UpdateState(PowerToysSettings::PowerToyValues values);
		virtual std::wstring GetName();
		virtual void SetName(std::wstring _Name);
		virtual std::wstring GetDescription();
		virtual void SetDescription(std::wstring _Description);
		virtual void EnablePreview() = 0;
		virtual void DisabledPreview() = 0;
	};


	class ExplrSVGSttngs: public FileExplorerPreviewSettings{
	public:
		ExplrSVGSttngs();

		virtual void EnablePreview();
		virtual void DisabledPreview();
	};

	class PrevPaneSVGRendrSettings: public FileExplorerPreviewSettings{
	public:
		PrevPaneSVGRendrSettings();

		virtual void EnablePreview();
		virtual void DisabledPreview();
	};

	class PrevPaneMDRendrSettings: public FileExplorerPreviewSettings{
	public:
		PrevPaneMDRendrSettings();

		virtual void EnablePreview();
		virtual void DisabledPreview();
	};

}