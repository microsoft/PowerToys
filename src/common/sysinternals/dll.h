//=========================================================================-==
//
// dll.h
//
// DLL support functions
//
//============================================================================
#pragma once

#ifdef __cplusplus
extern "C" {
#endif

    typedef enum
    {
        DLL_LOAD_LOCATION_MIN = 0,
        DLL_LOAD_LOCATION_SYSTEM = 1,
        DLL_LOAD_LOCATION_MAX
    } DLL_LOAD_LOCATION, *PDLL_LOAD_LOCATION;
    
    HMODULE LoadLibrarySafe(LPCTSTR libraryName, DLL_LOAD_LOCATION location);

#ifdef __cplusplus
}
#endif

