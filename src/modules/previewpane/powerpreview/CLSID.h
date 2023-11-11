#pragma once
#include <guiddef.h>

// 74619BDA-A66B-451D-864C-A7726F5FE650
// CLSID used in manifest file for Preview Handler.
const CLSID CLSID_SHIMActivateSvgPreviewHandler = { 0x74619BDA, 0xA66B, 0x451D, { 0x86, 0x4C, 0xA7, 0x72, 0x6F, 0x5F, 0xE6, 0x50 } };

// ddee2b8a-6807-48a6-bb20-2338174ff779
// CLSID of the .Net Com Class for Preview Handler. Should be included in the registry.dat file under \Classes\CLSID\{guid}.
// More details here: https://learn.microsoft.com/dotnet/framework/interop/registering-assemblies-with-com
const CLSID CLSID_SvgPreviewHandler = { 0xddee2b8a, 0x6807, 0x48a6, { 0xbb, 0x20, 0x23, 0x38, 0x17, 0x4f, 0xf7, 0x79 } };

// E0907A95-6F9A-4D1B-A97A-7D9D2648881E
const CLSID CLSID_SHIMActivateMdPreviewHandler = { 0xE0907A95, 0x6F9A, 0x4D1B, { 0xA9, 0x7A, 0x7D, 0x9D, 0x26, 0x48, 0x88, 0x1E } };

// 45769bcc-e8fd-42d0-947e-02beef77a1f5
const CLSID CLSID_MdPreviewHandler = { 0x45769bcc, 0xe8fd, 0x42d0, { 0x94, 0x7e, 0x02, 0xbe, 0xef, 0x77, 0xa1, 0xf5 } };

// 4F6D533B-4185-43A6-AD75-9B20034B14CA
const CLSID CLSID_SHIMActivatePdfPreviewHandler = { 0x4f6d533b, 0x4185, 0x43a6, { 0xad, 0x75, 0x9b, 0x20, 0x3, 0x4b, 0x14, 0xca } };

// 07665729-6243-4746-95b7-79579308d1b2
const CLSID CLSID_PdfPreviewHandler = { 0x07665729, 0x6243, 0x4746, { 0x95, 0xb7, 0x79, 0x57, 0x93, 0x08, 0xd1, 0xb2 } };

// 9C723B8C-4F5C-4147-9DE4-C2808F9AF66B
const CLSID CLSID_SHIMActivateSvgThumbnailProvider = { 0x9C723B8C, 0x4F5C, 0x4147, { 0x9D, 0xE4, 0xC2, 0x80, 0x8F, 0x9A, 0xF6, 0x6B } };

// 36B27788-A8BB-4698-A756-DF9F11F64F84
const CLSID CLSID_SvgThumbnailProvider = { 0x36B27788, 0xA8BB, 0x4698, { 0xA7, 0x56, 0xDF, 0x9F, 0x11, 0xF6, 0x4F, 0x84 } };

// BCC13D15-9720-4CC4-8371-EA74A274741E
const GUID CLSID_PdfThumbnailProvider = { 0xbcc13d15, 0x9720, 0x4cc4, { 0x83, 0x71, 0xea, 0x74, 0xa2, 0x74, 0x74, 0x1e } };

// 516CB24F-562F-422F-8B01-6B580474D093
const CLSID CLSID_SHIMActivateGcodePreviewHandler = { 0x516cb24f, 0x562f, 0x422f, { 0x8b, 0x1, 0x6b, 0x58, 0x4, 0x74, 0xd0, 0x93 } };

// ec52dea8-7c9f-4130-a77b-1737d0418507
const CLSID CLSID_GcodePreviewHandler = { 0xec52dea8, 0x7c9f, 0x4130, { 0xa7, 0x7b, 0x17, 0x37, 0xd0, 0x41, 0x85, 0x07 } };

// F498BE36-5C94-4EC9-A65A-AD1CF4C38271
const GUID CLSID_SHIMActivateQoiPreviewHandler = { 0xf498be36, 0x5c94, 0x4ec9, { 0xa6, 0x5a, 0xad, 0x1c, 0xf4, 0xc3, 0x82, 0x71 } };

// 8AA07897-C30B-4543-865B-00A0E5A1B32D
const GUID CLSID_QoiPreviewHandler = { 0x8aa07897, 0xc30b, 0x4543, { 0x86, 0x5b, 0x0, 0xa0, 0xe5, 0xa1, 0xb3, 0x2d } };

// BFEE99B4-B74D-4348-BCA5-E757029647FF
const GUID CLSID_GcodeThumbnailProvider = { 0xbfee99b4, 0xb74d, 0x4348, { 0xbc, 0xa5, 0xe7, 0x57, 0x02, 0x96, 0x47, 0xff } };

// 8BC8AFC2-4E7C-4695-818E-8C1FFDCEA2AF
const GUID CLSID_StlThumbnailProvider = { 0x8bc8afc2, 0x4e7c, 0x4695, { 0x81, 0x8e, 0x8c, 0x1f, 0xfd, 0xce, 0xa2, 0xaf } };
 
// 907B7E38-38ED-42E7-A276-9EF0ECABB003
const GUID CLSID_QoiThumbnailProvider = { 0x907b7e38, 0x38ed, 0x42e7, { 0xa2, 0x76, 0x9e, 0xf0, 0xec, 0xab, 0xb0, 0x3 } };

// Pairs of NativeClsid vs ManagedClsid used for preview handlers.
const std::vector<std::pair<CLSID, CLSID>> NativeToManagedClsid({ 
    { CLSID_SHIMActivateMdPreviewHandler, CLSID_MdPreviewHandler },
    { CLSID_SHIMActivatePdfPreviewHandler, CLSID_PdfPreviewHandler },
    { CLSID_SHIMActivateGcodePreviewHandler, CLSID_GcodePreviewHandler },
    { CLSID_SHIMActivateQoiPreviewHandler, CLSID_QoiPreviewHandler },
    { CLSID_SHIMActivateSvgPreviewHandler, CLSID_SvgPreviewHandler },
    { CLSID_SHIMActivateSvgThumbnailProvider, CLSID_SvgThumbnailProvider }
});
