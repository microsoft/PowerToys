#pragma once

#include "targetver.h"

#define WIN32_LEAN_AND_MEAN             // Exclude rarely-used stuff from Windows headers
// Windows Header Files:
#include <winrt/base.h>
#include <windows.h>
#include <unknwn.h>
#include <shlwapi.h>
#include <atlbase.h>
#include <Shobjidl.h>
#include <Shlobj.h>
#include "common/common.h"

void DllAddRef();
void DllRelease();

#define INITGUID
#include <guiddef.h>

// {0440049F-D1DC-4E46-B27B-98393D79486B}
DEFINE_GUID(CLSID_PowerRenameMenu, 0x0440049F, 0xD1DC, 0x4E46, 0xB2, 0x7B, 0x98, 0x39, 0x3D, 0x79, 0x48, 0x6B);

#pragma comment(linker,"/manifestdependency:\"type='win32' name='Microsoft.Windows.Common-Controls' version='6.0.0.0' processorArchitecture='*' publicKeyToken='6595b64144ccf1df' language='*'\"")

