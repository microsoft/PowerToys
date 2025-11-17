//=========================================================================-==
//
// dll.c
//
// DLL support functions
//
//============================================================================

#include <windows.h>
#include <assert.h>
#include <tchar.h>
#include <stdlib.h>
#include "dll.h"

#ifndef LOAD_LIBRARY_SEARCH_SYSTEM32
    #define LOAD_LIBRARY_SEARCH_SYSTEM32 0x800
#endif


//=========================================================================-==
//
// ExtendedFlagsSupported
//
// Returns TRUE if running on Windows 7 or later and FALSE otherwise
//
//============================================================================
static BOOLEAN ExtendedFlagsSupported()
{
    OSVERSIONINFO osInfo;
    BOOLEAN rc = FALSE;

    ZeroMemory(&osInfo, sizeof(OSVERSIONINFO));
    osInfo.dwOSVersionInfoSize = sizeof(OSVERSIONINFO);

#pragma warning ( disable : 4996 )  // deprecated in favour of version helper functions which we can't use 

    if (GetVersionEx(&osInfo) && (osInfo.dwMajorVersion > 6 || (osInfo.dwMajorVersion == 6 && osInfo.dwMinorVersion > 0)))
        rc = TRUE;

#pragma warning ( default : 4996 ) 

    return rc;
}

//=========================================================================-==
//
// LoadLibrarySafe
//
// Loads a DLL from the system folder in a way that mitigates DLL spoofing /
// side-loading attacks
//
//============================================================================
HMODULE LoadLibrarySafe(LPCTSTR libraryName, DLL_LOAD_LOCATION location)
{
    HMODULE hMod = NULL;

    if (NULL == libraryName || location <= DLL_LOAD_LOCATION_MIN || location >= DLL_LOAD_LOCATION_MAX) {

        SetLastError(ERROR_INVALID_PARAMETER);
        return NULL;
    }

    // LOAD_LIBRARY_SEARCH_SYSTEM32 is only supported on Window 7 or later. On earlier SKUs we could use a fully
    // qualified path to the system folder but specifying a path causes Ldr to skip SxS file redirection. This can 
    // cause the wrong library to be loaded if the application is using a manifest that defines a specific version 
    // of Microsoft.Windows.Common-Controls when loading comctl32.dll
    if (DLL_LOAD_LOCATION_SYSTEM == location) {

        DWORD flags = ExtendedFlagsSupported() ? LOAD_LIBRARY_SEARCH_SYSTEM32 : 0;
        hMod = LoadLibraryEx(libraryName, NULL, flags);
    }

    return hMod;
}
