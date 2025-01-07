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
} VERSION_INFO, *PVERSION_INFO;

//
// Version translation
//
typedef struct {
    WORD langID;            // language ID
    WORD charset;            // character set (code page)
} VER_TRANSLATION, *PVER_TRANSLATION;

PTCHAR GetVersionString(PVERSION_INFO VersionInfo, LPCTSTR VersionString);

PTCHAR GetLanguageVersionString(PVERSION_INFO VersionInfo,
    LANGID LanguageId,
    WORD Charset,
    LPCTSTR VersionString);

PWCHAR GetLanguageVersionStringW(PVERSION_INFO VersionInfo,
    LANGID LanguageId, WORD Charset,
    LPCWSTR VersionString);

PWCHAR GetVersionStringW(PVERSION_INFO VersionInfo,
    LPCWSTR VersionString);

