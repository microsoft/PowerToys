#pragma once

#include <common.h>
#include <lib/PowerRenameInterfaces.h>

HRESULT EnumerateDataObject(_In_ IUnknown* pdo, _In_ IPowerRenameManager* psrm);
BOOL GetEnumeratedFileName(
    __out_ecount(cchMax) PWSTR pszUniqueName,
    UINT cchMax,
    __in PCWSTR pszTemplate,
    __in_opt PCWSTR pszDir,
    unsigned long ulMinLong,
    __inout unsigned long* pulNumUsed);