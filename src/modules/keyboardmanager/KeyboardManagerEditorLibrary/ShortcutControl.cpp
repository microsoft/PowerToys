#include "pch.h"
#include "ShortcutControl.h"
#include <Windows.h>
#include <commdlg.h>
#include <ShlObj.h>
#include <common/interop/shared_constants.h>

#include "KeyboardManagerState.h"
#include "KeyboardManagerEditorStrings.h"
#include "KeyDropDownControl.h"
#include "UIHelpers.h"
#include "EditorHelpers.h"
#include "EditorConstants.h"

//Both static members are initialized to null
HWND ShortcutControl::editShortcutsWindowHandle = nullptr;
KBMEditor::KeyboardManagerState* ShortcutControl::keyboardManagerState = nullptr;
// Initialized as new vector
RemapBuffer ShortcutControl::shortcutRemapBuffer;

ShortcutControl::ShortcutControl(StackPanel table, StackPanel row, const int colIndex, TextBox targetApp)
{
    shortcutDropDownVariableSizedWrapGrid = VariableSizedWrapGrid();
    btnPickShortcut = Button();
    shortcutControlLayout = StackPanel();

    const bool isHybridControl = colIndex == 1;

    // TODO: Check if there is a VariableSizedWrapGrid equivalent.
    // shortcutDropDownVariableSizedWrapGrid.as<VariableSizedWrapGrid>().Spacing(EditorConstants::ShortcutTableDropDownSpacing);
    shortcutDropDownVariableSizedWrapGrid.as<VariableSizedWrapGrid>().Orientation(Windows::UI::Xaml::Controls::Orientation::Horizontal);
    shortcutDropDownVariableSizedWrapGrid.as<VariableSizedWrapGrid>().MaximumRowsOrColumns(3);

    btnPickShortcut.as<Button>().Content(winrt::box_value(GET_RESOURCE_STRING(IDS_TYPE_BUTTON)));
    btnPickShortcut.as<Button>().Width(EditorConstants::ShortcutTableDropDownWidth / 2);
    btnPickShortcut.as<Button>().Click([&, table, row, colIndex, isHybridControl, targetApp](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        keyboardManagerState->SetUIState(KBMEditor::KeyboardManagerUIState::DetectShortcutWindowActivated, editShortcutsWindowHandle);
        // Using the XamlRoot of the typeShortcut to get the root of the XAML host
        CreateDetectShortcutWindow(sender, sender.as<Button>().XamlRoot(), *keyboardManagerState, colIndex, table, keyDropDownControlObjects, row, targetApp, isHybridControl, false, editShortcutsWindowHandle, shortcutRemapBuffer);
    });

    FontIcon fontIcon;
    fontIcon.Glyph(L"\uE70F"); // Unicode for the accept icon
    fontIcon.FontFamily(Media::FontFamily(L"Segoe MDL2 Assets")); // Set the font family to Segoe MDL2 Assets
    // Set the FontIcon as the content of the button
    btnPickShortcut.as<Button>().Content(fontIcon);

    uint32_t rowIndex;

    UIElementCollection children = table.Children();
    bool indexFound = children.IndexOf(row, rowIndex);

    auto nameX = L"btnPickShortcut_" + std::to_wstring(colIndex);
    btnPickShortcut.as<Button>().Name(nameX);

    // Set an accessible name for the type shortcut button
    btnPickShortcut.as<Button>().SetValue(Automation::AutomationProperties::NameProperty(), box_value(GET_RESOURCE_STRING(IDS_TYPE_BUTTON)));

    shortcutControlLayout.as<StackPanel>().Spacing(EditorConstants::ShortcutTableDropDownSpacing);

    keyComboStackPanel = StackPanel();
    keyComboStackPanel.as<StackPanel>().Orientation(Windows::UI::Xaml::Controls::Orientation::Horizontal);
    keyComboStackPanel.as<StackPanel>().Spacing(EditorConstants::ShortcutTableDropDownSpacing);

    shortcutControlLayout.as<StackPanel>().Children().Append(keyComboStackPanel.as<StackPanel>());

    spBtnPickShortcut = UIHelpers::GetLabelWrapped(btnPickShortcut.as<Button>(), GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_LABEL_SHORTCUT), 80).as<StackPanel>();
    shortcutControlLayout.as<StackPanel>().Children().Append(spBtnPickShortcut);

    shortcutControlLayout.as<StackPanel>().Children().Append(shortcutDropDownVariableSizedWrapGrid.as<VariableSizedWrapGrid>());

    try
    {
        // If a layout update has been triggered by other methods (e.g.: adapting to zoom level), this may throw an exception.
        shortcutControlLayout.as<StackPanel>().UpdateLayout();
    }
    catch (...)
    {
    }
}

void ShortcutControl::OpenNewShortcutControlRow(StackPanel table, StackPanel row)
{
    keyboardManagerState->SetUIState(KBMEditor::KeyboardManagerUIState::DetectShortcutWindowActivated, editShortcutsWindowHandle);
    // Using the XamlRoot of the typeShortcut to get the root of the XAML host
    CreateDetectShortcutWindow(btnPickShortcut, btnPickShortcut.XamlRoot(), *keyboardManagerState, 0, table, keyDropDownControlObjects, row, nullptr, false, false, editShortcutsWindowHandle, shortcutRemapBuffer);
}

// Function to set the accessible name of the target App text box
void ShortcutControl::SetAccessibleNameForTextBox(TextBox targetAppTextBox, int rowIndex)
{
    // To set the accessible name of the target App text box by adding the string `All Apps` if the text box is empty, if not the application name is read by narrator.
    std::wstring targetAppTextBoxAccessibleName = GET_RESOURCE_STRING(IDS_AUTOMATIONPROPERTIES_ROW) + std::to_wstring(rowIndex) + L", " + GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_TARGETAPPHEADER);
    if (targetAppTextBox.Text() == L"")
    {
        targetAppTextBoxAccessibleName += GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_ALLAPPS);
    }

    targetAppTextBox.SetValue(Automation::AutomationProperties::NameProperty(), box_value(targetAppTextBoxAccessibleName));
}

// Function to set the accessible names for all the controls in a row
void ShortcutControl::UpdateAccessibleNames(StackPanel sourceColumn, StackPanel mappedToColumn, TextBox targetAppTextBox, Button deleteButton, int rowIndex)
{
    sourceColumn.SetValue(Automation::AutomationProperties::NameProperty(), box_value(GET_RESOURCE_STRING(IDS_AUTOMATIONPROPERTIES_ROW) + std::to_wstring(rowIndex) + L", " + GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_SOURCEHEADER)));
    mappedToColumn.SetValue(Automation::AutomationProperties::NameProperty(), box_value(GET_RESOURCE_STRING(IDS_AUTOMATIONPROPERTIES_ROW) + std::to_wstring(rowIndex) + L", " + GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_TARGETHEADER)));
    ShortcutControl::SetAccessibleNameForTextBox(targetAppTextBox, rowIndex);
    deleteButton.SetValue(Automation::AutomationProperties::NameProperty(), box_value(GET_RESOURCE_STRING(IDS_AUTOMATIONPROPERTIES_ROW) + std::to_wstring(rowIndex) + L", " + GET_RESOURCE_STRING(IDS_DELETE_REMAPPING_BUTTON)));
}

void ShortcutControl::DeleteShortcutControl(StackPanel& parent, std::vector<std::vector<std::unique_ptr<ShortcutControl>>>& keyboardRemapControlObjects, int rowIndex)
{
    UIElementCollection children = parent.Children();
    children.RemoveAt(rowIndex);
    shortcutRemapBuffer.erase(shortcutRemapBuffer.begin() + rowIndex);
    // delete the SingleKeyRemapControl objects so that they get destructed
    keyboardRemapControlObjects.erase(keyboardRemapControlObjects.begin() + rowIndex);
}

// Function to add a new row to the shortcut table. If the originalKeys and newKeys args are provided, then the displayed shortcuts are set to those values.
ShortcutControl& ShortcutControl::AddNewShortcutControlRow(StackPanel& parent, std::vector<std::vector<std::unique_ptr<ShortcutControl>>>& keyboardRemapControlObjects, const Shortcut& originalKeys, const KeyShortcutTextUnion& newKeys, const std::wstring& targetAppName)
{
    // Textbox for target application
    TextBox targetAppTextBox;
    int runProgramLabelWidth = 80;

    // Create new ShortcutControl objects dynamically so that we does not get destructed
    std::vector<std::unique_ptr<ShortcutControl>> newrow;
    StackPanel row = StackPanel();

    row.Name(L"row");

    parent.Children().Append(row);

    newrow.emplace_back(std::make_unique<ShortcutControl>(parent, row, 0, targetAppTextBox));

    ShortcutControl& newShortcutToRemap = *(newrow.back());

    newrow.emplace_back(std::make_unique<ShortcutControl>(parent, row, 1, targetAppTextBox));

    keyboardRemapControlObjects.push_back(std::move(newrow));

    row.Padding({ 10, 15, 10, 5 });
    row.Margin({ 0, 0, 0, 2 });
    row.Orientation(Orientation::Horizontal);
    row.Background(Application::Current().Resources().Lookup(box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());
    row.BorderBrush(Application::Current().Resources().Lookup(box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
    row.BorderThickness({ 0, 1, 0, 1 });

    // ShortcutControl for the original shortcut
    auto origin = keyboardRemapControlObjects.back()[0]->GetShortcutControl();

    origin.Width(EditorConstants::ShortcutOriginColumnWidth);

    row.Children().Append(origin);

    // Arrow icon
    SymbolIcon arrowIcon(Symbol::Forward);
    arrowIcon.VerticalAlignment(VerticalAlignment::Center);
    arrowIcon.HorizontalAlignment(HorizontalAlignment::Center);
    auto arrowIconContainer = UIHelpers::GetWrapped(arrowIcon, EditorConstants::ShortcutArrowColumnWidth).as<StackPanel>();
    arrowIconContainer.Orientation(Orientation::Vertical);
    arrowIconContainer.VerticalAlignment(VerticalAlignment::Center);
    arrowIconContainer.Margin({ 0, 0, 0, 10 });
    row.Children().Append(arrowIconContainer);

    // ShortcutControl for the new shortcut
    auto target = keyboardRemapControlObjects.back()[1]->GetShortcutControl();
    target.Width(EditorConstants::ShortcutTargetColumnWidth);

    uint32_t rowIndex = -1;
    if (!parent.Children().IndexOf(row, rowIndex))
    {
        return newShortcutToRemap;
    }

    // add shortcut type choice
    auto actionTypeCombo = ComboBox();
    actionTypeCombo.Name(L"actionTypeCombo_" + std::to_wstring(rowIndex));
    actionTypeCombo.Width(EditorConstants::RemapTableDropDownWidth);
    actionTypeCombo.Items().Append(winrt::box_value(KeyboardManagerEditorStrings::MappingTypeKeyShortcut()));
    actionTypeCombo.Items().Append(winrt::box_value(KeyboardManagerEditorStrings::MappingTypeText()));
    actionTypeCombo.Items().Append(winrt::box_value(KeyboardManagerEditorStrings::MappingTypeRunProgram()));
    actionTypeCombo.Items().Append(winrt::box_value(KeyboardManagerEditorStrings::MappingTypeOpenUri()));

    auto controlStackPanel = keyboardRemapControlObjects.back()[1]->shortcutControlLayout.as<StackPanel>();
    auto firstLineStackPanel = keyboardRemapControlObjects.back()[1]->keyComboStackPanel.as<StackPanel>();

    firstLineStackPanel.Children().InsertAt(0, UIHelpers::GetLabelWrapped(actionTypeCombo, GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_LABEL_ACTION), runProgramLabelWidth).as<StackPanel>());

    // add textbox for when it's a text input

    auto unicodeTextKeysInput = TextBox();

    unicodeTextKeysInput.Name(L"unicodeTextKeysInput_" + std::to_wstring(rowIndex));

    auto textInputMargin = Windows::UI::Xaml::Thickness();
    textInputMargin.Bottom = EditorConstants::ShortcutTableDropDownSpacing; // compensate for a collapsed UIElement
    unicodeTextKeysInput.Margin(textInputMargin);

    unicodeTextKeysInput.AcceptsReturn(false);
    //unicodeTextKeysInput.Visibility(Visibility::Collapsed);
    unicodeTextKeysInput.Width(EditorConstants::TableDropDownHeight);

    StackPanel spUnicodeTextKeysInput = UIHelpers::GetLabelWrapped(unicodeTextKeysInput, GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_LABEL_KEYS), runProgramLabelWidth).as<StackPanel>();
    controlStackPanel.Children().Append(spUnicodeTextKeysInput);

    unicodeTextKeysInput.HorizontalAlignment(HorizontalAlignment::Left);

    unicodeTextKeysInput.TextChanged([parent, row](winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::Controls::TextChangedEventArgs const& e) mutable {
        auto textbox = sender.as<TextBox>();
        auto text = textbox.Text();
        uint32_t rowIndex = -1;

        if (!parent.Children().IndexOf(row, rowIndex))
        {
            return;
        }

        shortcutRemapBuffer[rowIndex].first[1] = text.c_str();
    });

    const bool textSelected = newKeys.index() == 2;
    bool isRunProgram = false;
    bool isOpenUri = false;
    Shortcut shortCut;
    if (!textSelected && newKeys.index() == 1)
    {
        shortCut = std::get<Shortcut>(newKeys);
        isRunProgram = (shortCut.operationType == Shortcut::OperationType::RunProgram);
        isOpenUri = (shortCut.operationType == Shortcut::OperationType::OpenURI);
    }

    // add TextBoxes for when it's a runProgram fields

    auto runProgramStackPanel = SetupRunProgramControls(parent, row, shortCut, textInputMargin, controlStackPanel);

    runProgramStackPanel.Margin({ 0, -30, 0, 0 });

    auto openURIStackPanel = SetupOpenURIControls(parent, row, shortCut, textInputMargin, controlStackPanel);

    // add grid for when it's a key/shortcut
    auto shortcutItemsGrid = keyboardRemapControlObjects.back()[1]->shortcutDropDownVariableSizedWrapGrid.as<VariableSizedWrapGrid>();
    auto gridMargin = Windows::UI::Xaml::Thickness();
    gridMargin.Bottom = -EditorConstants::ShortcutTableDropDownSpacing; // compensate for a collapsed textInput
    shortcutItemsGrid.Margin(gridMargin);
    auto shortcutButton = keyboardRemapControlObjects.back()[1]->btnPickShortcut.as<Button>();
    auto spBtnPickShortcut = keyboardRemapControlObjects.back()[1]->spBtnPickShortcut.as<StackPanel>();

    // event code for when type changes
    actionTypeCombo.SelectionChanged([parent, row, controlStackPanel, actionTypeCombo, shortcutItemsGrid, spBtnPickShortcut, spUnicodeTextKeysInput, runProgramStackPanel, openURIStackPanel](winrt::Windows::Foundation::IInspectable const&, SelectionChangedEventArgs const&) {
        const auto shortcutType = ShortcutControl::GetShortcutType(actionTypeCombo);

        if (shortcutType == ShortcutControl::ShortcutType::Shortcut)
        {
            spBtnPickShortcut.Visibility(Visibility::Visible);
            shortcutItemsGrid.Visibility(Visibility::Visible);
            spUnicodeTextKeysInput.Visibility(Visibility::Collapsed);
            runProgramStackPanel.Visibility(Visibility::Collapsed);
            openURIStackPanel.Visibility(Visibility::Collapsed);
        }
        else if (shortcutType == ShortcutControl::ShortcutType::Text)
        {
            spBtnPickShortcut.Visibility(Visibility::Collapsed);
            shortcutItemsGrid.Visibility(Visibility::Collapsed);
            spUnicodeTextKeysInput.Visibility(Visibility::Visible);
            runProgramStackPanel.Visibility(Visibility::Collapsed);
            openURIStackPanel.Visibility(Visibility::Collapsed);
        }
        else if (shortcutType == ShortcutControl::ShortcutType::RunProgram)
        {
            spBtnPickShortcut.Visibility(Visibility::Collapsed);
            shortcutItemsGrid.Visibility(Visibility::Collapsed);
            spUnicodeTextKeysInput.Visibility(Visibility::Collapsed);
            runProgramStackPanel.Visibility(Visibility::Visible);
            openURIStackPanel.Visibility(Visibility::Collapsed);
        }
        else
        {
            spBtnPickShortcut.Visibility(Visibility::Collapsed);
            shortcutItemsGrid.Visibility(Visibility::Collapsed);
            spUnicodeTextKeysInput.Visibility(Visibility::Collapsed);
            runProgramStackPanel.Visibility(Visibility::Collapsed);
            openURIStackPanel.Visibility(Visibility::Visible);
        }
    });

    row.Children().Append(target);

    if (textSelected)
    {
        actionTypeCombo.SelectedIndex(1);
    }
    else
    {
        if (shortCut.operationType == Shortcut::OperationType::RunProgram)
        {
            actionTypeCombo.SelectedIndex(2);
        }
        else if (shortCut.operationType == Shortcut::OperationType::OpenURI)
        {
            actionTypeCombo.SelectedIndex(3);
        }
        else
        {
            actionTypeCombo.SelectedIndex(0);
        }
    }

    targetAppTextBox.Width(EditorConstants::ShortcutTableDropDownWidth);
    targetAppTextBox.PlaceholderText(KeyboardManagerEditorStrings::DefaultAppName());
    targetAppTextBox.Text(targetAppName);
    targetAppTextBox.Margin({ 0, 0, 0, 10 });

    // GotFocus handler will be called whenever the user tabs into or clicks on the textbox
    targetAppTextBox.GotFocus([targetAppTextBox](auto const& sender, auto const& e) {
        // Select all text for accessible purpose
        targetAppTextBox.SelectAll();
    });

    // LostFocus handler will be called whenever text is updated by a user and then they click something else or tab to another control. Does not get called if Text is updated while the TextBox isn't in focus (i.e. from code)
    targetAppTextBox.LostFocus([&keyboardRemapControlObjects, parent, row, targetAppTextBox, actionTypeCombo, unicodeTextKeysInput](auto const& sender, auto const& e) {
        // Get index of targetAppTextBox button
        uint32_t rowIndex;
        if (!parent.Children().IndexOf(row, rowIndex))
        {
            return;
        }

        // rowIndex could be out of bounds if the row got deleted after LostFocus handler was invoked. In this case it should return
        if (rowIndex >= keyboardRemapControlObjects.size())
        {
            return;
        }

        // Validate both set of drop downs
        KeyDropDownControl::ValidateShortcutFromDropDownList(parent, row, keyboardRemapControlObjects[rowIndex][0]->shortcutDropDownVariableSizedWrapGrid.as<VariableSizedWrapGrid>(), 0, ShortcutControl::shortcutRemapBuffer, keyboardRemapControlObjects[rowIndex][0]->keyDropDownControlObjects, targetAppTextBox, false, false);
        KeyDropDownControl::ValidateShortcutFromDropDownList(parent, row, keyboardRemapControlObjects[rowIndex][1]->shortcutDropDownVariableSizedWrapGrid.as<VariableSizedWrapGrid>(), 1, ShortcutControl::shortcutRemapBuffer, keyboardRemapControlObjects[rowIndex][1]->keyDropDownControlObjects, targetAppTextBox, true, false);

        // Reset the buffer based on the selected drop down items
        std::get<Shortcut>(shortcutRemapBuffer[rowIndex].first[0]).SetKeyCodes(KeyDropDownControl::GetSelectedCodesFromStackPanel(keyboardRemapControlObjects[rowIndex][0]->shortcutDropDownVariableSizedWrapGrid.as<VariableSizedWrapGrid>()));
        // second column is a hybrid column

        const bool regularShortcut = actionTypeCombo.SelectedIndex() == 0;
        const bool textSelected = actionTypeCombo.SelectedIndex() == 1;
        const bool runProgram = actionTypeCombo.SelectedIndex() == 2;
        const bool openUri = actionTypeCombo.SelectedIndex() == 3;

        if (textSelected)
        {
            shortcutRemapBuffer[rowIndex].first[1] = unicodeTextKeysInput.Text().c_str();
        }
        else
        {
            if (regularShortcut)
            {
                std::vector<int32_t> selectedKeyCodes = KeyDropDownControl::GetSelectedCodesFromStackPanel(keyboardRemapControlObjects[rowIndex][1]->shortcutDropDownVariableSizedWrapGrid.as<VariableSizedWrapGrid>());

                // If exactly one key is selected consider it to be a key remap
                if (selectedKeyCodes.size() == 1)
                {
                    shortcutRemapBuffer[rowIndex].first[1] = (DWORD)selectedKeyCodes[0];
                }
                else
                {
                    Shortcut tempShortcut;
                    tempShortcut.SetKeyCodes(selectedKeyCodes);
                    // Assign instead of setting the value in the buffer since the previous value may not be a Shortcut
                    shortcutRemapBuffer[rowIndex].first[1] = tempShortcut;
                }
            }
            else if (runProgram)
            {
                auto runProgramPathInput = row.FindName(L"runProgramPathInput_" + std::to_wstring(rowIndex)).as<TextBox>();
                auto runProgramArgsForProgramInput = row.FindName(L"runProgramArgsForProgramInput_" + std::to_wstring(rowIndex)).as<TextBox>();
                auto runProgramStartInDirInput = row.FindName(L"runProgramStartInDirInput_" + std::to_wstring(rowIndex)).as<TextBox>();
                auto runProgramElevationTypeCombo = row.FindName(L"runProgramElevationTypeCombo_" + std::to_wstring(rowIndex)).as<ComboBox>();
                auto runProgramAlreadyRunningAction = row.FindName(L"runProgramAlreadyRunningAction_" + std::to_wstring(rowIndex)).as<ComboBox>();

                Shortcut tempShortcut;
                tempShortcut.operationType = Shortcut::OperationType::RunProgram;

                tempShortcut.runProgramFilePath = ShortcutControl::RemoveExtraQuotes(runProgramPathInput.Text().c_str());
                tempShortcut.runProgramArgs = (runProgramArgsForProgramInput.Text().c_str());
                tempShortcut.runProgramStartInDir = (runProgramStartInDirInput.Text().c_str());

                tempShortcut.elevationLevel = static_cast<Shortcut::ElevationLevel>(runProgramElevationTypeCombo.SelectedIndex());
                tempShortcut.alreadyRunningAction = static_cast<Shortcut::ProgramAlreadyRunningAction>(runProgramAlreadyRunningAction.SelectedIndex());

                // Assign instead of setting the value in the buffer since the previous value may not be a Shortcut
                shortcutRemapBuffer[rowIndex].first[1] = tempShortcut;
            }
            else if (openUri)
            {
            }
        }

        std::wstring newText = targetAppTextBox.Text().c_str();
        std::wstring lowercaseDefAppName = KeyboardManagerEditorStrings::DefaultAppName();
        std::transform(newText.begin(), newText.end(), newText.begin(), towlower);
        std::transform(lowercaseDefAppName.begin(), lowercaseDefAppName.end(), lowercaseDefAppName.begin(), towlower);
        if (newText == lowercaseDefAppName)
        {
            shortcutRemapBuffer[rowIndex].second = L"";
        }
        else
        {
            shortcutRemapBuffer[rowIndex].second = targetAppTextBox.Text().c_str();
        }

        // To set the accessible name of the target app text box when focus is lost
        ShortcutControl::SetAccessibleNameForTextBox(targetAppTextBox, rowIndex + 1);
    });

    // We need two containers in order to align it horizontally and vertically

    StackPanel targetAppHorizontal = UIHelpers::GetWrapped(targetAppTextBox, EditorConstants::TableTargetAppColWidth).as<StackPanel>();
    targetAppHorizontal.Orientation(Orientation::Horizontal);
    targetAppHorizontal.HorizontalAlignment(HorizontalAlignment::Left);
    StackPanel targetAppContainer = UIHelpers::GetWrapped(targetAppHorizontal, EditorConstants::TableTargetAppColWidth).as<StackPanel>();
    targetAppContainer.Orientation(Orientation::Vertical);
    targetAppContainer.VerticalAlignment(VerticalAlignment::Center);

    row.Children().Append(targetAppContainer);

    // Delete row button
    Windows::UI::Xaml::Controls::Button deleteShortcut;

    deleteShortcut.Content(SymbolIcon(Symbol::Delete));
    deleteShortcut.Background(Media::SolidColorBrush(Colors::Transparent()));
    deleteShortcut.HorizontalAlignment(HorizontalAlignment::Center);
    deleteShortcut.Margin({ 0, 0, 0, 10 });
    deleteShortcut.Click([&, parent, row, deleteShortcut](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        Button currentButton = sender.as<Button>();
        uint32_t rowIndex;
        // Get index of delete button
        UIElementCollection children = parent.Children();
        bool indexFound = children.IndexOf(row, rowIndex);

        // IndexOf could fail if the row got deleted and the button handler was invoked twice. In this case it should return
        if (!indexFound)
        {
            return;
        }

        for (uint32_t i = rowIndex + 1; i < children.Size(); i++)
        {
            StackPanel row = children.GetAt(i).as<StackPanel>();
            StackPanel sourceCol = row.Children().GetAt(0).as<StackPanel>();
            StackPanel targetCol = row.Children().GetAt(2).as<StackPanel>();
            TextBox targetApp = row.Children().GetAt(3).as<StackPanel>().Children().GetAt(0).as<StackPanel>().Children().GetAt(0).as<TextBox>();
            Button delButton = row.Children().GetAt(4).as<StackPanel>().Children().GetAt(0).as<Button>();
            UpdateAccessibleNames(sourceCol, targetCol, targetApp, delButton, i);
        }

        if (auto automationPeer{ Automation::Peers::FrameworkElementAutomationPeer::FromElement(deleteShortcut) })
        {
            automationPeer.RaiseNotificationEvent(
                Automation::Peers::AutomationNotificationKind::ActionCompleted,
                Automation::Peers::AutomationNotificationProcessing::ImportantMostRecent,
                GET_RESOURCE_STRING(IDS_DELETE_REMAPPING_EVENT),
                L"ShortcutRemappingDeletedNotificationEvent" /* unique name for this notification category */);
        }

        children.RemoveAt(rowIndex);
        shortcutRemapBuffer.erase(shortcutRemapBuffer.begin() + rowIndex);
        // delete the SingleKeyRemapControl objects so that they get destructed
        keyboardRemapControlObjects.erase(keyboardRemapControlObjects.begin() + rowIndex);
    });

    // To set the accessible name of the delete button
    deleteShortcut.SetValue(Automation::AutomationProperties::NameProperty(), box_value(GET_RESOURCE_STRING(IDS_DELETE_REMAPPING_BUTTON)));

    // Add tooltip for delete button which would appear on hover
    ToolTip deleteShortcuttoolTip;
    deleteShortcuttoolTip.Content(box_value(GET_RESOURCE_STRING(IDS_DELETE_REMAPPING_BUTTON)));
    ToolTipService::SetToolTip(deleteShortcut, deleteShortcuttoolTip);

    StackPanel deleteShortcutContainer = StackPanel();
    deleteShortcutContainer.Name(L"deleteShortcutContainer");
    deleteShortcutContainer.Children().Append(deleteShortcut);
    deleteShortcutContainer.Orientation(Orientation::Vertical);
    deleteShortcutContainer.VerticalAlignment(VerticalAlignment::Center);

    row.Children().Append(deleteShortcutContainer);

    // Set accessible names
    UpdateAccessibleNames(keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][0]->GetShortcutControl(), keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->GetShortcutControl(), targetAppTextBox, deleteShortcut, static_cast<int>(keyboardRemapControlObjects.size()));

    // Set the shortcut text if the two vectors are not empty (i.e. default args)
    if (EditorHelpers::IsValidShortcut(originalKeys) && !(newKeys.index() == 0 && std::get<DWORD>(newKeys) == NULL) && !(newKeys.index() == 1 && !EditorHelpers::IsValidShortcut(std::get<Shortcut>(newKeys))))
    {
        // change to load app name

        if (isRunProgram || isOpenUri)
        {
            // not sure why by we need to add the current item in here, so we have it even if does not change.
            auto newShortcut = std::get<Shortcut>(newKeys);
            shortcutRemapBuffer.push_back(RemapBufferRow{ RemapBufferItem{ Shortcut(), newShortcut }, std::wstring(targetAppName) });
        }
        else
        {
            shortcutRemapBuffer.push_back(RemapBufferRow{ RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring(targetAppName) });
        }

        KeyDropDownControl::AddShortcutToControl(originalKeys, parent, keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][0]->shortcutDropDownVariableSizedWrapGrid.as<VariableSizedWrapGrid>(), *keyboardManagerState, 0, keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][0]->keyDropDownControlObjects, shortcutRemapBuffer, row, targetAppTextBox, false, false);

        if (newKeys.index() == 0)
        {
            auto shortcut = new Shortcut;
            shortcut->SetKey(std::get<DWORD>(newKeys));
            KeyDropDownControl::AddShortcutToControl(*shortcut, parent, keyboardRemapControlObjects.back()[1]->shortcutDropDownVariableSizedWrapGrid.as<VariableSizedWrapGrid>(), *keyboardManagerState, 1, keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->keyDropDownControlObjects, shortcutRemapBuffer, row, targetAppTextBox, true, false);
        }
        else if (newKeys.index() == 1)
        {
            KeyDropDownControl::AddShortcutToControl(std::get<Shortcut>(newKeys), parent, keyboardRemapControlObjects.back()[1]->shortcutDropDownVariableSizedWrapGrid.as<VariableSizedWrapGrid>(), *keyboardManagerState, 1, keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->keyDropDownControlObjects, shortcutRemapBuffer, row, targetAppTextBox, true, false);
        }
        else if (newKeys.index() == 2)
        {
            shortcutRemapBuffer.back().first[1] = std::get<std::wstring>(newKeys);
            const auto& remapControl = keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1];
            actionTypeCombo.SelectedIndex(1);
            unicodeTextKeysInput.Text(std::get<std::wstring>(newKeys));
        }
    }
    else
    {
        // Initialize both shortcuts as empty shortcuts
        shortcutRemapBuffer.push_back(RemapBufferRow{ RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring(targetAppName) });

        KeyDropDownControl::AddShortcutToControl(originalKeys, parent, keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][0]->shortcutDropDownVariableSizedWrapGrid.as<VariableSizedWrapGrid>(), *keyboardManagerState, 0, keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][0]->keyDropDownControlObjects, shortcutRemapBuffer, row, targetAppTextBox, false, false);

        KeyDropDownControl::AddShortcutToControl(std::get<Shortcut>(newKeys), parent, keyboardRemapControlObjects.back()[1]->shortcutDropDownVariableSizedWrapGrid.as<VariableSizedWrapGrid>(), *keyboardManagerState, 1, keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->keyDropDownControlObjects, shortcutRemapBuffer, row, targetAppTextBox, true, false);

    }

    return newShortcutToRemap;
}

StackPanel SetupOpenURIControls(StackPanel& parent, StackPanel& row, Shortcut& shortCut, winrt::Windows::UI::Xaml::Thickness& textInputMargin, ::StackPanel& _controlStackPanel)
{
    StackPanel openUriStackPanel;
    auto uriTextBox = TextBox();

    int runProgramLabelWidth = 80;

    uriTextBox.Text(shortCut.uriToOpen);
    uriTextBox.PlaceholderText(GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_URI_EXAMPLE));
    uriTextBox.Margin(textInputMargin);
    uriTextBox.Width(EditorConstants::TableDropDownHeight);
    uriTextBox.HorizontalAlignment(HorizontalAlignment::Left);

    winrt::Windows::UI::Xaml::Controls::HyperlinkButton hyperlinkButton;
    hyperlinkButton.NavigateUri(Windows::Foundation::Uri(L"https://learn.microsoft.com/windows/uwp/launch-resume/launch-app-with-uri"));
    hyperlinkButton.Content(winrt::box_value(GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_WHAT_CAN_I_USE_LINK)));
    hyperlinkButton.Margin(textInputMargin);

    StackPanel boxAndLink;
    boxAndLink.Orientation(Orientation::Horizontal);
    boxAndLink.Children().Append(uriTextBox);
    boxAndLink.Children().Append(hyperlinkButton);

    openUriStackPanel.Children().Append(UIHelpers::GetLabelWrapped(boxAndLink, GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_LABEL_PATH_URI), runProgramLabelWidth).as<StackPanel>());

    uriTextBox.TextChanged([parent, row, uriTextBox](winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::Controls::TextChangedEventArgs const& e) mutable {
        uint32_t rowIndex = -1;
        if (!parent.Children().IndexOf(row, rowIndex))
        {
            return;
        }
        Shortcut tempShortcut;
        tempShortcut.operationType = Shortcut::OperationType::OpenURI;
        tempShortcut.uriToOpen = ShortcutControl::RemoveExtraQuotes(uriTextBox.Text().c_str());
        ShortcutControl::shortcutRemapBuffer[rowIndex].first[1] = tempShortcut;
    });

    _controlStackPanel.Children().Append(openUriStackPanel);
    return openUriStackPanel;
}

StackPanel SetupRunProgramControls(StackPanel& parent, StackPanel& row, Shortcut& shortCut, winrt::Windows::UI::Xaml::Thickness& textInputMargin, ::StackPanel& _controlStackPanel)
{
    uint32_t rowIndex;
    // Get index of delete button
    UIElementCollection children = parent.Children();
    children.IndexOf(row, rowIndex);

    StackPanel controlStackPanel;
    controlStackPanel.Name(L"RunProgramControls_" + std::to_wstring(rowIndex));

    auto runProgramPathInput = TextBox();
    runProgramPathInput.Name(L"runProgramPathInput_" + std::to_wstring(rowIndex));
    auto runProgramArgsForProgramInput = TextBox();
    runProgramArgsForProgramInput.Name(L"runProgramArgsForProgramInput_" + std::to_wstring(rowIndex));
    auto runProgramStartInDirInput = TextBox();
    runProgramStartInDirInput.Name(L"runProgramStartInDirInput_" + std::to_wstring(rowIndex));

    Button pickFileBtn;
    Button pickPathBtn;
    auto runProgramElevationTypeCombo = ComboBox();
    runProgramElevationTypeCombo.Name(L"runProgramElevationTypeCombo_" + std::to_wstring(rowIndex));

    auto runProgramAlreadyRunningAction = ComboBox();
    runProgramAlreadyRunningAction.Name(L"runProgramAlreadyRunningAction_" + std::to_wstring(rowIndex));

    _controlStackPanel.Children().Append(controlStackPanel);

    StackPanel stackPanelForRunProgramPath;
    StackPanel stackPanelRunProgramStartInDir;

    runProgramPathInput.PlaceholderText(GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_PATH_TO_PROGRAM));

    runProgramPathInput.Margin(textInputMargin);

    runProgramPathInput.AcceptsReturn(false);
    runProgramPathInput.IsSpellCheckEnabled(false);
    runProgramPathInput.Width(EditorConstants::TableDropDownHeight);
    runProgramPathInput.HorizontalAlignment(HorizontalAlignment::Left);

    runProgramArgsForProgramInput.PlaceholderText(GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_ARGS_FOR_PROGRAM));
    runProgramArgsForProgramInput.Margin(textInputMargin);
    runProgramArgsForProgramInput.AcceptsReturn(false);
    runProgramArgsForProgramInput.IsSpellCheckEnabled(false);
    runProgramArgsForProgramInput.Width(EditorConstants::TableDropDownHeight);
    runProgramArgsForProgramInput.HorizontalAlignment(HorizontalAlignment::Left);

    runProgramStartInDirInput.IsSpellCheckEnabled(false);
    runProgramStartInDirInput.PlaceholderText(GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_START_IN_DIR_FOR_PROGRAM));
    runProgramStartInDirInput.Margin(textInputMargin);
    runProgramStartInDirInput.AcceptsReturn(false);
    runProgramStartInDirInput.Width(EditorConstants::TableDropDownHeight);
    runProgramStartInDirInput.HorizontalAlignment(HorizontalAlignment::Left);

    stackPanelForRunProgramPath.Orientation(Orientation::Horizontal);
    stackPanelRunProgramStartInDir.Orientation(Orientation::Horizontal);

    pickFileBtn.Content(winrt::box_value(GET_RESOURCE_STRING(IDS_BROWSE_FOR_PROGRAM_BUTTON)));
    pickPathBtn.Content(winrt::box_value(GET_RESOURCE_STRING(IDS_BROWSE_FOR_PATH_BUTTON)));
    pickFileBtn.Margin(textInputMargin);
    pickPathBtn.Margin(textInputMargin);

    stackPanelForRunProgramPath.Children().Append(runProgramPathInput);
    stackPanelForRunProgramPath.Children().Append(pickFileBtn);

    stackPanelRunProgramStartInDir.Children().Append(runProgramStartInDirInput);
    stackPanelRunProgramStartInDir.Children().Append(pickPathBtn);

    int runProgramLabelWidth = 90;

    controlStackPanel.Children().Append(UIHelpers::GetLabelWrapped(stackPanelForRunProgramPath, GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_LABEL_PROGRAM), runProgramLabelWidth).as<StackPanel>());

    controlStackPanel.Children().Append(UIHelpers::GetLabelWrapped(runProgramArgsForProgramInput, GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_LABEL_ARGS), runProgramLabelWidth).as<StackPanel>());

    controlStackPanel.Children().Append(UIHelpers::GetLabelWrapped(stackPanelRunProgramStartInDir, GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_LABEL_START_IN), runProgramLabelWidth).as<StackPanel>());

    // add shortcut type choice
    runProgramElevationTypeCombo.Width(EditorConstants::TableDropDownHeight);
    runProgramElevationTypeCombo.Items().Append(winrt::box_value(GET_RESOURCE_STRING(IDS_ELEVATION_TYPE_NORMAL)));
    runProgramElevationTypeCombo.Items().Append(winrt::box_value(GET_RESOURCE_STRING(IDS_ELEVATION_TYPE_ELEVATED)));
    runProgramElevationTypeCombo.Items().Append(winrt::box_value(GET_RESOURCE_STRING(IDS_ELEVATION_TYPE_DIFFERENT_USER)));
    runProgramElevationTypeCombo.SelectedIndex(0);
    // runProgramAlreadyRunningAction
    runProgramAlreadyRunningAction.Width(EditorConstants::TableDropDownHeight);
    runProgramAlreadyRunningAction.Items().Append(winrt::box_value(GET_RESOURCE_STRING(IDS_ALREADY_RUNNING_SHOW_WINDOW)));
    runProgramAlreadyRunningAction.Items().Append(winrt::box_value(GET_RESOURCE_STRING(IDS_ALREADY_RUNNING_START_ANOTHER)));
    runProgramAlreadyRunningAction.Items().Append(winrt::box_value(GET_RESOURCE_STRING(IDS_ALREADY_RUNNING_DO_NOTHING)));
    runProgramAlreadyRunningAction.Items().Append(winrt::box_value(GET_RESOURCE_STRING(IDS_ALREADY_RUNNING_CLOSE)));
    runProgramAlreadyRunningAction.Items().Append(winrt::box_value(GET_RESOURCE_STRING(IDS_ALREADY_RUNNING_TERMINATE)));

    runProgramAlreadyRunningAction.SelectedIndex(0);

    controlStackPanel.Children().Append(UIHelpers::GetLabelWrapped(runProgramElevationTypeCombo, GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_LABEL_ELEVATION), runProgramLabelWidth).as<StackPanel>());
    controlStackPanel.Children().Append(UIHelpers::GetLabelWrapped(runProgramAlreadyRunningAction, GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_LABEL_IF_RUNNING), runProgramLabelWidth).as<StackPanel>());

    auto runProgramStartWindow = ComboBox();
    runProgramStartWindow.Name(L"runProgramStartWindow_" + std::to_wstring(rowIndex));
    runProgramStartWindow.Width(EditorConstants::TableDropDownHeight);
    runProgramStartWindow.Items().Append(winrt::box_value(GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_VISIBILITY_NORMAL)));
    runProgramStartWindow.Items().Append(winrt::box_value(GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_VISIBILITY_HIDDEN)));
    runProgramStartWindow.SelectedIndex(0);
    controlStackPanel.Children().Append(UIHelpers::GetLabelWrapped(runProgramStartWindow, GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_LABEL_START_AS), runProgramLabelWidth).as<StackPanel>());

    // add events to TextBoxes for runProgram fields.
    runProgramPathInput.TextChanged([parent, row](winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::Controls::TextChangedEventArgs const& e) mutable {
        uint32_t rowIndex = -1;
        if (!parent.Children().IndexOf(row, rowIndex))
        {
            return;
        }
        Shortcut tempShortcut;
        CreateNewTempShortcut(row, tempShortcut, rowIndex);
        ShortcutControl::shortcutRemapBuffer[rowIndex].first[1] = tempShortcut;
    });

    runProgramArgsForProgramInput.TextChanged([parent, row](winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::Controls::TextChangedEventArgs const& e) mutable {
        uint32_t rowIndex = -1;
        if (!parent.Children().IndexOf(row, rowIndex))
        {
            return;
        }

        Shortcut tempShortcut;
        CreateNewTempShortcut(row, tempShortcut, rowIndex);
        ShortcutControl::shortcutRemapBuffer[rowIndex].first[1] = tempShortcut;
    });

    runProgramStartInDirInput.TextChanged([parent, row](winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::Controls::TextChangedEventArgs const& e) mutable {
        uint32_t rowIndex = -1;
        if (!parent.Children().IndexOf(row, rowIndex))
        {
            return;
        }

        if (ShortcutControl::shortcutRemapBuffer.size() <= rowIndex)
        {
            return;
        }

        Shortcut tempShortcut;
        CreateNewTempShortcut(row, tempShortcut, rowIndex);
        ShortcutControl::shortcutRemapBuffer[rowIndex].first[1] = tempShortcut;
    });

    runProgramAlreadyRunningAction.SelectionChanged([parent, row](winrt::Windows::Foundation::IInspectable const&, SelectionChangedEventArgs const&) {
        uint32_t rowIndex;
        if (!parent.Children().IndexOf(row, rowIndex))
        {
            return;
        }

        if (ShortcutControl::shortcutRemapBuffer.size() <= rowIndex)
        {
            return;
        }

        Shortcut tempShortcut;
        CreateNewTempShortcut(static_cast<StackPanel>(row), tempShortcut, rowIndex);
        ShortcutControl::shortcutRemapBuffer[rowIndex].first[1] = tempShortcut;
    });

    runProgramElevationTypeCombo.SelectionChanged([parent, row](winrt::Windows::Foundation::IInspectable const&, SelectionChangedEventArgs const&) {
        uint32_t rowIndex;
        if (!parent.Children().IndexOf(row, rowIndex))
        {
            return;
        }

        if (ShortcutControl::shortcutRemapBuffer.size() <= rowIndex)
        {
            return;
        }
        Shortcut tempShortcut;
        CreateNewTempShortcut(static_cast<StackPanel>(row), tempShortcut, rowIndex);
        ShortcutControl::shortcutRemapBuffer[rowIndex].first[1] = tempShortcut;
    });

    runProgramStartWindow.SelectionChanged([parent, row](winrt::Windows::Foundation::IInspectable const&, SelectionChangedEventArgs const&) {
        uint32_t rowIndex;
        if (!parent.Children().IndexOf(row, rowIndex))
        {
            return;
        }

        if (ShortcutControl::shortcutRemapBuffer.size() <= rowIndex)
        {
            return;
        }

        Shortcut tempShortcut;
        CreateNewTempShortcut(static_cast<StackPanel>(row), tempShortcut, rowIndex);

        ShortcutControl::shortcutRemapBuffer[rowIndex].first[1] = tempShortcut;
    });

    pickFileBtn.Click([&, parent, row](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        Button currentButton = sender.as<Button>();
        uint32_t rowIndex;
        UIElementCollection children = parent.Children();
        bool indexFound = children.IndexOf(row, rowIndex);

        if (!indexFound)
        {
            return;
        }

        OPENFILENAME openFileName;
        TCHAR szFile[260] = { 0 };

        ZeroMemory(&openFileName, sizeof(openFileName));
        openFileName.lStructSize = sizeof(openFileName);
        openFileName.hwndOwner = NULL;
        openFileName.lpstrFile = szFile;
        openFileName.nMaxFile = sizeof(szFile);
        openFileName.lpstrFilter = TEXT("All Files (*.*)\0*.*\0");
        openFileName.nFilterIndex = 1;
        openFileName.lpstrFileTitle = NULL;
        openFileName.nMaxFileTitle = 0;
        openFileName.lpstrInitialDir = NULL;
        openFileName.Flags = OFN_PATHMUSTEXIST | OFN_FILEMUSTEXIST;

        auto runProgramPathInput = row.FindName(L"runProgramPathInput_" + std::to_wstring(rowIndex)).as<TextBox>();

        if (GetOpenFileName(&openFileName) == TRUE)
        {
            runProgramPathInput.Text(szFile);
        }
    });

    pickPathBtn.Click([&, parent, row](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        Button currentButton = sender.as<Button>();
        uint32_t rowIndex;
        UIElementCollection children = parent.Children();
        bool indexFound = children.IndexOf(row, rowIndex);
        if (!indexFound)
        {
            return;
        }

        HRESULT hr = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED | COINIT_DISABLE_OLE1DDE);
        if (!FAILED(hr))
        {
            // Create a buffer to store the selected folder path
            wchar_t path[MAX_PATH];
            ZeroMemory(path, sizeof(path));

            // Initialize the BROWSEINFO structure
            BROWSEINFO browseInfo = { 0 };
            browseInfo.hwndOwner = NULL; // Use NULL if there's no owner window
            browseInfo.pidlRoot = NULL; // Use NULL to start from the desktop
            browseInfo.pszDisplayName = path; // Buffer to store the display name
            browseInfo.lpszTitle = L"Select a folder"; // Title of the dialog
            browseInfo.ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE; // Show only file system directories

            // Show the dialog

            LPITEMIDLIST pidl = SHBrowseForFolder(&browseInfo);
            if (pidl != NULL)
            {
                // Get the selected folder's path
                if (SHGetPathFromIDList(pidl, path))
                {
                    auto runProgramStartInDirInput = row.FindName(L"runProgramStartInDirInput_" + std::to_wstring(rowIndex)).as<TextBox>();
                    runProgramStartInDirInput.Text(path);
                }

                // Free the PIDL
                CoTaskMemFree(pidl);
            }

            // Release COM
            CoUninitialize();

            // Uninitialize COM
            CoUninitialize();
        }
    });

    // this really should not be here, it just works because SelectionChanged is always changed?
    runProgramPathInput.Text(shortCut.runProgramFilePath);
    runProgramArgsForProgramInput.Text(shortCut.runProgramArgs);
    runProgramStartInDirInput.Text(shortCut.runProgramStartInDir);

    runProgramElevationTypeCombo.SelectedIndex(shortCut.elevationLevel);
    runProgramAlreadyRunningAction.SelectedIndex(shortCut.alreadyRunningAction);
    runProgramStartWindow.SelectedIndex(shortCut.startWindowType);

    return controlStackPanel;
}

void CreateNewTempShortcut(StackPanel& row, Shortcut& tempShortcut, const uint32_t& rowIndex)
{
    tempShortcut.operationType = Shortcut::OperationType::RunProgram;
    //tempShortcut.isRunProgram = true;

    auto runProgramPathInput = row.FindName(L"runProgramPathInput_" + std::to_wstring(rowIndex)).as<TextBox>();
    auto runProgramArgsForProgramInput = row.FindName(L"runProgramArgsForProgramInput_" + std::to_wstring(rowIndex)).as<TextBox>();
    auto runProgramStartInDirInput = row.FindName(L"runProgramStartInDirInput_" + std::to_wstring(rowIndex)).as<TextBox>();
    auto runProgramElevationTypeCombo = row.FindName(L"runProgramElevationTypeCombo_" + std::to_wstring(rowIndex)).as<ComboBox>();
    auto runProgramAlreadyRunningAction = row.FindName(L"runProgramAlreadyRunningAction_" + std::to_wstring(rowIndex)).as<ComboBox>();
    auto runProgramStartWindow = row.FindName(L"runProgramStartWindow_" + std::to_wstring(rowIndex)).as<ComboBox>();

    tempShortcut.runProgramFilePath = ShortcutControl::RemoveExtraQuotes(runProgramPathInput.Text().c_str());
    tempShortcut.runProgramArgs = (runProgramArgsForProgramInput.Text().c_str());
    tempShortcut.runProgramStartInDir = (runProgramStartInDirInput.Text().c_str());

    // Assign instead of setting the value in the buffer since the previous value may not be a Shortcut

    tempShortcut.elevationLevel = static_cast<Shortcut::ElevationLevel>(runProgramElevationTypeCombo.SelectedIndex());
    tempShortcut.alreadyRunningAction = static_cast<Shortcut::ProgramAlreadyRunningAction>(runProgramAlreadyRunningAction.SelectedIndex());
    tempShortcut.startWindowType = static_cast<Shortcut::StartWindowType>(runProgramStartWindow.SelectedIndex());
}

std::wstring ShortcutControl::RemoveExtraQuotes(const std::wstring& str)
{
    if (!str.empty() && str.front() == L'"' && str.back() == L'"')
    {
        return str.substr(1, str.size() - 2);
    }

    return str;
}

ShortcutControl::ShortcutType ShortcutControl::GetShortcutType(const winrt::Windows::UI::Xaml::Controls::ComboBox& typeCombo)
{
    if (typeCombo.SelectedIndex() == 0)
    {
        return ShortcutControl::ShortcutType::Shortcut;
    }
    else if (typeCombo.SelectedIndex() == 1)
    {
        return ShortcutControl::ShortcutType::Text;
    }
    else if (typeCombo.SelectedIndex() == 2)
    {
        return ShortcutControl::ShortcutType::RunProgram;
    }
    else
    {
        return ShortcutControl::ShortcutType::OpenURI;
    }
}

// Function to return the stack panel element of the ShortcutControl. This is the externally visible UI element which can be used to add it to other layouts
StackPanel ShortcutControl::GetShortcutControl()
{
    return shortcutControlLayout.as<StackPanel>();
}

// Function to create the detect shortcut UI window
void ShortcutControl::CreateDetectShortcutWindow(winrt::Windows::Foundation::IInspectable const& sender, XamlRoot xamlRoot, KBMEditor::KeyboardManagerState& keyboardManagerState, const int colIndex, StackPanel table, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, StackPanel row, TextBox targetApp, bool isHybridControl, bool isSingleKeyWindow, HWND parentWindow, RemapBuffer& remapBuffer)
{
    // check to see if this orig or map-to shortcut;
    bool isOrigShortcut = (colIndex == 0);

    uint32_t rowIndex;

    UIElementCollection children = table.Children();
    bool indexFound = children.IndexOf(row, rowIndex);

    Shortcut shortcut;

    if (shortcutRemapBuffer.size() > 0)
    {
        if (colIndex == 0)
        {
            shortcut = std::get<Shortcut>(shortcutRemapBuffer[rowIndex].first[0]);
        }
        else
        {
            if (shortcutRemapBuffer[rowIndex].first[1].index() != 1)
            {
                // not a shortcut, let's fix that.
                Shortcut newShortcut;
                shortcutRemapBuffer[rowIndex].first[1] = newShortcut;
            }
            shortcut = std::get<Shortcut>(shortcutRemapBuffer[rowIndex].first[1]);
        }

        if (!shortcut.IsEmpty() && shortcut.HasChord())
        {
            keyboardManagerState.AllowChord = true;
        } else {
            keyboardManagerState.AllowChord = false;
        }
    }

    //remapBuffer[rowIndex].first.

    // ContentDialog for detecting shortcuts. This is the parent UI element.
    ContentDialog detectShortcutBox;
    ToggleSwitch allowChordSwitch;

    // ContentDialog requires manually setting the XamlRoot (https://learn.microsoft.com/uwp/api/windows.ui.xaml.controls.contentdialog#contentdialog-in-appwindow-or-xaml-islands)
    detectShortcutBox.XamlRoot(xamlRoot);
    detectShortcutBox.Title(box_value(GET_RESOURCE_STRING(IDS_TYPESHORTCUT_TITLE)));

    // Get the parent linked stack panel for the "Type shortcut" button that was clicked

    VariableSizedWrapGrid linkedShortcutVariableSizedWrapGrid = UIHelpers::GetSiblingElement(sender.as<FrameworkElement>().Parent()).as<VariableSizedWrapGrid>();

    auto unregisterKeys = [&keyboardManagerState]() {
        keyboardManagerState.ClearRegisteredKeyDelays();
    };

    auto selectDetectedShortcutAndResetKeys = [&keyboardManagerState](DWORD key) {
        keyboardManagerState.SelectDetectedShortcut(key);
        keyboardManagerState.ResetDetectedShortcutKey(key);
    };

    auto onPressEnter = [linkedShortcutVariableSizedWrapGrid,
                         detectShortcutBox,
                         &keyboardManagerState,
                         unregisterKeys,
                         colIndex,
                         table,
                         targetApp,
                         &keyDropDownControlObjects,
                         row,
                         isHybridControl,
                         isSingleKeyWindow,
                         &remapBuffer] {
        // Save the detected shortcut in the linked text block
        Shortcut detectedShortcutKeys = keyboardManagerState.GetDetectedShortcut();

        if (!detectedShortcutKeys.IsEmpty())
        {
            // The shortcut buffer gets set in this function
            KeyDropDownControl::AddShortcutToControl(detectedShortcutKeys, table, linkedShortcutVariableSizedWrapGrid, keyboardManagerState, colIndex, keyDropDownControlObjects, remapBuffer, row, targetApp, isHybridControl, isSingleKeyWindow);
        }
        // Hide the type shortcut UI
        detectShortcutBox.Hide();
    };

    auto onReleaseEnter = [&keyboardManagerState,
                           unregisterKeys,
                           isSingleKeyWindow,
                           parentWindow] {
        // Reset the keyboard manager UI state
        keyboardManagerState.ResetUIState();
        if (isSingleKeyWindow)
        {
            // Revert UI state back to Edit Keyboard window
            keyboardManagerState.SetUIState(KBMEditor::KeyboardManagerUIState::EditKeyboardWindowActivated, parentWindow);
        }
        else
        {
            // Revert UI state back to Edit Shortcut window
            keyboardManagerState.SetUIState(KBMEditor::KeyboardManagerUIState::EditShortcutsWindowActivated, parentWindow);
        }

        unregisterKeys();
    };

    auto onReleaseSpace = [&keyboardManagerState,
                           allowChordSwitch] {

        keyboardManagerState.AllowChord = !keyboardManagerState.AllowChord;
        allowChordSwitch.IsOn(keyboardManagerState.AllowChord);
    };

    auto onAccept = [onPressEnter,
                     onReleaseEnter] {
        onPressEnter();
        onReleaseEnter();
    };

    // OK button
    detectShortcutBox.DefaultButton(ContentDialogButton::Primary);
    detectShortcutBox.PrimaryButtonText(GET_RESOURCE_STRING(IDS_OK_BUTTON));
    detectShortcutBox.PrimaryButtonClick([onAccept](winrt::Windows::Foundation::IInspectable const& sender, ContentDialogButtonClickEventArgs const& args) {
        // Cancel default dialog events
        args.Cancel(true);

        onAccept();
    });

    // NOTE: UnregisterKeys should never be called on the DelayThread, as it will re-enter the mutex. To avoid this it is run on the dispatcher thread
    keyboardManagerState.RegisterKeyDelay(
        VK_RETURN,
        selectDetectedShortcutAndResetKeys,
        [onPressEnter, detectShortcutBox](DWORD) {
            detectShortcutBox.Dispatcher().RunAsync(
                Windows::UI::Core::CoreDispatcherPriority::Normal,
                [onPressEnter] {
                    onPressEnter();
                });
        },
        [onReleaseEnter, detectShortcutBox](DWORD) {
            detectShortcutBox.Dispatcher().RunAsync(
                Windows::UI::Core::CoreDispatcherPriority::Normal,
                [onReleaseEnter]() {
                    onReleaseEnter();
                });
        });

    auto onCancel = [&keyboardManagerState,
                     detectShortcutBox,
                     unregisterKeys,
                     isSingleKeyWindow,
                     parentWindow] {
        detectShortcutBox.Hide();

        // Reset the keyboard manager UI state
        keyboardManagerState.ResetUIState();
        if (isSingleKeyWindow)
        {
            // Revert UI state back to Edit Keyboard window
            keyboardManagerState.SetUIState(KBMEditor::KeyboardManagerUIState::EditKeyboardWindowActivated, parentWindow);
        }
        else
        {
            // Revert UI state back to Edit Shortcut window
            keyboardManagerState.SetUIState(KBMEditor::KeyboardManagerUIState::EditShortcutsWindowActivated, parentWindow);
        }
        unregisterKeys();
    };

    if (isOrigShortcut)
    {
        // Hold space to allow chords. Chords are only available for origin shortcuts.
        keyboardManagerState.RegisterKeyDelay(
            VK_SPACE,
            selectDetectedShortcutAndResetKeys,
            [onReleaseSpace, detectShortcutBox](DWORD) {
                detectShortcutBox.Dispatcher().RunAsync(
                    Windows::UI::Core::CoreDispatcherPriority::Normal,
                    [onReleaseSpace] {
                        onReleaseSpace();
                    });
            },
            nullptr);
    }

    // Cancel button
    detectShortcutBox.CloseButtonText(GET_RESOURCE_STRING(IDS_CANCEL_BUTTON));
    detectShortcutBox.CloseButtonClick([onCancel](winrt::Windows::Foundation::IInspectable const& sender, ContentDialogButtonClickEventArgs const& args) {
        // Cancel default dialog events
        args.Cancel(true);

        onCancel();
    });

    // NOTE: UnregisterKeys should never be called on the DelayThread, as it will re-enter the mutex. To avoid this it is run on the dispatcher thread
    keyboardManagerState.RegisterKeyDelay(
        VK_ESCAPE,
        selectDetectedShortcutAndResetKeys,
        [onCancel, detectShortcutBox](DWORD) {
            detectShortcutBox.Dispatcher().RunAsync(
                Windows::UI::Core::CoreDispatcherPriority::Normal,
                [onCancel] {
                    onCancel();
                });
        },
        nullptr);

    // StackPanel parent for the displayed text in the dialog
    Windows::UI::Xaml::Controls::StackPanel stackPanel;
    detectShortcutBox.Content(stackPanel);

    // Header textblock
    TextBlock text;
    text.Text(GET_RESOURCE_STRING(IDS_TYPESHORTCUT_HEADER));
    text.Margin({ 0, 0, 0, 10 });
    stackPanel.Children().Append(text);

    // Target StackPanel to place the selected key - first row (for 1-3 keys)
    Windows::UI::Xaml::Controls::StackPanel keyStackPanel1;
    keyStackPanel1.Orientation(Orientation::Horizontal);
    stackPanel.Children().Append(keyStackPanel1);

    // Target StackPanel to place the selected key - second row (for 4-5 keys)
    Windows::UI::Xaml::Controls::StackPanel keyStackPanel2;
    keyStackPanel2.Orientation(Orientation::Horizontal);
    keyStackPanel2.Margin({ 0, 20, 0, 0 });
    keyStackPanel2.Visibility(Visibility::Collapsed);
    stackPanel.Children().Append(keyStackPanel2);

    // Detect Chord
    Windows::UI::Xaml::Controls::StackPanel chordStackPanel;

    if (isOrigShortcut)
    {
        constexpr double verticalMargin = 20.f;
        TextBlock allowChordText;
        allowChordText.Text(GET_RESOURCE_STRING(IDS_EDITSHORTCUTS_ALLOW_CHORDS));
        allowChordText.FontSize(12);
        allowChordText.Margin({ 0, 12 + verticalMargin, 0, 0 });
        chordStackPanel.VerticalAlignment(VerticalAlignment::Center);
        allowChordText.TextAlignment(TextAlignment::Center);
        chordStackPanel.Orientation(Orientation::Horizontal);

        allowChordSwitch.OnContent(nullptr);
        allowChordSwitch.OffContent(nullptr);
        allowChordSwitch.Margin({ 12, verticalMargin, 0, 0 });

        chordStackPanel.Children().Append(allowChordText);
        chordStackPanel.Children().Append(allowChordSwitch);

        stackPanel.Children().Append(chordStackPanel);
        allowChordSwitch.IsOn(keyboardManagerState.AllowChord);

        auto toggleHandler = [allowChordSwitch, &keyboardManagerState](auto const& sender, auto const& e) {
            keyboardManagerState.AllowChord = allowChordSwitch.IsOn();

            if (!allowChordSwitch.IsOn())
            {
                keyboardManagerState.ClearStoredShortcut();
            }
        };

        allowChordSwitch.Toggled(toggleHandler);
    }

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

    if (isOrigShortcut)
    {
        // Hold space to allow chords. Chords are only available for origin shortcuts.
        TextBlock holdSpaceInfo;
        holdSpaceInfo.Text(GET_RESOURCE_STRING(IDS_TYPE_HOLDSPACE));
        holdSpaceInfo.FontSize(12);
        holdSpaceInfo.Margin({ 0, 0, 0, 0 });
        stackPanel.Children().Append(holdSpaceInfo);
    }

    try
    {
        // If a layout update has been triggered by other methods (e.g.: adapting to zoom level), this may throw an exception.
        stackPanel.UpdateLayout();
    }
    catch (...)
    {
    }

    // Configure the keyboardManagerState to store the UI information.
    keyboardManagerState.ConfigureDetectShortcutUI(keyStackPanel1, keyStackPanel2);

    // Show the dialog
    detectShortcutBox.ShowAsync();

    if (!shortcut.IsEmpty() && keyboardManagerState.AllowChord)
    {
        keyboardManagerState.SetDetectedShortcut(shortcut);
    }
}
