#pragma once
#include <vector>
#include <map>
#include "srwlock.h"

#include <lib/PowerRenameManager.h>
#include <lib/PowerRenameInterfaces.h>

class CPowerRenameManager : public IPowerRenameManager,  public IPowerRenameRegExEvents
{
public:
    // IUnknown
    IFACEMETHODIMP  QueryInterface(_In_ REFIID iid, _Outptr_ void** resultInterface) override;
    IFACEMETHODIMP_(ULONG) AddRef() override;
    IFACEMETHODIMP_(ULONG) Release() override;

    // IPowerRenameManager
    IFACEMETHODIMP Advise(_In_ IPowerRenameManagerEvents* renameOpEvent, _Out_ DWORD *cookie) override;
    IFACEMETHODIMP UnAdvise(_In_ DWORD cookie) override;
    IFACEMETHODIMP Start() override;
    IFACEMETHODIMP Stop() override;
    IFACEMETHODIMP Reset() override;
    IFACEMETHODIMP Shutdown() override;
    IFACEMETHODIMP Rename(_In_ HWND hwndParent) override;
    IFACEMETHODIMP AddItem(_In_ IPowerRenameItem* pItem) override;
    IFACEMETHODIMP GetItemByIndex(_In_ UINT index, _COM_Outptr_ IPowerRenameItem** ppItem) override;
    IFACEMETHODIMP GetItemById(_In_ int id, _COM_Outptr_ IPowerRenameItem** ppItem) override;
    IFACEMETHODIMP GetItemCount(_Out_ UINT* count) override;
    IFACEMETHODIMP GetSelectedItemCount(_Out_ UINT* count) override;
    IFACEMETHODIMP GetRenameItemCount(_Out_ UINT* count) override;
    IFACEMETHODIMP get_flags(_Out_ DWORD* flags) override;
    IFACEMETHODIMP put_flags(_In_ DWORD flags) override;
    IFACEMETHODIMP get_renameRegEx(_COM_Outptr_ IPowerRenameRegEx** ppRegEx) override;
    IFACEMETHODIMP put_renameRegEx(_In_ IPowerRenameRegEx* pRegEx) override;
    IFACEMETHODIMP get_renameItemFactory(_COM_Outptr_ IPowerRenameItemFactory** ppItemFactory) override;
    IFACEMETHODIMP put_renameItemFactory(_In_ IPowerRenameItemFactory* pItemFactory) override;

    // IPowerRenameRegExEvents
    IFACEMETHODIMP OnSearchTermChanged(_In_ PCWSTR searchTerm) override;
    IFACEMETHODIMP OnReplaceTermChanged(_In_ PCWSTR replaceTerm) override;
    IFACEMETHODIMP OnFlagsChanged(_In_ DWORD flags) override;

    static HRESULT s_CreateInstance(_Outptr_ IPowerRenameManager** ppsrm);

protected:
    CPowerRenameManager();
    virtual ~CPowerRenameManager();

    HRESULT _Init();
    void _Cleanup();

    void _Cancel();

    void _OnItemAdded(_In_ IPowerRenameItem* renameItem);
    void _OnUpdate(_In_ IPowerRenameItem* renameItem);
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

    struct RENAME_MGR_EVENT
    {
        IPowerRenameManagerEvents* pEvents;
        DWORD cookie;
    };

    CComPtr<IPowerRenameItemFactory> m_spItemFactory;
    CComPtr<IPowerRenameRegEx> m_spRegEx;

    _Guarded_by_(m_lockEvents) std::vector<RENAME_MGR_EVENT> m_powerRenameManagerEvents;
    _Guarded_by_(m_lockItems) std::map<int, IPowerRenameItem*> m_renameItems;

    // Parent HWND used by IFileOperation
    HWND m_hwndParent = nullptr;

    HWND m_hwndMessage = nullptr;

    CRITICAL_SECTION m_critsecReentrancy;

    long m_refCount;
};