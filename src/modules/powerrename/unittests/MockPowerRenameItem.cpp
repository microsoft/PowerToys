#include "pch.h"
#include "MockPowerRenameItem.h"

HRESULT CMockPowerRenameItem::CreateInstance(_In_opt_ PCWSTR path, _In_opt_ PCWSTR originalName, _In_ UINT depth, _In_ bool isFolder, _In_ SYSTEMTIME time, _Outptr_ IPowerRenameItem** ppItem)
{
    *ppItem = nullptr;
    CMockPowerRenameItem* newItem = new CMockPowerRenameItem();
    HRESULT hr = E_OUTOFMEMORY;
    if (newItem)
    {
        newItem->Init(path, originalName, depth, isFolder, time);
        hr = newItem->QueryInterface(IID_PPV_ARGS(ppItem));
        newItem->Release();
    }

    return hr;
}

void CMockPowerRenameItem::Init(_In_opt_ PCWSTR path, _In_opt_ PCWSTR originalName, _In_ UINT depth, _In_ bool isFolder, _In_ SYSTEMTIME time)
{
    if (path != nullptr)
    {
        SHStrDup(path, &m_path);
    }

    if (originalName != nullptr)
    {
        SHStrDup(originalName, &m_originalName);
    }

    m_depth = depth;
    m_isFolder = isFolder;
    m_time = time;
    m_isTimeParsed = true;
}
