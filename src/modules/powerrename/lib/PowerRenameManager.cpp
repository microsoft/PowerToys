#include "stdafx.h"
#include "PowerRenameManager.h"
#include "PowerRenameRegEx.h" // Default RegEx handler
#include <algorithm>
#include <shlobj.h>
#include "helpers.h"
#include <filesystem>
#include "trace.h"

namespace fs = std::filesystem;

extern HINSTANCE g_hInst;

// The default FOF flags to use in the rename operations
#define FOF_DEFAULTFLAGS (FOF_ALLOWUNDO | FOFX_ADDUNDORECORD | FOFX_SHOWELEVATIONPROMPT | FOF_RENAMEONCOLLISION)

IFACEMETHODIMP_(ULONG) CPowerRenameManager::AddRef()
{
    return InterlockedIncrement(&m_refCount);
}

IFACEMETHODIMP_(ULONG) CPowerRenameManager::Release()
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
    SMART_RENAME_MGR_EVENT srme;
    srme.cookie = m_cookie;
    srme.pEvents = renameOpEvents;
    renameOpEvents->AddRef();
    m_PowerRenameManagerEvents.push_back(srme);

    *cookie = m_cookie;

    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::UnAdvise(_In_ DWORD cookie)
{
    HRESULT hr = E_FAIL;
    CSRWExclusiveAutoLock lock(&m_lockEvents);

    for (auto it : m_PowerRenameManagerEvents)
    {
        if (it.cookie == cookie)
        {
            hr = S_OK;
            it.cookie = 0;
            if (it.pEvents)
            {
                it.pEvents->Release();
                it.pEvents = nullptr;
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

IFACEMETHODIMP CPowerRenameManager::Rename(_In_ HWND hwndParent)
{
    m_hwndParent = hwndParent;
    return _PerformFileOperation();
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
        pItem->get_id(&id);
        // Verify the item isn't already added
        if (m_smartRenameItems.find(id) == m_smartRenameItems.end())
        {
            m_smartRenameItems[id] = pItem;
            pItem->AddRef();
            hr = S_OK;
        }
    }

    if (SUCCEEDED(hr))
    {
        _OnItemAdded(pItem);
    }

    return hr;
}

IFACEMETHODIMP CPowerRenameManager::GetItemByIndex(_In_ UINT index, _COM_Outptr_ IPowerRenameItem** ppItem)
{
    *ppItem = nullptr;
    CSRWSharedAutoLock lock(&m_lockItems);
    HRESULT hr = E_FAIL;
    if (index < m_smartRenameItems.size())
    {
        std::map<int, IPowerRenameItem*>::iterator it = m_smartRenameItems.begin();
        std::advance(it, index);
        *ppItem = it->second;
        (*ppItem)->AddRef();
        hr = S_OK;
    }

    return hr;
}

IFACEMETHODIMP CPowerRenameManager::GetItemById(_In_ int id, _COM_Outptr_ IPowerRenameItem** ppItem)
{
    *ppItem = nullptr;

    CSRWSharedAutoLock lock(&m_lockItems);
    HRESULT hr = E_FAIL;
    std::map<int, IPowerRenameItem*>::iterator it;
    it = m_smartRenameItems.find(id);
    if (it !=  m_smartRenameItems.end())
    {
        *ppItem = m_smartRenameItems[id];
        (*ppItem)->AddRef();
        hr = S_OK;
    }

    return hr;
}

IFACEMETHODIMP CPowerRenameManager::GetItemCount(_Out_ UINT* count)
{
    CSRWSharedAutoLock lock(&m_lockItems);
    *count = static_cast<UINT>(m_smartRenameItems.size());
    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::GetSelectedItemCount(_Out_ UINT* count)
{
    *count = 0;
    CSRWSharedAutoLock lock(&m_lockItems);

    for (auto it : m_smartRenameItems)
    {
        IPowerRenameItem* pItem = it.second;
        bool selected = false;
        if (SUCCEEDED(pItem->get_selected(&selected)) && selected)
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

    for (auto it : m_smartRenameItems)
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

IFACEMETHODIMP CPowerRenameManager::get_flags(_Out_ DWORD* flags)
{
    _EnsureRegEx();
    *flags = m_flags;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::put_flags(_In_ DWORD flags)
{
    if (flags != m_flags)
    {
        m_flags = flags;
        _EnsureRegEx();
        m_spRegEx->put_flags(flags);
    }
    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::get_smartRenameRegEx(_COM_Outptr_ IPowerRenameRegEx** ppRegEx)
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

IFACEMETHODIMP CPowerRenameManager::put_smartRenameRegEx(_In_ IPowerRenameRegEx* pRegEx)
{
    _ClearRegEx();
    m_spRegEx = pRegEx;
    return S_OK;
}

IFACEMETHODIMP CPowerRenameManager::get_smartRenameItemFactory(_COM_Outptr_ IPowerRenameItemFactory** ppItemFactory)
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

IFACEMETHODIMP CPowerRenameManager::put_smartRenameItemFactory(_In_ IPowerRenameItemFactory* pItemFactory)
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
    // Flags were updated in the smart rename regex.  Update our preview.
    m_flags = flags;
    _PerformRegExRename();
    return S_OK;
}

HRESULT CPowerRenameManager::s_CreateInstance(_Outptr_ IPowerRenameManager** ppsrm)
{
    *ppsrm = nullptr;
    CPowerRenameManager *psrm = new CPowerRenameManager();
    HRESULT hr = psrm ? S_OK : E_OUTOFMEMORY;
    if (SUCCEEDED(hr))
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

    m_hwndMessage = CreateMsgWindow(g_hInst, s_msgWndProc, this);

    return S_OK;
}

// Custom messages for worker threads
enum
{
    SRM_REGEX_ITEM_UPDATED = (WM_APP + 1),  // Single smart rename item processed by regex worker thread
    SRM_REGEX_STARTED,                      // RegEx operation was started
    SRM_REGEX_CANCELED,                     // Regex operation was canceled
    SRM_REGEX_COMPLETE,                     // Regex worker thread completed
    SRM_FILEOP_COMPLETE                     // File Operation worker thread completed
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

    CPowerRenameManager* pThis = (CPowerRenameManager*)GetWindowLongPtr(hwnd, 0);
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
        int id = static_cast<int>(lParam);
        CComPtr<IPowerRenameItem> spItem;
        if (SUCCEEDED(GetItemById(id, &spItem)))
        {
            _OnUpdate(spItem);
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
    get_flags(&flags);


    // Enumerate extensions used into a map
    std::map<std::wstring, int> extensionsMap;
    for (UINT i = 0; i < totalItemCount; i++)
    {
        CComPtr<IPowerRenameItem> spItem;
        if (SUCCEEDED(GetItemByIndex(i, &spItem)))
        {
            PWSTR originalName;
            if (SUCCEEDED(spItem->get_originalName(&originalName)))
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

    return 0;
}

HRESULT CPowerRenameManager::_CreateFileOpWorkerThread()
{
    WorkerThreadData* pwtd = new WorkerThreadData;
    HRESULT hr = pwtd ? S_OK : E_OUTOFMEMORY;
    if (SUCCEEDED(hr))
    {
        pwtd->hwndManager = m_hwndMessage;
        pwtd->startEvent = m_startRegExWorkerEvent;
        pwtd->cancelEvent = nullptr;
        pwtd->spsrm = this;
        m_fileOpWorkerThreadHandle = CreateThread(nullptr, 0, s_fileOpWorkerThread, pwtd, 0, nullptr);
        hr = (m_fileOpWorkerThreadHandle) ? S_OK : E_FAIL;
        if (FAILED(hr))
        {
            delete pwtd;
        }
    }

    return hr;
}

DWORD WINAPI CPowerRenameManager::s_fileOpWorkerThread(_In_ void* pv)
{
    if (SUCCEEDED(CoInitializeEx(NULL, 0)))
    {
        WorkerThreadData* pwtd = reinterpret_cast<WorkerThreadData*>(pv);
        if (pwtd)
        {
            // Wait to be told we can begin
            if (WaitForSingleObject(pwtd->startEvent, INFINITE) == WAIT_OBJECT_0)
            {
                CComPtr<IPowerRenameRegEx> spRenameRegEx;
                if (SUCCEEDED(pwtd->spsrm->get_smartRenameRegEx(&spRenameRegEx)))
                {
                    // Create IFileOperation interface
                    CComPtr<IFileOperation> spFileOp;
                    if (SUCCEEDED(CoCreateInstance(CLSID_FileOperation, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&spFileOp))))
                    {
                        DWORD flags = 0;
                        spRenameRegEx->get_flags(&flags);

                        UINT itemCount = 0;
                        pwtd->spsrm->GetItemCount(&itemCount);
                        // Add each rename operation
                        for (UINT u = 0; u <= itemCount; u++)
                        {
                            CComPtr<IPowerRenameItem> spItem;
                            if (SUCCEEDED(pwtd->spsrm->GetItemByIndex(u, &spItem)))
                            {
                                bool shouldRename = false;
                                if (SUCCEEDED(spItem->ShouldRenameItem(flags, &shouldRename)) && shouldRename)
                                {
                                    PWSTR newName = nullptr;
                                    if (SUCCEEDED(spItem->get_newName(&newName)))
                                    {
                                        CComPtr<IShellItem> spShellItem;
                                        if (SUCCEEDED(spItem->get_shellItem(&spShellItem)))
                                        {
                                            spFileOp->RenameItem(spShellItem, newName, nullptr);
                                        }
                                        CoTaskMemFree(newName);
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
    HRESULT hr = pwtd ? S_OK : E_OUTOFMEMORY;
    if (SUCCEEDED(hr))
    {
        pwtd->hwndManager = m_hwndMessage;
        pwtd->startEvent = m_startRegExWorkerEvent;
        pwtd->cancelEvent = m_cancelRegExWorkerEvent;
        pwtd->hwndParent = m_hwndParent;
        pwtd->spsrm = this;
        m_regExWorkerThreadHandle = CreateThread(nullptr, 0, s_regexWorkerThread, pwtd, 0, nullptr);
        hr = (m_regExWorkerThreadHandle) ? S_OK : E_FAIL;
        if (FAILED(hr))
        {
            delete pwtd;
        }
    }

    return hr;
}

DWORD WINAPI CPowerRenameManager::s_regexWorkerThread(_In_ void* pv)
{
    if (SUCCEEDED(CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE)))
    {
        WorkerThreadData* pwtd = reinterpret_cast<WorkerThreadData*>(pv);
        if (pwtd)
        {
            PostMessage(pwtd->hwndManager, SRM_REGEX_STARTED, GetCurrentThreadId(), 0);

            // Wait to be told we can begin
            if (WaitForSingleObject(pwtd->startEvent, INFINITE) == WAIT_OBJECT_0)
            {
                CComPtr<IPowerRenameRegEx> spRenameRegEx;
                if (SUCCEEDED(pwtd->spsrm->get_smartRenameRegEx(&spRenameRegEx)))
                {
                    DWORD flags = 0;
                    spRenameRegEx->get_flags(&flags);

                    UINT itemCount = 0;
                    unsigned long itemEnumIndex = 1;
                    pwtd->spsrm->GetItemCount(&itemCount);
                    for (UINT u = 0; u <= itemCount; u++)
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
                        if (SUCCEEDED(pwtd->spsrm->GetItemByIndex(u, &spItem)))
                        {
                            int id = -1;
                            spItem->get_id(&id);

                            bool isFolder = false;
                            bool isSubFolderContent = false;
                            spItem->get_isFolder(&isFolder);
                            spItem->get_isSubFolderContent(&isSubFolderContent);
                            if ((isFolder && (flags & PowerRenameFlags::ExcludeFolders)) ||
                                (!isFolder && (flags & PowerRenameFlags::ExcludeFiles)) ||
                                (isSubFolderContent && (flags & PowerRenameFlags::ExcludeSubfolders)))
                            {
                                // Exclude this item from renaming.  Ensure new name is cleared.
                                spItem->put_newName(nullptr);

                                // Send the manager thread the item processed message
                                PostMessage(pwtd->hwndManager, SRM_REGEX_ITEM_UPDATED, GetCurrentThreadId(), id);

                                continue;
                            }

                            PWSTR originalName = nullptr;
                            if (SUCCEEDED(spItem->get_originalName(&originalName)))
                            {
                                PWSTR currentNewName = nullptr;
                                spItem->get_newName(&currentNewName);

                                wchar_t sourceName[MAX_PATH] = { 0 };
                                if (flags & NameOnly)
                                {
                                    StringCchCopy(sourceName, ARRAYSIZE(sourceName), fs::path(originalName).stem().c_str());
                                }
                                else if (flags & ExtensionOnly)
                                {
                                    std::wstring extension = fs::path(originalName).extension().wstring();
                                    if (!extension.empty() && extension.front() == '.')
                                    {
                                        extension = extension.erase(0, 1);
                                    }
                                    StringCchCopy(sourceName, ARRAYSIZE(sourceName), extension.c_str());
                                }
                                else
                                {
                                    StringCchCopy(sourceName, ARRAYSIZE(sourceName), originalName);
                                }


                                PWSTR newName = nullptr;
                                // Failure here means we didn't match anything or had nothing to match
                                // Call put_newName with null in that case to reset it
                                spRenameRegEx->Replace(sourceName, &newName);

                                wchar_t resultName[MAX_PATH] = { 0 };

                                PWSTR newNameToUse = nullptr;

                                // newName == nullptr likely means we have an empty search string.  We should leave newNameToUse
                                // as nullptr so we clear the renamed column
                                if (newName != nullptr)
                                {
                                    newNameToUse = resultName;
                                    if (flags & NameOnly)
                                    {
                                        StringCchPrintf(resultName, ARRAYSIZE(resultName), L"%s%s", newName, fs::path(originalName).extension().c_str());
                                    }
                                    else if (flags & ExtensionOnly)
                                    {
                                        std::wstring extension = fs::path(originalName).extension().wstring();
                                        if (!extension.empty())
                                        {
                                            StringCchPrintf(resultName, ARRAYSIZE(resultName), L"%s.%s", fs::path(originalName).stem().c_str(), newName);
                                        }
                                        else
                                        {
                                            StringCchCopy(resultName, ARRAYSIZE(resultName), originalName);
                                        }
                                    }
                                    else
                                    {
                                        StringCchCopy(resultName, ARRAYSIZE(resultName), newName);
                                    }
                                }
                                
                                // No change from originalName so set newName to
                                // null so we clear it from our UI as well.
                                if (lstrcmp(originalName, newNameToUse) == 0)
                                {
                                    newNameToUse = nullptr;
                                }

                                wchar_t uniqueName[MAX_PATH] = { 0 };
                                if (newNameToUse != nullptr && (flags & EnumerateItems))
                                {
                                    unsigned long countUsed = 0;
                                    if (GetEnumeratedFileName(uniqueName, ARRAYSIZE(uniqueName), newNameToUse, nullptr, itemEnumIndex, &countUsed))
                                    {
                                        newNameToUse = uniqueName;
                                    }
                                    itemEnumIndex++;
                                }

                                spItem->put_newName(newNameToUse);

                                // Was there a change?
                                if (lstrcmp(currentNewName, newNameToUse) != 0)
                                {
                                    // Send the manager thread the item processed message
                                    PostMessage(pwtd->hwndManager, SRM_REGEX_ITEM_UPDATED, GetCurrentThreadId(), id);
                                }

                                CoTaskMemFree(newName);
                                CoTaskMemFree(currentNewName);
                                CoTaskMemFree(originalName);
                            }
                        }
                    }
                }
            }

            // Send the manager thread the completion message
            PostMessage(pwtd->hwndManager, SRM_REGEX_COMPLETE, GetCurrentThreadId(), 0);

            delete pwtd;
        }
        CoUninitialize();
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
                m_spRegEx->get_flags(&m_flags);
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

void CPowerRenameManager::_OnItemAdded(_In_ IPowerRenameItem* renameItem)
{
    CSRWSharedAutoLock lock(&m_lockEvents);

    for (auto it : m_PowerRenameManagerEvents)
    {
        if (it.pEvents)
        {
            it.pEvents->OnItemAdded(renameItem);
        }
    }
}

void CPowerRenameManager::_OnUpdate(_In_ IPowerRenameItem* renameItem)
{
    CSRWSharedAutoLock lock(&m_lockEvents);

    for (auto it : m_PowerRenameManagerEvents)
    {
        if (it.pEvents)
        {
            it.pEvents->OnUpdate(renameItem);
        }
    }
}

void CPowerRenameManager::_OnError(_In_ IPowerRenameItem* renameItem)
{
    CSRWSharedAutoLock lock(&m_lockEvents);

    for (auto it : m_PowerRenameManagerEvents)
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

    for (auto it : m_PowerRenameManagerEvents)
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

    for (auto it : m_PowerRenameManagerEvents)
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

    for (auto it : m_PowerRenameManagerEvents)
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

    for (auto it : m_PowerRenameManagerEvents)
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

    for (auto it : m_PowerRenameManagerEvents)
    {
        if (it.pEvents)
        {
            it.pEvents->OnRenameCompleted();
        }
    }
}

void CPowerRenameManager::_ClearEventHandlers()
{
    CSRWExclusiveAutoLock lock(&m_lockEvents);

    // Cleanup event handlers
    for (auto it : m_PowerRenameManagerEvents)
    {
        it.cookie = 0;
        if (it.pEvents)
        {
            it.pEvents->Release();
            it.pEvents = nullptr;
        }
    }

    m_PowerRenameManagerEvents.clear();
}

void CPowerRenameManager::_ClearPowerRenameItems()
{
    CSRWExclusiveAutoLock lock(&m_lockItems);

    // Cleanup smart rename items
    for (auto it : m_smartRenameItems)
    {
        IPowerRenameItem* pItem = it.second;
        pItem->Release();
    }

    m_smartRenameItems.clear();
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
