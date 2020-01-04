#pragma once
#include <PowerRenameInterfaces.h>
#include <shldisp.h>

void ModuleAddRef();
void ModuleRelease();

class CPowerRenameListView
{
public:
    CPowerRenameListView() = default;
    ~CPowerRenameListView() = default;

    void Init(_In_ HWND hwndLV);
    void ToggleAll(_In_ IPowerRenameManager* psrm, _In_ bool selected);
    void ToggleItem(_In_ IPowerRenameManager* psrm, _In_ int item);
    void UpdateItemCheckState(_In_ IPowerRenameManager* psrm, _In_ int iItem);
    void RedrawItems(_In_ int first, _In_ int last);
    void SetItemCount(_In_ UINT itemCount);
    void OnKeyDown(_In_ IPowerRenameManager* psrm, _In_ LV_KEYDOWN* lvKeyDown);
    void OnClickList(_In_ IPowerRenameManager* psrm, NM_LISTVIEW* pnmListView);
    void GetDisplayInfo(_In_ IPowerRenameManager* psrm, _Inout_ LV_DISPINFO* plvdi);
    void OnSize();
    HWND GetHWND() { return m_hwndLV; }

private:
    void _UpdateColumns();
    void _UpdateColumnSizes();
    void _UpdateHeaderCheckState(_In_ bool check);

    HWND m_hwndLV = nullptr;
};

class CPowerRenameUI :
    public IDropTarget,
    public IPowerRenameUI,
    public IPowerRenameManagerEvents
{
public:
    CPowerRenameUI() :
        m_refCount(1)
    {
        (void)OleInitialize(nullptr);
        ModuleAddRef();
    }

    // IUnknown
    IFACEMETHODIMP QueryInterface(__in REFIID riid, __deref_out void** ppv);
    IFACEMETHODIMP_(ULONG)
    AddRef();
    IFACEMETHODIMP_(ULONG)
    Release();

    // IPowerRenameUI
    IFACEMETHODIMP Show(_In_opt_ HWND hwndParent);
    IFACEMETHODIMP Close();
    IFACEMETHODIMP Update();
    IFACEMETHODIMP get_hwnd(_Out_ HWND* hwnd);
    IFACEMETHODIMP get_showUI(_Out_ bool* showUI);

    // IPowerRenameManagerEvents
    IFACEMETHODIMP OnItemAdded(_In_ IPowerRenameItem* renameItem);
    IFACEMETHODIMP OnUpdate(_In_ IPowerRenameItem* renameItem);
    IFACEMETHODIMP OnError(_In_ IPowerRenameItem* renameItem);
    IFACEMETHODIMP OnRegExStarted(_In_ DWORD threadId);
    IFACEMETHODIMP OnRegExCanceled(_In_ DWORD threadId);
    IFACEMETHODIMP OnRegExCompleted(_In_ DWORD threadId);
    IFACEMETHODIMP OnRenameStarted();
    IFACEMETHODIMP OnRenameCompleted();

    // IDropTarget
    IFACEMETHODIMP DragEnter(_In_ IDataObject* pdtobj, DWORD grfKeyState, POINTL pt, _Inout_ DWORD* pdwEffect);
    IFACEMETHODIMP DragOver(DWORD grfKeyState, POINTL pt, _Inout_ DWORD* pdwEffect);
    IFACEMETHODIMP DragLeave();
    IFACEMETHODIMP Drop(_In_ IDataObject* pdtobj, DWORD grfKeyState, POINTL pt, _Inout_ DWORD* pdwEffect);

    static HRESULT s_CreateInstance(_In_ IPowerRenameManager* psrm, _In_opt_ IUnknown* dataSource, _In_ bool enableDragDrop, _Outptr_ IPowerRenameUI** ppsrui);

private:
    ~CPowerRenameUI()
    {
        DeleteObject(m_iconMain);
        OleUninitialize();
        ModuleRelease();
    }

    HRESULT _DoModal(__in_opt HWND hwnd);
    HRESULT _DoModeless(__in_opt HWND hwnd);

    static INT_PTR CALLBACK s_DlgProc(HWND hdlg, UINT uMsg, WPARAM wParam, LPARAM lParam)
    {
        CPowerRenameUI* pDlg = reinterpret_cast<CPowerRenameUI*>(GetWindowLongPtr(hdlg, DWLP_USER));
        if (uMsg == WM_INITDIALOG)
        {
            pDlg = reinterpret_cast<CPowerRenameUI*>(lParam);
            pDlg->m_hwnd = hdlg;
            SetWindowLongPtr(hdlg, DWLP_USER, reinterpret_cast<LONG_PTR>(pDlg));
        }
        return pDlg ? pDlg->_DlgProc(uMsg, wParam, lParam) : FALSE;
    }

    HRESULT _Initialize(_In_ IPowerRenameManager* psrm, _In_opt_ IUnknown* dataSource, _In_ bool enableDragDrop);
    HRESULT _InitAutoComplete();
    void _Cleanup();

    INT_PTR _DlgProc(UINT uMsg, WPARAM wParam, LPARAM lParam);
    void _OnCommand(_In_ WPARAM wParam, _In_ LPARAM lParam);
    BOOL _OnNotify(_In_ WPARAM wParam, _In_ LPARAM lParam);
    void _OnSize(_In_ WPARAM wParam);
    void _OnGetMinMaxInfo(_In_ LPARAM lParam);
    void _OnInitDlg();
    void _OnRename();
    void _OnAbout();
    void _OnCloseDlg();
    void _OnDestroyDlg();
    void _OnSearchReplaceChanged();
    void _MoveControl(_In_ DWORD id, _In_ DWORD repositionFlags, _In_ int xDelta, _In_ int yDelta);

    HRESULT _ReadSettings();
    HRESULT _WriteSettings();

    DWORD _GetFlagsFromCheckboxes();
    void _SetCheckboxesFromFlags(_In_ DWORD flags);
    void _ValidateFlagCheckbox(_In_ DWORD checkBoxId);

    void _EnumerateItems(_In_ IUnknown* pdtobj);
    void _UpdateCounts();

    long m_refCount = 0;
    bool m_initialized = false;
    bool m_enableDragDrop = false;
    bool m_disableCountUpdate = false;
    bool m_modeless = true;
    HWND m_hwnd = nullptr;
    HWND m_hwndLV = nullptr;
    HICON m_iconMain = nullptr;
    DWORD m_cookie = 0;
    DWORD m_currentRegExId = 0;
    UINT m_selectedCount = 0;
    UINT m_renamingCount = 0;
    int m_initialWidth = 0;
    int m_initialHeight = 0;
    int m_lastWidth = 0;
    int m_lastHeight = 0;
    CComPtr<IPowerRenameManager> m_spsrm;
    CComPtr<IUnknown> m_dataSource;
    CComPtr<IDropTargetHelper> m_spdth;
    CComPtr<IAutoComplete2> m_spSearchAC;
    CComPtr<IUnknown> m_spSearchACL;
    CComPtr<IAutoComplete2> m_spReplaceAC;
    CComPtr<IUnknown> m_spReplaceACL;
    CPowerRenameListView m_listview;
};