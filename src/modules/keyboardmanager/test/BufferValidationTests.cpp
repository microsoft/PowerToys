#include "pch.h"
#include "CppUnitTest.h"
#include <keyboardmanager/ui/BufferValidationHelpers.h>
#include "TestHelpers.h"
#include <common/keyboard_layout.h>
#include <common/shared_constants.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RemappingUITests
{
    // Tests for methods in the BufferValidationHelpers namespace
    TEST_CLASS (BufferValidationTests)
    {
        std::wstring testApp1 = L"testprocess1.exe";
        std::wstring testApp2 = L"testprocess2.exe";
        LayoutMap keyboardLayout;

    public:
        TEST_METHOD_INITIALIZE(InitializeTestEnv)
        {
        }

        // Test if the ValidateAndUpdateKeyBufferElement method is successful when setting a key to null in a new row
        TEST_METHOD (ValidateAndUpdateKeyBufferElement_ShouldUpdateAndReturnNoError_OnSettingKeyToNullInANewRow)
        {
            RemapBuffer remapBuffer;

            // Add 2 empty rows
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ NULL, NULL }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ NULL, NULL }), std::wstring()));

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
            RemapBuffer remapBuffer;

            // Add an empty row
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ NULL, NULL }), std::wstring()));

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
            RemapBuffer remapBuffer;

            // Add a row with A as the target
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ NULL, 0x41 }), std::wstring()));

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
            RemapBuffer remapBuffer;

            // Add a row with Ctrl+A as the target
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x41);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ NULL, dest }), std::wstring()));

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
            RemapBuffer remapBuffer;

            // Add a row with A as the target
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ NULL, 0x41 }), std::wstring()));

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
            RemapBuffer remapBuffer;

            // Add a row from A->B and a row with C as target
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ 0x41, 0x42 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ NULL, 0x43 }), std::wstring()));

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
            RemapBuffer remapBuffer;

            // Add a row from A->B and a row with Ctrl+A as target
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ 0x41, 0x42 }), std::wstring()));
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x41);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ NULL, dest }), std::wstring()));

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
            RemapBuffer remapBuffer;

            // Add a row from Ctrl->B and a row with C as target
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ VK_CONTROL, 0x42 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ NULL, 0x43 }), std::wstring()));

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
            RemapBuffer remapBuffer;

            // Add a row from Ctrl->B and a row with Ctrl+A as target
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ VK_CONTROL, 0x42 }), std::wstring()));
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x41);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ NULL, dest }), std::wstring()));

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

        // Test if the ValidateShortcutBufferElement method is successful and no drop down action is required on setting a column to null in a new or valid row
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoErrorAndNoAction_OnSettingColumnToNullInANewOrValidRow)
        {
            RemapBuffer remapBuffer;

            // Add empty rows
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), Shortcut() }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), NULL }), std::wstring()));
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            src1.SetKey(0x43);
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x41);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ src1, dest1 }), std::wstring()));
            Shortcut src2;
            src2.SetKey(VK_CONTROL);
            src2.SetKey(VK_SHIFT);
            src2.SetKey(0x44);
            Shortcut dest2;
            dest2.SetKey(VK_CONTROL);
            dest2.SetKey(VK_SHIFT);
            dest2.SetKey(0x42);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ src2, dest2 }), std::wstring()));

            // Case 1: Validate the element when making null-selection (-1 index) on first column of empty shortcut to shortcut row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
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

            // Case 5: Validate the element when making null-selection (-1 index) on first dropdown of first column of valid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 0, true, -1, std::vector<int32_t>({ -1, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 6: Validate the element when making null-selection (-1 index) on first dropdown of second column of valid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 1, 0, true, -1, std::vector<int32_t>({ -1, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x41)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 7: Validate the element when making null-selection (-1 index) on first dropdown of second column of valid hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 1, 0, true, -1, std::vector<int32_t>({ -1, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x41)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 8: Validate the element when making null-selection (-1 index) on second dropdown of first column of valid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 1, true, -1, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), -1 }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 9: Validate the element when making null-selection (-1 index) on second dropdown of second column of valid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 1, 1, true, -1, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), -1 }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 10: Validate the element when making null-selection (-1 index) on second dropdown of second column of valid hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 1, 1, true, -1, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), -1 }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 11: Validate the element when making null-selection (-1 index) on first dropdown of first column of valid 3 key shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 0, 0, true, -1, std::vector<int32_t>({ -1, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x44)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 12: Validate the element when making null-selection (-1 index) on first dropdown of second column of valid 3 key shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 0, true, -1, std::vector<int32_t>({ -1, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 13: Validate the element when making null-selection (-1 index) on first dropdown of second column of valid 3 key hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 0, true, -1, std::vector<int32_t>({ -1, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 14: Validate the element when making null-selection (-1 index) on second dropdown of first column of valid 3 key shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 0, 1, true, -1, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), -1, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x44)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 15: Validate the element when making null-selection (-1 index) on second dropdown of second column of valid 3 key shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 1, true, -1, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), -1, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 16: Validate the element when making null-selection (-1 index) on second dropdown of second column of valid 3 key hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 1, true, -1, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), -1, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 17: Validate the element when making null-selection (-1 index) on third dropdown of first column of valid 3 key shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 0, 2, true, -1, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), -1 }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 18: Validate the element when making null-selection (-1 index) on third dropdown of second column of valid 3 key shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 2, true, -1, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), -1 }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 19: Validate the element when making null-selection (-1 index) on third dropdown of second column of valid 3 key hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 2, true, -1, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), -1 }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutStartWithModifier error and no drop down action is required on setting first drop down to an action key on a non-hybrid control column
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutStartWithModifierErrorAndNoAction_OnSettingFirstDropDownToActionKeyOnANonHybridColumn)
        {
            RemapBuffer remapBuffer;

            // Add empty rows and Ctrl+C->Ctrl+A
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x43);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x41);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), Shortcut() }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), NULL }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ src, dest }), std::wstring()));

            // Case 1: Validate the element when selecting A on first dropdown of first column of empty shortcut to shortcut row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x41));
            std::vector<int32_t> selectedIndices = std::vector<int32_t>({ (int32_t)index });
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
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutStartWithModifier);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 5: Validate the element when selecting A on first dropdown of second column of valid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x41)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutStartWithModifier);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
        }

        // Test if the ValidateShortcutBufferElement method returns no error and no drop down action is required on setting first drop down to an action key on an empty hybrid control column
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoErrorAndNoAction_OnSettingFirstDropDownToActionKeyOnAnEmptyHybridColumn)
        {
            RemapBuffer remapBuffer;

            // Add empty rows and Ctrl+C->Ctrl+A
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x43);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x41);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), Shortcut() }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), NULL }), std::wstring()));

            // Case 1: Validate the element when selecting A on first dropdown of second column of empty shortcut to shortcut row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x41));
            std::vector<int32_t> selectedIndices = std::vector<int32_t>({ (int32_t)index });
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

        // Test if the ValidateShortcutBufferElement method returns ShortcutNotMoreThanOneActionKey error and no drop down action is required on setting first drop down to an action key on a hybrid control column with full shortcut
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutNotMoreThanOneActionKeyAndNoAction_OnSettingNonLastDropDownToActionKeyOnAHybridColumnWithFullShortcut)
        {
            RemapBuffer remapBuffer;

            // Ctrl+C and Ctrl+Shift+B on right column
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x43);
            Shortcut dest2;
            dest2.SetKey(VK_CONTROL);
            dest2.SetKey(VK_SHIFT);
            dest2.SetKey(0x42);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), dest1 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), dest2 }), std::wstring()));

            // Case 1: Validate the element when selecting A on first dropdown of second column of hybrid shortcut to shortcut row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x41));
            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutNotMoreThanOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 2: Validate the element when selecting A on second dropdown of second column of hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutNotMoreThanOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutNotMoreThanOneActionKey error and no drop down action is required on setting non first non last drop down to an action key on a non hybrid control column with full shortcut
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutNotMoreThanOneActionKeyAndNoAction_OnSettingNonFirstNonLastDropDownToActionKeyOnANonHybridColumnWithFullShortcut)
        {
            RemapBuffer remapBuffer;

            // Ctrl+Shift+C on left column, Ctrl+Shift+B on right column
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(VK_SHIFT);
            src.SetKey(0x43);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(VK_SHIFT);
            dest.SetKey(0x42);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ src, dest }), std::wstring()));

            // Case 1: Validate the element when selecting A on second dropdown of first column of shortcut to shortcut row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x41));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutNotMoreThanOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 2: Validate the element when selecting A on second dropdown of second column of shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutNotMoreThanOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
        }

        // Test if the ValidateShortcutBufferElement method returns no error and no drop down action is required on setting last drop down to an action key on a column with atleast two drop downs
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoErrorAndNoAction_OnSettingLastDropDownToActionKeyOnAColumnWithAtleastTwoDropDowns)
        {
            RemapBuffer remapBuffer;

            // Ctrl+Shift+C on left column, Ctrl+Shift+B on right column
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            src1.SetKey(VK_SHIFT);
            src1.SetKey(0x43);
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(VK_SHIFT);
            dest1.SetKey(0x42);
            Shortcut src2;
            src2.SetKey(VK_CONTROL);
            src2.SetKey(0x43);
            Shortcut dest2;
            dest2.SetKey(VK_CONTROL);
            dest2.SetKey(0x42);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ src1, dest1 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ src2, dest2 }), std::wstring()));

            // Case 1: Validate the element when selecting A on last dropdown of first column of three key shortcut to shortcut row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x41));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 2: Validate the element when selecting A on second dropdown of second column of three key shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 3: Validate the element when selecting A on second dropdown of hybrid second column of three key shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 4: Validate the element when selecting A on last dropdown of first column of two key shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 2: Validate the element when selecting A on second dropdown of second column of two key shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 3: Validate the element when selecting A on second dropdown of hybrid second column of two key shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
        }

        // Test if the ValidateShortcutBufferElement method returns no error and ClearUnusedDropDowns action is required on setting non first drop down to an action key on a column if all the drop downs after it are empty
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoErrorAndClearUnusedDropDownsAction_OnSettingNonFirstDropDownToActionKeyOnAColumnIfAllTheDropDownsAfterItAreEmpty)
        {
            RemapBuffer remapBuffer;

            // Ctrl+C on left column, Ctrl+B on right column
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x43);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x42);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ src, dest }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), Shortcut() }), std::wstring()));

            // Case 1: Validate the element when selecting A on second dropdown of first column of 3 dropdown shortcut to shortcut row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x41));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::ClearUnusedDropDowns);

            // Case 2: Validate the element when selecting A on second dropdown of second column of 3 dropdown shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::ClearUnusedDropDowns);

            // Case 3: Validate the element when selecting A on second dropdown of second column of 3 dropdown hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::ClearUnusedDropDowns);

            // Case 4: Validate the element when selecting A on second dropdown of first column of empty 3 dropdown shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 1, true, (int)index, std::vector<int32_t>({ -1, (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::ClearUnusedDropDowns);

            // Case 2: Validate the element when selecting A on second dropdown of second column of empty 3 dropdown shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ -1, (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::ClearUnusedDropDowns);

            // Case 3: Validate the element when selecting A on second dropdown of second column of empty 3 dropdown hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ -1, (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::ClearUnusedDropDowns);
        }

        // Test if the ValidateShortcutBufferElement method returns no error and ClearUnusedDropDowns action is required on setting first drop down to an action key on a hybrid column if all the drop downs after it are empty
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoErrorAndClearUnusedDropDownsAction_OnSettingFirstDropDownToActionKeyOnAHybridColumnIfAllTheDropDownsAfterItAreEmpty)
        {
            RemapBuffer remapBuffer;

            // empty row
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), Shortcut() }), std::wstring()));

            // Case 1: Validate the element when selecting A on first dropdown of second column of empty 3 dropdown hybrid shortcut to shortcut row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x41));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, -1, -1 }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no drop down action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::ClearUnusedDropDowns);
        }

        // Test if the ValidateShortcutBufferElement method returns no error and AddDropDown action is required on setting last drop down to a non-repeated modifier key on a column there are less than 3 drop downs
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoErrorAndAddDropDownAction_OnSettingLastDropDownToNonRepeatedModifierKeyOnAColumnIfThereAreLessThan3DropDowns)
        {
            RemapBuffer remapBuffer;

            // Ctrl+C on left column, Ctrl+B on right column
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x43);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x42);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ src, dest }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), Shortcut() }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), 0x44 }), std::wstring()));

            // Case 1: Validate the element when selecting A on second dropdown of first column of 2 dropdown shortcut to shortcut row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and AddDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::AddDropDown);

            // Case 2: Validate the element when selecting Shift on second dropdown of second column of 2 dropdown shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and AddDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::AddDropDown);

            // Case 3: Validate the element when selecting Shift on second dropdown of second column of 2 dropdown hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and AddDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::AddDropDown);

            // Case 4: Validate the element when selecting Shift on first dropdown of first column of 1 dropdown shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and AddDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::AddDropDown);

            // Case 5: Validate the element when selecting Shift on first dropdown of second column of 1 dropdown shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and AddDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::AddDropDown);

            // Case 6: Validate the element when selecting Shift on first dropdown of second column of 1 dropdown hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and AddDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::AddDropDown);

            // Case 7: Validate the element when selecting Shift on first dropdown of second column of 1 dropdown hybrid shortcut to key row with an action key selected
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and AddDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::AddDropDown);
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutCannotHaveRepeatedModifier error and no action is required on setting last drop down to a repeated modifier key on a column there are less than 3 drop downs
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutCannotHaveRepeatedModifierErrorAndNoAction_OnSettingLastDropDownToRepeatedModifierKeyOnAColumnIfThereAreLessThan3DropDowns)
        {
            RemapBuffer remapBuffer;

            // Ctrl+C on left column, Ctrl+B on right column
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x43);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x42);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ src, dest }), std::wstring()));

            // Case 1: Validate the element when selecting Ctrl on second dropdown of first column of 2 dropdown shortcut to shortcut row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutCannotHaveRepeatedModifier);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 2: Validate the element when selecting Ctrl on second dropdown of second column of 2 dropdown shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutCannotHaveRepeatedModifier);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 3: Validate the element when selecting Ctrl on second dropdown of second column of 2 dropdown hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutCannotHaveRepeatedModifier);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 4: Validate the element when selecting LCtrl on second dropdown of first column of 2 dropdown shortcut to shortcut row
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL));
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutCannotHaveRepeatedModifier);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 5: Validate the element when selecting LCtrl on second dropdown of second column of 2 dropdown shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutCannotHaveRepeatedModifier);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 6: Validate the element when selecting LCtrl on second dropdown of second column of 2 dropdown hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutCannotHaveRepeatedModifier);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutMaxShortcutSizeOneActionKey error and no action is required on setting last drop down to a non repeated modifier key on a column there 3 or more drop downs
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutMaxShortcutSizeOneActionKeyErrorAndNoAction_OnSettingLastDropDownToNonRepeatedModifierKeyOnAColumnIfThereAre3OrMoreDropDowns)
        {
            RemapBuffer remapBuffer;

            // Ctrl+C on left column, Ctrl+B on right column
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            src1.SetKey(0x43);
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x42);
            Shortcut src2;
            src2.SetKey(VK_CONTROL);
            src2.SetKey(VK_MENU);
            src2.SetKey(0x43);
            Shortcut dest2;
            dest2.SetKey(VK_CONTROL);
            dest2.SetKey(VK_MENU);
            dest2.SetKey(0x42);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ src1, dest1 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), Shortcut() }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), 0x44 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ src2, dest2 }), std::wstring()));

            // Case 1: Validate the element when selecting A on second dropdown of first column of 3 dropdown shortcut to shortcut row with middle empty
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 2: Validate the element when selecting Shift on second dropdown of second column of 3 dropdown shortcut to shortcut row with middle empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 3: Validate the element when selecting Shift on second dropdown of second column of 3 dropdown hybrid shortcut to shortcut row with middle empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 4: Validate the element when selecting Shift on second dropdown of first column of 3 dropdown shortcut to shortcut row with first empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 2, true, (int)index, std::vector<int32_t>({ -1, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 5: Validate the element when selecting Shift on second dropdown of second column of 3 dropdown shortcut to shortcut row with first empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 2, true, (int)index, std::vector<int32_t>({ -1, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 6: Validate the element when selecting Shift on second dropdown of second column of 3 dropdown hybrid shortcut to shortcut row with first empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 2, true, (int)index, std::vector<int32_t>({ -1, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 7: Validate the element when selecting Shift on first dropdown of first column of 3 dropdown shortcut to shortcut row with first two empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 2, true, (int)index, std::vector<int32_t>({ -1, -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 8: Validate the element when selecting Shift on first dropdown of second column of 3 dropdown shortcut to shortcut row with first two empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 2, true, (int)index, std::vector<int32_t>({ -1, -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 9: Validate the element when selecting Shift on first dropdown of second column of 3 dropdown hybrid shortcut to shortcut row with first two empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 2, true, (int)index, std::vector<int32_t>({ -1, -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 10: Validate the element when selecting Shift on first dropdown of second column of 3 dropdown hybrid shortcut to key row with an action key selected and with first two empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 1, 2, true, (int)index, std::vector<int32_t>({ -1, -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 11: Validate the element when selecting Shift on second dropdown of first column of 3 dropdown shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 12: Validate the element when selecting Shift on second dropdown of second column of 3 dropdown shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 13: Validate the element when selecting Shift on second dropdown of second column of 3 dropdown hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutMaxShortcutSizeOneActionKey error and no action is required on setting last drop down to a repeated modifier key on a column there 3 or more drop downs
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutMaxShortcutSizeOneActionKeyErrorAndNoAction_OnSettingLastDropDownToRepeatedModifierKeyOnAColumnIfThereAre3OrMoreDropDowns)
        {
            RemapBuffer remapBuffer;

            // Ctrl+C on left column, Ctrl+B on right column
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            src1.SetKey(0x43);
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x42);
            Shortcut src2;
            src2.SetKey(VK_CONTROL);
            src2.SetKey(VK_MENU);
            src2.SetKey(0x43);
            Shortcut dest2;
            dest2.SetKey(VK_CONTROL);
            dest2.SetKey(VK_MENU);
            dest2.SetKey(0x42);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ src1, dest1 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ src2, dest2 }), std::wstring()));

            // Case 1: Validate the element when selecting Ctrl on second dropdown of first column of 3 dropdown shortcut to shortcut row with middle empty
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 2: Validate the element when selecting Ctrl on second dropdown of second column of 3 dropdown shortcut to shortcut row with middle empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 3: Validate the element when selecting Ctrl on second dropdown of second column of 3 dropdown hybrid shortcut to shortcut row with middle empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 4: Validate the element when selecting Ctrl on second dropdown of first column of 3 dropdown shortcut to shortcut row with first empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 2, true, (int)index, std::vector<int32_t>({ -1, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 5: Validate the element when selecting Ctrl on second dropdown of second column of 3 dropdown shortcut to shortcut row with first empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 2, true, (int)index, std::vector<int32_t>({ -1, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 6: Validate the element when selecting Ctrl on second dropdown of second column of 3 dropdown hybrid shortcut to shortcut row with first empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 2, true, (int)index, std::vector<int32_t>({ -1, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 7: Validate the element when selecting Ctrl on third dropdown of first column of 3 dropdown shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 8: Validate the element when selecting Ctrl on third dropdown of second column of 3 dropdown shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 9: Validate the element when selecting Ctrl on third dropdown of second column of 3 dropdown hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
        }

        // Test if the ValidateShortcutBufferElement method returns no error and no action is required on setting non-last drop down to a non repeated modifier key on a column
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoErrorAndNoAction_OnSettingNonLastDropDownToNonRepeatedModifierKeyOnAColumn)
        {
            RemapBuffer remapBuffer;

            // Ctrl+C on left column, Ctrl+B on right column
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            src1.SetKey(0x43);
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x42);
            Shortcut src2;
            src2.SetKey(VK_CONTROL);
            src2.SetKey(VK_MENU);
            src2.SetKey(0x43);
            Shortcut dest2;
            dest2.SetKey(VK_CONTROL);
            dest2.SetKey(VK_MENU);
            dest2.SetKey(0x42);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ src1, dest1 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ src2, dest2 }), std::wstring()));

            // Case 1: Validate the element when selecting Shift on first dropdown of first column of 2 dropdown shortcut to shortcut
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 2: Validate the element when selecting Shift on first dropdown of second column of 2 dropdown shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 3: Validate the element when selecting Shift on first dropdown of second column of 2 dropdown hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 4: Validate the element when selecting Shift on first dropdown of first column of 3 dropdown shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 5: Validate the element when selecting Shift on first dropdown of second column of 3 dropdown shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 6: Validate the element when selecting Shift on first dropdown of second column of 3 dropdown hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 7: Validate the element when selecting Shift on second dropdown of first column of 3 dropdown shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 8: Validate the element when selecting Shift on second dropdown of second column of 3 dropdown shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 9: Validate the element when selecting Shift on second dropdown of second column of 3 dropdown hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutCannotHaveRepeatedModifier error and no action is required on setting non-last drop down to a repeated modifier key on a column
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutCannotHaveRepeatedModifierErrorAndNoAction_OnSettingNonLastDropDownToRepeatedModifierKeyOnAColumn)
        {
            RemapBuffer remapBuffer;

            // Ctrl+C on left column, Ctrl+B on right column
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            src1.SetKey(0x43);
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x42);
            Shortcut src2;
            src2.SetKey(VK_CONTROL);
            src2.SetKey(VK_MENU);
            src2.SetKey(0x43);
            Shortcut dest2;
            dest2.SetKey(VK_CONTROL);
            dest2.SetKey(VK_MENU);
            dest2.SetKey(0x42);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ src2, dest2 }), std::wstring()));

            // Case 1: Validate the element when selecting Shift on first dropdown of first column of 3 dropdown shortcut to shortcut row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutCannotHaveRepeatedModifier);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 2: Validate the element when selecting Alt on first dropdown of second column of 3 dropdown shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutCannotHaveRepeatedModifier);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 3: Validate the element when selecting Alt on first dropdown of second column of 3 dropdown hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutCannotHaveRepeatedModifier);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL));

            // Case 4: Validate the element when selecting Alt on second dropdown of first column of 3 dropdown shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutCannotHaveRepeatedModifier);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 5: Validate the element when selecting Ctrl on second dropdown of second column of 3 dropdown shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutCannotHaveRepeatedModifier);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 6: Validate the element when selecting Ctrl on second dropdown of second column of 3 dropdown hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutCannotHaveRepeatedModifier);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutStartWithModifier error and no action is required on setting first drop down to None on a non-hybrid column with one drop down
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutStartWithModifierErrorAndNoAction_OnSettingFirstDropDownToNoneOnNonHybridColumnWithOneDropDown)
        {
            RemapBuffer remapBuffer;

            // empty row
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), Shortcut() }), std::wstring()));

            // Case 1: Validate the element when selecting None on first dropdown of first column of 1 dropdown shortcut to shortcut row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = 0;

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutStartWithModifier);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 2: Validate the element when selecting None on first dropdown of second column of 1 dropdown shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutStartWithModifier);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutOneActionKey error and no action is required on setting first drop down to None on a hybrid column with one drop down
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutOneActionKeyErrorAndNoAction_OnSettingFirstDropDownToNoneOnHybridColumnWithOneDropDown)
        {
            RemapBuffer remapBuffer;

            // empty row
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), Shortcut() }), std::wstring()));

            // Case 1: Validate the element when selecting None on first dropdown of first column of 1 dropdown hybrid shortcut to shortcut row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = 0;

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutAtleast2Keys error and no action is required on setting first drop down to None on a non-hybrid column with two drop down
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutAtleast2KeysAndNoAction_OnSettingFirstDropDownToNoneOnNonHybridColumnWithTwoDropDowns)
        {
            RemapBuffer remapBuffer;

            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), Shortcut() }), std::wstring()));
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x43);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x42);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ src, dest }), std::wstring()));

            // Case 1: Validate the element when selecting None on first dropdown of first column of 2 dropdown empty shortcut to shortcut row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = 0;

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutAtleast2Keys);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 2: Validate the element when selecting None on first dropdown of second column of 2 dropdown empty shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutAtleast2Keys);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 3: Validate the element when selecting None on first dropdown of first column of 2 dropdown valid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutAtleast2Keys);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 4: Validate the element when selecting None on first dropdown of second column of 2 dropdown valid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutAtleast2Keys);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutOneActionKey error and no action is required on setting second drop down to None on a non-hybrid column with two drop down
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutOneActionKeyAndNoAction_OnSettingSecondDropDownToNoneOnNonHybridColumnWithTwoDropDowns)
        {
            RemapBuffer remapBuffer;

            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), Shortcut() }), std::wstring()));
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x43);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x42);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ src, dest }), std::wstring()));

            // Case 1: Validate the element when selecting None on second dropdown of first column of 2 dropdown empty shortcut to shortcut row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = 0;

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 1, true, (int)index, std::vector<int32_t>({ -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 2: Validate the element when selecting None on second dropdown of second column of 2 dropdown empty shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 1, true, (int)index, std::vector<int32_t>({ -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 3: Validate the element when selecting None on second dropdown of first column of 2 dropdown valid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 4: Validate the element when selecting None on second dropdown of second column of 2 dropdown valid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
        }

        // Test if the ValidateShortcutBufferElement method returns no error and DeleteDropDown action is required on setting drop down to None on a hybrid column with two drop down
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoErrorAndDeleteDropDownAction_OnSettingDropDownToNoneOnHybridColumnWithTwoDropDowns)
        {
            RemapBuffer remapBuffer;

            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), Shortcut() }), std::wstring()));
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(0x43);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(0x42);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ src, dest }), std::wstring()));

            // Case 1: Validate the element when selecting None on first dropdown of second column of 2 dropdown empty hybrid shortcut to shortcut row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = 0;

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and DeleteDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::DeleteDropDown);

            // Case 2: Validate the element when selecting None on second dropdown of second column of 2 dropdown empty hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 1, true, (int)index, std::vector<int32_t>({ -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and DeleteDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::DeleteDropDown);

            // Case 3: Validate the element when selecting None on first dropdown of second column of 2 dropdown valid hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and DeleteDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::DeleteDropDown);

            // Case 4: Validate the element when selecting None on second dropdown of second column of 2 dropdown valid hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and DeleteDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::DeleteDropDown);
        }

        // Test if the ValidateShortcutBufferElement method returns no error and DeleteDropDown action is required on setting non last drop down to None on a column with three drop down
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoErrorAndDeleteDropDownAction_OnSettingNonLastDropDownToNoneOnColumnWithThreeDropDowns)
        {
            RemapBuffer remapBuffer;

            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), Shortcut() }), std::wstring()));
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(VK_SHIFT);
            src.SetKey(0x43);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(VK_SHIFT);
            dest.SetKey(0x42);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ src, dest }), std::wstring()));

            // Case 1: Validate the element when selecting None on first dropdown of first column of 3 dropdown empty shortcut to shortcut row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = 0;

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, -1, -1 }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and DeleteDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::DeleteDropDown);

            // Case 2: Validate the element when selecting None on second dropdown of first column of 3 dropdown empty hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 1, true, (int)index, std::vector<int32_t>({ -1, (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and DeleteDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::DeleteDropDown);

            // Case 3: Validate the element when selecting None on first dropdown of first column of 3 dropdown valid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and DeleteDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::DeleteDropDown);

            // Case 4: Validate the element when selecting None on second dropdown of first column of 3 dropdown valid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and DeleteDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::DeleteDropDown);

            // Case 5: Validate the element when selecting None on first dropdown of second column of 3 dropdown empty shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, -1, -1 }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and DeleteDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::DeleteDropDown);

            // Case 6: Validate the element when selecting None on second dropdown of second column of 3 dropdown empty shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 1, true, (int)index, std::vector<int32_t>({ -1, (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and DeleteDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::DeleteDropDown);

            // Case 7: Validate the element when selecting None on first dropdown of second column of 3 dropdown valid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and DeleteDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::DeleteDropDown);

            // Case 8: Validate the element when selecting None on second dropdown of second column of 3 dropdown valid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid and DeleteDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::DeleteDropDown);

            // Case 9: Validate the element when selecting None on first dropdown of second column of 3 dropdown empty hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, -1, -1 }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and DeleteDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::DeleteDropDown);

            // Case 10: Validate the element when selecting None on second dropdown of second column of 3 dropdown empty hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 1, true, (int)index, std::vector<int32_t>({ -1, (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and DeleteDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::DeleteDropDown);

            // Case 11: Validate the element when selecting None on first dropdown of second column of 3 dropdown valid hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and DeleteDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::DeleteDropDown);

            // Case 12: Validate the element when selecting None on second dropdown of second column of 3 dropdown valid hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is valid and DeleteDropDown action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::DeleteDropDown);
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutOneActionKey error and no action is required on setting last drop down to None on a column with three drop down
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutOneActionKeyErrorAndNoAction_OnSettingLastDropDownToNoneOnColumnWithThreeDropDowns)
        {
            RemapBuffer remapBuffer;

            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), Shortcut() }), std::wstring()));
            Shortcut src;
            src.SetKey(VK_CONTROL);
            src.SetKey(VK_SHIFT);
            src.SetKey(0x43);
            Shortcut dest;
            dest.SetKey(VK_CONTROL);
            dest.SetKey(VK_SHIFT);
            dest.SetKey(0x42);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ src, dest }), std::wstring()));

            // Case 1: Validate the element when selecting None on last dropdown of first column of 3 dropdown empty shortcut to shortcut row
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = 0;

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 2, true, (int)index, std::vector<int32_t>({ -1, -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 2: Validate the element when selecting None on last dropdown of first column of 3 dropdown valid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 3: Validate the element when selecting None on last dropdown of second column of 3 dropdown empty shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 2, true, (int)index, std::vector<int32_t>({ -1, -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 4: Validate the element when selecting None on last dropdown of second column of 3 dropdown valid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 5: Validate the element when selecting None on last dropdown of second column of 3 dropdown empty hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 2, true, (int)index, std::vector<int32_t>({ -1, -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);

            // Case 6: Validate the element when selecting None on last dropdown of second column of 3 dropdown valid hybrid shortcut to shortcut row
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid and no action is required
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ShortcutOneActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
        }

        // Test if the ValidateShortcutBufferElement method returns WinL error on setting a drop down to Win or L on a column resulting in Win+L
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnWinLError_OnSettingDropDownToWinOrLOnColumnResultingInWinL)
        {
            RemapBuffer remapBuffer;

            // empty row
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), Shortcut() }), std::wstring()));
            Shortcut s1;
            s1.SetKey(VK_LWIN);
            Shortcut s2;
            s2.SetKey(CommonSharedConstants::VK_WIN_BOTH);
            Shortcut s3;
            s3.SetKey(0x4C);
            Shortcut s4;
            s4.SetKey(VK_CONTROL);
            s4.SetKey(0x4C);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s1, Shortcut() }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), s1 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s2, Shortcut() }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), s2 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s3, Shortcut() }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), s3 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s4, Shortcut() }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), s4 }), std::wstring()));

            // Case 1: Validate the element when selecting L on second dropdown of first column of LWin+Empty shortcut
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x4C));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LWIN)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 2: Validate the element when selecting L on second dropdown of second column of LWin+Empty shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LWIN)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 3: Validate the element when selecting L on second dropdown of second column of LWin+Empty hybrid shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LWIN)), (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 4: Validate the element when selecting L on second dropdown of first column of LWin+Empty+Empty shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LWIN)), (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 5: Validate the element when selecting L on second dropdown of second column of LWin+Empty+Empty shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LWIN)), (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 6: Validate the element when selecting L on second dropdown of second column of LWin+Empty+Empty hybrid shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LWIN)), (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 7: Validate the element when selecting L on third dropdown of first column of LWin+Empty+Empty shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LWIN)), -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 8: Validate the element when selecting L on third dropdown of second column of LWin+Empty+Empty shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LWIN)), -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 9: Validate the element when selecting L on third dropdown of second column of LWin+Empty+Empty hybrid shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LWIN)), -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 10: Validate the element when selecting L on second dropdown of first column of Win+Empty shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), CommonSharedConstants::VK_WIN_BOTH)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 11: Validate the element when selecting L on second dropdown of second column of Win+Empty shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), CommonSharedConstants::VK_WIN_BOTH)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 12: Validate the element when selecting L on second dropdown of second column of Win+Empty hybrid shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), CommonSharedConstants::VK_WIN_BOTH)), (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LWIN));

            // Case 13: Validate the element when selecting LWin on first dropdown of first column of Empty+L shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x4C)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 14: Validate the element when selecting LWin on first dropdown of second column of Empty+L shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x4C)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 15: Validate the element when selecting LWin on first dropdown of second column of Empty+L hybrid shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x4C)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 16: Validate the element when selecting LWin on first dropdown of first column of Ctrl+L shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(4, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x4C)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 17: Validate the element when selecting LWin on first dropdown of second column of Ctrl+L shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x4C)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 18: Validate the element when selecting LWin on first dropdown of second column of Ctrl+L hybrid shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x4C)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);
        }

        // Test if the ValidateShortcutBufferElement method returns WinL error on setting a drop down to null or none on a column resulting in Win+L
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnWinLError_OnSettingDropDownToNullOrNoneOnColumnResultingInWinL)
        {
            RemapBuffer remapBuffer;

            // LWin+Ctrl+L
            Shortcut s4;
            s4.SetKey(VK_LWIN);
            s4.SetKey(VK_CONTROL);
            s4.SetKey(0x4C);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s4, Shortcut() }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), s4 }), std::wstring()));

            // Case 1 : Validate the element when selecting null on second dropdown of first column of LWin + Ctrl + L shortcut
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = -1;

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LWIN)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x4C)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 2 : Validate the element when selecting null on second dropdown of second column of LWin + Ctrl + L shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LWIN)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x4C)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 3 : Validate the element when selecting null on second dropdown of second column of LWin + Ctrl + L hybrid shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LWIN)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x4C)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 4 : Validate the element when selecting null on first dropdown of first column of Ctrl + LWin + L shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LWIN)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x4C)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 5 : Validate the element when selecting null on first dropdown of second column of Ctrl + LWin + L shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LWIN)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x4C)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 6 : Validate the element when selecting null on first dropdown of second column of Ctrl + LWin + L hybrid shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LWIN)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x4C)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            index = 0;

            // Case 7 : Validate the element when selecting None on second dropdown of first column of LWin + Ctrl + L shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LWIN)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x4C)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 8 : Validate the element when selecting None on second dropdown of second column of LWin + Ctrl + L shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LWIN)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x4C)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 9 : Validate the element when selecting None on second dropdown of second column of LWin + Ctrl + L hybrid shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LWIN)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x4C)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 10 : Validate the element when selecting None on first dropdown of first column of Ctrl + LWin + L shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LWIN)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x4C)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 11 : Validate the element when selecting None on first dropdown of second column of Ctrl + LWin + L shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LWIN)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x4C)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);

            // Case 12 : Validate the element when selecting None on first dropdown of second column of Ctrl + LWin + L hybrid shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LWIN)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x4C)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::WinL);
        }

        // Test if the ValidateShortcutBufferElement method returns CtrlAltDel error on setting a drop down to Ctrl, Alt or Del on a column resulting in Ctrl+Alt+Del
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnCtrlAltDelError_OnSettingDropDownToCtrlAltOrDelOnColumnResultingInCtrlAltDel)
        {
            RemapBuffer remapBuffer;

            Shortcut s1;
            s1.SetKey(VK_CONTROL);
            s1.SetKey(VK_MENU);
            Shortcut s2;
            s2.SetKey(VK_CONTROL);
            s2.SetKey(VK_DELETE);
            Shortcut s3;
            s3.SetKey(VK_CONTROL);
            s3.SetKey(VK_MENU);
            s3.SetKey(0x41);
            Shortcut s4;
            s4.SetKey(VK_SHIFT);
            s4.SetKey(VK_MENU);
            s4.SetKey(VK_DELETE);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s1, Shortcut() }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), s1 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s2, Shortcut() }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), s2 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s3, Shortcut() }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), s3 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s4, Shortcut() }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ Shortcut(), s4 }), std::wstring()));

            // Case 1 : Validate the element when selecting Del on third dropdown of first column of Ctrl+Alt+Empty shortcut
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_DELETE));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::CtrlAltDel);

            // Case 2 : Validate the element when selecting Del on third dropdown of second column of Ctrl+Alt+Empty shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::CtrlAltDel);

            // Case 3 : Validate the element when selecting Del on third dropdown of second column of Ctrl+Alt+Empty hybrid shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::CtrlAltDel);

            // Case 4 : Validate the element when selecting Del on third dropdown of first column of Alt+Ctrl+Empty shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::CtrlAltDel);

            // Case 5 : Validate the element when selecting Del on third dropdown of second column of Alt+Ctrl+Empty shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::CtrlAltDel);

            // Case 6 : Validate the element when selecting Del on third dropdown of second column of Alt+Ctrl+Empty hybrid shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::CtrlAltDel);

            // Case 7 : Validate the element when selecting Alt on second dropdown of first column of Ctrl+Empty+Del shortcut
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_DELETE)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::CtrlAltDel);

            // Case 8 : Validate the element when selecting Alt on second dropdown of second column of Ctrl+Empty+Del shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_DELETE)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::CtrlAltDel);

            // Case 9 : Validate the element when selecting Alt on second dropdown of second column of Ctrl+Empty+Del hybrid shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_DELETE)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::CtrlAltDel);

            // Case 10 : Validate the element when selecting Del on third dropdown of first column of Ctrl+Alt+A shortcut
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_DELETE));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(4, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::CtrlAltDel);

            // Case 11 : Validate the element when selecting Del on third dropdown of second column of Ctrl+Alt+A shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::CtrlAltDel);

            // Case 12 : Validate the element when selecting Del on third dropdown of second column of Ctrl+Alt+A hybrid shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::CtrlAltDel);

            // Case 13 : Validate the element when selecting Ctrl on first dropdown of first column of Shift+Alt+Del shortcut
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(6, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_DELETE)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::CtrlAltDel);

            // Case 14 : Validate the element when selecting Ctrl on first dropdown of second column of Shift+Alt+Del shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(7, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_DELETE)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::CtrlAltDel);

            // Case 15 : Validate the element when selecting Ctrl on first dropdown of second column of Shift+Alt+Del hybrid shortcut
            result = BufferValidationHelpers::ValidateShortcutBufferElement(7, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_MENU)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_DELETE)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::CtrlAltDel);
        }

        // Test if the ValidateShortcutBufferElement method returns MapToSameKey error on setting hybrid second column to match first column in a remap keys table
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnMapToSameKeyError_OnSettingHybridSecondColumnToFirstColumnInKeyTable)
        {
            RemapBuffer remapBuffer;

            Shortcut s1;
            s1.SetKey(VK_CONTROL);
            s1.SetKey(0x43);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ 0x41, NULL }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ 0x42, 0x43 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ 0x43, s1 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ VK_CONTROL, s1 }), std::wstring()));

            // Case 1 : Validate the element when selecting A on first dropdown of empty hybrid second column
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x41));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameKey);

            // Case 2 : Validate the element when selecting A on second dropdown of empty hybrid second column
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 1, true, (int)index, std::vector<int32_t>({ -1, (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameKey);

            // Case 3 : Validate the element when selecting A on third dropdown of empty hybrid second column
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 2, true, (int)index, std::vector<int32_t>({ -1, -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameKey);

            // Case 4 : Validate the element when selecting B on first dropdown of hybrid second column with key
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x42));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameKey);

            // Case 5 : Validate the element when selecting null on first dropdown of hybrid second column with shortcut
            index = -1;

            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameKey);

            // Case 6 : Validate the element when selecting None on first dropdown of hybrid second column with shortcut
            index = 0;

            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameKey);

            // Case 7 : Validate the element when selecting null on second dropdown of hybrid second column with shortcut
            index = -1;

            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameKey);

            // Case 8 : Validate the element when selecting None on second dropdown of hybrid second column with shortcut
            index = 0;

            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameKey);
        }

        // Test if the ValidateShortcutBufferElement method returns MapToSameShortcut error on setting one column to match the other and both are valid 3 key shortcuts
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnMapToSameShortcutError_OnSettingOneColumnToTheOtherAndBothAreValid3KeyShortcuts)
        {
            RemapBuffer remapBuffer;

            Shortcut s1;
            s1.SetKey(VK_CONTROL);
            s1.SetKey(VK_SHIFT);
            s1.SetKey(0x43);
            Shortcut s2;
            s2.SetKey(VK_CONTROL);
            s2.SetKey(VK_SHIFT);
            Shortcut s3;
            s3.SetKey(VK_CONTROL);
            s3.SetKey(0x43);
            Shortcut s4;
            s4.SetKey(VK_CONTROL);
            s4.SetKey(VK_SHIFT);
            s4.SetKey(0x41);
            Shortcut s5;
            s5.SetKey(VK_MENU);
            s5.SetKey(VK_SHIFT);
            s5.SetKey(0x43);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s2, s1 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s1, s2 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s3, s1 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s1, s3 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s4, s1 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s1, s4 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s5, s1 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s1, s5 }), std::wstring()));

            // Case 1 : Validate the element when selecting C on third dropdown of first column with Ctrl+Shift+Empty
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 2 : Validate the element when selecting C on third dropdown of second column with Ctrl+Shift+Empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 3 : Validate the element when selecting C on third dropdown of second column with hybrid Ctrl+Shift+Empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 4 : Validate the element when selecting Shift on second dropdown of first column with Ctrl+Empty+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 5 : Validate the element when selecting Shift on second dropdown of second column with Ctrl+Empty+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 6 : Validate the element when selecting Shift on second dropdown of second column with hybrid Ctrl+Empty+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 7 : Validate the element when selecting Shift on first dropdown of first column with Empty+Ctrl+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 8 : Validate the element when selecting Shift on first dropdown of second column with Empty+Ctrl+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 9 : Validate the element when selecting Shift on first dropdown of second column with hybrid Empty+Ctrl+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 10 : Validate the element when selecting C on third dropdown of first column with Ctrl+Shift+A
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(4, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 11 : Validate the element when selecting C on third dropdown of second column with Ctrl+Shift+A
            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 12 : Validate the element when selecting C on third dropdown of second column with hybrid Ctrl+Shift+A
            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 13 : Validate the element when selecting Ctrl on second dropdown of first column with Shift+Alt+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(6, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 14 : Validate the element when selecting Ctrl on second dropdown of second column with Shift+Alt+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(7, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 15 : Validate the element when selecting Ctrl on second dropdown of second column with hybrid Shift+Alt+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(7, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 16 : Validate the element when selecting Ctrl on first dropdown of first column with Alt+Shift+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(6, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 17 : Validate the element when selecting Ctrl on first dropdown of second column with Alt+Shift+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(7, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 18 : Validate the element when selecting Ctrl on first dropdown of second column with hybrid Alt+Shift+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(7, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);
        }

        // Test if the ValidateShortcutBufferElement method returns MapToSameShortcut error on setting one column to match the other and both are valid 2 key shortcuts
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnMapToSameShortcutError_OnSettingOneColumnToTheOtherAndBothAreValid2KeyShortcuts)
        {
            RemapBuffer remapBuffer;

            Shortcut s1;
            s1.SetKey(VK_CONTROL);
            s1.SetKey(0x43);
            Shortcut s2;
            s2.SetKey(VK_CONTROL);
            Shortcut s3;
            s3.SetKey(0x43);
            Shortcut s4;
            s4.SetKey(VK_CONTROL);
            s4.SetKey(0x41);
            Shortcut s5;
            s5.SetKey(VK_MENU);
            s5.SetKey(0x43);
            Shortcut s6;
            s6.SetKey(VK_CONTROL);
            s6.SetKey(VK_SHIFT);
            s6.SetKey(0x43);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s2, s1 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s1, s2 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s3, s1 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s1, s3 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s4, s1 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s1, s4 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s5, s1 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s1, s5 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s6, s1 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s1, s6 }), std::wstring()));

            // Case 1 : Validate the element when selecting C on second dropdown of first column with Ctrl+Empty
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 2 : Validate the element when selecting C on second dropdown of second column with Ctrl+Empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 3 : Validate the element when selecting C on second dropdown of second column with hybrid Ctrl+Empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 4 : Validate the element when selecting C on third dropdown of first column with Ctrl+Empty+Empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 5 : Validate the element when selecting C on third dropdown of second column with Ctrl+Empty+Empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 6 : Validate the element when selecting C on third dropdown of second column with hybrid Ctrl+Empty+Empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 7 : Validate the element when selecting C on second dropdown of first column with Ctrl+Empty+Empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 8 : Validate the element when selecting C on second dropdown of second column with Ctrl+Empty+Empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 9 : Validate the element when selecting C on second dropdown of second column with hybrid Ctrl+Empty+Empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 10 : Validate the element when selecting Ctrl on first dropdown of first column with Empty+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 11 : Validate the element when selecting Ctrl on first dropdown of second column with Empty+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 12 : Validate the element when selecting Ctrl on first dropdown of second column with hybrid Empty+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 13 : Validate the element when selecting Ctrl on first dropdown of first column with Empty+Empty+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, -1, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 14 : Validate the element when selecting Ctrl on first dropdown of second column with Empty+Empty+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, -1, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 15 : Validate the element when selecting Ctrl on first dropdown of second column with hybrid Empty+Empty+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, -1, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 16 : Validate the element when selecting Ctrl on second dropdown of first column with Empty+Empty+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 1, true, (int)index, std::vector<int32_t>({ -1, (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 17 : Validate the element when selecting Ctrl on second dropdown of second column with Empty+Empty+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 1, true, (int)index, std::vector<int32_t>({ -1, (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 18 : Validate the element when selecting Ctrl on second dropdown of second column with hybrid Empty+Empty+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 1, 1, true, (int)index, std::vector<int32_t>({ -1, (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 19 : Validate the element when selecting C on second dropdown of first column with Ctrl+A
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(4, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 20 : Validate the element when selecting C on second dropdown of second column with Ctrl+A
            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 21 : Validate the element when selecting C on second dropdown of second column with hybrid Ctrl+A
            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 22 : Validate the element when selecting Ctrl on first dropdown of first column with Alt+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(6, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 23 : Validate the element when selecting Ctrl on first dropdown of second column with Alt+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(7, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 24 : Validate the element when selecting Ctrl on first dropdown of second column with hybrid Alt+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(7, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 25 : Validate the element when selecting null on second dropdown of first column with Ctrl+Shift+C
            index = -1;

            result = BufferValidationHelpers::ValidateShortcutBufferElement(8, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 26 : Validate the element when selecting null on second dropdown of second column with Ctrl+Shift+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(9, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 27 : Validate the element when selecting null on second dropdown of second column with hybrid Ctrl+Shift+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(9, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 28 : Validate the element when selecting null on first dropdown of first column with Shift+Ctrl+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(8, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 29 : Validate the element when selecting null on first dropdown of second column with Shift+Ctrl+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(9, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 30 : Validate the element when selecting null on first dropdown of second column with hybrid Shift+Ctrl+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(9, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 31 : Validate the element when selecting None on second dropdown of first column with Ctrl+Shift+C
            index = 0;

            result = BufferValidationHelpers::ValidateShortcutBufferElement(8, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 32 : Validate the element when selecting None on second dropdown of second column with Ctrl+Shift+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(9, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 33 : Validate the element when selecting None on second dropdown of second column with hybrid Ctrl+Shift+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(9, 1, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 34 : Validate the element when selecting None on first dropdown of first column with Shift+Ctrl+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(8, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 35 : Validate the element when selecting None on first dropdown of second column with Shift+Ctrl+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(9, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);

            // Case 36 : Validate the element when selecting None on first dropdown of second column with hybrid Shift+Ctrl+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(9, 1, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, true);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::MapToSameShortcut);
        }

        // Test if the ValidateShortcutBufferElement method returns SameShortcutPreviouslyMapped error on setting first column to match first column in another row with same target app and both are valid 3 key shortcuts
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnSameShortcutPreviouslyMappedError_OnSettingFirstColumnToFirstColumnInAnotherRowWithSameTargetAppAndBothAreValid3KeyShortcuts)
        {
            RemapBuffer remapBuffer;

            Shortcut s1;
            s1.SetKey(VK_CONTROL);
            s1.SetKey(VK_SHIFT);
            s1.SetKey(0x43);
            Shortcut s2;
            s2.SetKey(VK_CONTROL);
            s2.SetKey(VK_SHIFT);
            Shortcut s3;
            s3.SetKey(VK_CONTROL);
            s3.SetKey(0x43);
            Shortcut s4;
            s4.SetKey(VK_CONTROL);
            s4.SetKey(VK_SHIFT);
            s4.SetKey(0x41);
            Shortcut s5;
            s5.SetKey(VK_MENU);
            s5.SetKey(VK_SHIFT);
            s5.SetKey(0x43);
            Shortcut dest;
            dest.SetKey(VK_LWIN);
            dest.SetKey(0x43);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s1, dest }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s2, dest }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s3, dest }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s4, dest }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s5, dest }), std::wstring()));

            // Case 1 : Validate the element when selecting C on third dropdown of first column with Ctrl+Shift+Empty
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::SameShortcutPreviouslyMapped);

            // Case 2 : Validate the element when selecting Shift on second dropdown of first column with Ctrl+Empty+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::SameShortcutPreviouslyMapped);

            // Case 3 : Validate the element when selecting Shift on first dropdown of first column with Empty+Ctrl+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::SameShortcutPreviouslyMapped);

            // Case 4 : Validate the element when selecting C on third dropdown of first column with Ctrl+Shift+A
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::SameShortcutPreviouslyMapped);

            // Case 5 : Validate the element when selecting Ctrl on second dropdown of first column with Shift+Alt+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(4, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::SameShortcutPreviouslyMapped);

            // Case 6 : Validate the element when selecting Ctrl on first dropdown of first column with Alt+Shift+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(4, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::SameShortcutPreviouslyMapped);
        }

        // Test if the ValidateShortcutBufferElement method returns no error on setting first column to match first column in another row with different target app and both are valid 3 key shortcuts
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoError_OnSettingFirstColumnToFirstColumnInAnotherRowWithDifferentTargetAppAndBothAreValid3KeyShortcuts)
        {
            RemapBuffer remapBuffer;

            Shortcut s1;
            s1.SetKey(VK_CONTROL);
            s1.SetKey(VK_SHIFT);
            s1.SetKey(0x43);
            Shortcut s2;
            s2.SetKey(VK_CONTROL);
            s2.SetKey(VK_SHIFT);
            Shortcut s3;
            s3.SetKey(VK_CONTROL);
            s3.SetKey(0x43);
            Shortcut s4;
            s4.SetKey(VK_CONTROL);
            s4.SetKey(VK_SHIFT);
            s4.SetKey(0x41);
            Shortcut s5;
            s5.SetKey(VK_MENU);
            s5.SetKey(VK_SHIFT);
            s5.SetKey(0x43);
            Shortcut dest;
            dest.SetKey(VK_LWIN);
            dest.SetKey(0x43);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s1, dest }), testApp1));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s2, dest }), testApp2));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s3, dest }), testApp2));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s4, dest }), testApp2));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s5, dest }), testApp2));

            // Case 1 : Validate the element when selecting C on third dropdown of first column with Ctrl+Shift+Empty
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 2 : Validate the element when selecting Shift on second dropdown of first column with Ctrl+Empty+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 3 : Validate the element when selecting Shift on first dropdown of first column with Empty+Ctrl+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 4 : Validate the element when selecting C on third dropdown of first column with Ctrl+Shift+A
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 5 : Validate the element when selecting Ctrl on second dropdown of first column with Shift+Alt+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(4, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 6 : Validate the element when selecting Ctrl on first dropdown of first column with Alt+Shift+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(4, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
        }

        // Test if the ValidateShortcutBufferElement method returns ConflictingModifierShortcut error on setting first column to conflict with first column in another row with same target app and both are valid 3 key shortcuts
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnConflictingModifierShortcutError_OnSettingFirstColumnToConflictWithFirstColumnInAnotherRowWithSameTargetAppAndBothAreValid3KeyShortcuts)
        {
            RemapBuffer remapBuffer;

            Shortcut s1;
            s1.SetKey(VK_CONTROL);
            s1.SetKey(VK_SHIFT);
            s1.SetKey(0x43);
            Shortcut s2;
            s2.SetKey(VK_LCONTROL);
            s2.SetKey(VK_SHIFT);
            Shortcut s3;
            s3.SetKey(VK_LCONTROL);
            s3.SetKey(0x43);
            Shortcut s4;
            s4.SetKey(VK_CONTROL);
            s4.SetKey(VK_LSHIFT);
            s4.SetKey(0x41);
            Shortcut s5;
            s5.SetKey(VK_MENU);
            s5.SetKey(VK_SHIFT);
            s5.SetKey(0x43);
            Shortcut dest;
            dest.SetKey(VK_LWIN);
            dest.SetKey(0x43);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s1, dest }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s2, dest }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s3, dest }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s4, dest }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s5, dest }), std::wstring()));

            // Case 1 : Validate the element when selecting C on third dropdown of first column with LCtrl+Shift+Empty
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ConflictingModifierShortcut);

            // Case 2 : Validate the element when selecting Shift on second dropdown of first column with LCtrl+Empty+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ConflictingModifierShortcut);

            // Case 3 : Validate the element when selecting Shift on first dropdown of first column with Empty+LCtrl+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ConflictingModifierShortcut);

            // Case 4 : Validate the element when selecting C on third dropdown of first column with Ctrl+LShift+A
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LSHIFT)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ConflictingModifierShortcut);

            // Case 5 : Validate the element when selecting LCtrl on second dropdown of first column with Shift+Alt+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(4, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ConflictingModifierShortcut);

            // Case 6 : Validate the element when selecting LCtrl on first dropdown of first column with Alt+Shift+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(4, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ConflictingModifierShortcut);
        }

        // Test if the ValidateShortcutBufferElement method returns no error on setting first column to conflict with first column in another row with different target app and both are valid 3 key shortcuts
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoError_OnSettingFirstColumnToConflictWithFirstColumnInAnotherRowWithDifferentTargetAppAndBothAreValid3KeyShortcuts)
        {
            RemapBuffer remapBuffer;

            Shortcut s1;
            s1.SetKey(VK_CONTROL);
            s1.SetKey(VK_SHIFT);
            s1.SetKey(0x43);
            Shortcut s2;
            s2.SetKey(VK_LCONTROL);
            s2.SetKey(VK_SHIFT);
            Shortcut s3;
            s3.SetKey(VK_LCONTROL);
            s3.SetKey(0x43);
            Shortcut s4;
            s4.SetKey(VK_CONTROL);
            s4.SetKey(VK_LSHIFT);
            s4.SetKey(0x41);
            Shortcut s5;
            s5.SetKey(VK_MENU);
            s5.SetKey(VK_SHIFT);
            s5.SetKey(0x43);
            Shortcut dest;
            dest.SetKey(VK_LWIN);
            dest.SetKey(0x43);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s1, dest }), testApp1));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s2, dest }), testApp2));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s3, dest }), testApp2));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s4, dest }), testApp2));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s5, dest }), testApp2));

            // Case 1 : Validate the element when selecting C on third dropdown of first column with LCtrl+Shift+Empty
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 2 : Validate the element when selecting Shift on second dropdown of first column with LCtrl+Empty+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 3 : Validate the element when selecting Shift on first dropdown of first column with Empty+LCtrl+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 4 : Validate the element when selecting C on third dropdown of first column with Ctrl+LShift+A
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LSHIFT)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 5 : Validate the element when selecting LCtrl on second dropdown of first column with Shift+Alt+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(4, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 6 : Validate the element when selecting LCtrl on first dropdown of first column with Alt+Shift+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(4, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_SHIFT)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
        }

        // Test if the ValidateShortcutBufferElement method returns SameShortcutPreviouslyMapped error on setting first column to match first column in another row with same target app and both are valid 2 key shortcuts
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnSameShortcutPreviouslyMappedError_OnSettingFirstColumnToFirstColumnInAnotherRowWithSameTargetAppAndBothAreValid2KeyShortcuts)
        {
            RemapBuffer remapBuffer;

            Shortcut s1;
            s1.SetKey(VK_CONTROL);
            s1.SetKey(0x43);
            Shortcut s2;
            s2.SetKey(VK_CONTROL);
            Shortcut s3;
            s3.SetKey(0x43);
            Shortcut s4;
            s4.SetKey(VK_CONTROL);
            s4.SetKey(0x41);
            Shortcut s5;
            s5.SetKey(VK_MENU);
            s5.SetKey(0x43);
            Shortcut s6;
            s6.SetKey(VK_CONTROL);
            s6.SetKey(VK_SHIFT);
            s6.SetKey(0x43);
            Shortcut dest;
            dest.SetKey(VK_LWIN);
            dest.SetKey(0x43);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s1, dest }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s2, dest }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s3, dest }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s4, dest }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s5, dest }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s6, dest }), std::wstring()));

            // Case 1 : Validate the element when selecting C on second dropdown of first column with Ctrl+Empty
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::SameShortcutPreviouslyMapped);

            // Case 2 : Validate the element when selecting C on third dropdown of first column with Ctrl+Empty+Empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::SameShortcutPreviouslyMapped);

            // Case 3 : Validate the element when selecting C on second dropdown of first column with Ctrl+Empty+Empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::SameShortcutPreviouslyMapped);

            // Case 4 : Validate the element when selecting Ctrl on first dropdown of first column with Empty+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::SameShortcutPreviouslyMapped);

            // Case 5 : Validate the element when selecting Ctrl on first dropdown of first column with Empty+Empty+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, -1, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::SameShortcutPreviouslyMapped);

            // Case 6 : Validate the element when selecting Ctrl on second dropdown of first column with Empty+Empty+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 1, true, (int)index, std::vector<int32_t>({ -1, (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::SameShortcutPreviouslyMapped);

            // Case 7 : Validate the element when selecting C on second dropdown of first column with Ctrl+A
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::SameShortcutPreviouslyMapped);

            // Case 8 : Validate the element when selecting Ctrl on first dropdown of first column with Alt+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(4, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::SameShortcutPreviouslyMapped);

            // Case 9 : Validate the element when selecting null on second dropdown of first column with Ctrl+Shift+C
            index = -1;

            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::SameShortcutPreviouslyMapped);

            // Case 10 : Validate the element when selecting null on first dropdown of first column with Shift+Ctrl+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::SameShortcutPreviouslyMapped);

            // Case 11 : Validate the element when selecting None on second dropdown of first column with Ctrl+Shift+C
            index = 0;

            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::SameShortcutPreviouslyMapped);

            // Case 12 : Validate the element when selecting None on first dropdown of first column with Shift+Ctrl+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::SameShortcutPreviouslyMapped);
        }

        // Test if the ValidateShortcutBufferElement method returns no error on setting first column to match first column in another row with different target app and both are valid 2 key shortcuts
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoError_OnSettingFirstColumnToFirstColumnInAnotherRowWithDifferentTargetAppAndBothAreValid2KeyShortcuts)
        {
            RemapBuffer remapBuffer;

            Shortcut s1;
            s1.SetKey(VK_CONTROL);
            s1.SetKey(0x43);
            Shortcut s2;
            s2.SetKey(VK_CONTROL);
            Shortcut s3;
            s3.SetKey(0x43);
            Shortcut s4;
            s4.SetKey(VK_CONTROL);
            s4.SetKey(0x41);
            Shortcut s5;
            s5.SetKey(VK_MENU);
            s5.SetKey(0x43);
            Shortcut s6;
            s6.SetKey(VK_CONTROL);
            s6.SetKey(VK_SHIFT);
            s6.SetKey(0x43);
            Shortcut dest;
            dest.SetKey(VK_LWIN);
            dest.SetKey(0x43);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s1, dest }), testApp1));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s2, dest }), testApp2));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s3, dest }), testApp2));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s4, dest }), testApp2));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s5, dest }), testApp2));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s6, dest }), testApp2));

            // Case 1 : Validate the element when selecting C on second dropdown of first column with Ctrl+Empty
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 2 : Validate the element when selecting C on third dropdown of first column with Ctrl+Empty+Empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 3 : Validate the element when selecting C on second dropdown of first column with Ctrl+Empty+Empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 4 : Validate the element when selecting Ctrl on first dropdown of first column with Empty+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 5 : Validate the element when selecting Ctrl on first dropdown of first column with Empty+Empty+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, -1, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 6 : Validate the element when selecting Ctrl on second dropdown of first column with Empty+Empty+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 1, true, (int)index, std::vector<int32_t>({ -1, (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 7 : Validate the element when selecting C on second dropdown of first column with Ctrl+A
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 8 : Validate the element when selecting Ctrl on first dropdown of first column with Alt+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(4, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 9 : Validate the element when selecting null on second dropdown of first column with Ctrl+Shift+C
            index = -1;

            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 10 : Validate the element when selecting null on first dropdown of first column with Shift+Ctrl+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 11 : Validate the element when selecting None on second dropdown of first column with Ctrl+Shift+C
            index = 0;

            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 12 : Validate the element when selecting None on first dropdown of first column with Shift+Ctrl+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_CONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
        }

        // Test if the ValidateShortcutBufferElement method returns ConflictingModifierShortcut error on setting first column to conflict with first column in another row with same target app and both are valid 2 key shortcuts
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnConflictingModifierShortcutError_OnSettingFirstColumnToConflictWithFirstColumnInAnotherRowWithSameTargetAppAndBothAreValid2KeyShortcuts)
        {
            RemapBuffer remapBuffer;

            Shortcut s1;
            s1.SetKey(VK_CONTROL);
            s1.SetKey(0x43);
            Shortcut s2;
            s2.SetKey(VK_LCONTROL);
            Shortcut s3;
            s3.SetKey(0x43);
            Shortcut s4;
            s4.SetKey(VK_LCONTROL);
            s4.SetKey(0x41);
            Shortcut s5;
            s5.SetKey(VK_MENU);
            s5.SetKey(0x43);
            Shortcut s6;
            s6.SetKey(VK_LCONTROL);
            s6.SetKey(VK_SHIFT);
            s6.SetKey(0x43);
            Shortcut dest;
            dest.SetKey(VK_LWIN);
            dest.SetKey(0x43);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s1, dest }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s2, dest }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s3, dest }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s4, dest }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s5, dest }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s6, dest }), std::wstring()));

            // Case 1 : Validate the element when selecting C on second dropdown of first column with LCtrl+Empty
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ConflictingModifierShortcut);

            // Case 2 : Validate the element when selecting C on third dropdown of first column with LCtrl+Empty+Empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL)), -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ConflictingModifierShortcut);

            // Case 3 : Validate the element when selecting C on second dropdown of first column with LCtrl+Empty+Empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL)), (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ConflictingModifierShortcut);

            // Case 4 : Validate the element when selecting LCtrl on first dropdown of first column with Empty+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ConflictingModifierShortcut);

            // Case 5 : Validate the element when selecting LCtrl on first dropdown of first column with Empty+Empty+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, -1, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ConflictingModifierShortcut);

            // Case 6 : Validate the element when selecting LCtrl on second dropdown of first column with Empty+Empty+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 1, true, (int)index, std::vector<int32_t>({ -1, (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ConflictingModifierShortcut);

            // Case 7 : Validate the element when selecting C on second dropdown of first column with LCtrl+A
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ConflictingModifierShortcut);

            // Case 8 : Validate the element when selecting LCtrl on first dropdown of first column with Alt+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(4, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ConflictingModifierShortcut);

            // Case 9 : Validate the element when selecting null on second dropdown of first column with LCtrl+Shift+C
            index = -1;

            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ConflictingModifierShortcut);

            // Case 10 : Validate the element when selecting null on first dropdown of first column with Shift+LCtrl+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ConflictingModifierShortcut);

            // Case 11 : Validate the element when selecting None on second dropdown of first column with LCtrl+Shift+C
            index = 0;

            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ConflictingModifierShortcut);

            // Case 12 : Validate the element when selecting None on first dropdown of first column with Shift+LCtrl+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is invalid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::ConflictingModifierShortcut);
        }

        // Test if the ValidateShortcutBufferElement method returns no error on setting first column to conflict with first column in another row with different target app and both are valid 2 key shortcuts
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoError_OnSettingFirstColumnToConflictWithFirstColumnInAnotherRowWithDifferentTargetAppAndBothAreValid2KeyShortcuts)
        {
            RemapBuffer remapBuffer;

            Shortcut s1;
            s1.SetKey(VK_CONTROL);
            s1.SetKey(0x43);
            Shortcut s2;
            s2.SetKey(VK_LCONTROL);
            Shortcut s3;
            s3.SetKey(0x43);
            Shortcut s4;
            s4.SetKey(VK_LCONTROL);
            s4.SetKey(0x41);
            Shortcut s5;
            s5.SetKey(VK_MENU);
            s5.SetKey(0x43);
            Shortcut s6;
            s6.SetKey(VK_LCONTROL);
            s6.SetKey(VK_SHIFT);
            s6.SetKey(0x43);
            Shortcut dest;
            dest.SetKey(VK_LWIN);
            dest.SetKey(0x43);
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s1, dest }), testApp1));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s2, dest }), testApp2));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s3, dest }), testApp2));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s4, dest }), testApp2));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s5, dest }), testApp2));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ s6, dest }), testApp2));

            // Case 1 : Validate the element when selecting C on second dropdown of first column with LCtrl+Empty
            std::vector<DWORD> keyList = keyboardLayout.GetKeyCodeList(true);
            size_t index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43));

            std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 2 : Validate the element when selecting C on third dropdown of first column with LCtrl+Empty+Empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 2, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL)), -1, (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 3 : Validate the element when selecting C on second dropdown of first column with LCtrl+Empty+Empty
            result = BufferValidationHelpers::ValidateShortcutBufferElement(1, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL)), (int32_t)index, -1 }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 4 : Validate the element when selecting LCtrl on first dropdown of first column with Empty+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 5 : Validate the element when selecting LCtrl on first dropdown of first column with Empty+Empty+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, -1, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 6 : Validate the element when selecting LCtrl on second dropdown of first column with Empty+Empty+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(2, 0, 1, true, (int)index, std::vector<int32_t>({ -1, (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 7 : Validate the element when selecting C on second dropdown of first column with LCtrl+A
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(3, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL)), (int32_t)index }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 8 : Validate the element when selecting LCtrl on first dropdown of first column with Alt+C
            index = std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL));

            result = BufferValidationHelpers::ValidateShortcutBufferElement(4, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 9 : Validate the element when selecting null on second dropdown of first column with LCtrl+Shift+C
            index = -1;

            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 10 : Validate the element when selecting null on first dropdown of first column with Shift+LCtrl+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 11 : Validate the element when selecting None on second dropdown of first column with LCtrl+Shift+C
            index = 0;

            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 0, 1, true, (int)index, std::vector<int32_t>({ (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL)), (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);

            // Case 12 : Validate the element when selecting None on first dropdown of first column with Shift+LCtrl+C
            result = BufferValidationHelpers::ValidateShortcutBufferElement(5, 0, 0, true, (int)index, std::vector<int32_t>({ (int32_t)index, (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), VK_LCONTROL)), (int32_t)std::distance(keyList.begin(), std::find(keyList.begin(), keyList.end(), 0x43)) }), std::wstring(), keyList, remapBuffer, false);

            // Assert that the element is valid
            Assert::AreEqual(true, result.first == KeyboardManagerHelper::ErrorType::NoError);
        }
    };
}
