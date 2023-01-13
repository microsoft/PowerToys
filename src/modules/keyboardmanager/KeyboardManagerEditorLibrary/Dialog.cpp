#include "pch.h"
#include "Dialog.h"

using namespace winrt::Windows::Foundation;

IAsyncOperation<bool> Dialog::PartialRemappingConfirmationDialog(XamlRoot root, std::wstring dialogTitle)
{
    ContentDialog confirmationDialog;
    confirmationDialog.XamlRoot(root);
    confirmationDialog.Title(box_value(dialogTitle));
    confirmationDialog.IsPrimaryButtonEnabled(true);
    confirmationDialog.DefaultButton(ContentDialogButton::Primary);
    confirmationDialog.PrimaryButtonText(winrt::hstring(GET_RESOURCE_STRING(IDS_CONTINUE_BUTTON)));
    confirmationDialog.IsSecondaryButtonEnabled(true);
    confirmationDialog.SecondaryButtonText(winrt::hstring(GET_RESOURCE_STRING(IDS_CANCEL_BUTTON)));

    ContentDialogResult res = co_await confirmationDialog.ShowAsync();
    co_return res == ContentDialogResult::Primary;
}
