

//--------------------------------------------------------------------
//
// GetLanguageVersionString
//
// Gets version information for a particular code page.
//
//--------------------------------------------------------------------
#include <stdio.h>
#include "pch.h"
#include "VersionHelper.h"

PTCHAR GetLanguageVersionString(PVERSION_INFO VersionInfo,
    LANGID LanguageId, WORD Charset,
    LPCTSTR VersionString)
{
    TCHAR szQueryStr[0x100];
    PTCHAR pszVerRetVal;
    UINT cbReturn;
    BOOL fFound = FALSE;

    // Format the string  
    _stprintf_s(szQueryStr, ARRAYSIZE(szQueryStr), _T("\\StringFileInfo\\%04X%04X\\%s"),
        LanguageId, Charset,
        VersionString);

    fFound = VerQueryValue(VersionInfo, szQueryStr,
        (LPVOID *)&pszVerRetVal, &cbReturn);

    if (!fFound) {
        return NULL;
    }
    else {
        return pszVerRetVal;
    }
}

//
// explicitly use wide char version
// this is used when show banner in remote session before display eula from console which requires unicode for French
// it's needed for multibyte app
//
PWCHAR GetLanguageVersionStringW(PVERSION_INFO VersionInfo,
    LANGID LanguageId, WORD Charset,
    LPCWSTR VersionString)
{
    WCHAR szQueryStr[0x100];
    PWCHAR pszVerRetVal;
    UINT cbReturn;
    BOOL fFound = FALSE;

    // Format the string  
    swprintf_s(szQueryStr, ARRAYSIZE(szQueryStr), L"\\StringFileInfo\\%04X%04X\\%s",
        LanguageId, Charset,
        VersionString);

    fFound = VerQueryValueW(VersionInfo, szQueryStr,
        (LPVOID *)&pszVerRetVal, &cbReturn);

    if (!fFound) {
        return NULL;
    }
    else {
        return pszVerRetVal;
    }
}

//--------------------------------------------------------------------
//
// GetVersionString
//
// Gets a version string.
//
//--------------------------------------------------------------------
PTCHAR GetVersionString(PVERSION_INFO VersionInfo,
    LPCTSTR VersionString)
{
    PTCHAR pszVerRetVal;
    PVER_TRANSLATION pTranslation;
    VER_TRANSLATION translation;
    unsigned int length;

    //
    // Get the language id
    //
    translation.langID = MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT);
    pTranslation = &translation;
    VerQueryValue(VersionInfo,
        _T("\\VarFileInfo\\Translation"),
        (PVOID *)&pTranslation,
        &length);

    pszVerRetVal = GetLanguageVersionString(VersionInfo,
        pTranslation->langID,
        pTranslation->charset,
        VersionString);
    return pszVerRetVal;
}

//
// explicitly use wide char version
// this is used when show banner in remote session before display eula from console which requires unicode for French
// it's needed for multibyte app
//
PWCHAR GetVersionStringW(PVERSION_INFO VersionInfo,
    LPCWSTR VersionString)
{
    PWCHAR pszVerRetVal;
    PVER_TRANSLATION pTranslation;
    VER_TRANSLATION translation;
    unsigned int length;

    //
    // Get the language id
    //
    translation.langID = MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT);
    pTranslation = &translation;
    VerQueryValueW(VersionInfo,
        L"\\VarFileInfo\\Translation",
        (PVOID *)&pTranslation,
        &length);

    pszVerRetVal = GetLanguageVersionStringW(VersionInfo,
        pTranslation->langID,
        pTranslation->charset,
        VersionString);
    return pszVerRetVal;
}
