//----------------------------------------------------------------------
//
// WindowsVersions.h
//
// Provides helpers for Windows builds and versions.
//
//----------------------------------------------------------------------

#pragma once

#define BUILD_WINDOWS_SERVER_2008        6003
#define BUILD_WINDOWS_SERVER_2008_R2     7601
#define BUILD_WINDOWS_SERVER_2012        9200
#define BUILD_WINDOWS_8_1                9600
#define BUILD_WINDOWS_SERVER_2012_R2     9600
#define BUILD_WINDOWS_10_1507           10240
#define BUILD_WINDOWS_10_1607           14393
#define BUILD_WINDOWS_SERVER_2016       14393
#define BUILD_WINDOWS_10_1809           17763
#define BUILD_WINDOWS_SERVER_2019       17763
#define BUILD_WINDOWS_10_1903           18362
#define BUILD_WINDOWS_10_1909           18363
#define BUILD_WINDOWS_10_2004           19041
#define BUILD_WINDOWS_10_20H2           19042
#define BUILD_WINDOWS_SERVER_20H2       19042
#define BUILD_WINDOWS_10_21H1           19043
#define BUILD_WINDOWS_10_21H2           19044
#define BUILD_WINDOWS_SERVER_2022       20348
#define BUILD_WINDOWS_11_21H2           22000
#define BUILD_WINDOWS_11_22H2           22621

DWORD GetWindowsBuild( DWORD* revision );
