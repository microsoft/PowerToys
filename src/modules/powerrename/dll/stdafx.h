#pragma once

#include "targetver.h"

#define WIN32_LEAN_AND_MEAN // Exclude rarely-used stuff from Windows headers
// Windows Header Files:
#include <winrt/base.h>
#include <windows.h>
#include <unknwn.h>
#include <shlwapi.h>
#include <atlbase.h>
#include <Shobjidl.h>
#include <Shlobj.h>
#include "CLSID.h"

void ModuleAddRef();
void ModuleRelease();
