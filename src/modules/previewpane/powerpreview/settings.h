#pragma once
#include <pch.h>
#include <string>
#include "resource.h"
#include <settings_objects.h>
#include "registry_wrapper_interface.h"

namespace PowerPreviewSettings
{
	// PowerToy Winodws Explore File Preview Settings.
	class FileExplorerPreviewSettings
	{
	private:
		bool m_toggleSettingEnabled;
        std::wstring m_toggleSettingName;
        std::wstring m_toggleSettingDescription;
		std::wstring m_registryValueData;
        RegistryWrapperIface * m_registryWrapper;
		LPCWSTR m_clsid;

	public:
        FileExplorerPreviewSettings(bool toggleSettingEnabled, const std::wstring& toggleSettingName, const std::wstring& toggleSettingDescription, LPCWSTR clsid, const std::wstring& registryValueData, RegistryWrapperIface* registryWrapper);
        ~ FileExplorerPreviewSettings();

		virtual bool GetToggleSettingState() const;
        virtual void UpdateToggleSettingState(bool state);
		virtual std::wstring GetToggleSettingName() const;
        virtual std::wstring GetToggleSettingDescription() const;
        virtual LPCWSTR GetCLSID() const;
        virtual std::wstring GetRegistryValueData() const;
        virtual void LoadState(PowerToysSettings::PowerToyValues& settings);
        virtual void UpdateState(PowerToysSettings::PowerToyValues& settings, bool enabled);
        virtual LONG EnablePreview();
        virtual LONG DisablePreview();
	};
}
