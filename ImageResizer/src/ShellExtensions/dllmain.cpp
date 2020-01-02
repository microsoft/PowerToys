#include "stdafx.h"
#include "resource.h"
#include "ShellExtensions_i.h"
#include "dllmain.h"

CShellExtensionsModule _AtlModule;

extern "C" BOOL WINAPI DllMain(HINSTANCE hInstance, DWORD dwReason, LPVOID lpReserved)
{
	return _AtlModule.DllMain(dwReason, lpReserved);
}
