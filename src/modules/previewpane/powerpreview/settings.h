#pragma once
#include <pch.h>
#include <string>
#include "resource.h"
#include <settings_objects.h>

namespace PowerPreviewSettings
{
	// PowerToy Winodws Explore File Preview Settings.
	class FileExplorerPreviewSettings
	{
	private:
		bool m_isPreviewEnabled;
        std::wstring m_name;
        std::wstring m_description;
		std::wstring m_displayName;

		LPCWSTR m_clsid;
		LPCWSTR m_subKey = L"Software\\Microsoft\\Windows\\CurrentVersion\\PreviewHandlers";

	public:
        FileExplorerPreviewSettings(bool state, const std::wstring name, const std::wstring description, LPCWSTR clsid, const std::wstring displayname);
		FileExplorerPreviewSettings();

		virtual bool GetState() const;
		virtual void SetState(bool state);
        virtual void LoadState(PowerToysSettings::PowerToyValues &settings);
		virtual void UpdateState(PowerToysSettings::PowerToyValues &values);
        virtual std::wstring GetName() const;
		virtual void SetName(const std::wstring &name);
        virtual std::wstring GetDescription() const;
        virtual void SetDescription(const std::wstring &description);
		virtual void SetDisplayName(const std::wstring &displayName);
		virtual LONG SetRegistryValue() const;
		virtual LONG RemvRegistryValue() const;
	    virtual bool GetRegistryValue() const;
		virtual std::wstring GetDisplayName() const;
		virtual LPCWSTR GetCLSID() const;
		virtual LPCWSTR GetSubKey() const;
		virtual void EnablePreview() = 0;
		virtual void DisabledPreview() = 0;
	};

	class PrevPaneSVGRendrSettings: public FileExplorerPreviewSettings
	{
	public:
		PrevPaneSVGRendrSettings();

		virtual void EnablePreview();
		virtual void DisabledPreview();
	};

	class PrevPaneMDRendrSettings: public FileExplorerPreviewSettings
	{
	public:
		PrevPaneMDRendrSettings();

		virtual void EnablePreview();
		virtual void DisabledPreview();
	};

}