#pragma once
#include "stdafx.h"
#include <PowerRenameItem.h>
#include "srwlock.h"

class CMockPowerRenameItem :
    public CPowerRenameItem
{
public:
    static HRESULT CreateInstance(_In_opt_ PCWSTR path, _In_opt_ PCWSTR originalName, _In_ UINT depth, _In_ bool isFolder, _Outptr_ IPowerRenameItem** ppItem);
    void Init(_In_opt_ PCWSTR path, _In_opt_ PCWSTR originalName, _In_ UINT depth, _In_ bool isFolder);
};