#include "pch.h"
#include "CppUnitTest.h"
#include <keyboardmanager/ui/BufferValidationHelpers.h>
#include "TestHelpers.h"
#include <common/keyboard_layout.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RemappingUITests
{
    // Tests for methods in the BufferValidationHelpers namespace
    TEST_CLASS (BufferValidationTests)
    {
        std::wstring testApp1 = L"testtrocess1.exe";
        std::wstring testApp2 = L"testprocess2.exe";
        LayoutMap keyboardLayout;

    public:
        TEST_METHOD_INITIALIZE(InitializeTestEnv)
        {
        }

        // Test if the ValidateAndUpdateKeyBufferElement method is successful when setting a key to null in a new row
        TEST_METHOD (ValidateAndUpdateKeyBufferElement_ShouldUpdateAndReturnNoError_OnSettingKeyToNullInANewRow)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Add 2 empty rows
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ NULL, NULL }), std::wstring()));
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ NULL, NULL }), std::wstring()));

            // Validate and update the element when -1 i.e. null selection is made on an empty row.
            KeyboardManagerHelper::ErrorType error = BufferValidationHelpers::ValidateAndUpdateKeyBufferElement(0, 0, -1, keyboardLayout.GetKeyCodeList(false), remapBuffer);

            // Assert that the element is validated and buffer is updated
            Assert::AreEqual(true, error == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual((DWORD)NULL, std::get<DWORD>(remapBuffer[0].first[0]));
            Assert::AreEqual((DWORD)NULL, std::get<DWORD>(remapBuffer[0].first[1]));
            Assert::AreEqual((DWORD)NULL, std::get<DWORD>(remapBuffer[1].first[0]));
            Assert::AreEqual((DWORD)NULL, std::get<DWORD>(remapBuffer[1].first[1]));
        }

        // Test if the ValidateAndUpdateKeyBufferElement method is successful when setting a key to non-null in a new row
        TEST_METHOD (ValidateAndUpdateKeyBufferElement_ShouldUpdateAndReturnNoError_OnSettingKeyToNonNullInANewRow)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Add an empty row
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ NULL, NULL }), std::wstring()));

            // Validate and update the element when selecting B on an empty row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(false);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42));
            KeyboardManagerHelper::ErrorType error = BufferValidationHelpers::ValidateAndUpdateKeyBufferElement(0, 0, (int)index, keyList, remapBuffer);

            // Assert that the element is validated and buffer is updated
            Assert::AreEqual(true, error == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual((DWORD)0x42, std::get<DWORD>(remapBuffer[0].first[0]));
            Assert::AreEqual((DWORD)NULL, std::get<DWORD>(remapBuffer[0].first[1]));
        }

        // Test if the ValidateAndUpdateKeyBufferElement method is successful when setting a key to non-null in a valid key to key
        TEST_METHOD (ValidateAndUpdateKeyBufferElement_ShouldUpdateAndReturnNoError_OnSettingKeyToNonNullInAValidKeyToKeyRow)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Add a row with A as the target
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ NULL, 0x41 }), std::wstring()));

            // Validate and update the element when selecting B on a row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(false);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42));
            KeyboardManagerHelper::ErrorType error = BufferValidationHelpers::ValidateAndUpdateKeyBufferElement(0, 0, (int)index, keyList, remapBuffer);

            // Assert that the element is validated and buffer is updated
            Assert::AreEqual(true, error == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual((DWORD)0x42, std::get<DWORD>(remapBuffer[0].first[0]));
            Assert::AreEqual((DWORD)0x41, std::get<DWORD>(remapBuffer[0].first[1]));
        }

        // Test if the ValidateAndUpdateKeyBufferElement method is successful when setting a key to non-null in a valid key to shortcut
        TEST_METHOD (ValidateAndUpdateKeyBufferElement_ShouldUpdateAndReturnNoError_OnSettingKeyToNonNullInAValidKeyToShortcutRow)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Add a row with Ctrl+A as the target
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x41);
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ NULL, dest }), std::wstring()));

            // Validate and update the element when selecting B on a row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(false);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42));
            KeyboardManagerHelper::ErrorType error = BufferValidationHelpers::ValidateAndUpdateKeyBufferElement(0, 0, (int)index, keyList, remapBuffer);

            // Assert that the element is validated and buffer is updated
            Assert::AreEqual(true, error == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual((DWORD)0x42, std::get<DWORD>(remapBuffer[0].first[0]));
            Assert::AreEqual(true, dest == std::get<Shortcut>(remapBuffer[0].first[1]));
        }

        // Test if the ValidateAndUpdateKeyBufferElement method is unsuccessful when setting first column to the same value as the right column
        TEST_METHOD (ValidateAndUpdateKeyBufferElement_ShouldReturnMapToSameKeyError_OnSettingFirstColumnToSameValueAsRightColumn)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Add a row with A as the target
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ NULL, 0x41 }), std::wstring()));

            // Validate and update the element when selecting A on a row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(false);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x41));
            KeyboardManagerHelper::ErrorType error = BufferValidationHelpers::ValidateAndUpdateKeyBufferElement(0, 0, (int)index, keyList, remapBuffer);

            // Assert that the element is invalid and buffer is not updated
            Assert::AreEqual(true, error == KeyboardManagerHelper::ErrorType::MapToSameKey);
            Assert::AreEqual((DWORD)NULL, std::get<DWORD>(remapBuffer[0].first[0]));
            Assert::AreEqual((DWORD)0x41, std::get<DWORD>(remapBuffer[0].first[1]));
        }

        // Test if the ValidateAndUpdateKeyBufferElement method is unsuccessful when setting first column of a key to key row to the same value as in another row
        TEST_METHOD (ValidateAndUpdateKeyBufferElement_ShouldReturnSameKeyPreviouslyMappedError_OnSettingFirstColumnOfAKeyToKeyRowToSameValueAsInAnotherRow)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Add a row from A->B and a row with C as target
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ 0x41, 0x42 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ NULL, 0x43 }), std::wstring()));

            // Validate and update the element when selecting A on second row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(false);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x41));
            KeyboardManagerHelper::ErrorType error = BufferValidationHelpers::ValidateAndUpdateKeyBufferElement(1, 0, (int)index, keyList, remapBuffer);

            // Assert that the element is invalid and buffer is not updated
            Assert::AreEqual(true, error == KeyboardManagerHelper::ErrorType::SameKeyPreviouslyMapped);
            Assert::AreEqual((DWORD)NULL, std::get<DWORD>(remapBuffer[1].first[0]));
            Assert::AreEqual((DWORD)0x43, std::get<DWORD>(remapBuffer[1].first[1]));
        }

        // Test if the ValidateAndUpdateKeyBufferElement method is unsuccessful when setting first column of a key to shortcut row to the same value as in another row
        TEST_METHOD (ValidateAndUpdateKeyBufferElement_ShouldReturnSameKeyPreviouslyMappedError_OnSettingFirstColumnOfAKeyToShortcutRowToSameValueAsInAnotherRow)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Add a row from A->B and a row with Ctrl+A as target
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ 0x41, 0x42 }), std::wstring()));
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x41);
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ NULL, dest }), std::wstring()));

            // Validate and update the element when selecting A on second row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(false);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x41));
            KeyboardManagerHelper::ErrorType error = BufferValidationHelpers::ValidateAndUpdateKeyBufferElement(1, 0, (int)index, keyList, remapBuffer);

            // Assert that the element is invalid and buffer is not updated
            Assert::AreEqual(true, error == KeyboardManagerHelper::ErrorType::SameKeyPreviouslyMapped);
            Assert::AreEqual((DWORD)NULL, std::get<DWORD>(remapBuffer[1].first[0]));
            Assert::AreEqual(true, dest == std::get<Shortcut>(remapBuffer[1].first[1]));
        }

        // Test if the ValidateAndUpdateKeyBufferElement method is unsuccessful when setting first column of a key to key row to a conflicting modifier with another row
        TEST_METHOD (ValidateAndUpdateKeyBufferElement_ShouldReturnConflictingModifierKeyError_OnSettingFirstColumnOfAKeyToKeyRowToConflictingModifierWithAnotherRow)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Add a row from Ctrl->B and a row with C as target
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ VK_CONTROL, 0x42 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ NULL, 0x43 }), std::wstring()));

            // Validate and update the element when selecting LCtrl on second row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(false);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL));
            KeyboardManagerHelper::ErrorType error = BufferValidationHelpers::ValidateAndUpdateKeyBufferElement(1, 0, (int)index, keyList, remapBuffer);

            // Assert that the element is invalid and buffer is not updated
            Assert::AreEqual(true, error == KeyboardManagerHelper::ErrorType::ConflictingModifierKey);
            Assert::AreEqual((DWORD)NULL, std::get<DWORD>(remapBuffer[1].first[0]));
            Assert::AreEqual((DWORD)0x43, std::get<DWORD>(remapBuffer[1].first[1]));

            // Change first row to LCtrl->B
            remapBuffer[0].first[0] = VK_LCONTROL;

            // Select Ctrl
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL));
            error = BufferValidationHelpers::ValidateAndUpdateKeyBufferElement(1, 0, (int)index, keyList, remapBuffer);

            // Assert that the element is invalid and buffer is not updated
            Assert::AreEqual(true, error == KeyboardManagerHelper::ErrorType::ConflictingModifierKey);
            Assert::AreEqual((DWORD)NULL, std::get<DWORD>(remapBuffer[1].first[0]));
            Assert::AreEqual((DWORD)0x43, std::get<DWORD>(remapBuffer[1].first[1]));
        }

        // Test if the ValidateAndUpdateKeyBufferElement method is unsuccessful when setting first column of a key to shortcut row to a conflicting modifier with another row
        TEST_METHOD (ValidateAndUpdateKeyBufferElement_ShouldReturnConflictingModifierKeyError_OnSettingFirstColumnOfAKeyToShortcutRowToConflictingModifierWithAnotherRow)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Add a row from Ctrl->B and a row with Ctrl+A as target
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ VK_CONTROL, 0x42 }), std::wstring()));
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x41);
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ NULL, dest }), std::wstring()));

            // Validate and update the element when selecting LCtrl on second row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(false);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL));
            KeyboardManagerHelper::ErrorType error = BufferValidationHelpers::ValidateAndUpdateKeyBufferElement(1, 0, (int)index, keyList, remapBuffer);

            // Assert that the element is invalid and buffer is not updated
            Assert::AreEqual(true, error == KeyboardManagerHelper::ErrorType::ConflictingModifierKey);
            Assert::AreEqual((DWORD)NULL, std::get<DWORD>(remapBuffer[1].first[0]));
            Assert::AreEqual(true, dest == std::get<Shortcut>(remapBuffer[1].first[1]));

            // Change first row to LCtrl->B
            remapBuffer[0].first[0] = VK_LCONTROL;

            // Select Ctrl
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL));
            error = BufferValidationHelpers::ValidateAndUpdateKeyBufferElement(1, 0, (int)index, keyList, remapBuffer);

            // Assert that the element is invalid and buffer is not updated
            Assert::AreEqual(true, error == KeyboardManagerHelper::ErrorType::ConflictingModifierKey);
            Assert::AreEqual((DWORD)NULL, std::get<DWORD>(remapBuffer[1].first[0]));
            Assert::AreEqual(true, dest == std::get<Shortcut>(remapBuffer[1].first[1]));
        }

        // Test if the ValidateShortcutBufferElement method is successful and no drop down action is required on setting a column to null in a new row
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoErrorAndNoAction_OnSettingColumnToNullInANewRow)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Add empty rows
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ Shortcut(), Shortcut() }), std::wstring()));
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ Shortcut(), NULL }), std::wstring()));

            // Case 1: Validate the element when making null-selection (-1 index) on first column of empty shortcut to shortcut row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(false);
            std::vector<int32_t> selectedIndices = std::vector<int32_t>({ -1 });
            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 0, true, -1, selectedIndices, std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 2: Validate the element when making null-selection (-1 index) on first column of empty shortcut to key row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 0, true, -1, selectedIndices, std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 3: Validate the element when making null-selection (-1 index) on second column of empty shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 0, true, -1, selectedIndices, std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 4: Validate the element when making null-selection (-1 index) on second column of empty shortcut to key row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 0, true, -1, selectedIndices, std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutStartWithModifier error and no drop down action is required on setting first drop down to an action key on a non-hybrid control column
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutStartWithModifierErrorAndNoAction_OnSettingFirstDropDownToActionKeyOnANonHybridColumn)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Add empty rows and Ctrl+C->Ctrl+A
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x43);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x41);
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ Shortcut(), Shortcut() }), std::wstring()));
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ Shortcut(), NULL }), std::wstring()));
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ src, dest }), std::wstring()));

            // Case 1: Validate the element when selecting A on first dropdown of first column of empty shortcut to shortcut row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(false);
            std::vector<int32_t> selectedIndices = std::vector<int32_t>({ -1 });
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x41));
            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 0, true, (int)index, selectedIndices, std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutStartWithModifier);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 2: Validate the element when selecting A on first dropdown of first column of empty shortcut to key row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 0, true, (int)index, selectedIndices, std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutStartWithModifier);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 3: Validate the element when selecting A on first dropdown of second column of empty shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 0, true, (int)index, selectedIndices, std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutStartWithModifier);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 4: Validate the element when selecting A on first dropdown of first column of valid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutStartWithModifier);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 5: Validate the element when selecting A on first dropdown of second column of valid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x41)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutStartWithModifier);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
        }

        // Test if the ValidateShortcutBufferElement method returns no error and no drop down action is required on setting first drop down to an action key on an empty hybrid control column
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoErrorAndNoAction_OnSettingFirstDropDownToActionKeyOnAnEmptyHybridColumn)
        {
            // Or LAST?
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Add empty rows and Ctrl+C->Ctrl+A
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x43);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x41);
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ Shortcut(), Shortcut() }), std::wstring()));
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ Shortcut(), NULL }), std::wstring()));

            // Case 1: Validate the element when selecting A on first dropdown of second column of empty shortcut to shortcut row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(false);
            std::vector<int32_t> selectedIndices = std::vector<int32_t>({ -1 });
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x41));            
            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 0, true, (int)index, selectedIndices, std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 2: Validate the element when selecting A on first dropdown of second column of empty shortcut to key row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 0, true, (int)index, selectedIndices, std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
        }
        
        // Test if the ValidateShortcutBufferElement method returns ShortcutNotMoreThanOneActionKey error and no drop down action is required on setting first drop down to an action key on ahybrid control column with full shortcut 
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutNotMoreThanOneActionKeyAndNoAction_OnSettingNonLastDropDownToActionKeyOnAHybridColumnWithFullShortcut)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Ctrl+C and Ctrl+Shift+B on right column
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x43);
            Shortcut dest2;
            dest2.SetKey(VK_CONTROL);
            dest2.SetKey(VK_SHIFT);
            dest2.SetKey(0x42);
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ Shortcut(), dest1 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ Shortcut(), dest2 }), std::wstring()));

            // Case 1: Validate the element when selecting A on first dropdown of second column of hybrid shortcut to shortcut row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(false);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x41));
            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutNotMoreThanOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 2: Validate the element when selecting A on second dropdown of second column of hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutNotMoreThanOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
        }
    };
}
