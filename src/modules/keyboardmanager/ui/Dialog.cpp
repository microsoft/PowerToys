#include "pch.h"
#include "Dialog.h"

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
