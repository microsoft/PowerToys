#pragma once

#include "PowerRenameInterfaces.h"

#include <string>

HRESULT GetTrimmedFileName(_Out_ PWSTR result, UINT cchMax, _In_ PCWSTR source);
HRESULT GetTransformedFileName(_Out_ PWSTR result, UINT cchMax, _In_ PCWSTR source, DWORD flags, bool isFolder);
HRESULT GetDatedFileName(_Out_ PWSTR result, UINT cchMax, _In_ PCWSTR source, SYSTEMTIME fileTime);
bool isFileTimeUsed(_In_ PCWSTR source);
bool ShellItemArrayContainsRenamableItem(_In_ IShellItemArray* shellItemArray);
bool DataObjectContainsRenamableItem(_In_ IUnknown* dataSource);
HRESULT GetShellItemArrayFromDataObject(_In_ IUnknown* dataSource, _COM_Outptr_ IShellItemArray** items);
BOOL GetEnumeratedFileName(
    __out_ecount(cchMax) PWSTR pszUniqueName,
    UINT cchMax,
    __in PCWSTR pszTemplate,
    __in_opt PCWSTR pszDir,
    unsigned long ulMinLong,
    __inout unsigned long* pulNumUsed);
HWND CreateMsgWindow(_In_ HINSTANCE hInst, _In_ WNDPROC pfnWndProc, _In_ void* p);

std::wstring GetRegString(const std::wstring& valueName, const std::wstring& subPath);
unsigned int GetRegNumber(const std::wstring& valueName, unsigned int defaultValue);
void SetRegNumber(const std::wstring& valueName, unsigned int value);
bool GetRegBoolean(const std::wstring& valueName, bool defaultValue);
void SetRegBoolean(const std::wstring& valueName, bool value);
bool LastModifiedTime(const std::wstring& filePath, FILETIME* lpFileTime);
std::wstring CreateGuidStringWithoutBrackets();