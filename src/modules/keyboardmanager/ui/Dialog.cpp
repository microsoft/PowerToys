#include "pch.h"
#include "Dialog.h"
#include <set>

using namespace winrt::Windows::Foundation;

KeyboardManagerHelper::ErrorType Dialog::CheckIfRemappingsAreValid(const std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>>& remappings)
{
    KeyboardManagerHelper::ErrorType isSuccess = KeyboardManagerHelper::ErrorType::NoError;
    std::map<std::wstring, std::set<std::variant<DWORD, Shortcut>>> ogKeys;
    for (int i = 0; i < remappings.size(); i++)
    {
        std::variant<DWORD, Shortcut> ogKey = remappings[i].first[0];
        std::variant<DWORD, Shortcut> newKey = remappings[i].first[1];
        std::wstring appName = remappings[i].second;

        bool ogKeyValidity = (ogKey.index() == 0 && std::get<DWORD>(ogKey) != NULL) || (ogKey.index() == 1 && std::get<Shortcut>(ogKey).IsValidShortcut());
        bool newKeyValidity = (newKey.index() == 0 && std::get<DWORD>(newKey) != NULL) || (newKey.index() == 1 && std::get<Shortcut>(newKey).IsValidShortcut());

        // Add new set for a new target app name
        if (ogKeys.find(appName) == ogKeys.end())
        {
            ogKeys[appName] = std::set<std::variant<DWORD, Shortcut>>();
        }

        if (ogKeyValidity && newKeyValidity && ogKeys[appName].find(ogKey) == ogKeys[appName].end())
        {
            ogKeys[appName].insert(ogKey);
        }
        else if (ogKeyValidity && newKeyValidity && ogKeys[appName].find(ogKey) != ogKeys[appName].end())
        {
            isSuccess = KeyboardManagerHelper::ErrorType::RemapUnsuccessful;
        }
        else
        {
            isSuccess = KeyboardManagerHelper::ErrorType::RemapUnsuccessful;
        }
    }
    return isSuccess;
}

IAsyncOperation<bool> Dialog::PartialRemappingConfirmationDialog(XamlRoot root, std::wstring dialogTitle)
{
    ContentDialog confirmationDialog;
    confirmationDialog.XamlRoot(root);
    confirmationDialog.Title(box_value(dialogTitle));
    confirmationDialog.IsPrimaryButtonEnabled(true);
    confirmationDialog.DefaultButton(ContentDialogButton::Primary);
    confirmationDialog.PrimaryButtonText(winrt::hstring(L"Continue Anyway"));
    confirmationDialog.IsSecondaryButtonEnabled(true);
    confirmationDialog.SecondaryButtonText(winrt::hstring(L"Cancel"));

    ContentDialogResult res = co_await confirmationDialog.ShowAsync();
    co_return res == ContentDialogResult::Primary;
}
