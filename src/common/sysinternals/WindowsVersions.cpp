#include <Windows.h>

#include "WindowsVersions.h"

// Declared in wdm.h
typedef NTSYSAPI NTSTATUS (NTAPI *RtlGetVersionType)( PRTL_OSVERSIONINFOW );

DWORD GetWindowsBuild( DWORD* revision )
{
    if( revision ) {

        DWORD size = sizeof( *revision );
        if( RegGetValueW( HKEY_LOCAL_MACHINE, L"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion", L"UBR", RRF_RT_REG_DWORD, NULL, revision, &size ) != ERROR_SUCCESS ) {

            *revision = 0;
        }
    }

    RtlGetVersionType pRtlGetVersion = reinterpret_cast<RtlGetVersionType>(GetProcAddress( GetModuleHandleW( L"ntdll.dll" ), "RtlGetVersion" ));
    
    RTL_OSVERSIONINFOW version;
    pRtlGetVersion( &version );
    return version.dwBuildNumber;
}
