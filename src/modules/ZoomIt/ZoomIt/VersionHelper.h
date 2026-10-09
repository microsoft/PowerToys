//============================================================================
//
// VersionHelper.h
//
// Functions to retrieve version information
//
//============================================================================

#pragma once

#include <windows.h>
#include <tchar.h>
#include "winver.h"

//
// File version information
//
typedef struct {
    WORD    wLength;
    WORD    wValueLength;
    WORD    wType;
    WCHAR    szKey[16];
    WORD    Padding1;
    VS_FIXEDFILEINFO Value;
} VERSION_INFO, *P_VERSION_INFO;

//
// Version translation
//
typedef struct {
    WORD langID;            // language ID
    WORD charset;            // character set (code page)
} VERSION_TRANSLATION, *P_VERSION_TRANSLATION;

PTCHAR GetVersionString(P_VERSION_INFO VersionInfo, LPCTSTR VersionString);

PTCHAR GetLanguageVersionString(P_VERSION_INFO VersionInfo,
    LANGID LanguageId,
    WORD Charset,
    LPCTSTR VersionString);

PWCHAR GetLanguageVersionStringW(P_VERSION_INFO VersionInfo,
    LANGID LanguageId, WORD Charset,
    LPCWSTR VersionString);

PWCHAR GetVersionStringW(P_VERSION_INFO VersionInfo,
    LPCWSTR VersionString);

