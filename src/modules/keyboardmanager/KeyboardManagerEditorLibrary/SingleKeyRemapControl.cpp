#include "pch.h"
#include "SingleKeyRemapControl.h"

#include "KeyboardManagerState.h"
#include "KeyboardManagerEditorStrings.h"
#include "ShortcutControl.h"
#include "UIHelpers.h"
#include "EditorHelpers.h"
#include "EditorConstants.h"

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
    singleKeyRemapControlLayout.as<StackPanel>().Spacing(EditorConstants::ShortcutTableDropDownSpacing);

    // Key column (From key)
    if (colIndex == 0)
    {
        singleKeyRemapControlLayout.as<StackPanel>().Children().Append(typeKey.as<Button>());

        keyDropDownControlObjects.emplace_back(std::make_unique<KeyDropDownControl>(false));
        singleKeyRemapControlLayout.as<StackPanel>().Children().Append(keyDropDownControlObjects[0]->GetComboBox());
        // Set selection handler for the drop down
        keyDropDownControlObjects[0]->SetSelectionHandler(table, row, colIndex, singleKeyRemapBuffer);
    }

    // Hybrid column (To Key/Shortcut/Text)
    else
    {
        StackPanel keyComboAndSelectStackPanel;
        keyComboAndSelectStackPanel.Orientation(Windows::UI::Xaml::Controls::Orientation::Horizontal);
        keyComboAndSelectStackPanel.Spacing(EditorConstants::ShortcutTableDropDownSpacing);

        hybridDropDownVariableSizedWrapGrid = VariableSizedWrapGrid();
        auto grid = hybridDropDownVariableSizedWrapGrid.as<VariableSizedWrapGrid>();
        grid.Orientation(Windows::UI::Xaml::Controls::Orientation::Horizontal);
        auto gridMargin = Windows::UI::Xaml::Thickness();
        gridMargin.Bottom = -EditorConstants::ShortcutTableDropDownSpacing; // compensate for a collapsed textInput
        grid.Margin(gridMargin);

        KeyDropDownControl::AddDropDown(table, row, grid, colIndex, singleKeyRemapBuffer, keyDropDownControlObjects, nullptr, true, true);

        singleKeyRemapControlLayout.as<StackPanel>().Children().Append(grid);

        auto textInput = TextBox();

        auto textBoxMargin = Windows::UI::Xaml::Thickness();
        textBoxMargin.Top = -EditorConstants::ShortcutTableDropDownSpacing; // compensate for a collapsed grid
        textBoxMargin.Bottom = EditorConstants::ShortcutTableDropDownSpacing;
        textInput.Margin(textBoxMargin);
        textInput.AcceptsReturn(false);
        textInput.Visibility(Visibility::Collapsed);
        textInput.Width(EditorConstants::TableDropDownHeight);
        singleKeyRemapControlLayout.as<StackPanel>().Children().Append(textInput);
        textInput.HorizontalAlignment(HorizontalAlignment::Left);
        textInput.TextChanged([this, row, table](winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::Controls::TextChangedEventArgs const& e) mutable {
            auto textbox = sender.as<TextBox>();
            auto text = textbox.Text();
            uint32_t rowIndex = -1;
            if (!table.Children().IndexOf(row, rowIndex))
            {
                return;
            }

            singleKeyRemapBuffer[rowIndex].first[1] = text.c_str();
        });

        auto typeCombo = ComboBox();
        typeCombo.Width(EditorConstants::RemapTableDropDownWidth);
        typeCombo.Items().Append(winrt::box_value(KeyboardManagerEditorStrings::MappingTypeKeyShortcut()));
        typeCombo.Items().Append(winrt::box_value(KeyboardManagerEditorStrings::MappingTypeText()));
        keyComboAndSelectStackPanel.Children().Append(typeCombo);
        keyComboAndSelectStackPanel.Children().Append(typeKey.as<Button>());
        singleKeyRemapControlLayout.as<StackPanel>().Children().InsertAt(0, keyComboAndSelectStackPanel);

        typeCombo.SelectedIndex(0);
        typeCombo.SelectionChanged([this, typeCombo, grid, textInput](winrt::Windows::Foundation::IInspectable const&, SelectionChangedEventArgs const&) {
            const bool textSelected = typeCombo.SelectedIndex() == 1;

            const auto keyInputVisibility = textSelected ? Visibility::Collapsed : Visibility::Visible;

            grid.Visibility(keyInputVisibility);
            typeKey.as<Button>().Visibility(keyInputVisibility);

            const auto textInputVisibility = textSelected ? Visibility::Visible : Visibility::Collapsed;
            textInput.Visibility(textInputVisibility);
        });
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

void SingleKeyRemapControl::TextToMapChangedHandler(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::Controls::TextChangedEventArgs const& e) // TODO: remove
{
    auto textbox = sender.as<TextBox>();
    auto text = textbox.Text();
    (void)text;
}

// Function to add a new row to the remap keys table. If the originalKey and newKey args are provided, then the displayed remap keys are set to those values.
void SingleKeyRemapControl::AddNewControlKeyRemapRow(StackPanel& parent, std::vector<std::vector<std::unique_ptr<SingleKeyRemapControl>>>& keyboardRemapControlObjects, const DWORD originalKey, const KeyShortcutTextUnion newKey)
{
    // Create new SingleKeyRemapControl objects dynamically so that we does not get destructed
    std::vector<std::unique_ptr<SingleKeyRemapControl>> newrow;
    StackPanel row = StackPanel();
    parent.Children().Append(row);
    newrow.emplace_back(std::make_unique<SingleKeyRemapControl>(parent, row, 0));
    newrow.emplace_back(std::make_unique<SingleKeyRemapControl>(parent, row, 1));
    keyboardRemapControlObjects.push_back(std::move(newrow));

    row.Padding({ 10, 15, 10, 5 });
    row.Margin({ 0, 0, 0, 2 });
    row.Orientation(Orientation::Horizontal);
    row.Background(Application::Current().Resources().Lookup(box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());
    row.BorderBrush(Application::Current().Resources().Lookup(box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
    row.BorderThickness({ 0, 1, 0, 1 });

    // SingleKeyRemapControl for the original key.
    auto originalElement = keyboardRemapControlObjects.back()[0]->getSingleKeyRemapControl();
    originalElement.Width(EditorConstants::RemapTableDropDownWidth + EditorConstants::RemapTableDropDownSpacing);
    row.Children().Append(originalElement);

    // Arrow icon
    SymbolIcon arrowIcon(Symbol::Forward);
    arrowIcon.VerticalAlignment(VerticalAlignment::Center);
    arrowIcon.HorizontalAlignment(HorizontalAlignment::Center);
    auto arrowIconContainer = UIHelpers::GetWrapped(arrowIcon, EditorConstants::TableArrowColWidth).as<StackPanel>();
    arrowIconContainer.Orientation(Orientation::Vertical);
    arrowIconContainer.VerticalAlignment(VerticalAlignment::Center);
    arrowIconContainer.Margin({ 0, 0, 0, 10 });
    row.Children().Append(arrowIconContainer);

    // SingleKeyRemapControl for the new remap key
    auto targetElement = keyboardRemapControlObjects.back()[1]->getSingleKeyRemapControl();
    targetElement.Width(EditorConstants::RemapTargetColumnWidth);
    row.Children().Append(targetElement);

    // Set the key text if the two keys are not null (i.e. default args)
    if (originalKey != NULL && !(newKey.index() == 0 && std::get<DWORD>(newKey) == NULL) && !(newKey.index() == 1 && !EditorHelpers::IsValidShortcut(std::get<Shortcut>(newKey))) && !(newKey.index() == 2 && std::get<std::wstring>(newKey).empty()))
    {
        singleKeyRemapBuffer.push_back(RemapBufferRow{ RemapBufferItem{ originalKey, newKey }, L"" });
        keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][0]->keyDropDownControlObjects[0]->SetSelectedValue(std::to_wstring(originalKey));
        if (newKey.index() == 0)
        {
            keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->keyDropDownControlObjects[0]->SetSelectedValue(std::to_wstring(std::get<DWORD>(newKey)));
        }
        else if (newKey.index() == 1)
        {
            KeyDropDownControl::AddShortcutToControl(std::get<Shortcut>(newKey), parent, keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->hybridDropDownVariableSizedWrapGrid.as<VariableSizedWrapGrid>(), *keyboardManagerState, 1, keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->keyDropDownControlObjects, singleKeyRemapBuffer, row, nullptr, true, true);
        }
        else if (newKey.index() == 2)
        {
            auto& singleKeyRemapControl = keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1];

            const auto& firstLineStackPanel = singleKeyRemapControl->singleKeyRemapControlLayout.as<StackPanel>().Children().GetAt(0).as<StackPanel>();

            firstLineStackPanel.Children().GetAt(0).as<ComboBox>().SelectedIndex(1);

            singleKeyRemapControl->singleKeyRemapControlLayout.as<StackPanel>().Children().GetAt(2).as<TextBox>().Text(std::get<std::wstring>(newKey));
        }
    }
    else
    {
        // Initialize both keys to NULL
        singleKeyRemapBuffer.push_back(RemapBufferRow{ RemapBufferItem{ (DWORD)0, (DWORD)0 }, L"" });
    }

    // Delete row button
    Windows::UI::Xaml::Controls::Button deleteRemapKeys;
    deleteRemapKeys.Content(SymbolIcon(Symbol::Delete));
    deleteRemapKeys.Background(Media::SolidColorBrush(Colors::Transparent()));
    deleteRemapKeys.HorizontalAlignment(HorizontalAlignment::Center);
    deleteRemapKeys.Margin({ 0, 0, 0, 10 });
    deleteRemapKeys.Click([&, parent, row, deleteRemapKeys](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
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
    ToolTip deleteRemapKeysToolTip;
    deleteRemapKeysToolTip.Content(box_value(GET_RESOURCE_STRING(IDS_DELETE_REMAPPING_BUTTON)));
    ToolTipService::SetToolTip(deleteRemapKeys, deleteRemapKeysToolTip);
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
    UpdateAccessibleNames(keyboardRemapControlObjects.back()[0]->getSingleKeyRemapControl(), keyboardRemapControlObjects.back()[1]->getSingleKeyRemapControl(), deleteRemapKeys, static_cast<int>(keyboardRemapControlObjects.size()));
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

    // ContentDialog requires manually setting the XamlRoot (https://learn.microsoft.com/uwp/api/windows.ui.xaml.controls.contentdialog#contentdialog-in-appwindow-or-xaml-islands)
    detectRemapKeyBox.XamlRoot(xamlRoot);
    detectRemapKeyBox.Title(box_value(GET_RESOURCE_STRING(IDS_TYPEKEY_TITLE)));

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

    // OK button
    detectRemapKeyBox.DefaultButton(ContentDialogButton::Primary);
    detectRemapKeyBox.PrimaryButtonText(GET_RESOURCE_STRING(IDS_OK_BUTTON));
    detectRemapKeyBox.PrimaryButtonClick([onAccept](winrt::Windows::Foundation::IInspectable const& sender, ContentDialogButtonClickEventArgs const& args) {
        // Cancel default dialog events
        args.Cancel(true);

        onAccept();
    });

    // NOTE: UnregisterKeys should never be called on the DelayThread, as it will re-enter the mutex. To avoid this it is run on the dispatcher thread
    keyboardManagerState.RegisterKeyDelay(
        VK_RETURN,
        std::bind(&KBMEditor::KeyboardManagerState::SelectDetectedRemapKey, &keyboardManagerState, std::placeholders::_1),
        [onPressEnter, detectRemapKeyBox](DWORD) {
            detectRemapKeyBox.Dispatcher().RunAsync(
                Windows::UI::Core::CoreDispatcherPriority::Normal,
                [onPressEnter] {
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

    // Cancel button
    detectRemapKeyBox.CloseButtonText(GET_RESOURCE_STRING(IDS_CANCEL_BUTTON));
    detectRemapKeyBox.CloseButtonClick([onCancel](winrt::Windows::Foundation::IInspectable const& sender, ContentDialogButtonClickEventArgs const& args) {
        // Cancel default dialog events
        args.Cancel(true);

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