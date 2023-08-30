#pragma once

#include "targetver.h"

#define WIN32_LEAN_AND_MEAN // Exclude rarely-used stuff from Windows headers
#define NOMINMAX
// Windows Header Files:
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <windows.h>
#include <unknwn.h>
#include <shlwapi.h>
#include <atlbase.h>
#include <atlcom.h>
#include <atlfile.h>
#include <atlstr.h>
#include <Shobjidl.h>
#include <Shlobj.h>
#include "CLSID.h"

void ModuleAddRef();
void ModuleRelease();
