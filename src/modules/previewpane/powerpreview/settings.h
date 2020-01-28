#pragma once
#include <string>
#include "resource.h"
#include <settings_objects.h>

namespace PowerPreviewSettings
{
	// PowerToy Winodws Explore File Preview Settings.
	class FileExplorerPreviewSettings
	{
	protected:
		bool m_isPreviewEnabled;
        std::wstring m_name;
        std::wstring m_description;

	public:
		FileExplorerPreviewSettings(bool state, std::wstring name, std::wstring description);
		FileExplorerPreviewSettings(bool state);
		FileExplorerPreviewSettings();

		virtual bool GetState() const;
		virtual void SetState(bool state);
		virtual void LoadState(PowerToysSettings::PowerToyValues &settings);
		virtual void UpdateState(PowerToysSettings::PowerToyValues &values);
        virtual std::wstring GetName() const;
		virtual void SetName(const std::wstring name);
        virtual std::wstring GetDescription() const;
        virtual void SetDescription(const std::wstring description);
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