#pragma once
#include "stdafx.h"

HRESULT EnumerateDataObject(_In_ IDataObject* pdo, _In_ IPowerRenameManager* psrm);
HRESULT GetIconIndexFromPath(_In_ PCWSTR path, _Out_ int* index);
HWND CreateMsgWindow(_In_ HINSTANCE hInst, _In_ WNDPROC pfnWndProc, _In_ void* p);
BOOL GetEnumeratedFileName(
    __out_ecount(cchMax) PWSTR pszUniqueName, UINT cchMax,
    __in PCWSTR pszTemplate, __in_opt PCWSTR pszDir, unsigned long ulMinLong,
    __inout unsigned long* pulNumUsed);