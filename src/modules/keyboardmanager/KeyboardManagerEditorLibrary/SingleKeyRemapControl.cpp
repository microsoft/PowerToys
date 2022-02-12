#include "pch.h"
#include "SingleKeyRemapControl.h"

#include "BufferValidationHelpers.h"
#include "KeyboardManagerState.h"
#include "ShortcutControl.h"
#include "UIHelpers.h"
#include "EditorHelpers.h"
#include "EditorConstants.h"

using namespace Windows::UI::Xaml::Controls::Primitives;
using namespace Windows::UI::Xaml::Input;
using namespace Windows::UI::Xaml::Media;

//Both static members are initialized to null
HWND SingleKeyRemapControl::EditKeyboardWindowHandle = nullptr;
KBMEditor::KeyboardManagerState* SingleKeyRemapControl::keyboardManagerState = nullptr;
// Initialized as new vector
RemapBuffer SingleKeyRemapControl::singleKeyRemapBuffer;

SingleKeyRemapControl::SingleKeyRemapControl(StackPanel table, StackPanel row, const int colIndex)
{
    typeKey = Button();
    typeKey.as<Button>().Width(EditorConstants::RemapTableDropDownWidth);
    typeKey.as<Button>().Content(winrt::box_value(GET_RESOURCE_STRING(IDS_TYPE_BUTTON)));

    singleKeyRemapControlLayout = StackPanel();
    singleKeyRemapControlLayout.as<StackPanel>().Spacing(10);
    singleKeyRemapControlLayout.as<StackPanel>().Children().Append(typeKey.as<Button>());

    // Key column
    if (colIndex == 0)
    {
        keyDropDownControlObjects.emplace_back(std::make_unique<KeyDropDownControl>(false));
        singleKeyRemapControlLayout.as<StackPanel>().Children().Append(keyDropDownControlObjects[0]->GetComboBox());
        keyDropDownControlObjects[0]->SetSelectionHandler(table, row, colIndex, singleKeyRemapBuffer);
    }

    // Hybrid column
    else
    {
        hybridDropDownVariableSizedWrapGrid = VariableSizedWrapGrid();
        hybridDropDownVariableSizedWrapGrid.as<VariableSizedWrapGrid>().Orientation(Windows::UI::Xaml::Controls::Orientation::Horizontal);
        KeyDropDownControl::AddDropDown(table, row, hybridDropDownVariableSizedWrapGrid.as<VariableSizedWrapGrid>(), colIndex, singleKeyRemapBuffer, keyDropDownControlObjects, nullptr, true, true);
        singleKeyRemapControlLayout.as<StackPanel>().Children().Append(hybridDropDownVariableSizedWrapGrid.as<VariableSizedWrapGrid>());
    }

    typeKey.as<Button>().Click([&, table, colIndex, row](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        // Using the XamlRoot of the typeKey to get the root of the XAML host
        if (colIndex == 0)
        {
            keyboardManagerState->SetUIState(KBMEditor::KeyboardManagerUIState::DetectSingleKeyRemapWindowActivated, EditKeyboardWindowHandle);
            createDetectKeyWindow(sender, sender.as<Button>().XamlRoot(), *keyboardManagerState);
        }
        else
        {
            keyboardManagerState->SetUIState(KBMEditor::KeyboardManagerUIState::DetectShortcutWindowInEditKeyboardWindowActivated, EditKeyboardWindowHandle);
            ShortcutControl::CreateDetectShortcutWindow(sender, sender.as<Button>().XamlRoot(), *keyboardManagerState, colIndex, table, keyDropDownControlObjects, row, nullptr, true, true, EditKeyboardWindowHandle, singleKeyRemapBuffer);
        }
    });

    try
    {
        // If a layout update has been triggered by other methods (e.g.: adapting to zoom level), this may throw an exception.
        singleKeyRemapControlLayout.as<StackPanel>().UpdateLayout();
    }
    catch (...)
    {
    }
}

// Function to set the accessible names for all the controls in a row
void SingleKeyRemapControl::UpdateAccessibleNames(StackPanel sourceColumn, StackPanel mappedToColumn, Button deleteButton, int rowIndex)
{
    sourceColumn.SetValue(Automation::AutomationProperties::NameProperty(), box_value(GET_RESOURCE_STRING(IDS_AUTOMATIONPROPERTIES_ROW) + std::to_wstring(rowIndex) + L", " + GET_RESOURCE_STRING(IDS_EDITKEYBOARD_SOURCEHEADER)));
    mappedToColumn.SetValue(Automation::AutomationProperties::NameProperty(), box_value(GET_RESOURCE_STRING(IDS_AUTOMATIONPROPERTIES_ROW) + std::to_wstring(rowIndex) + L", " + GET_RESOURCE_STRING(IDS_EDITKEYBOARD_TARGETHEADER)));
    deleteButton.SetValue(Automation::AutomationProperties::NameProperty(), box_value(GET_RESOURCE_STRING(IDS_AUTOMATIONPROPERTIES_ROW) + std::to_wstring(rowIndex) + L", " + GET_RESOURCE_STRING(IDS_DELETE_REMAPPING_BUTTON)));
}

// Function to add a new row to the remap keys table. If the originalKey and newKey args are provided, then the displayed remap keys are set to those values.
void SingleKeyRemapControl::AddNewControlKeyRemapRow(
    StackPanel& parent,
    winrt::weak_ref<Button> weakApplyButton,
    std::vector<std::vector<std::unique_ptr<SingleKeyRemapControl>>>& keyboardRemapControlObjects,
    DWORD originalKey,
    const KeyShortcutUnion& newKey,
    RemapCondition condition)
{
    // Create new SingleKeyRemapControl objects dynamically so that we does not get destructed
    std::vector<std::unique_ptr<SingleKeyRemapControl>> newrow;
    StackPanel row = StackPanel();
    parent.Children().Append(row);
    newrow.emplace_back(std::make_unique<SingleKeyRemapControl>(parent, row, 0));
    newrow.emplace_back(std::make_unique<SingleKeyRemapControl>(parent, row, 1));
    keyboardRemapControlObjects.push_back(std::move(newrow));

    row.Padding({ 10, 10, 10, 10 });
    row.Orientation(Orientation::Horizontal);
    auto brush = Windows::UI::Xaml::Application::Current().Resources().Lookup(box_value(L"SystemControlBackgroundListLowBrush")).as<Windows::UI::Xaml::Media::SolidColorBrush>();
    if (keyboardRemapControlObjects.size() % 2)
    {
        row.Background(brush);
    }

    // SingleKeyRemapControl for the original key.
    auto originalElement = keyboardRemapControlObjects.back()[0]->getSingleKeyRemapControl();
    originalElement.Width(EditorConstants::RemapTableDropDownWidth + EditorConstants::ShortcutTableDropDownSpacing);
    row.Children().Append(originalElement);

    FontIcon arrowIcon;
    arrowIcon.FontFamily(Media::FontFamily(L"Segoe MDL2 Assets"));
    arrowIcon.Glyph(L"\xE72A");
    arrowIcon.VerticalAlignment(VerticalAlignment::Center);
    arrowIcon.HorizontalAlignment(HorizontalAlignment::Center);

    FontIcon statusCircleIcon;
    statusCircleIcon.FontFamily(FontFamily(L"Segoe MDL2 Assets"));
    statusCircleIcon.Glyph(L"\xEA81");
    statusCircleIcon.Foreground(SolidColorBrush(Colors::Red()));
    statusCircleIcon.VerticalAlignment(VerticalAlignment::Center);
    statusCircleIcon.HorizontalAlignment(HorizontalAlignment::Center);

    FontIcon statusErrorIcon;
    statusErrorIcon.FontFamily(FontFamily(L"Segoe MDL2 Assets"));
    statusErrorIcon.Glyph(L"\xEA83");
    statusErrorIcon.VerticalAlignment(VerticalAlignment::Center);
    statusErrorIcon.HorizontalAlignment(HorizontalAlignment::Center);

    auto statusIcon = Grid{};
    statusIcon.Children().Append(statusCircleIcon);
    statusIcon.Children().Append(statusErrorIcon);
    statusIcon.Visibility(Visibility::Collapsed);

    auto layeredIcon = Grid{};
    layeredIcon.Children().Append(arrowIcon);
    layeredIcon.Children().Append(statusIcon);

    auto errorTextBlock = TextBlock{};
    auto errorFlyout = Flyout{};
    errorFlyout.Content(errorTextBlock);

    // Enable narrator for Content of FlyoutPresenter.
    // For details https://docs.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.flyout#accessibility
    auto style = Style(winrt::xaml_typename<FlyoutPresenter>());
    style.Setters().Append(Setter(Control::IsTabStopProperty(), winrt::box_value(true)));
    style.Setters().Append(Setter(Control::TabNavigationProperty(), winrt::box_value(KeyboardNavigationMode::Cycle)));
    errorFlyout.FlyoutPresenterStyle(style);
    FlyoutBase::SetAttachedFlyout(statusIcon, errorFlyout);

    auto arrowIconContainer = UIHelpers::GetWrapped(layeredIcon, EditorConstants::TableArrowColWidth).as<StackPanel>();
    arrowIconContainer.Spacing(10);
    arrowIconContainer.Orientation(Orientation::Vertical);
    arrowIconContainer.VerticalAlignment(VerticalAlignment::Center);

    auto toolTip = ToolTip{};
    toolTip.Content(TextBlock{});
    ToolTipService::SetToolTip(statusIcon, toolTip);

    auto weakStatusIcon = winrt::make_weak(statusIcon);
    auto errorHandler = [weakStatusIcon, weakApplyButton](ShortcutErrorType errorType, PCWSTR message)
    {
        const auto isError = errorType != ShortcutErrorType::NoError;

        if (auto statusIcon = weakStatusIcon.get())
        {
            const auto visibility = isError ? Visibility::Visible : Visibility::Collapsed;
            statusIcon.Visibility(visibility);

            auto errorFlyout = FlyoutBase::GetAttachedFlyout(statusIcon).as<Flyout>();
            if (isError)
            {
                errorFlyout.Content().as<TextBlock>().Text(message);
                errorFlyout.ShowAt(statusIcon);

                auto errorToolTip = ToolTipService::GetToolTip(statusIcon).as<ToolTip>();
                errorToolTip.Content().as<TextBlock>().Text(message);
            }
            else
            {
                errorFlyout.Hide();
            }
        }

        if (auto applyButton = weakApplyButton.get())
        {
            applyButton.IsEnabled(!isError);
        }
    };

    auto comboBox = ComboBox{};
    comboBox.ItemsSource(UIHelpers::ToBoxValue(
        { { 0, GET_RESOURCE_STRING(IDS_REMAPCONDITION_ALWAYS) },
          { 1, GET_RESOURCE_STRING(IDS_REMAPCONDITION_ALONE) },
          { 2, GET_RESOURCE_STRING(IDS_REMAPCONDITION_COMBINATION) } }));
    comboBox.SelectedIndex(static_cast<int32_t>(condition));
    comboBox.HorizontalAlignment(HorizontalAlignment::Center);
    comboBox.VerticalAlignment(VerticalAlignment::Center);
    comboBox.SelectionChanged([parent, row](const winrt::Windows::Foundation::IInspectable& sender, SelectionChangedEventArgs args)
    {
        uint32_t rowIndex;
        UIElementCollection children = parent.Children();
        if (!children.IndexOf(row, rowIndex))
        {
            return;
        }

        const auto senderComboBox = sender.as<ComboBox>();
        const auto selectedIndex = senderComboBox.SelectedIndex();
        if (selectedIndex >= 0)
        {
            singleKeyRemapBuffer.at(rowIndex).condition = static_cast<RemapCondition>(selectedIndex);
            BufferValidationHelpers::ValidateRemapBuffer(singleKeyRemapBuffer);
        }
    });
    arrowIconContainer.Children().Append(comboBox);

    row.Children().Append(arrowIconContainer);

    // SingleKeyRemapControl for the new remap key
    auto targetElement = keyboardRemapControlObjects.back()[1]->getSingleKeyRemapControl();
    targetElement.Width(EditorConstants::ShortcutTargetColumnWidth);
    row.Children().Append(targetElement);

    // Set the key text if the two keys are not null (i.e. default args)
    if (IsValidSingleKey(originalKey) && IsValidSingleKeyOrShortcut(newKey))
    {
        singleKeyRemapBuffer.emplace_back(RemapBufferItem{ originalKey, newKey }, L"", condition, errorHandler);
        keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][0]->keyDropDownControlObjects[0]->SetSelectedValue(std::to_wstring(originalKey));
        if (newKey.index() == 0)
        {
            keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->keyDropDownControlObjects[0]->SetSelectedValue(std::to_wstring(std::get<DWORD>(newKey)));
        }
        else
        {
            KeyDropDownControl::AddShortcutToControl(std::get<Shortcut>(newKey), parent, keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->hybridDropDownVariableSizedWrapGrid.as<VariableSizedWrapGrid>(), *keyboardManagerState, 1, keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->keyDropDownControlObjects, singleKeyRemapBuffer, row, nullptr, true, true);
        }
    }
    else
    {
        // Initialize both keys to NULL
        singleKeyRemapBuffer.emplace_back(RemapBufferItem{}, L"", RemapCondition::Always, errorHandler);
    }

    // Delete row button
    Windows::UI::Xaml::Controls::Button deleteRemapKeys;
    FontIcon deleteSymbol;
    deleteSymbol.FontFamily(Media::FontFamily(L"Segoe MDL2 Assets"));
    deleteSymbol.Glyph(L"\xE74D");
    deleteRemapKeys.Content(deleteSymbol);
    deleteRemapKeys.Background(Media::SolidColorBrush(Colors::Transparent()));
    deleteRemapKeys.HorizontalAlignment(HorizontalAlignment::Center);
    deleteRemapKeys.Click([&, parent, row, brush, deleteRemapKeys](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        uint32_t rowIndex;
        // Get index of delete button
        UIElementCollection children = parent.Children();
        bool indexFound = children.IndexOf(row, rowIndex);

        // IndexOf could fail if the row got deleted and the button handler was invoked twice. In this case it should return
        if (!indexFound)
        {
            return;
        }

        // Update accessible names and background for each row after the deleted row
        for (uint32_t i = rowIndex + 1; i < children.Size(); i++)
        {
            StackPanel row = children.GetAt(i).as<StackPanel>();
            row.Background(i % 2 ? brush : Media::SolidColorBrush(Colors::Transparent()));
            StackPanel sourceCol = row.Children().GetAt(0).as<StackPanel>();
            StackPanel targetCol = row.Children().GetAt(2).as<StackPanel>();
            Button delButton = row.Children().GetAt(3).as<Button>();
            UpdateAccessibleNames(sourceCol, targetCol, delButton, i);
        }

        if (auto automationPeer{ Automation::Peers::FrameworkElementAutomationPeer::FromElement(deleteRemapKeys) })
        {
            automationPeer.RaiseNotificationEvent(
                Automation::Peers::AutomationNotificationKind::ActionCompleted,
                Automation::Peers::AutomationNotificationProcessing::ImportantMostRecent,
                GET_RESOURCE_STRING(IDS_DELETE_REMAPPING_EVENT),
                L"KeyRemappingDeletedNotificationEvent" /* unique name for this notification category */);
        }

        children.RemoveAt(rowIndex);
        try
        {
            // If a layout update has been triggered by other methods (e.g.: adapting to zoom level), this may throw an exception.
            parent.UpdateLayout();
        }
        catch (...)
        {
        }
        singleKeyRemapBuffer.erase(singleKeyRemapBuffer.begin() + rowIndex);
    
        // delete the SingleKeyRemapControl objects so that they get destructed
        keyboardRemapControlObjects.erase(keyboardRemapControlObjects.begin() + rowIndex);
    });

    // To set the accessible name of the delete button
    deleteRemapKeys.SetValue(Automation::AutomationProperties::NameProperty(), box_value(GET_RESOURCE_STRING(IDS_DELETE_REMAPPING_BUTTON)));

    // Add tooltip for delete button which would appear on hover
    ToolTip deleteRemapKeystoolTip;
    deleteRemapKeystoolTip.Content(box_value(GET_RESOURCE_STRING(IDS_DELETE_REMAPPING_BUTTON)));
    ToolTipService::SetToolTip(deleteRemapKeys, deleteRemapKeystoolTip);
    row.Children().Append(deleteRemapKeys);
    try
    {
        // If a layout update has been triggered by other methods (e.g.: adapting to zoom level), this may throw an exception.
        parent.UpdateLayout();
    }
    catch (...)
    {
    }

    // Set accessible names
    UpdateAccessibleNames(keyboardRemapControlObjects.back()[0]->getSingleKeyRemapControl(), keyboardRemapControlObjects.back()[1]->getSingleKeyRemapControl(), deleteRemapKeys, (int)keyboardRemapControlObjects.size());
}

// Function to return the stack panel element of the SingleKeyRemapControl. This is the externally visible UI element which can be used to add it to other layouts
StackPanel SingleKeyRemapControl::getSingleKeyRemapControl()
{
    return singleKeyRemapControlLayout.as<StackPanel>();
}

// Function to create the detect remap key UI window
void SingleKeyRemapControl::createDetectKeyWindow(winrt::Windows::Foundation::IInspectable const& sender, XamlRoot xamlRoot, KBMEditor::KeyboardManagerState& keyboardManagerState)
{
    // ContentDialog for detecting remap key. This is the parent UI element.
    ContentDialog detectRemapKeyBox;

    // ContentDialog requires manually setting the XamlRoot (https://docs.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.contentdialog#contentdialog-in-appwindow-or-xaml-islands)
    detectRemapKeyBox.XamlRoot(xamlRoot);
    detectRemapKeyBox.Title(box_value(GET_RESOURCE_STRING(IDS_TYPEKEY_TITLE)));
    detectRemapKeyBox.IsPrimaryButtonEnabled(false);
    detectRemapKeyBox.IsSecondaryButtonEnabled(false);

    // Get the linked text block for the "Type Key" button that was clicked
    ComboBox linkedRemapDropDown = UIHelpers::GetSiblingElement(sender).as<ComboBox>();

    auto unregisterKeys = [&keyboardManagerState]() {
        keyboardManagerState.ClearRegisteredKeyDelays();
    };

    auto onPressEnter = [linkedRemapDropDown,
                         detectRemapKeyBox,
                         &keyboardManagerState,
                         unregisterKeys] {
        // Save the detected key in the linked text block
        DWORD detectedKey = keyboardManagerState.GetDetectedSingleRemapKey();

        if (detectedKey != NULL)
        {
            std::vector<DWORD> keyCodeList = keyboardManagerState.keyboardMap.GetKeyCodeList();

            // Update the drop down list with the new language to ensure that the correct key is displayed
            linkedRemapDropDown.ItemsSource(UIHelpers::ToBoxValue(keyboardManagerState.keyboardMap.GetKeyNameList()));
            linkedRemapDropDown.SelectedValue(winrt::box_value(std::to_wstring(detectedKey)));
        }

        // Hide the type key UI
        detectRemapKeyBox.Hide();
    };

    auto onReleaseEnter = [&keyboardManagerState,
                           unregisterKeys] {
        // Reset the keyboard manager UI state
        keyboardManagerState.ResetUIState();
        // Revert UI state back to Edit Keyboard window
        keyboardManagerState.SetUIState(KBMEditor::KeyboardManagerUIState::EditKeyboardWindowActivated, EditKeyboardWindowHandle);
        unregisterKeys();
    };

    auto onAccept = [onPressEnter,
                     onReleaseEnter] {
        onPressEnter();
        onReleaseEnter();
    };

    TextBlock primaryButtonText;
    primaryButtonText.Text(GET_RESOURCE_STRING(IDS_OK_BUTTON));

    Button primaryButton;
    primaryButton.HorizontalAlignment(HorizontalAlignment::Stretch);
    primaryButton.Margin({ 2, 2, 2, 2 });
    primaryButton.Content(primaryButtonText);
    primaryButton.Click([onAccept](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        onAccept();
    });

    // NOTE: UnregisterKeys should never be called on the DelayThread, as it will re-enter the mutex. To avoid this it is run on the dispatcher thread
    keyboardManagerState.RegisterKeyDelay(
        VK_RETURN,
        std::bind(&KBMEditor::KeyboardManagerState::SelectDetectedRemapKey, &keyboardManagerState, std::placeholders::_1),
        [primaryButton, onPressEnter, detectRemapKeyBox](DWORD) {
            detectRemapKeyBox.Dispatcher().RunAsync(
                Windows::UI::Core::CoreDispatcherPriority::Normal,
                [primaryButton, onPressEnter] {
                    // Use the base medium low brush to be consistent with the theme
                    primaryButton.Background(Windows::UI::Xaml::Application::Current().Resources().Lookup(box_value(L"SystemControlBackgroundBaseMediumLowBrush")).as<Windows::UI::Xaml::Media::SolidColorBrush>());
                    onPressEnter();
                });
        },
        [onReleaseEnter, detectRemapKeyBox](DWORD) {
            detectRemapKeyBox.Dispatcher().RunAsync(
                Windows::UI::Core::CoreDispatcherPriority::Normal,
                [onReleaseEnter]() {
                    onReleaseEnter();
                });
        });

    TextBlock cancelButtonText;
    cancelButtonText.Text(GET_RESOURCE_STRING(IDS_CANCEL_BUTTON));

    auto onCancel = [&keyboardManagerState,
                     detectRemapKeyBox,
                     unregisterKeys] {
        detectRemapKeyBox.Hide();

        // Reset the keyboard manager UI state
        keyboardManagerState.ResetUIState();

        // Revert UI state back to Edit Keyboard window
        keyboardManagerState.SetUIState(KBMEditor::KeyboardManagerUIState::EditKeyboardWindowActivated, EditKeyboardWindowHandle);
        unregisterKeys();
    };

    Button cancelButton;
    cancelButton.HorizontalAlignment(HorizontalAlignment::Stretch);
    cancelButton.Margin({ 2, 2, 2, 2 });
    cancelButton.Content(cancelButtonText);

    // Cancel button
    cancelButton.Click([onCancel](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        onCancel();
    });

    // NOTE: UnregisterKeys should never be called on the DelayThread, as it will re-enter the mutex. To avoid this it is run on the dispatcher thread
    keyboardManagerState.RegisterKeyDelay(
        VK_ESCAPE,
        std::bind(&KBMEditor::KeyboardManagerState::SelectDetectedRemapKey, &keyboardManagerState, std::placeholders::_1),
        [onCancel, detectRemapKeyBox](DWORD) {
            detectRemapKeyBox.Dispatcher().RunAsync(
                Windows::UI::Core::CoreDispatcherPriority::Normal,
                [onCancel] {
                    onCancel();
                });
        },
        nullptr);

    // StackPanel parent for the displayed text in the dialog
    Windows::UI::Xaml::Controls::StackPanel stackPanel;
    detectRemapKeyBox.Content(stackPanel);

    // Header textblock
    TextBlock text;
    text.Text(GET_RESOURCE_STRING(IDS_TYPEKEY_HEADER));
    text.Margin({ 0, 0, 0, 10 });
    stackPanel.Children().Append(text);

    // Target StackPanel to place the selected key
    Windows::UI::Xaml::Controls::StackPanel keyStackPanel;
    keyStackPanel.Orientation(Orientation::Horizontal);
    stackPanel.Children().Append(keyStackPanel);

    TextBlock holdEscInfo;
    holdEscInfo.Text(GET_RESOURCE_STRING(IDS_TYPE_HOLDESC));
    holdEscInfo.FontSize(12);
    holdEscInfo.Margin({ 0, 20, 0, 0 });
    stackPanel.Children().Append(holdEscInfo);

    TextBlock holdEnterInfo;
    holdEnterInfo.Text(GET_RESOURCE_STRING(IDS_TYPE_HOLDENTER));
    holdEnterInfo.FontSize(12);
    holdEnterInfo.Margin({ 0, 0, 0, 0 });
    stackPanel.Children().Append(holdEnterInfo);

    ColumnDefinition primaryButtonColumn;
    ColumnDefinition cancelButtonColumn;

    Grid buttonPanel;
    buttonPanel.Margin({ 0, 20, 0, 0 });
    buttonPanel.HorizontalAlignment(HorizontalAlignment::Stretch);
    buttonPanel.ColumnDefinitions().Append(primaryButtonColumn);
    buttonPanel.ColumnDefinitions().Append(cancelButtonColumn);
    buttonPanel.SetColumn(primaryButton, 0);
    buttonPanel.SetColumn(cancelButton, 1);

    buttonPanel.Children().Append(primaryButton);
    buttonPanel.Children().Append(cancelButton);

    stackPanel.Children().Append(buttonPanel);
    try
    {
        // If a layout update has been triggered by other methods (e.g.: adapting to zoom level), this may throw an exception.
        stackPanel.UpdateLayout();
    }
    catch (...)
    {
    }

    // Configure the keyboardManagerState to store the UI information.
    keyboardManagerState.ConfigureDetectSingleKeyRemapUI(keyStackPanel);

    // Show the dialog
    detectRemapKeyBox.ShowAsync();
}
