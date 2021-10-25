#pragma once
#include "pch.h"

#include "resource.h"
#include "XamlBridge.h"

#include <PowerRenameEnum.h>
#include <PowerRenameItem.h>
#include <PowerRenameManager.h>
#include <PowerRenameInterfaces.h>
#include <PowerRenameMRU.h>

#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.system.h>
#pragma push_macro("GetCurrentTime")
#undef GetCurrentTime
#include <winrt/windows.ui.xaml.hosting.h>
#pragma pop_macro("GetCurrentTime")
#include <windows.ui.xaml.hosting.desktopwindowxamlsource.h>
#include <winrt/windows.ui.xaml.controls.h>
#include <winrt/windows.ui.xaml.controls.primitives.h>
#include <winrt/Windows.ui.xaml.media.h>
#include <winrt/Windows.ui.xaml.data.h>
#include <winrt/Windows.UI.Core.h>
#include <winrt/PowerRenameUILib.h>

using namespace winrt;
using namespace Windows::UI;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Composition;
using namespace Windows::UI::Xaml::Hosting;
using namespace Windows::Foundation::Numerics;
using namespace Windows::UI::Xaml::Controls;

class AppWindow : public DesktopWindowT<AppWindow>
{
public:
    // Proxy class to Advise() PRManager, as AppWindow can't implement IPowerRenameManagerEvents
    class UIHostPowerRenameManagerEvents : public IPowerRenameManagerEvents
    {
    public:
        UIHostPowerRenameManagerEvents(AppWindow* app) :
            m_refCount{ 1 }, m_app{ app }
        {
        }

        IFACEMETHODIMP_(ULONG)
        AddRef()
        {
            return InterlockedIncrement(&m_refCount);
        }

        IFACEMETHODIMP_(ULONG)
        Release()
        {
            long refCount = InterlockedDecrement(&m_refCount);

            if (refCount == 0)
            {
                delete this;
            }
            return refCount;
        }

        IFACEMETHODIMP QueryInterface(_In_ REFIID riid, _Outptr_ void** ppv)
        {
            static const QITAB qit[] = {
                QITABENT(UIHostPowerRenameManagerEvents, IPowerRenameManagerEvents),
                { 0 }
            };
            return QISearch(this, qit, riid, ppv);
        }

        HRESULT OnItemAdded(_In_ IPowerRenameItem* renameItem) override { return m_app->OnItemAdded(renameItem); }
        HRESULT OnUpdate(_In_ IPowerRenameItem* renameItem) override { return m_app->OnUpdate(renameItem); }
        HRESULT OnRename(_In_ IPowerRenameItem* renameItem) override { return m_app->OnRename(renameItem); }
        HRESULT OnError(_In_ IPowerRenameItem* renameItem) override { return m_app->OnError(renameItem); }
        HRESULT OnRegExStarted(_In_ DWORD threadId) override { return m_app->OnRegExStarted(threadId); }
        HRESULT OnRegExCanceled(_In_ DWORD threadId) override { return m_app->OnRegExCanceled(threadId); }
        HRESULT OnRegExCompleted(_In_ DWORD threadId) override { return m_app->OnRegExCompleted(threadId); }
        HRESULT OnRenameStarted() override { return m_app->OnRenameStarted(); }
        HRESULT OnRenameCompleted(bool closeUIWindowAfterRenaming) override { return m_app->OnRenameCompleted(closeUIWindowAfterRenaming); }

    private:
        long m_refCount;

        AppWindow* m_app;
    };

    static int Show(HINSTANCE hInstance, std::vector<std::wstring> files);
    LRESULT MessageHandler(UINT message, WPARAM wParam, LPARAM lParam) noexcept;

private:
    enum class UpdateFlagCommand
    {
        Set = 0,
        Reset
    };

    AppWindow(HINSTANCE hInstance, std::vector<std::wstring> files) noexcept;
    void CreateAndShowWindow();
    bool OnCreate(HWND, LPCREATESTRUCT) noexcept;
    void OnCommand(HWND, int id, HWND hwndControl, UINT codeNotify) noexcept;
    void OnDestroy(HWND hwnd) noexcept;
    void OnResize(HWND, UINT state, int cx, int cy) noexcept;
    HRESULT CreateShellItemArrayFromPaths(std::vector<std::wstring> files, IShellItemArray** shellItemArray);

    void PopulateExplorerItems();
    HRESULT InitAutoComplete();
    HRESULT EnumerateShellItems(_In_ IEnumShellItems* enumShellItems);
    void SearchReplaceChanged(bool forceRenaming = false);
    void ValidateFlags(PowerRenameFlags flag);
    void UpdateFlag(PowerRenameFlags flag, UpdateFlagCommand command);
    void SetHandlers();
    void ToggleItem(int32_t id, bool checked);
    void ToggleAll();
    void SwitchView();
    void Rename(bool closeWindow);
    HRESULT ReadSettings();
    HRESULT WriteSettings();
    HRESULT OpenSettingsApp();
    void SetCheckboxesFromFlags(DWORD flags);
    void UpdateCounts();

    HRESULT OnItemAdded(_In_ IPowerRenameItem* renameItem);
    HRESULT OnUpdate(_In_ IPowerRenameItem* renameItem);
    HRESULT OnRename(_In_ IPowerRenameItem* renameItem);
    HRESULT OnError(_In_ IPowerRenameItem* renameItem);
    HRESULT OnRegExStarted(_In_ DWORD threadId);
    HRESULT OnRegExCanceled(_In_ DWORD threadId);
    HRESULT OnRegExCompleted(_In_ DWORD threadId);
    HRESULT OnRenameStarted();
    HRESULT OnRenameCompleted(bool closeUIWindowAfterRenaming);

    wil::unique_haccel m_accelerators;
    const HINSTANCE m_instance;
    HWND m_xamlIsland{};
    HWND m_window{};
    winrt::PowerRenameUILib::MainWindow m_mainUserControl{ nullptr };

    bool m_disableCountUpdate = false;
    CComPtr<IPowerRenameManager> m_prManager;
    CComPtr<IUnknown> m_dataSource;
    CComPtr<IPowerRenameEnum> m_prEnum;
    UIHostPowerRenameManagerEvents m_managerEvents;
    DWORD m_cookie = 0;
    CComPtr<IPowerRenameMRU> m_searchMRU;
    CComPtr<IPowerRenameMRU> m_replaceMRU;
    UINT m_selectedCount = 0;
    UINT m_renamingCount = 0;

    bool m_flagValidationInProgress = false;
};
