#pragma once
#include "settings.h"

namespace PowerPreviewSettings
{
    class ThumbnailProvider :
        public FileExplorerPreviewSettings
    {
    private:
        // Relative HKCR sub key path of thumbnail provider in registry. Registry key for Thumbnail Providers is generally HKCR\fileExtension\{E357FCCD-A995-4576-B01F-234630154E96}, and the default value in it is set to the CLSID of the provider
        LPCWSTR thumbnail_provider_subkey;

    public:
        ThumbnailProvider(bool toggleSettingEnabled, const std::wstring& toggleSettingName, const std::wstring& toggleSettingDescription, LPCWSTR clsid, const std::wstring& registryValueData, RegistryWrapperIface* registryWrapper, LPCWSTR subkey) :
            FileExplorerPreviewSettings(toggleSettingEnabled, toggleSettingName, toggleSettingDescription, clsid, registryValueData, registryWrapper), thumbnail_provider_subkey(subkey)
        {
        }

        // Function to enable the thumbnail provider in registry
        LONG Enable();

        // Function to disable the thumbnail provider in registry
        LONG Disable();

        // Function to check if the thumbnail provider is enabled in registry
        bool CheckRegistryState();
    };
}
