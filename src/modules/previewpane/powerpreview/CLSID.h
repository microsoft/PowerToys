#pragma once
#include <guiddef.h>

// 74619BDA-A66B-451D-864C-A7726F5FE650
// CLSID used in manifest file for Preview Handler.
const CLSID CLSID_SHIMActivateSvgPreviewHandler = { 0x74619BDA, 0xA66B, 0x451D, { 0x86, 0x4C, 0xA7, 0x72, 0x6F, 0x5F, 0xE6, 0x50 } };

// ddee2b8a-6807-48a6-bb20-2338174ff779
// CLSID of the .Net Com Class for Preview Handler. Should be included in the registry.dat file under \Classes\CLSID\{guid}.
// More details here: https://docs.microsoft.com/en-us/dotnet/framework/interop/registering-assemblies-with-com
const CLSID CLSID_SvgPreviewHandler = { 0xddee2b8a, 0x6807, 0x48a6, { 0xbb, 0x20, 0x23, 0x38, 0x17, 0x4f, 0xf7, 0x79 } };

// E0907A95-6F9A-4D1B-A97A-7D9D2648881E
const CLSID CLSID_SHIMActivateMdPreviewHandler = { 0xE0907A95, 0x6F9A, 0x4D1B, { 0xA9, 0x7A, 0x7D, 0x9D, 0x26, 0x48, 0x88, 0x1E } };

// 45769bcc-e8fd-42d0-947e-02beef77a1f5
const CLSID CLSID_MdPreviewHandler = { 0x45769bcc, 0xe8fd, 0x42d0, { 0x94, 0x7e, 0x02, 0xbe, 0xef, 0x77, 0xa1, 0xf5 } };

// 9C723B8C-4F5C-4147-9DE4-C2808F9AF66B
const CLSID CLSID_SHIMActivateSvgThumbnailProvider = { 0x9C723B8C, 0x4F5C, 0x4147, { 0x9D, 0xE4, 0xC2, 0x80, 0x8F, 0x9A, 0xF6, 0x6B } };

// 36B27788-A8BB-4698-A756-DF9F11F64F84
const CLSID CLSID_SvgThumbnailProvider = { 0x36B27788, 0xA8BB, 0x4698, { 0xA7, 0x56, 0xDF, 0x9F, 0x11, 0xF6, 0x4F, 0x84 } };

// Pairs of NativeClsid vs ManagedClsid used for preview handlers.
const std::vector<std::pair<CLSID, CLSID>> NativeToManagedClsid({
    { CLSID_SHIMActivateMdPreviewHandler, CLSID_MdPreviewHandler },
    { CLSID_SHIMActivateSvgPreviewHandler, CLSID_SvgPreviewHandler },
    { CLSID_SHIMActivateSvgThumbnailProvider, CLSID_SvgThumbnailProvider }
});