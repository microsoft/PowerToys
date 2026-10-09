#include "pch.h"

#include <functional>

#include "ExplorerItemViewModel.h"
#if __has_include("ExplorerItemViewModel.g.cpp")
#include "ExplorerItemViewModel.g.cpp"
#endif
#include <PowerRenameManager.h>
#include <Renaming.h>

extern CComPtr<IPowerRenameManager> g_prManager;
extern std::function<void(void)> g_itemToggledCallback;

namespace
{
    const wchar_t fileImagePath[] = L"ms-appx:///Assets/PowerRename/file.png";
    const wchar_t folderImagePath[] = L"ms-appx:///Assets/PowerRename/folder.png";
}

namespace winrt::PowerRenameUI::implementation
{
    winrt::event_token ExplorerItemViewModel::PropertyChanged(winrt::Microsoft::UI::Xaml::Data::PropertyChangedEventHandler const& handler)
    {
        return m_propertyChanged.add(handler);
    }

    void ExplorerItemViewModel::PropertyChanged(winrt::event_token const& token) noexcept
    {
        m_propertyChanged.remove(token);
    }
    ExplorerItemViewModel::ExplorerItemViewModel(const uint32_t _index) :
        _index{ _index }
    {
    }

    int32_t ExplorerItemViewModel::IdVM()
    {
        return _index;
    }
    hstring ExplorerItemViewModel::IdStrVM()
    {
        return to_hstring(_index);
    }
    hstring ExplorerItemViewModel::OriginalVM()
    {
        CComPtr<IPowerRenameItem> spItem;
        winrt::check_hresult(g_prManager->GetItemByIndex(_index, &spItem));

        PWSTR originalName = nullptr;
        winrt::check_hresult(spItem->GetOriginalName(&originalName));

        return hstring{ originalName };
    }
    hstring ExplorerItemViewModel::RenamedVM()
    {
        CComPtr<IPowerRenameItem> spItem;
        winrt::check_hresult(g_prManager->GetItemByIndex(_index, &spItem));

        PWSTR newName = nullptr;
        spItem->GetNewName(&newName);
        if (!newName)
        {
            return L"";
        }
        else
        {
            hstring result{ newName };
            SHFree(newName);

            return result;
        }
    }

    double ExplorerItemViewModel::IndentationVM()
    {
        CComPtr<IPowerRenameItem> spItem;
        winrt::check_hresult(g_prManager->GetItemByIndex(_index, &spItem));

        UINT depth = 0;
        spItem->GetDepth(&depth);

        return static_cast<double>(depth) * 12;
    }
    hstring ExplorerItemViewModel::ImagePathVM()
    {
        return TypeVM() ? fileImagePath : folderImagePath;
    }
    int32_t ExplorerItemViewModel::TypeVM()
    {
        CComPtr<IPowerRenameItem> spItem;
        winrt::check_hresult(g_prManager->GetItemByIndex(_index, &spItem));

        bool isFolder = false;
        spItem->GetIsFolder(&isFolder);
        return isFolder ? 0 : 1;
    }

    bool ExplorerItemViewModel::CheckedVM()
    {
        CComPtr<IPowerRenameItem> spItem;
        winrt::check_hresult(g_prManager->GetItemByIndex(_index, &spItem));
        bool result = false;
        winrt::check_hresult(spItem->GetSelected(&result));
        return result;
    }
    void ExplorerItemViewModel::CheckedVM(bool value)
    {
        CComPtr<IPowerRenameItem> spItem;
        winrt::check_hresult(g_prManager->GetItemByIndex(_index, &spItem));
        winrt::check_hresult(spItem->PutSelected(value));
        g_itemToggledCallback();
    }

    int32_t ExplorerItemViewModel::StateVM()
    {
        CComPtr<IPowerRenameItem> spItem;
        winrt::check_hresult(g_prManager->GetItemByIndex(_index, &spItem));
        PowerRenameItemRenameStatus status {};
        spItem->GetStatus(&status);
        return static_cast<int32_t>(status);
    }
}
