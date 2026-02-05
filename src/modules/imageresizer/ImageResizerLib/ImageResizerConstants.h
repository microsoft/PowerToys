#pragma once
#include <string>
#include <vector>
#include <algorithm>

namespace ImageResizerConstants
{
    // Name of the powertoy module.
    inline const std::wstring ModuleKey = L"Image Resizer";

    // Name of the ImageResizer save folder.
    inline const std::wstring ModuleOldSaveFolderKey = L"ImageResizer";
    inline const std::wstring ModuleSaveFolderKey = L"Image Resizer";
    inline const std::wstring ModulePackageDisplayName = L"ImageResizerContextMenu";

    // List of supported image extensions that Image Resizer can process
    // This must match the list in RuntimeRegistration.h
    inline const std::vector<std::wstring> SupportedImageExtensions = {
        L".bmp", L".dib", L".gif", L".jfif", L".jpe", L".jpeg", L".jpg", 
        L".jxr", L".png", L".rle", L".tif", L".tiff", L".wdp"
    };

    // Helper function to check if a file extension is supported by Image Resizer
    inline bool IsSupportedImageExtension(LPCWSTR extension)
    {
        if (nullptr == extension || wcslen(extension) == 0)
        {
            return false;
        }

        // Convert to lowercase for case-insensitive comparison
        std::wstring ext(extension);
        std::transform(ext.begin(), ext.end(), ext.begin(), ::towlower);

        return std::find(SupportedImageExtensions.begin(), SupportedImageExtensions.end(), ext) != SupportedImageExtensions.end();
    }
}
