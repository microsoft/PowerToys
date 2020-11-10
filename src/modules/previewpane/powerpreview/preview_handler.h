#pragma once
#include "settings.h"

namespace PowerPreviewSettings
{
    class PreviewHandlerSettings :
        public FileExplorerPreviewSettings
    {
    private:
        // Relative(HKLM/HKCU) sub key path of Preview Handlers list in registry. Registry key for Preview Handlers is generally HKLM\Software\Microsoft\Windows\CurrentVersion\PreviewHandlers, and the value name with CLSID of the handler in it is set to the name of the handler
        static const LPCWSTR preview_handlers_subkey;

    public:
        PreviewHandlerSettings(bool toggleSettingEnabled, const std::wstring& toggleSettingName, const std::wstring& toggleSettingDescription, LPCWSTR clsid, const std::wstring& registryValueData, std::unique_ptr<RegistryWrapperIface> registryWrapper) :
            FileExplorerPreviewSettings(toggleSettingEnabled, toggleSettingName, toggleSettingDescription, clsid, registryValueData, std::move(registryWrapper))
        {
        }

        // Function to enable the preview handler in registry
        LONG Enable();

        // Function to disable the preview handler in registry
        LONG Disable();

        // Function to check if the preview handler is enabled in registry
        bool CheckRegistryState();

        // Function to retrieve the registry subkey
        static LPCWSTR GetSubkey();
    };
}
