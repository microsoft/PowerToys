// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

#pragma once

#define MRU_CACHEWRITE 0x0002
#define REGSTR_PATH_EXPLORER TEXT("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer")

// https://learn.microsoft.com/en-us/windows/win32/shell/mrucmpproc
typedef int(CALLBACK* MRUCMPPROC)(
    LPCTSTR pString1,
    LPCTSTR pString2);

// https://learn.microsoft.com/en-us/windows/win32/shell/mruinfo
struct MRUINFO
{
    DWORD cbSize;
    UINT uMax;
    UINT fFlags;
    HKEY hKey;
    LPCTSTR lpszSubKey;
    MRUCMPPROC lpfnCompare;
};
