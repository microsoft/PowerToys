#pragma once
#include <vector>
#include <map>
#include "srwlock.h"

#include <PowerRenameInterfaces.h>

class CPowerRenameManager :
    public IPowerRenameManager,
    public IPowerRenameRegExEvents
{
public:
    // IUnknown
    IFACEMETHODIMP QueryInterface(_In_ REFIID iid, _Outptr_ void** resultInterface);
    IFACEMETHODIMP_(ULONG) AddRef();
    IFACEMETHODIMP_(ULONG) Release();

    // IPowerRenameManager
    IFACEMETHODIMP Advise(_In_ IPowerRenameManagerEvents* renameOpEvent, _Out_ DWORD* cookie);
    IFACEMETHODIMP UnAdvise(_In_ DWORD cookie);
    IFACEMETHODIMP Start();
    IFACEMETHODIMP Stop();
    IFACEMETHODIMP Reset();
    IFACEMETHODIMP Shutdown();
    IFACEMETHODIMP Rename(_In_ HWND hwndParent, bool closeWindow);
    IFACEMETHODIMP UpdateChildrenPath(_In_ int parentId, _In_ size_t oldParentPathSize);
    IFACEMETHODIMP GetCloseUIWindowAfterRenaming(_Out_ bool* closeUIWindowAfterRenaming);
    IFACEMETHODIMP AddItem(_In_ IPowerRenameItem* pItem);
    IFACEMETHODIMP GetItemByIndex(_In_ UINT index, _COM_Outptr_ IPowerRenameItem** ppItem);
    IFACEMETHODIMP GetVisibleItemByIndex(_In_ UINT index, _COM_Outptr_ IPowerRenameItem** ppItem);
    IFACEMETHODIMP GetItemById(_In_ int id, _COM_Outptr_ IPowerRenameItem** ppItem);
    IFACEMETHODIMP GetItemCount(_Out_ UINT* count);
    IFACEMETHODIMP SetVisible();
    IFACEMETHODIMP GetVisibleItemCount(_Out_ UINT* count);
    IFACEMETHODIMP GetSelectedItemCount(_Out_ UINT* count);
    IFACEMETHODIMP GetRenameItemCount(_Out_ UINT* count);
    IFACEMETHODIMP GetFlags(_Out_ DWORD* flags);
    IFACEMETHODIMP PutFlags(_In_ DWORD flags);
    IFACEMETHODIMP GetFilter(_Out_ DWORD* filter);
    IFACEMETHODIMP SwitchFilter(_In_ int columnNumber);
    IFACEMETHODIMP GetRenameRegEx(_COM_Outptr_ IPowerRenameRegEx** ppRegEx);
    IFACEMETHODIMP PutRenameRegEx(_In_ IPowerRenameRegEx* pRegEx);
    IFACEMETHODIMP GetRenameItemFactory(_COM_Outptr_ IPowerRenameItemFactory** ppItemFactory);
    IFACEMETHODIMP PutRenameItemFactory(_In_ IPowerRenameItemFactory* pItemFactory);
    
    uint32_t GetVisibleItemRealIndex(const uint32_t index) const override;
    
    // IPowerRenameRegExEvents
    IFACEMETHODIMP OnSearchTermChanged(_In_ PCWSTR searchTerm);
    IFACEMETHODIMP OnReplaceTermChanged(_In_ PCWSTR replaceTerm);
    IFACEMETHODIMP OnFlagsChanged(_In_ DWORD flags);
    IFACEMETHODIMP OnFileTimeChanged(_In_ SYSTEMTIME fileTime);

    static HRESULT s_CreateInstance(_Outptr_ IPowerRenameManager** ppsrm);

protected:
    CPowerRenameManager();
    virtual ~CPowerRenameManager();

    HRESULT _Init();
    void _Cleanup();

    void _Cancel();

    void _OnRename(_In_ IPowerRenameItem* renameItem);
    void _OnError(_In_ IPowerRenameItem* renameItem);
    void _OnRegExStarted(_In_ DWORD threadId);
    void _OnRegExCanceled(_In_ DWORD threadId);
    void _OnRegExCompleted(_In_ DWORD threadId);
    void _OnRenameStarted();
    void _OnRenameCompleted();

    void _ClearEventHandlers();
    void _ClearPowerRenameItems();

    HRESULT _PerformRegExRename();
    HRESULT _PerformFileOperation();

    HRESULT _CreateRegExWorkerThread();
    void _CancelRegExWorkerThread();
    void _WaitForRegExWorkerThread();
    HRESULT _CreateFileOpWorkerThread();

    HRESULT _EnsureRegEx();
    HRESULT _InitRegEx();
    void _ClearRegEx();

    // Thread proc for performing the regex rename of each item
    static DWORD WINAPI s_regexWorkerThread(_In_ void* pv);
    // Thread proc for performing the actual file operation that does the file rename
    static DWORD WINAPI s_fileOpWorkerThread(_In_ void* pv);

    static LRESULT CALLBACK s_msgWndProc(_In_ HWND hwnd, _In_ UINT uMsg, _In_ WPARAM wParam, _In_ LPARAM lParam);
    LRESULT _WndProc(_In_ HWND hwnd, _In_ UINT msg, _In_ WPARAM wParam, _In_ LPARAM lParam);

    void _LogOperationTelemetry();

    HANDLE m_regExWorkerThreadHandle = nullptr;
    HANDLE m_startRegExWorkerEvent = nullptr;
    HANDLE m_cancelRegExWorkerEvent = nullptr;

    HANDLE m_fileOpWorkerThreadHandle = nullptr;
    HANDLE m_startFileOpWorkerEvent = nullptr;

    CSRWLock m_lockEvents;
    CSRWLock m_lockItems;

    DWORD m_flags = 0;

    DWORD m_cookie = 0;
    DWORD m_regExAdviseCookie = 0;

    DWORD m_filter = PowerRenameFilters::None;

    struct RENAME_MGR_EVENT
    {
        IPowerRenameManagerEvents* pEvents;
        DWORD cookie;
    };

    CComPtr<IPowerRenameItemFactory> m_spItemFactory;
    CComPtr<IPowerRenameRegEx> m_spRegEx;

    _Guarded_by_(m_lockEvents) std::vector<RENAME_MGR_EVENT> m_powerRenameManagerEvents;
    _Guarded_by_(m_lockItems) std::map<int, IPowerRenameItem*> m_renameItems;
    _Guarded_by_(m_lockItems) std::vector<bool> m_isVisible;

    // Parent HWND used by IFileOperation
    HWND m_hwndParent = nullptr;
    bool m_closeUIWindowAfterRenaming = true;

    HWND m_hwndMessage = nullptr;

    CRITICAL_SECTION m_critsecReentrancy;

    long m_refCount;
};