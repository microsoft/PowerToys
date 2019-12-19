#pragma once

#include <common.h>
#include <lib/PowerRenameInterfaces.h>

HRESULT EnumerateDataObject(_In_ IUnknown* pdo, _In_ IPowerRenameManager* psrm);
HRESULT GetIconIndexFromPath(_In_ PCWSTR path, _Out_ int* index);
HBITMAP CreateBitmapFromIcon(_In_ HICON hIcon, _In_opt_ UINT width = 0, _In_opt_ UINT height = 0);
HWND CreateMsgWindow(_In_ HINSTANCE hInst, _In_ WNDPROC pfnWndProc, _In_ void* p);
BOOL GetEnumeratedFileName(
    __out_ecount(cchMax) PWSTR pszUniqueName, UINT cchMax,
    __in PCWSTR pszTemplate, __in_opt PCWSTR pszDir, unsigned long ulMinLong,
    __inout unsigned long* pulNumUsed);