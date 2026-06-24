#include "pch.h"
#include "PowerRenameManager.h"
#include "PowerRenameRegEx.h" // Default RegEx handler
#include <algorithm>
#include <shlobj.h>
#include <cstring>
#include "helpers.h"
#include "trace.h"
#include <Renaming.h>

namespace fs = std::filesystem;

extern HINSTANCE g_hostHInst;

// The default FOF flags to use in the rename operations
#define FOF_DEFAULTFLAGS (FOF_ALLOWUNDO | FOFX_ADDUNDORECORD | FOFX_SHOWELEVATIONPROMPT | FOF_RENAMEONCOLLISION)

IFACEMETHODIMP_(ULONG)
CPowerRenameManager::AddRef()
{
    return InterlockedIncrement(&m_refCount);
}

IFACEMETHODIMP_(ULONG)
CPowerRenameManager::Release()
{
    long refCount = InterlockedDecrement(&m_refCount);

    if (refCount == 0)
    {
        delete this;
    }
    return refCount;
}

IFACEMETHODIMP CPowerRenameManager::QueryInterface(_In_ REFIID riid, _Outptr_ void** ppv)
{
    static const QITAB qit[] = {
        QITABENT(CPowerRenameManager, IPowerRenameManager),
        QITABENT(CPowerRenameManager, IPowerRenameRegExEvents),
        { 0 }
    };
    return QISearch(this, qit, riid, ppv);
}

IFACEMETHODIMP CPowerRenameManager::Advise(_In_ IPowerRenameManagerEvents* renameOpEvents, _Out_ DWORD* cookie)
{
    CSRWExclusiveAutoLock lock(&m_lockEvents);
    m_cookie++;
    RENAME_MGR_EVENT srme;
    srme.cookie = m_cookie;
    srme.pEvents = renameOpEvents;
    renameOpEvents->AddRef();
    m_powerRenameManagerEvents.push_back(srme);

    *cookie = m_cookie;

    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::UnAdvise(_In_ DWORD cookie)
{
    HRESULT hr = E_FAIL;
    CSRWExclusiveAutoLock lock(&m_lockEvents);

    for (std::vector<RENAME_MGR_EVENT>::iterator it = m_powerRenameManagerEvents.begin(); it != m_powerRenameManagerEvents.end(); ++it)
    {
        if (it->cookie == cookie)
        {
            hr = S_OK;
            it->cookie = 0;
            if (it->pEvents)
            {
                it->pEvents->Release();
                it->pEvents = nullptr;
            }
            break;
        }
    }

    return hr;
}

IFACEMETHODIMP CPowerRenameManager::Start()
{
    return E_NOTIMPL;
}

IFACEMETHODIMP CPowerRenameManager::Stop()
{
    return E_NOTIMPL;
}

IFACEMETHODIMP CPowerRenameManager::Rename(_In_ HWND hwndParent, bool closeWindow)
{
    m_hwndParent = hwndParent;
    m_closeUIWindowAfterRenaming = closeWindow;
    return _PerformFileOperation();
}

IFACEMETHODIMP CPowerRenameManager::UpdateChildrenPath(_In_ int parentId, _In_ size_t oldParentPathSize)
{
    auto parentIt = m_renameItems.find(parentId);
    if (parentIt != m_renameItems.end())
    {
        UINT depth = 0;
        winrt::check_hresult(parentIt->second->GetDepth(&depth));

        PWSTR renamedPath = nullptr;
        winrt::check_hresult(parentIt->second->GetPath(&renamedPath));
        std::wstring renamedPathStr{ renamedPath };

        for (auto it = ++parentIt; it != m_renameItems.end(); ++it)
        {
            UINT nextDepth = 0;
            winrt::check_hresult(it->second->GetDepth(&nextDepth));

            if (nextDepth > depth)
            {
                // This is child, update path
                PWSTR path = nullptr;
                winrt::check_hresult(it->second->GetPath(&path));
                std::wstring pathStr{ path };

                std::wstring newPath = pathStr.replace(0, oldParentPathSize, renamedPath);
                it->second->PutPath(newPath.c_str());
            }
            else
            {
                break;
            }
        }
    }

    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::GetCloseUIWindowAfterRenaming(_Out_ bool* closeUIWindowAfterRenaming)
{
    *closeUIWindowAfterRenaming = m_closeUIWindowAfterRenaming;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::Reset()
{
    // Stop all threads and wait
    // Reset all rename items
    return E_NOTIMPL;
}

IFACEMETHODIMP CPowerRenameManager::Shutdown()
{
    _ClearRegEx();
    _Cleanup();
    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::AddItem(_In_ IPowerRenameItem* pItem)
{
    HRESULT hr = E_FAIL;
    // Scope lock
    {
        CSRWExclusiveAutoLock lock(&m_lockItems);
        int id = 0;
        pItem->GetId(&id);
        // Verify the item isn't already added
        if (m_renameItems.find(id) == m_renameItems.end())
        {
            m_renameItems[id] = pItem;
            m_isVisible.push_back(true);
            pItem->AddRef();
            hr = S_OK;
        }
    }

    return hr;
}

IFACEMETHODIMP CPowerRenameManager::GetItemByIndex(_In_ UINT index, _COM_Outptr_ IPowerRenameItem** ppItem)
{
    *ppItem = nullptr;
    CSRWSharedAutoLock lock(&m_lockItems);
    HRESULT hr = E_FAIL;
    if (index < m_renameItems.size())
    {
        std::map<int, IPowerRenameItem*>::iterator it = m_renameItems.begin();
        std::advance(it, index);
        *ppItem = it->second;
        (*ppItem)->AddRef();
        hr = S_OK;
    }

    return hr;
}

IFACEMETHODIMP CPowerRenameManager::GetVisibleItemByIndex(_In_ UINT index, _COM_Outptr_ IPowerRenameItem** ppItem)
{
    *ppItem = nullptr;
    CSRWSharedAutoLock lock(&m_lockItems);
    UINT count = 0;
    HRESULT hr = E_FAIL;

    if (m_filter == PowerRenameFilters::None)
    {
        hr = GetItemByIndex(index, ppItem);
    }
    else if (SUCCEEDED(GetVisibleItemCount(&count)) && index < count)
    {
        const UINT realIndex = GetVisibleItemRealIndex(index);
        hr = GetItemByIndex(realIndex, ppItem);
    }

    return hr;
}

uint32_t CPowerRenameManager::GetVisibleItemRealIndex(const uint32_t index) const
{
    UINT realIndex = 0, visibleIndex = 0;
    for (size_t i = 0; i < m_isVisible.size(); i++)
    {
        if (m_isVisible[i] && visibleIndex == index)
        {
            realIndex = static_cast<UINT>(i);
            break;
        }
        if (m_isVisible[i])
        {
            visibleIndex++;
        }
    }

    return realIndex;
}

IFACEMETHODIMP CPowerRenameManager::GetItemById(_In_ int id, _COM_Outptr_ IPowerRenameItem** ppItem)
{
    *ppItem = nullptr;

    CSRWSharedAutoLock lock(&m_lockItems);
    HRESULT hr = E_FAIL;
    std::map<int, IPowerRenameItem*>::iterator it;
    it = m_renameItems.find(id);
    if (it != m_renameItems.end())
    {
        *ppItem = m_renameItems[id];
        (*ppItem)->AddRef();
        hr = S_OK;
    }

    return hr;
}

IFACEMETHODIMP CPowerRenameManager::GetItemCount(_Out_ UINT* count)
{
    CSRWSharedAutoLock lock(&m_lockItems);
    *count = static_cast<UINT>(m_renameItems.size());
    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::SetVisible()
{
    CSRWSharedAutoLock lock(&m_lockItems);
    HRESULT hr = E_FAIL;
    UINT lastVisibleDepth = 0;
    size_t i = m_isVisible.size() - 1;
    PWSTR searchTerm = nullptr;
    for (auto rit = m_renameItems.rbegin(); rit != m_renameItems.rend(); ++rit, --i)
    {
        bool isVisible = false;
        if (m_filter == PowerRenameFilters::ShouldRename &&
            (FAILED(m_spRegEx->GetSearchTerm(&searchTerm)) || searchTerm && wcslen(searchTerm) == 0))
        {
            isVisible = true;
        }
        else
        {
            rit->second->IsItemVisible(m_filter, m_flags, &isVisible);
        }

        UINT itemDepth = 0;
        rit->second->GetDepth(&itemDepth);

        //Make an item visible if it has a least one visible subitem
        if (isVisible)
        {
            lastVisibleDepth = itemDepth;
        }
        else if (lastVisibleDepth == itemDepth + 1)
        {
            isVisible = true;
            lastVisibleDepth = itemDepth;
        }

        m_isVisible[i] = isVisible;
        hr = S_OK;
    }

    return hr;
}

IFACEMETHODIMP CPowerRenameManager::GetVisibleItemCount(_Out_ UINT* count)
{
    *count = 0;
    CSRWSharedAutoLock lock(&m_lockItems);

    if (m_filter != PowerRenameFilters::None)
    {
        SetVisible();

        for (size_t i = 0; i < m_isVisible.size(); i++)
        {
            if (m_isVisible[i])
            {
                (*count)++;
            }
        }
    }
    else
    {
        GetItemCount(count);
    }

    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::GetSelectedItemCount(_Out_ UINT* count)
{
    *count = 0;
    CSRWSharedAutoLock lock(&m_lockItems);

    for (auto it : m_renameItems)
    {
        IPowerRenameItem* pItem = it.second;
        bool selected = false;
        if (SUCCEEDED(pItem->GetSelected(&selected)) && selected)
        {
            (*count)++;
        }
    }

    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::GetRenameItemCount(_Out_ UINT* count)
{
    *count = 0;
    CSRWSharedAutoLock lock(&m_lockItems);

    for (auto it : m_renameItems)
    {
        IPowerRenameItem* pItem = it.second;
        bool shouldRename = false;
        if (SUCCEEDED(pItem->ShouldRenameItem(m_flags, &shouldRename)) && shouldRename)
        {
            (*count)++;
        }
    }

    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::GetFlags(_Out_ DWORD* flags)
{
    _EnsureRegEx();
    *flags = m_flags;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::PutFlags(_In_ DWORD flags)
{
    if (flags != m_flags)
    {
        m_flags = flags;
        _EnsureRegEx();
        m_spRegEx->PutFlags(flags);
    }
    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::GetFilter(_Out_ DWORD* filter)
{
    *filter = m_filter;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::SwitchFilter(_In_ int)
{
    switch (m_filter)
    {
    case PowerRenameFilters::None:
        m_filter = PowerRenameFilters::ShouldRename;
        break;
    case PowerRenameFilters::ShouldRename:
        m_filter = PowerRenameFilters::None;
        break;
    }

    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::GetRenameRegEx(_COM_Outptr_ IPowerRenameRegEx** ppRegEx)
{
    *ppRegEx = nullptr;
    HRESULT hr = _EnsureRegEx();
    if (SUCCEEDED(hr))
    {
        *ppRegEx = m_spRegEx;
        (*ppRegEx)->AddRef();
    }
    return hr;
}

IFACEMETHODIMP CPowerRenameManager::PutRenameRegEx(_In_ IPowerRenameRegEx* pRegEx)
{
    _ClearRegEx();
    m_spRegEx = pRegEx;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::GetRenameItemFactory(_COM_Outptr_ IPowerRenameItemFactory** ppItemFactory)
{
    *ppItemFactory = nullptr;
    HRESULT hr = E_FAIL;
    if (m_spItemFactory)
    {
        hr = S_OK;
        *ppItemFactory = m_spItemFactory;
        (*ppItemFactory)->AddRef();
    }
    return hr;
}

IFACEMETHODIMP CPowerRenameManager::PutRenameItemFactory(_In_ IPowerRenameItemFactory* pItemFactory)
{
    m_spItemFactory = pItemFactory;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::OnSearchTermChanged(_In_ PCWSTR /*searchTerm*/)
{
    _PerformRegExRename();
    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::OnReplaceTermChanged(_In_ PCWSTR /*replaceTerm*/)
{
    _PerformRegExRename();
    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::OnFlagsChanged(_In_ DWORD flags)
{
    // Flags were updated in the rename regex.  Update our preview.
    m_flags = flags;
    _PerformRegExRename();
    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::OnFileTimeChanged(_In_ SYSTEMTIME /*fileTime*/)
{
    _PerformRegExRename();
    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::OnMetadataChanged()
{
    _PerformRegExRename();
    return S_OK;
}

HRESULT CPowerRenameManager::s_CreateInstance(_Outptr_ IPowerRenameManager** ppsrm)
{
    *ppsrm = nullptr;
    CPowerRenameManager* psrm = new CPowerRenameManager();
    HRESULT hr = E_OUTOFMEMORY;
    if (psrm)
    {
        hr = psrm->_Init();
        if (SUCCEEDED(hr))
        {
            hr = psrm->QueryInterface(IID_PPV_ARGS(ppsrm));
        }
        psrm->Release();
    }
    return hr;
}

CPowerRenameManager::CPowerRenameManager() :
    m_refCount(1)
{
    InitializeCriticalSection(&m_critsecReentrancy);
}

CPowerRenameManager::~CPowerRenameManager()
{
    DeleteCriticalSection(&m_critsecReentrancy);
}

HRESULT CPowerRenameManager::_Init()
{
    // Guaranteed to succeed
    m_startFileOpWorkerEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);
    m_startRegExWorkerEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);
    m_cancelRegExWorkerEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);

    m_hwndMessage = CreateMsgWindow(g_hostHInst, s_msgWndProc, this);

    return S_OK;
}

// Custom messages for worker threads
enum
{
    SRM_REGEX_ITEM_UPDATED = (WM_APP + 1), // Single rename item processed by regex worker thread
    SRM_REGEX_ITEM_RENAMED_KEEP_UI, // Single rename item processed by rename worker thread in case UI remains opened
    SRM_REGEX_STARTED, // RegEx operation was started
    SRM_REGEX_CANCELED, // Regex operation was canceled
    SRM_REGEX_COMPLETE, // Regex worker thread completed
    SRM_FILEOP_COMPLETE // File Operation worker thread completed
};

struct WorkerThreadData
{
    HWND hwndManager = nullptr;
    HANDLE startEvent = nullptr;
    HANDLE cancelEvent = nullptr;
    HWND hwndParent = nullptr;
    CComPtr<IPowerRenameManager> spsrm;
};

// Msg-only worker window proc for communication from our worker threads
LRESULT CALLBACK CPowerRenameManager::s_msgWndProc(_In_ HWND hwnd, _In_ UINT uMsg, _In_ WPARAM wParam, _In_ LPARAM lParam)
{
    LRESULT lRes = 0;

    CPowerRenameManager* pThis = reinterpret_cast<CPowerRenameManager*>(GetWindowLongPtr(hwnd, 0));
    if (pThis != nullptr)
    {
        lRes = pThis->_WndProc(hwnd, uMsg, wParam, lParam);
        if (uMsg == WM_NCDESTROY)
        {
            SetWindowLongPtr(hwnd, 0, NULL);
            pThis->m_hwndMessage = nullptr;
        }
    }
    else
    {
        lRes = DefWindowProc(hwnd, uMsg, wParam, lParam);
    }

    return lRes;
}

LRESULT CPowerRenameManager::_WndProc(_In_ HWND hwnd, _In_ UINT msg, _In_ WPARAM wParam, _In_ LPARAM lParam)
{
    LRESULT lRes = 0;

    AddRef();

    switch (msg)
    {
    case SRM_REGEX_ITEM_UPDATED:
    {
        // Do nothing.
        break;
    }
    case SRM_REGEX_ITEM_RENAMED_KEEP_UI:
    {
        int id = static_cast<int>(lParam);
        CComPtr<IPowerRenameItem> spItem;
        if (SUCCEEDED(GetItemById(id, &spItem)))
        {
            _OnRename(spItem);
        }
        break;
    }
    case SRM_REGEX_STARTED:
        _OnRegExStarted(static_cast<DWORD>(wParam));
        break;

    case SRM_REGEX_CANCELED:
        _OnRegExCanceled(static_cast<DWORD>(wParam));
        break;

    case SRM_REGEX_COMPLETE:
        _OnRegExCompleted(static_cast<DWORD>(wParam));
        break;

    default:
        lRes = DefWindowProc(hwnd, msg, wParam, lParam);
        break;
    }

    Release();

    return lRes;
}

void CPowerRenameManager::_LogOperationTelemetry()
{
    UINT renameItemCount = 0;
    UINT selectedItemCount = 0;
    UINT totalItemCount = 0;
    DWORD flags = 0;

    GetItemCount(&totalItemCount);
    GetSelectedItemCount(&selectedItemCount);
    GetRenameItemCount(&renameItemCount);
    GetFlags(&flags);

    // Enumerate extensions used into a map
    std::map<std::wstring, int> extensionsMap;
    for (UINT i = 0; i < totalItemCount; i++)
    {
        CComPtr<IPowerRenameItem> spItem;
        if (SUCCEEDED(GetItemByIndex(i, &spItem)))
        {
            PWSTR originalName;
            if (SUCCEEDED(spItem->GetOriginalName(&originalName)))
            {
                std::wstring extension = fs::path(originalName).extension().wstring();
                std::map<std::wstring, int>::iterator it = extensionsMap.find(extension);
                if (it == extensionsMap.end())
                {
                    extensionsMap.insert({ extension, 1 });
                }
                else
                {
                    it->second++;
                }

                CoTaskMemFree(originalName);
            }
        }
    }

    std::wstring extensionList = L"";
    for (auto elem : extensionsMap)
    {
        extensionList.append(elem.first);
        extensionList.append(L":");
        extensionList.append(std::to_wstring(elem.second));
        extensionList.append(L",");
    }

    Trace::RenameOperation(totalItemCount, selectedItemCount, renameItemCount, flags, extensionList.c_str());
}

HRESULT CPowerRenameManager::_PerformFileOperation()
{
    // Do we have items to rename?
    UINT renameItemCount = 0;
    if (FAILED(GetRenameItemCount(&renameItemCount)) || renameItemCount == 0)
    {
        return E_FAIL;
    }

    _LogOperationTelemetry();

    // Wait for existing regex thread to finish
    _WaitForRegExWorkerThread();

    // Create worker thread which will perform the actual rename
    HRESULT hr = _CreateFileOpWorkerThread();
    if (SUCCEEDED(hr))
    {
        _OnRenameStarted();

        // Signal the worker thread that they can start working. We needed to wait until we
        // were ready to process thread messages.
        SetEvent(m_startFileOpWorkerEvent);

        while (true)
        {
            // Check if worker thread has exited
            if (WaitForSingleObject(m_fileOpWorkerThreadHandle, 0) == WAIT_OBJECT_0)
            {
                break;
            }

            MSG msg;
            while (PeekMessage(&msg, nullptr, 0, 0, PM_REMOVE))
            {
                if (msg.message == SRM_FILEOP_COMPLETE)
                {
                    // Worker thread completed
                    break;
                }
                else
                {
                    TranslateMessage(&msg);
                    DispatchMessage(&msg);
                }
            }
        }

        _OnRenameCompleted();
    }

    return S_OK;
}

HRESULT CPowerRenameManager::_CreateFileOpWorkerThread()
{
    WorkerThreadData* pwtd = new WorkerThreadData;
    HRESULT hr = E_OUTOFMEMORY;
    if (pwtd)
    {
        pwtd->hwndManager = m_hwndMessage;
        pwtd->startEvent = m_startRegExWorkerEvent;
        pwtd->cancelEvent = nullptr;
        pwtd->spsrm = this;
        m_fileOpWorkerThreadHandle = CreateThread(nullptr, 0, s_fileOpWorkerThread, pwtd, 0, nullptr);
        hr = E_FAIL;
        if (m_fileOpWorkerThreadHandle)
        {
            hr = S_OK;
        }
        else
        {
            delete pwtd;
        }
    }

    return hr;
}

DWORD WINAPI CPowerRenameManager::s_fileOpWorkerThread(_In_ void* pv)
{
    if (SUCCEEDED(CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE)))
    {
        WorkerThreadData* pwtd = static_cast<WorkerThreadData*>(pv);
        if (pwtd)
        {
            bool closeUIWindowAfterRenaming = true;
            pwtd->spsrm->GetCloseUIWindowAfterRenaming(&closeUIWindowAfterRenaming);

            // Wait to be told we can begin
            if (WaitForSingleObject(pwtd->startEvent, INFINITE) == WAIT_OBJECT_0)
            {
                CComPtr<IPowerRenameRegEx> spRenameRegEx;
                if (SUCCEEDED(pwtd->spsrm->GetRenameRegEx(&spRenameRegEx)))
                {
                    // Create IFileOperation interface
                    CComPtr<IFileOperation> spFileOp;
                    if (SUCCEEDED(CoCreateInstance(CLSID_FileOperation, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&spFileOp))))
                    {
                        DWORD flags = 0;
                        spRenameRegEx->GetFlags(&flags);

                        UINT itemCount = 0;
                        pwtd->spsrm->GetItemCount(&itemCount);

                        // We add the items to the operation in depth-first order.  This allows child items to be
                        // renamed before parent items.

                        // First pass: find the maximum depth to properly size the matrix
                        UINT maxDepth = 0;
                        for (UINT u = 0; u < itemCount; u++)
                        {
                            CComPtr<IPowerRenameItem> spItem;
                            if (SUCCEEDED(pwtd->spsrm->GetItemByIndex(u, &spItem)))
                            {
                                UINT depth = 0;
                                spItem->GetDepth(&depth);
                                if (depth > maxDepth)
                                {
                                    maxDepth = depth;
                                }
                            }
                        }

                        // Creating a vector of vectors of items of the same depth
                        // Size by maxDepth+1 (not itemCount) to avoid excessive memory allocation
                        // Cast to size_t before arithmetic to avoid overflow on 32-bit UINT
                        std::vector<std::vector<UINT>> matrix(static_cast<size_t>(maxDepth) + 1);

                        for (UINT u = 0; u < itemCount; u++)
                        {
                            CComPtr<IPowerRenameItem> spItem;
                            if (SUCCEEDED(pwtd->spsrm->GetItemByIndex(u, &spItem)))
                            {
                                UINT depth = 0;
                                spItem->GetDepth(&depth);
                                matrix[depth].push_back(u);
                            }
                        }

                        // From the greatest depth first, add all items of that depth to the operation
                        for (LONG v = static_cast<LONG>(maxDepth); v >= 0; v--)
                        {
                            for (auto it : matrix[v])
                            {
                                CComPtr<IPowerRenameItem> spItem;
                                if (SUCCEEDED(pwtd->spsrm->GetItemByIndex(it, &spItem)))
                                {
                                    bool shouldRename = false;
                                    if (SUCCEEDED(spItem->ShouldRenameItem(flags, &shouldRename)) && shouldRename)
                                    {
                                        PWSTR newName = nullptr;
                                        if (SUCCEEDED(spItem->GetNewName(&newName)))
                                        {
                                            CComPtr<IShellItem> spShellItem;
                                            if (SUCCEEDED(spItem->GetShellItem(&spShellItem)))
                                            {
                                                spFileOp->RenameItem(spShellItem, newName, nullptr);
                                                if (!closeUIWindowAfterRenaming)
                                                {
                                                    // Update item data
                                                    PWSTR originalName = nullptr;
                                                    winrt::check_hresult(spItem->GetOriginalName(&originalName));
                                                    std::wstring originalNameStr{ originalName };

                                                    PWSTR path = nullptr;
                                                    winrt::check_hresult(spItem->GetPath(&path));
                                                    std::wstring pathStr{ path };
                                                    size_t oldPathSize = pathStr.size();

                                                    auto fileNamePos = pathStr.find_last_of(L"\\");
                                                    pathStr.replace(fileNamePos + 1, originalNameStr.length(), std::wstring{ newName });
                                                    spItem->PutPath(pathStr.c_str());
                                                    spItem->PutOriginalName(newName);
                                                    spItem->PutNewName(nullptr);

                                                    // if folder, update children path
                                                    bool isFolder = false;
                                                    winrt::check_hresult(spItem->GetIsFolder(&isFolder));
                                                    if (isFolder)
                                                    {
                                                        int id = -1;
                                                        winrt::check_hresult(spItem->GetId(&id));
                                                        pwtd->spsrm->UpdateChildrenPath(id, oldPathSize);
                                                    }

                                                    int id = -1;
                                                    winrt::check_hresult(spItem->GetId(&id));
                                                    PostMessage(pwtd->hwndManager, SRM_REGEX_ITEM_RENAMED_KEEP_UI, GetCurrentThreadId(), id);
                                                }
                                            }
                                            CoTaskMemFree(newName);
                                        }
                                    }
                                }
                            }
                        }

                        // Set the operation flags
                        if (SUCCEEDED(spFileOp->SetOperationFlags(FOF_DEFAULTFLAGS)))
                        {
                            // Set the parent window
                            if (pwtd->hwndParent)
                            {
                                spFileOp->SetOwnerWindow(pwtd->hwndParent);
                            }

                            // Perform the operation
                            // We don't care about the return code here. We would rather
                            // return control back to explorer so the user can cleanly
                            // undo the operation if it failed halfway through.
                            spFileOp->PerformOperations();
                        }
                    }
                }
            }

            // Send the manager thread the completion message
            PostMessage(pwtd->hwndManager, SRM_FILEOP_COMPLETE, GetCurrentThreadId(), 0);

            delete pwtd;
        }
        CoUninitialize();
    }

    return 0;
}

HRESULT CPowerRenameManager::_PerformRegExRename()
{
    HRESULT hr = E_FAIL;

    if (!TryEnterCriticalSection(&m_critsecReentrancy))
    {
        // Ensure we do not re-enter since we pump messages here.
        // TODO: If we do, post a message back to ourselves
    }
    else
    {
        // Ensure previous thread is canceled
        _CancelRegExWorkerThread();

        // Create worker thread which will message us progress and completion.
        hr = _CreateRegExWorkerThread();
        if (SUCCEEDED(hr))
        {
            ResetEvent(m_cancelRegExWorkerEvent);

            // Signal the worker thread that they can start working. We needed to wait until we
            // were ready to process thread messages.
            SetEvent(m_startRegExWorkerEvent);
        }
    }

    return hr;
}

HRESULT CPowerRenameManager::_CreateRegExWorkerThread()
{
    WorkerThreadData* pwtd = new WorkerThreadData;
    HRESULT hr = E_OUTOFMEMORY;
    if (pwtd)
    {
        pwtd->hwndManager = m_hwndMessage;
        pwtd->startEvent = m_startRegExWorkerEvent;
        pwtd->cancelEvent = m_cancelRegExWorkerEvent;
        pwtd->hwndParent = m_hwndParent;
        pwtd->spsrm = this;
        m_regExWorkerThreadHandle = CreateThread(nullptr, 0, s_regexWorkerThread, pwtd, 0, nullptr);
        hr = E_FAIL;
        if (m_regExWorkerThreadHandle)
        {
            hr = S_OK;
        }
        else
        {
            delete pwtd;
        }
    }

    return hr;
}

DWORD WINAPI CPowerRenameManager::s_regexWorkerThread(_In_ void* pv)
{
    try
    {
        winrt::check_hresult(CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE));
        WorkerThreadData* pwtd = static_cast<WorkerThreadData*>(pv);
        if (pwtd)
        {
            PostMessage(pwtd->hwndManager, SRM_REGEX_STARTED, GetCurrentThreadId(), 0);

            // Wait to be told we can begin
            if (WaitForSingleObject(pwtd->startEvent, INFINITE) == WAIT_OBJECT_0)
            {
                CComPtr<IPowerRenameRegEx> spRenameRegEx;

                winrt::check_hresult(pwtd->spsrm->GetRenameRegEx(&spRenameRegEx));

                UINT itemCount = 0;
                unsigned long itemEnumIndex = 0;
                winrt::check_hresult(pwtd->spsrm->GetItemCount(&itemCount));

                for (UINT u = 0; u < itemCount; u++)
                {
                    // Check if cancel event is signaled
                    if (WaitForSingleObject(pwtd->cancelEvent, 0) == WAIT_OBJECT_0)
                    {
                        // Canceled from manager
                        // Send the manager thread the canceled message
                        PostMessage(pwtd->hwndManager, SRM_REGEX_CANCELED, GetCurrentThreadId(), 0);
                        break;
                    }

                    CComPtr<IPowerRenameItem> spItem;
                    winrt::check_hresult(pwtd->spsrm->GetItemByIndex(u, &spItem));

                    DoRename(spRenameRegEx, itemEnumIndex, spItem);
                }
            }

            // Send the manager thread the completion message
            PostMessage(pwtd->hwndManager, SRM_REGEX_COMPLETE, GetCurrentThreadId(), 0);

            delete pwtd;
        }
        CoUninitialize();
    }
    catch (...)
    {
        // TODO: an exception can happen while typing the expression and the syntax is not correct yet,
        // we need to be more granular and raise an exception only when a real problem happened.
        // MessageBox(NULL, L"RegexWorkerThread failed to execute.\nPlease report the bug to https://aka.ms/powerToysReportBug", L"PowerRename Error", MB_OK);
    }

    return 0;
}

void CPowerRenameManager::_CancelRegExWorkerThread()
{
    if (m_startRegExWorkerEvent)
    {
        SetEvent(m_startRegExWorkerEvent);
    }

    if (m_cancelRegExWorkerEvent)
    {
        SetEvent(m_cancelRegExWorkerEvent);
    }

    _WaitForRegExWorkerThread();
}

void CPowerRenameManager::_WaitForRegExWorkerThread()
{
    if (m_regExWorkerThreadHandle)
    {
        WaitForSingleObject(m_regExWorkerThreadHandle, INFINITE);
        CloseHandle(m_regExWorkerThreadHandle);
        m_regExWorkerThreadHandle = nullptr;
    }
}

void CPowerRenameManager::_Cancel()
{
    SetEvent(m_startFileOpWorkerEvent);
    _CancelRegExWorkerThread();
}

HRESULT CPowerRenameManager::_EnsureRegEx()
{
    HRESULT hr = S_OK;
    if (!m_spRegEx)
    {
        // Create the default regex handler
        hr = CPowerRenameRegEx::s_CreateInstance(&m_spRegEx);
        if (SUCCEEDED(hr))
        {
            hr = _InitRegEx();
            // Get the flags
            if (SUCCEEDED(hr))
            {
                m_spRegEx->GetFlags(&m_flags);
            }
        }
    }
    return hr;
}

HRESULT CPowerRenameManager::_InitRegEx()
{
    HRESULT hr = E_FAIL;
    if (m_spRegEx)
    {
        hr = m_spRegEx->Advise(this, &m_regExAdviseCookie);
    }

    return hr;
}

void CPowerRenameManager::_ClearRegEx()
{
    if (m_spRegEx)
    {
        m_spRegEx->UnAdvise(m_regExAdviseCookie);
        m_regExAdviseCookie = 0;
    }
}

void CPowerRenameManager::_OnRename(_In_ IPowerRenameItem* renameItem)
{
    CSRWSharedAutoLock lock(&m_lockEvents);

    for (auto it : m_powerRenameManagerEvents)
    {
        if (it.pEvents)
        {
            it.pEvents->OnRename(renameItem);
        }
    }
}

void CPowerRenameManager::_OnError(_In_ IPowerRenameItem* renameItem)
{
    CSRWSharedAutoLock lock(&m_lockEvents);

    for (auto it : m_powerRenameManagerEvents)
    {
        if (it.pEvents)
        {
            it.pEvents->OnError(renameItem);
        }
    }
}

void CPowerRenameManager::_OnRegExStarted(_In_ DWORD threadId)
{
    CSRWSharedAutoLock lock(&m_lockEvents);

    for (auto it : m_powerRenameManagerEvents)
    {
        if (it.pEvents)
        {
            it.pEvents->OnRegExStarted(threadId);
        }
    }
}

void CPowerRenameManager::_OnRegExCanceled(_In_ DWORD threadId)
{
    CSRWSharedAutoLock lock(&m_lockEvents);

    for (auto it : m_powerRenameManagerEvents)
    {
        if (it.pEvents)
        {
            it.pEvents->OnRegExCanceled(threadId);
        }
    }
}

void CPowerRenameManager::_OnRegExCompleted(_In_ DWORD threadId)
{
    CSRWSharedAutoLock lock(&m_lockEvents);

    for (auto it : m_powerRenameManagerEvents)
    {
        if (it.pEvents)
        {
            it.pEvents->OnRegExCompleted(threadId);
        }
    }
}

void CPowerRenameManager::_OnRenameStarted()
{
    CSRWSharedAutoLock lock(&m_lockEvents);

    for (auto it : m_powerRenameManagerEvents)
    {
        if (it.pEvents)
        {
            it.pEvents->OnRenameStarted();
        }
    }
}

void CPowerRenameManager::_OnRenameCompleted()
{
    CSRWSharedAutoLock lock(&m_lockEvents);

    for (auto it : m_powerRenameManagerEvents)
    {
        if (it.pEvents)
        {
            it.pEvents->OnRenameCompleted(m_closeUIWindowAfterRenaming);
        }
    }
}

void CPowerRenameManager::_ClearEventHandlers()
{
    CSRWExclusiveAutoLock lock(&m_lockEvents);

    // Cleanup event handlers
    for (std::vector<RENAME_MGR_EVENT>::iterator it = m_powerRenameManagerEvents.begin(); it != m_powerRenameManagerEvents.end(); ++it)
    {
        it->cookie = 0;
        if (it->pEvents)
        {
            it->pEvents->Release();
            it->pEvents = nullptr;
        }
    }

    m_powerRenameManagerEvents.clear();
}

void CPowerRenameManager::_ClearPowerRenameItems()
{
    CSRWExclusiveAutoLock lock(&m_lockItems);

    // Cleanup rename items
    for (std::map<int, IPowerRenameItem*>::iterator it = m_renameItems.begin(); it != m_renameItems.end(); ++it)
    {
        IPowerRenameItem* pItem = it->second;
        if (pItem)
        {
            pItem->Release();
            it->second = nullptr;
        }
    }

    m_renameItems.clear();
}

void CPowerRenameManager::_Cleanup()
{
    if (m_hwndMessage)
    {
        DestroyWindow(m_hwndMessage);
        m_hwndMessage = nullptr;
    }

    CloseHandle(m_startFileOpWorkerEvent);
    m_startFileOpWorkerEvent = nullptr;

    CloseHandle(m_startRegExWorkerEvent);
    m_startRegExWorkerEvent = nullptr;

    CloseHandle(m_cancelRegExWorkerEvent);
    m_cancelRegExWorkerEvent = nullptr;

    _ClearRegEx();
    _ClearEventHandlers();
    _ClearPowerRenameItems();
}
