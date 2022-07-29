#pragma once

#include "winrt/Windows.UI.Xaml.h"
#include "winrt/Windows.UI.Xaml.Markup.h"
#include "winrt/Windows.UI.Xaml.Interop.h"
#include "winrt/Windows.UI.Xaml.Controls.Primitives.h"

#include "MainWindow.g.h"
#include "PatternSnippet.h"
#include "ExplorerItem.h"

#include <map>
#include <wil/resource.h>

#include <PowerRenameEnum.h>
#include <PowerRenameItem.h>
#include <PowerRenameManager.h>
#include <PowerRenameInterfaces.h>
#include <PowerRenameMRU.h>

namespace winrt::PowerRenameUI::implementation
{
    struct MainWindow : MainWindowT<MainWindow>
    {
        // Proxy class to Advise() PRManager, as MainWindow can't implement IPowerRenameManagerEvents
        class PowerRenameManagerEvents : public IPowerRenameManagerEvents
        {
        public:
            PowerRenameManagerEvents(MainWindow* app) :
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
                    QITABENT(PowerRenameManagerEvents, IPowerRenameManagerEvents),
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
            MainWindow* m_app;
        };

        MainWindow();

        Windows::Foundation::Collections::IObservableVector<hstring> SearchMRU() { return m_searchMRUList; }
        Windows::Foundation::Collections::IObservableVector<hstring> ReplaceMRU() { return m_replaceMRUList; }
        winrt::Windows::Foundation::Collections::IObservableVector<PowerRenameUI::ExplorerItem> ExplorerItems() { return m_explorerItems; }
        winrt::Windows::Foundation::Collections::IObservableVector<PowerRenameUI::PatternSnippet> SearchRegExShortcuts() { return m_searchRegExShortcuts; }
        winrt::Windows::Foundation::Collections::IObservableVector<PowerRenameUI::PatternSnippet> DateTimeShortcuts() { return m_dateTimeShortcuts; }
        PowerRenameUI::UIUpdates UIUpdatesItem() { return m_uiUpdatesItem; }

        void AddExplorerItem(int32_t id, hstring const& original, hstring const& renamed, int32_t type, uint32_t depth, bool checked);
        void UpdateExplorerItem(int32_t id, hstring const& newName);
        void UpdateRenamedExplorerItem(int32_t id, hstring const& newOriginalName);
        void AppendSearchMRU(hstring const& value);
        void AppendReplaceMRU(hstring const& value);

        void SelectAll(winrt::Windows::Foundation::IInspectable const& sender, winrt::Microsoft::UI::Xaml::RoutedEventArgs const& e);
        void ShowAll(winrt::Windows::Foundation::IInspectable const& sender, winrt::Microsoft::UI::Xaml::RoutedEventArgs const& e);
        void ShowRenamed(winrt::Windows::Foundation::IInspectable const& sender, winrt::Microsoft::UI::Xaml::RoutedEventArgs const& e);

    private:
        bool m_allSelected;
        PowerRenameUI::UIUpdates m_uiUpdatesItem;
        inline PowerRenameUI::ExplorerItem FindById(int32_t id);

        winrt::Windows::Foundation::Collections::IObservableVector<hstring> m_searchMRUList;
        winrt::Windows::Foundation::Collections::IObservableVector<hstring> m_replaceMRUList;
        winrt::Windows::Foundation::Collections::IObservableVector<PowerRenameUI::ExplorerItem> m_explorerItems;
        std::map<int32_t, PowerRenameUI::ExplorerItem> m_explorerItemsMap;
        winrt::Windows::Foundation::Collections::IObservableVector<PowerRenameUI::PatternSnippet> m_searchRegExShortcuts;
        winrt::Windows::Foundation::Collections::IObservableVector<PowerRenameUI::PatternSnippet> m_dateTimeShortcuts;

        // Used by PowerRenameManagerEvents
        HRESULT OnItemAdded(_In_ IPowerRenameItem* renameItem);
        HRESULT OnUpdate(_In_ IPowerRenameItem* renameItem);
        HRESULT OnRename(_In_ IPowerRenameItem* renameItem);
        HRESULT OnError(_In_ IPowerRenameItem* renameItem);
        HRESULT OnRegExStarted(_In_ DWORD threadId);
        HRESULT OnRegExCanceled(_In_ DWORD threadId);
        HRESULT OnRegExCompleted(_In_ DWORD threadId);
        HRESULT OnRenameStarted();
        HRESULT OnRenameCompleted(bool closeUIWindowAfterRenaming);

        enum class UpdateFlagCommand
        {
            Set = 0,
            Reset
        };

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

        wil::unique_haccel m_accelerators;
        const HINSTANCE m_instance;
        HWND m_xamlIsland{};
        HWND m_window{};

        bool m_disableCountUpdate = false;
        CComPtr<IPowerRenameManager> m_prManager;
        CComPtr<::IUnknown> m_dataSource;
        CComPtr<IPowerRenameEnum> m_prEnum;
        PowerRenameManagerEvents m_managerEvents;
        DWORD m_cookie = 0;
        CComPtr<IPowerRenameMRU> m_searchMRU;
        CComPtr<IPowerRenameMRU> m_replaceMRU;
        UINT m_selectedCount = 0;
        UINT m_renamingCount = 0;

        bool m_flagValidationInProgress = false;
    public:
        void RegExItemClick(winrt::Windows::Foundation::IInspectable const& sender, winrt::Microsoft::UI::Xaml::Controls::ItemClickEventArgs const& e);
        void DateTimeItemClick(winrt::Windows::Foundation::IInspectable const& sender, winrt::Microsoft::UI::Xaml::Controls::ItemClickEventArgs const& e);
        void button_rename_Click(winrt::Microsoft::UI::Xaml::Controls::SplitButton const& sender, winrt::Microsoft::UI::Xaml::Controls::SplitButtonClickEventArgs const& args);
        void MenuFlyoutItem_Click(winrt::Windows::Foundation::IInspectable const& sender, winrt::Microsoft::UI::Xaml::RoutedEventArgs const& e);
        void OpenDocs(winrt::Windows::Foundation::IInspectable const& sender, winrt::Microsoft::UI::Xaml::RoutedEventArgs const& e);
    };
}

namespace winrt::PowerRenameUI::factory_implementation
{
    struct MainWindow : MainWindowT<MainWindow, implementation::MainWindow>
    {
    };
}
