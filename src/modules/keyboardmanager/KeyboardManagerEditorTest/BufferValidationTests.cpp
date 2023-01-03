#include "pch.h"

// Suppressing 26466 - Don't use static_cast downcasts - in CppUnitTest.h
#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include <keyboardmanager/KeyboardManagerEditorLibrary/BufferValidationHelpers.h>
#include <common/interop/keyboard_layout.h>
#include <common/interop/shared_constants.h>
#include <functional>
#include <keyboardmanager/KeyboardManagerEditorLibrary/ShortcutErrorType.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RemappingUITests
{
    // Tests for methods in the BufferValidationHelpers namespace
    TEST_CLASS (BufferValidationTests)
    {
        std::wstring testApp1 = L"testprocess1.exe";
        std::wstring testApp2 = L"testprocess2.exe";
        LayoutMap keyboardLayout;

        struct ValidateAndUpdateKeyBufferElementArgs
        {
            int elementRowIndex;
            int elementColIndex;
            int selectedCodeFromDropDown;
        };

        struct ValidateShortcutBufferElementArgs
        {
            int elementRowIndex;
            int elementColIndex;
            uint32_t indexOfDropDownLastModified;
            std::vector<int32_t> selectedCodesOnDropDowns;
            std::wstring targetAppNameInTextBox;
            bool isHybridColumn;
            RemapBufferRow bufferRow;
        };

        void RunTestCases(const std::vector<ValidateShortcutBufferElementArgs>& testCases, std::function<void(const ValidateShortcutBufferElementArgs&)> testMethod)
        {
            for (int i = 0; i < testCases.size(); i++)
            {
                testMethod(testCases[i]);
            }
        }

    public:
        TEST_METHOD_INITIALIZE(InitializeTestEnv)
        {
        }

        // Test if the ValidateAndUpdateKeyBufferElement method is successful when setting a key to null in a new row
        TEST_METHOD (ValidateAndUpdateKeyBufferElement_ShouldUpdateAndReturnNoError_OnSettingKeyToNullInANewRow)
        {
            RemapBuffer remapBuffer;

            // Add 2 empty rows
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ (DWORD)0, (DWORD)0 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ (DWORD)0, (DWORD)0 }), std::wstring()));

            // Validate and update the element when -1 i.e. null selection is made on an empty row.
            ValidateAndUpdateKeyBufferElementArgs args = { 0, 0, -1 };
            ShortcutErrorType error = BufferValidationHelpers::ValidateAndUpdateKeyBufferElement(args.elementRowIndex, args.elementColIndex, args.selectedCodeFromDropDown, remapBuffer);

            // Assert that the element is validated and buffer is updated
            Assert::AreEqual(true, error == ShortcutErrorType::NoError);
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
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ (DWORD)0, (DWORD)0 }), std::wstring()));

            // Validate and update the element when selecting B on an empty row
            ValidateAndUpdateKeyBufferElementArgs args = { 0, 0, 0x42 };
            ShortcutErrorType error = BufferValidationHelpers::ValidateAndUpdateKeyBufferElement(args.elementRowIndex, args.elementColIndex, args.selectedCodeFromDropDown, remapBuffer);

            // Assert that the element is validated and buffer is updated
            Assert::AreEqual(true, error == ShortcutErrorType::NoError);
            Assert::AreEqual((DWORD)0x42, std::get<DWORD>(remapBuffer[0].first[0]));
            Assert::AreEqual((DWORD)NULL, std::get<DWORD>(remapBuffer[0].first[1]));
        }

        // Test if the ValidateAndUpdateKeyBufferElement method is successful when setting a key to non-null in a valid key to key
        TEST_METHOD (ValidateAndUpdateKeyBufferElement_ShouldUpdateAndReturnNoError_OnSettingKeyToNonNullInAValidKeyToKeyRow)
        {
            RemapBuffer remapBuffer;

            // Add a row with A as the target
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ (DWORD)0, (DWORD)0x41 }), std::wstring()));

            // Validate and update the element when selecting B on a row
            ValidateAndUpdateKeyBufferElementArgs args = { 0, 0, 0x42 };
            ShortcutErrorType error = BufferValidationHelpers::ValidateAndUpdateKeyBufferElement(args.elementRowIndex, args.elementColIndex, args.selectedCodeFromDropDown, remapBuffer);

            // Assert that the element is validated and buffer is updated
            Assert::AreEqual(true, error == ShortcutErrorType::NoError);
            Assert::AreEqual((DWORD)0x42, std::get<DWORD>(remapBuffer[0].first[0]));
            Assert::AreEqual((DWORD)0x41, std::get<DWORD>(remapBuffer[0].first[1]));
        }

        // Test if the ValidateAndUpdateKeyBufferElement method is successful when setting a key to non-null in a valid key to shortcut
        TEST_METHOD (ValidateAndUpdateKeyBufferElement_ShouldUpdateAndReturnNoError_OnSettingKeyToNonNullInAValidKeyToShortcutRow)
        {
            RemapBuffer remapBuffer;

            // Add a row with Ctrl+A as the target
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ (DWORD)0, Shortcut(std::vector<int32_t>{ VK_CONTROL, 0x41 }) }), std::wstring()));

            // Validate and update the element when selecting B on a row
            ValidateAndUpdateKeyBufferElementArgs args = { 0, 0, 0x42 };
            ShortcutErrorType error = BufferValidationHelpers::ValidateAndUpdateKeyBufferElement(args.elementRowIndex, args.elementColIndex, args.selectedCodeFromDropDown, remapBuffer);

            // Assert that the element is validated and buffer is updated
            Assert::AreEqual(true, error == ShortcutErrorType::NoError);
            Assert::AreEqual((DWORD)0x42, std::get<DWORD>(remapBuffer[0].first[0]));
            Assert::AreEqual(true, Shortcut(std::vector<int32_t>{ VK_CONTROL, 0x41 }) == std::get<Shortcut>(remapBuffer[0].first[1]));
        }

        // Test if the ValidateAndUpdateKeyBufferElement method is unsuccessful when setting first column to the same value as the right column
        TEST_METHOD (ValidateAndUpdateKeyBufferElement_ShouldReturnMapToSameKeyError_OnSettingFirstColumnToSameValueAsRightColumn)
        {
            RemapBuffer remapBuffer;

            // Add a row with A as the target
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ (DWORD)0, (DWORD)0x41 }), std::wstring()));

            // Validate and update the element when selecting A on a row
            ValidateAndUpdateKeyBufferElementArgs args = { 0, 0, 0x41 };
            ShortcutErrorType error = BufferValidationHelpers::ValidateAndUpdateKeyBufferElement(args.elementRowIndex, args.elementColIndex, args.selectedCodeFromDropDown, remapBuffer);

            // Assert that the element is invalid and buffer is not updated
            Assert::AreEqual(true, error == ShortcutErrorType::MapToSameKey);
            Assert::AreEqual((DWORD)NULL, std::get<DWORD>(remapBuffer[0].first[0]));
            Assert::AreEqual((DWORD)0x41, std::get<DWORD>(remapBuffer[0].first[1]));
        }

        // Test if the ValidateAndUpdateKeyBufferElement method is unsuccessful when setting first column of a key to key row to the same value as in another row
        TEST_METHOD (ValidateAndUpdateKeyBufferElement_ShouldReturnSameKeyPreviouslyMappedError_OnSettingFirstColumnOfAKeyToKeyRowToSameValueAsInAnotherRow)
        {
            RemapBuffer remapBuffer;

            // Add a row from A->B and a row with C as target
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ (DWORD)0x41, (DWORD)0x42 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ (DWORD)0, (DWORD)0x43 }), std::wstring()));

            // Validate and update the element when selecting A on second row
            ValidateAndUpdateKeyBufferElementArgs args = { 1, 0, 0x41 };
            ShortcutErrorType error = BufferValidationHelpers::ValidateAndUpdateKeyBufferElement(args.elementRowIndex, args.elementColIndex, args.selectedCodeFromDropDown, remapBuffer);

            // Assert that the element is invalid and buffer is not updated
            Assert::AreEqual(true, error == ShortcutErrorType::SameKeyPreviouslyMapped);
            Assert::AreEqual((DWORD)NULL, std::get<DWORD>(remapBuffer[1].first[0]));
            Assert::AreEqual((DWORD)0x43, std::get<DWORD>(remapBuffer[1].first[1]));
        }

        // Test if the ValidateAndUpdateKeyBufferElement method is unsuccessful when setting first column of a key to shortcut row to the same value as in another row
        TEST_METHOD (ValidateAndUpdateKeyBufferElement_ShouldReturnSameKeyPreviouslyMappedError_OnSettingFirstColumnOfAKeyToShortcutRowToSameValueAsInAnotherRow)
        {
            RemapBuffer remapBuffer;

            // Add a row from A->B and a row with Ctrl+A as target
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ (DWORD)0x41, (DWORD)0x42 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ (DWORD)0, Shortcut(std::vector<int32_t>{ VK_CONTROL, 0x41 }) }), std::wstring()));

            // Validate and update the element when selecting A on second row
            ValidateAndUpdateKeyBufferElementArgs args = { 1, 0, 0x41 };
            ShortcutErrorType error = BufferValidationHelpers::ValidateAndUpdateKeyBufferElement(args.elementRowIndex, args.elementColIndex, args.selectedCodeFromDropDown, remapBuffer);

            // Assert that the element is invalid and buffer is not updated
            Assert::AreEqual(true, error == ShortcutErrorType::SameKeyPreviouslyMapped);
            Assert::AreEqual((DWORD)NULL, std::get<DWORD>(remapBuffer[1].first[0]));
            Assert::AreEqual(true, Shortcut(std::vector<int32_t>{ VK_CONTROL, 0x41 }) == std::get<Shortcut>(remapBuffer[1].first[1]));
        }

        // Test if the ValidateAndUpdateKeyBufferElement method is unsuccessful when setting first column of a key to key row to a conflicting modifier with another row
        TEST_METHOD (ValidateAndUpdateKeyBufferElement_ShouldReturnConflictingModifierKeyError_OnSettingFirstColumnOfAKeyToKeyRowToConflictingModifierWithAnotherRow)
        {
            RemapBuffer remapBuffer;

            // Add a row from Ctrl->B and a row with C as target
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ (DWORD)VK_CONTROL, (DWORD)0x42 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ (DWORD)0, (DWORD)0x43 }), std::wstring()));

            // Validate and update the element when selecting LCtrl on second row
            ValidateAndUpdateKeyBufferElementArgs args = { 1, 0, VK_LCONTROL };
            ShortcutErrorType error = BufferValidationHelpers::ValidateAndUpdateKeyBufferElement(args.elementRowIndex, args.elementColIndex, args.selectedCodeFromDropDown, remapBuffer);

            // Assert that the element is invalid and buffer is not updated
            Assert::AreEqual(true, error == ShortcutErrorType::ConflictingModifierKey);
            Assert::AreEqual((DWORD)NULL, std::get<DWORD>(remapBuffer[1].first[0]));
            Assert::AreEqual((DWORD)0x43, std::get<DWORD>(remapBuffer[1].first[1]));
        }

        // Test if the ValidateAndUpdateKeyBufferElement method is unsuccessful when setting first column of a key to shortcut row to a conflicting modifier with another row
        TEST_METHOD (ValidateAndUpdateKeyBufferElement_ShouldReturnConflictingModifierKeyError_OnSettingFirstColumnOfAKeyToShortcutRowToConflictingModifierWithAnotherRow)
        {
            RemapBuffer remapBuffer;

            // Add a row from Ctrl->B and a row with Ctrl+A as target
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ (DWORD)VK_CONTROL, (DWORD)0x42 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(RemapBufferItem({ (DWORD)0, Shortcut(std::vector<int32_t>{ VK_CONTROL, 0x41 }) }), std::wstring()));

            // Validate and update the element when selecting LCtrl on second row
            ValidateAndUpdateKeyBufferElementArgs args = { 1, 0, VK_LCONTROL };
            ShortcutErrorType error = BufferValidationHelpers::ValidateAndUpdateKeyBufferElement(args.elementRowIndex, args.elementColIndex, args.selectedCodeFromDropDown, remapBuffer);

            // Assert that the element is invalid and buffer is not updated
            Assert::AreEqual(true, error == ShortcutErrorType::ConflictingModifierKey);
            Assert::AreEqual((DWORD)NULL, std::get<DWORD>(remapBuffer[1].first[0]));
            Assert::AreEqual(true, Shortcut(std::vector<int32_t>{ VK_CONTROL, 0x41 }) == std::get<Shortcut>(remapBuffer[1].first[1]));
        }

        // Test if the ValidateShortcutBufferElement method is successful and no drop down action is required on setting a column to null in a new or valid row
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoErrorAndNoAction_OnSettingColumnToNullInANewOrValidRow)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when making null-selection (-1 index) on first column of empty shortcut to shortcut row
            testCases.push_back({ 0, 0, 0, std::vector<int32_t>{ -1 }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 2: Validate the element when making null-selection (-1 index) on second column of empty shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ -1 }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 3: Validate the element when making null-selection (-1 index) on first column of empty shortcut to key row
            testCases.push_back({ 0, 0, 0, std::vector<int32_t>{ -1 }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), (DWORD)0 }, std::wstring()) });
            // Case 4: Validate the element when making null-selection (-1 index) on second column of empty shortcut to key row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ -1 }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), (DWORD)0 }, std::wstring()) });
            // Case 5: Validate the element when making null-selection (-1 index) on first dropdown of first column of valid shortcut to shortcut row
            testCases.push_back({ 0, 0, 0, std::vector<int32_t>({ -1, 0x43 }), std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x41 } }, std::wstring()) });
            // Case 6: Validate the element when making null-selection (-1 index) on first dropdown of second column of valid shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>({ -1, 0x41 }), std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x41 } }, std::wstring()) });
            // Case 7: Validate the element when making null-selection (-1 index) on first dropdown of second column of valid hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>({ -1, 0x41 }), std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x41 } }, std::wstring()) });
            // Case 8: Validate the element when making null-selection (-1 index) on second dropdown of first column of valid shortcut to shortcut row
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>({ VK_CONTROL, -1 }), std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x41 } }, std::wstring()) });
            // Case 9: Validate the element when making null-selection (-1 index) on second dropdown of second column of valid shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>({ VK_CONTROL, -1 }), std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x41 } }, std::wstring()) });
            // Case 10: Validate the element when making null-selection (-1 index) on second dropdown of second column of valid hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>({ VK_CONTROL, -1 }), std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x41 } }, std::wstring()) });
            // Case 11: Validate the element when making null-selection (-1 index) on first dropdown of first column of valid 3 key shortcut to shortcut row
            testCases.push_back({ 0, 0, 0, std::vector<int32_t>({ -1, VK_SHIFT, 0x44 }), std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x44 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x42 } }, std::wstring()) });
            // Case 12: Validate the element when making null-selection (-1 index) on first dropdown of second column of valid 3 key shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>({ -1, VK_SHIFT, 0x42 }), std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x44 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x42 } }, std::wstring()) });
            // Case 13: Validate the element when making null-selection (-1 index) on first dropdown of second column of valid hybrid 3 key shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>({ -1, VK_SHIFT, 0x42 }), std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x44 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x42 } }, std::wstring()) });
            // Case 14: Validate the element when making null-selection (-1 index) on second dropdown of first column of valid 3 key shortcut to shortcut row
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>({ VK_CONTROL, -1, 0x44 }), std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x44 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x42 } }, std::wstring()) });
            // Case 15: Validate the element when making null-selection (-1 index) on second dropdown of second column of valid 3 key shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>({ VK_CONTROL, -1, 0x42 }), std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x44 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x42 } }, std::wstring()) });
            // Case 16: Validate the element when making null-selection (-1 index) on second dropdown of second column of valid hybrid 3 key shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>({ VK_CONTROL, -1, 0x42 }), std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x44 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x42 } }, std::wstring()) });
            // Case 17: Validate the element when making null-selection (-1 index) on third dropdown of first column of valid 3 key shortcut to shortcut row
            testCases.push_back({ 0, 0, 2, std::vector<int32_t>({ VK_CONTROL, VK_SHIFT, -1 }), std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x44 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x42 } }, std::wstring()) });
            // Case 18: Validate the element when making null-selection (-1 index) on third dropdown of second column of valid 3 key shortcut to shortcut row
            testCases.push_back({ 0, 1, 2, std::vector<int32_t>({ VK_CONTROL, VK_SHIFT, -1 }), std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x44 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x42 } }, std::wstring()) });
            // Case 19: Validate the element when making null-selection (-1 index) on third dropdown of second column of valid hybrid 3 key shortcut to shortcut row
            testCases.push_back({ 0, 1, 2, std::vector<int32_t>({ VK_CONTROL, VK_SHIFT, -1 }), std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x44 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x42 } }, std::wstring()) });
            // Case 20: Validate the element when making null-selection (-1 index) on fourth dropdown of first column of valid 4 key shortcut to shortcut row
            testCases.push_back({ 0, 0, 3, std::vector<int32_t>({ VK_CONTROL, VK_SHIFT, VK_MENU, -1 }), std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, VK_MENU, 0x44 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, VK_MENU, 0x42 } }, std::wstring()) });
            // Case 21: Validate the element when making null-selection (-1 index) on fourth dropdown of second column of valid 4 key shortcut to shortcut row
            testCases.push_back({ 0, 1, 3, std::vector<int32_t>({ VK_CONTROL, VK_SHIFT, VK_MENU, -1 }), std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, VK_MENU, 0x44 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, VK_MENU, 0x42 } }, std::wstring()) });
            // Case 22: Validate the element when making null-selection (-1 index) on fourth dropdown of second column of valid hybrid 4 key shortcut to shortcut row
            testCases.push_back({ 0, 1, 3, std::vector<int32_t>({ VK_CONTROL, VK_SHIFT, VK_MENU, -1 }), std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, VK_MENU, 0x44 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, VK_MENU, 0x42 } }, std::wstring()) });
            // Case 23: Validate the element when making null-selection (-1 index) on fifth dropdown of first column of valid 5 key shortcut to shortcut row
            testCases.push_back({ 0, 0, 3, std::vector<int32_t>({ VK_CONTROL, VK_SHIFT, VK_MENU, VK_LWIN, -1 }), std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, VK_MENU, VK_LWIN, 0x44 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, VK_MENU, VK_LWIN, 0x42 } }, std::wstring()) });
            // Case 24: Validate the element when making null-selection (-1 index) on fifth dropdown of second column of valid 5 key shortcut to shortcut row
            testCases.push_back({ 0, 1, 3, std::vector<int32_t>({ VK_CONTROL, VK_SHIFT, VK_MENU, VK_LWIN, -1 }), std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, VK_MENU, VK_LWIN, 0x44 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, VK_MENU, VK_LWIN, 0x42 } }, std::wstring()) });
            // Case 25: Validate the element when making null-selection (-1 index) on fifth dropdown of second column of valid hybrid 5 key shortcut to shortcut row
            testCases.push_back({ 0, 1, 3, std::vector<int32_t>({ VK_CONTROL, VK_SHIFT, VK_MENU, VK_LWIN, -1 }), std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, VK_MENU, VK_LWIN, 0x44 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, VK_MENU, VK_LWIN, 0x42 } }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is valid and no drop down action is required
                Assert::AreEqual(true, result.first == ShortcutErrorType::NoError);
                Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutStartWithModifier error and no drop down action is required on setting first drop down to an action key on a non-hybrid control column
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutStartWithModifierErrorAndNoAction_OnSettingFirstDropDownToActionKeyOnANonHybridColumn)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting A (0x41) on first dropdown of first column of empty shortcut to shortcut row
            testCases.push_back({ 0, 0, 0, std::vector<int32_t>{ 0x41 }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 2: Validate the element when selecting A (0x41) on first dropdown of second column of empty shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ 0x41 }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 3: Validate the element when selecting A (0x41) on first dropdown of first column of empty shortcut to key row
            testCases.push_back({ 0, 0, 0, std::vector<int32_t>{ 0x41 }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), (DWORD)0 }, std::wstring()) });
            // Case 4: Validate the element when selecting A (0x41) on first dropdown of first column of valid shortcut to shortcut row
            testCases.push_back({ 0, 0, 0, std::vector<int32_t>{ 0x41, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x41 } }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is invalid and no drop down action is required
                Assert::AreEqual(true, result.first == ShortcutErrorType::ShortcutStartWithModifier);
                Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns no error and no drop down action is required on setting first drop down to an action key on an empty hybrid control column
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoErrorAndNoAction_OnSettingFirstDropDownToActionKeyOnAnEmptyHybridColumn)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting A (0x41) on first dropdown of second column of empty shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ 0x41 }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 2: Validate the element when selecting A (0x41) on first dropdown of second column of empty shortcut to key row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ 0x41 }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), (DWORD)0 }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is valid and no drop down action is required
                Assert::AreEqual(true, result.first == ShortcutErrorType::NoError);
                Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutNotMoreThanOneActionKey error and no drop down action is required on setting first drop down to an action key on a hybrid control column with full shortcut
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutNotMoreThanOneActionKeyAndNoAction_OnSettingNonLastDropDownToActionKeyOnAHybridColumnWithFullShortcut)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting A (0x41) on first dropdown of second column of hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ 0x41, 0x43 }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), std::vector<int32_t>{ VK_CONTROL, 0x43 } }, std::wstring()) });
            // Case 2: Validate the element when selecting A (0x41) on second dropdown of second column of hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, 0x41, 0x42 }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 } }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is invalid and no drop down action is required
                Assert::AreEqual(true, result.first == ShortcutErrorType::ShortcutNotMoreThanOneActionKey);
                Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutNotMoreThanOneActionKey error and no drop down action is required on setting non first non last drop down to an action key on a non hybrid control column with full shortcut
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutNotMoreThanOneActionKeyAndNoAction_OnSettingNonFirstNonLastDropDownToActionKeyOnANonHybridColumnWithFullShortcut)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting A (0x41) on second dropdown of first column of shortcut to shortcut row
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>{ VK_CONTROL, 0x41, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x42 } }, std::wstring()) });
            // Case 2: Validate the element when selecting A  (0x41)on second dropdown of second column of shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, 0x41, 0x42 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x42 } }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is invalid and no drop down action is required
                Assert::AreEqual(true, result.first == ShortcutErrorType::ShortcutNotMoreThanOneActionKey);
                Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns no error and no drop down action is required on setting last drop down to an action key on a column with atleast two drop downs
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoErrorAndNoAction_OnSettingLastDropDownToActionKeyOnAColumnWithAtleastTwoDropDowns)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting A (0x41) on last dropdown of first column of three key shortcut to shortcut row
            testCases.push_back({ 0, 0, 2, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x41 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x42 } }, std::wstring()) });
            // Case 2: Validate the element when selecting A (0x41) on last dropdown of second column of three key shortcut to shortcut row
            testCases.push_back({ 0, 1, 2, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x41 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x42 } }, std::wstring()) });
            // Case 3: Validate the element when selecting A (0x41) on last dropdown of hybrid second column of three key shortcut to shortcut row
            testCases.push_back({ 0, 1, 2, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x41 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x42 } }, std::wstring()) });
            // Case 4: Validate the element when selecting A (0x41) on last dropdown of first column of two key shortcut to shortcut row
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>{ VK_CONTROL, 0x41 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 5: Validate the element when selecting A (0x41) on last dropdown of second column of two key shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, 0x41 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 6: Validate the element when selecting A (0x41) on last dropdown of hybrid second column of two key shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, 0x41 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is valid and no drop down action is required
                Assert::AreEqual(true, result.first == ShortcutErrorType::NoError);
                Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns no error and ClearUnusedDropDowns action is required on setting non first drop down to an action key on a column if all the drop downs after it are empty
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoErrorAndClearUnusedDropDownsAction_OnSettingNonFirstDropDownToActionKeyOnAColumnIfAllTheDropDownsAfterItAreEmpty)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting A (0x41) on second dropdown of first column of 3 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>{ VK_CONTROL, 0x41, -1 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 2: Validate the element when selecting A (0x41) on second dropdown of second column of 3 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, 0x41, -1 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 3: Validate the element when selecting A (0x41) on second dropdown of second column of 3 dropdown hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, 0x41, -1 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 4: Validate the element when selecting A (0x41) on second dropdown of first column of empty 3 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>{ -1, 0x41, -1 }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 5: Validate the element when selecting A (0x41) on second dropdown of second column of empty 3 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ -1, 0x41, -1 }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 6: Validate the element when selecting A (0x41) on second dropdown of second column of empty 3 dropdown hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ -1, 0x41, -1 }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is valid and ClearUnusedDropDowns action is required
                Assert::AreEqual(true, result.first == ShortcutErrorType::NoError);
                Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::ClearUnusedDropDowns);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns no error and ClearUnusedDropDowns action is required on setting first drop down to an action key on a hybrid column if all the drop downs after it are empty
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoErrorAndClearUnusedDropDownsAction_OnSettingFirstDropDownToActionKeyOnAHybridColumnIfAllTheDropDownsAfterItAreEmpty)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting A (0x41) on first dropdown of second column of empty 3 dropdown hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ 0x41, -1, -1 }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is valid and ClearUnusedDropDowns action is required
                Assert::AreEqual(true, result.first == ShortcutErrorType::NoError);
                Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::ClearUnusedDropDowns);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns no error and AddDropDown action is required on setting last drop down to a non-repeated modifier key on a column there are less than 3 drop downs
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoErrorAndAddDropDownAction_OnSettingLastDropDownToNonRepeatedModifierKeyOnAColumnIfThereAreLessThan3DropDowns)
        {
            

            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting Shift (VK_SHIFT) on second dropdown of first column of 2 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 2: Validate the element when selecting Shift (VK_SHIFT) on second dropdown of second column of 2 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 3: Validate the element when selecting Shift (VK_SHIFT) on second dropdown of second column of 2 dropdown hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 4: Validate the element when selecting Shift (VK_SHIFT) on first dropdown of first column of 1 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 0, 0, std::vector<int32_t>{ VK_SHIFT }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 5: Validate the element when selecting Shift (VK_SHIFT) on first dropdown of second column of 1 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ VK_SHIFT }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 6: Validate the element when selecting Shift (VK_SHIFT) on first dropdown of second column of 1 dropdown hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ VK_SHIFT }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 7: Validate the element when selecting Shift (VK_SHIFT) on first dropdown of second column of 1 dropdown hybrid shortcut to key row with an action key selected
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ VK_SHIFT }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), (DWORD)0x44 }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is valid and AddDropDown action is required
                Assert::AreEqual(true, result.first == ShortcutErrorType::NoError);
                Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::AddDropDown);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutCannotHaveRepeatedModifier error and no action is required on setting last drop down to a repeated modifier key on a column there are less than 3 drop downs
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutCannotHaveRepeatedModifierErrorAndNoAction_OnSettingLastDropDownToRepeatedModifierKeyOnAColumnIfThereAreLessThan3DropDowns)
        {
            

            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting LCtrl (VK_LCONTROL) on second dropdown of first column of 2 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>{ VK_CONTROL, VK_LCONTROL }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 2: Validate the element when selecting LCtrl (VK_LCONTROL) on second dropdown of second column of 2 dropdown hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, VK_LCONTROL }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 3: Validate the element when selecting LCtrl (VK_LCONTROL) on second dropdown of second column of 2 dropdown hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, VK_LCONTROL }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is invalid and no action is required
                Assert::AreEqual(true, result.first == ShortcutErrorType::ShortcutCannotHaveRepeatedModifier);
                Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutMaxShortcutSizeOneActionKey error and no action is required on setting last drop down to a non repeated modifier key on a column there 3 or more drop downs
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutMaxShortcutSizeOneActionKeyErrorAndNoAction_OnSettingLastDropDownToNonRepeatedModifierKeyOnAColumnIfThereAre3OrMoreDropDowns)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting Shift (VK_SHIFT) on last dropdown of first column of 5 dropdown shortcut to shortcut row with middle empty
            testCases.push_back({ 0, 0, 4, std::vector<int32_t>{ VK_CONTROL, -1, -1, -1, VK_SHIFT }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 2: Validate the element when selecting Shift (VK_SHIFT) on last dropdown of second column of 5 dropdown shortcut to shortcut row with middle empty
            testCases.push_back({ 0, 1, 4, std::vector<int32_t>{ VK_CONTROL, -1, -1, -1, VK_SHIFT }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 3: Validate the element when selecting Shift (VK_SHIFT) on last dropdown of second column of 5 dropdown hybrid shortcut to shortcut row with middle empty
            testCases.push_back({ 0, 1, 4, std::vector<int32_t>{ VK_CONTROL, -1, -1, -1, VK_SHIFT }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 4: Validate the element when selecting Shift (VK_SHIFT) on last dropdown of first column of 5 dropdown shortcut to shortcut row with first four empty
            testCases.push_back({ 0, 0, 4, std::vector<int32_t>{ -1, -1, -1, -1, VK_SHIFT }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 5: Validate the element when selecting Shift (VK_SHIFT) on last dropdown of second column of 5 dropdown shortcut to shortcut row with first four empty
            testCases.push_back({ 0, 1, 4, std::vector<int32_t>{ -1, -1, -1, -1, VK_SHIFT }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 6: Validate the element when selecting Shift (VK_SHIFT) on last dropdown of second column of 5 dropdown hybrid shortcut to shortcut row with first four empty
            testCases.push_back({ 0, 1, 4, std::vector<int32_t>{ -1, -1, -1, -1, VK_SHIFT }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 7: Validate the element when selecting Shift (VK_SHIFT) on last dropdown of first column of 5 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 0, 4, std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_LWIN, VK_RWIN, VK_SHIFT }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_LWIN, VK_RWIN, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_LWIN, VK_RWIN, 0x42 } }, std::wstring()) });
            // Case 8: Validate the element when selecting Shift (VK_SHIFT) on last dropdown of second column of 5 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 1, 4, std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_LWIN, VK_RWIN, VK_SHIFT }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_LWIN, VK_RWIN, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_LWIN, VK_RWIN, 0x42 } }, std::wstring()) });
            // Case 9: Validate the element when selecting Shift (VK_SHIFT) on last dropdown of second column of 5 dropdown hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 4, std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_LWIN, VK_RWIN, VK_SHIFT }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_LWIN, VK_RWIN, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_LWIN, VK_RWIN, 0x42 } }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is invalid and no action is required
                Assert::AreEqual(true, result.first == ShortcutErrorType::ShortcutMaxShortcutSizeOneActionKey);
                Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutMaxShortcutSizeOneActionKey error and no action is required on setting last drop down to a repeated modifier key on a column there 3 or more drop downs
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutMaxShortcutSizeOneActionKeyErrorAndNoAction_OnSettingLastDropDownToRepeatedModifierKeyOnAColumnIfThereAre3OrMoreDropDowns)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting Ctrl (VK_CONTROL) on last dropdown of first column of 5 dropdown shortcut to shortcut row with middle empty
            testCases.push_back({ 0, 0, 4, std::vector<int32_t>{ VK_CONTROL, -1, -1, -1, VK_CONTROL }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 2: Validate the element when selecting Ctrl (VK_CONTROL) on last dropdown of second column of 5 dropdown shortcut to shortcut row with middle empty
            testCases.push_back({ 0, 1, 4, std::vector<int32_t>{ VK_CONTROL, -1, -1, -1, VK_CONTROL }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 3: Validate the element when selecting Ctrl (VK_CONTROL) on last dropdown of second column of 5 dropdown hybrid shortcut to shortcut row with middle empty
            testCases.push_back({ 0, 1, 4, std::vector<int32_t>{ VK_CONTROL, -1, -1, -1, VK_CONTROL }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 4: Validate the element when selecting Ctrl (VK_CONTROL) on last dropdown of first column of 5 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 0, 4, std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_LWIN, VK_SHIFT, VK_CONTROL }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_LWIN, VK_SHIFT, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_LWIN, VK_SHIFT, 0x42 } }, std::wstring()) });
            // Case 5: Validate the element when selecting Ctrl (VK_CONTROL) on last dropdown of second column of 5 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 1, 4, std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_LWIN, VK_SHIFT, VK_CONTROL }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_LWIN, VK_SHIFT, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_LWIN, VK_SHIFT, 0x42 } }, std::wstring()) });
            // Case 6: Validate the element when selecting Ctrl (VK_CONTROL) on last dropdown of second column of 5 dropdown hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 4, std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_LWIN, VK_SHIFT, VK_CONTROL }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_LWIN, VK_SHIFT, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_LWIN, VK_SHIFT, 0x42 } }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is invalid and no action is required
                Assert::AreEqual(true, result.first == ShortcutErrorType::ShortcutMaxShortcutSizeOneActionKey);
                Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns no error and no action is required on setting non-last drop down to a non repeated modifier key on a column
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoErrorAndNoAction_OnSettingNonLastDropDownToNonRepeatedModifierKeyOnAColumn)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting Shift (VK_SHIFT) on first dropdown of first column of 2 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 0, 0, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 2: Validate the element when selecting Shift (VK_SHIFT) on first dropdown of second column of 2 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 3: Validate the element when selecting Shift (VK_SHIFT) on first dropdown of second column of 2 dropdown hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 4: Validate the element when selecting Shift (VK_SHIFT) on first dropdown of first column of 3 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 0, 0, std::vector<int32_t>{ VK_SHIFT, VK_MENU, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x42 } }, std::wstring()) });
            // Case 5: Validate the element when selecting Shift (VK_SHIFT) on first dropdown of second column of 3 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ VK_SHIFT, VK_MENU, 0x42 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x42 } }, std::wstring()) });
            // Case 6: Validate the element when selecting Shift (VK_SHIFT) on first dropdown of second column of 3 dropdown hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ VK_SHIFT, VK_MENU, 0x42 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x42 } }, std::wstring()) });
            // Case 7: Validate the element when selecting Shift (VK_SHIFT) on second dropdown of first column of 3 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x42 } }, std::wstring()) });
            // Case 8: Validate the element when selecting Shift (VK_SHIFT) on second dropdown of second column of 3 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x42 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x42 } }, std::wstring()) });
            // Case 9: Validate the element when selecting Shift (VK_SHIFT) on second dropdown of second column of 3 dropdown hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x42 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x42 } }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is valid and no action is required
                Assert::AreEqual(true, result.first == ShortcutErrorType::NoError);
                Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutCannotHaveRepeatedModifier error and no action is required on setting non-last drop down to a repeated modifier key on a column
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutCannotHaveRepeatedModifierErrorAndNoAction_OnSettingNonLastDropDownToRepeatedModifierKeyOnAColumn)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting Ctrl (VK_CONTROL) on first dropdown of first column of 3 dropdown shortcut to shortcut row with first empty
            testCases.push_back({ 0, 0, 0, std::vector<int32_t>{ VK_CONTROL, VK_CONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 2: Validate the element when selecting Ctrl (VK_CONTROL) on first dropdown of second column of 3 dropdown shortcut to shortcut row with first empty
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ VK_CONTROL, VK_CONTROL, 0x42 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 3: Validate the element when selecting Ctrl (VK_CONTROL) on first dropdown of second column of 3 dropdown hybrid shortcut to shortcut row with first empty
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ VK_CONTROL, VK_CONTROL, 0x42 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 4: Validate the element when selecting Alt (VK_MENU) on first dropdown of first column of 3 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 0, 0, std::vector<int32_t>{ VK_MENU, VK_MENU, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x42 } }, std::wstring()) });
            // Case 5: Validate the element when selecting Alt (VK_MENU) on first dropdown of second column of 3 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ VK_MENU, VK_MENU, 0x42 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x42 } }, std::wstring()) });
            // Case 6: Validate the element when selecting Alt (VK_MENU) on first dropdown of second column of 3 dropdown hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ VK_MENU, VK_MENU, 0x42 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x42 } }, std::wstring()) });
            // Case 7: Validate the element when selecting Ctrl (VK_CONTROL) on second dropdown of first column of 3 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>{ VK_CONTROL, VK_CONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x42 } }, std::wstring()) });
            // Case 8: Validate the element when selecting Ctrl (VK_CONTROL) on second dropdown of second column of 3 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, VK_CONTROL, 0x42 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x42 } }, std::wstring()) });
            // Case 9: Validate the element when selecting Ctrl (VK_CONTROL) on second dropdown of second column of 3 dropdown hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, VK_CONTROL, 0x42 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x42 } }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is invalid and no action is required
                Assert::AreEqual(true, result.first == ShortcutErrorType::ShortcutCannotHaveRepeatedModifier);
                Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutStartWithModifier error and no action is required on setting first drop down to None on a non-hybrid column with one drop down
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutStartWithModifierErrorAndNoAction_OnSettingFirstDropDownToNoneOnNonHybridColumnWithOneDropDown)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting None (0) on first dropdown of first column of 1 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 0, 0, std::vector<int32_t>{ 0 }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 2: Validate the element when selecting None (0) on first dropdown of second column of 1 dropdown shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ 0 }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is invalid and no action is required
                Assert::AreEqual(true, result.first == ShortcutErrorType::ShortcutStartWithModifier);
                Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutOneActionKey error and no action is required on setting first drop down to None on a hybrid column with one drop down
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutOneActionKeyErrorAndNoAction_OnSettingFirstDropDownToNoneOnHybridColumnWithOneDropDown)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting None (0) on first dropdown of first column of 1 dropdown hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ 0 }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is invalid and no action is required
                Assert::AreEqual(true, result.first == ShortcutErrorType::ShortcutOneActionKey);
                Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutAtleast2Keys error and no action is required on setting first drop down to None on a non-hybrid column with two drop down
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutAtleast2KeysAndNoAction_OnSettingFirstDropDownToNoneOnNonHybridColumnWithTwoDropDowns)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting None (0) on first dropdown of first column of 2 dropdown empty shortcut to shortcut row
            testCases.push_back({ 0, 0, 0, std::vector<int32_t>{ 0, -1 }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 2: Validate the element when selecting None (0) on first dropdown of second column of 2 dropdown empty shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ 0, -1 }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 3: Validate the element when selecting None (0) on first dropdown of second column of 2 dropdown valid shortcut to shortcut row
            testCases.push_back({ 0, 0, 0, std::vector<int32_t>{ 0, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 4: Validate the element when selecting None (0) on first dropdown of second column of 2 dropdown valid shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ 0, 0x42 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is invalid and no action is required
                Assert::AreEqual(true, result.first == ShortcutErrorType::ShortcutAtleast2Keys);
                Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutOneActionKey error and no action is required on setting second drop down to None on a non-hybrid column with two drop down
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutOneActionKeyAndNoAction_OnSettingSecondDropDownToNoneOnNonHybridColumnWithTwoDropDowns)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting None (0) on second dropdown of first column of 2 dropdown empty shortcut to shortcut row
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>{ -1, 0 }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 2: Validate the element when selecting None (0) on second dropdown of second column of 2 dropdown empty shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ -1, 0 }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 3: Validate the element when selecting None (0) on second dropdown of second column of 2 dropdown valid shortcut to shortcut row
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>{ VK_CONTROL, 0 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 4: Validate the element when selecting None (0) on second dropdown of second column of 2 dropdown valid shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, 0 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is invalid and no action is required
                Assert::AreEqual(true, result.first == ShortcutErrorType::ShortcutOneActionKey);
                Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns no error and DeleteDropDown action is required on setting drop down to None on a hybrid column with two drop down
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoErrorAndDeleteDropDownAction_OnSettingDropDownToNoneOnHybridColumnWithTwoDropDowns)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting None (0) on first dropdown of second column of 2 dropdown empty hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ 0, -1 }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 2: Validate the element when selecting None (0) on second dropdown of second column of 2 dropdown empty hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ -1, 0 }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 3: Validate the element when selecting None (0) on first dropdown of second column of 2 dropdown valid hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ 0, VK_CONTROL }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });
            // Case 4: Validate the element when selecting None (0) on second dropdown of second column of 2 dropdown valid hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, 0 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x42 } }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is valid and DeleteDropDown action is required
                Assert::AreEqual(true, result.first == ShortcutErrorType::NoError);
                Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::DeleteDropDown);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns no error and DeleteDropDown action is required on setting non last drop down to None on a column with three drop down
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoErrorAndDeleteDropDownAction_OnSettingNonLastDropDownToNoneOnColumnWithThreeDropDowns)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting None (0) on first dropdown of first column of 3 dropdown empty shortcut to shortcut row
            testCases.push_back({ 0, 0, 0, std::vector<int32_t>{ 0, -1, -1 }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 2: Validate the element when selecting None (0) on first dropdown of second column of 3 dropdown empty shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ 0, -1, -1 }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 3: Validate the element when selecting None (0) on first dropdown of second column of 3 dropdown empty hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ 0, -1, -1 }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 4: Validate the element when selecting None (0) on second dropdown of first column of 3 dropdown empty shortcut to shortcut row
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>{ -1, 0, -1 }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 5: Validate the element when selecting None (0) on second dropdown of second column of 3 dropdown empty shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ -1, 0, -1 }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 6: Validate the element when selecting None (0) on second dropdown of second column of 3 dropdown empty hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ -1, 0, -1 }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 7: Validate the element when selecting None (0) on first dropdown of first column of 3 dropdown valid shortcut to shortcut row
            testCases.push_back({ 0, 0, 0, std::vector<int32_t>{ 0, VK_MENU, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x42 } }, std::wstring()) });
            // Case 8: Validate the element when selecting None (0) on first dropdown of second column of 3 dropdown valid shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ 0, VK_MENU, 0x42 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x42 } }, std::wstring()) });
            // Case 9: Validate the element when selecting None (0) on first dropdown of second column of 3 dropdown valid hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ 0, VK_MENU, 0x42 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x42 } }, std::wstring()) });
            // Case 10: Validate the element when selecting None (0) on first dropdown of first column of 3 dropdown valid shortcut to shortcut row
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>{ VK_CONTROL, 0, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x42 } }, std::wstring()) });
            // Case 11: Validate the element when selecting None (0) on first dropdown of second column of 3 dropdown valid shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, 0, 0x42 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x42 } }, std::wstring()) });
            // Case 12: Validate the element when selecting None (0) on first dropdown of second column of 3 dropdown valid hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, 0, 0x42 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x42 } }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is valid and DeleteDropDown action is required
                Assert::AreEqual(true, result.first == ShortcutErrorType::NoError);
                Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::DeleteDropDown);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns ShortcutOneActionKey error and no action is required on setting last drop down to None on a column with three drop down
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnShortcutOneActionKeyErrorAndNoAction_OnSettingLastDropDownToNoneOnColumnWithThreeDropDowns)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting None (0) on first dropdown of first column of 3 dropdown empty shortcut to shortcut row
            testCases.push_back({ 0, 0, 2, std::vector<int32_t>{ -1, -1, 0 }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 2: Validate the element when selecting None (0) on first dropdown of second column of 3 dropdown empty shortcut to shortcut row
            testCases.push_back({ 0, 1, 2, std::vector<int32_t>{ -1, -1, 0 }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 3: Validate the element when selecting None (0) on first dropdown of second column of 3 dropdown empty hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 2, std::vector<int32_t>{ -1, -1, 0 }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring()) });
            // Case 4: Validate the element when selecting None (0) on first dropdown of first column of 3 dropdown valid shortcut to shortcut row
            testCases.push_back({ 0, 0, 2, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x42 } }, std::wstring()) });
            // Case 5: Validate the element when selecting None (0) on first dropdown of second column of 3 dropdown valid shortcut to shortcut row
            testCases.push_back({ 0, 1, 2, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x42 } }, std::wstring()) });
            // Case 6: Validate the element when selecting None (0) on first dropdown of second column of 3 dropdown valid hybrid shortcut to shortcut row
            testCases.push_back({ 0, 1, 2, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_MENU, 0x42 } }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is invalid and no action is required
                Assert::AreEqual(true, result.first == ShortcutErrorType::ShortcutOneActionKey);
                Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns WinL error on setting a drop down to Win or L on a column resulting in Win+L
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnWinLError_OnSettingDropDownToWinOrLOnColumnResultingInWinL)
        {
            

            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting L (0x4C) on second dropdown of first column of LWin+Empty shortcut
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>{ VK_LWIN, 0x4C }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_LWIN }, Shortcut() }, std::wstring()) });
            // Case 2: Validate the element when selecting L (0x4C) on second dropdown of second column of LWin+Empty shortcut
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_LWIN, 0x4C }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), std::vector<int32_t>{ VK_LWIN } }, std::wstring()) });
            // Case 3: Validate the element when selecting L (0x4C) on second dropdown of second column of hybrid LWin+Empty shortcut
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_LWIN, 0x4C }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), std::vector<int32_t>{ VK_LWIN } }, std::wstring()) });
            // Case 4: Validate the element when selecting L (0x4C) on second dropdown of first column of Win+Empty shortcut
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>{ CommonSharedConstants::VK_WIN_BOTH, 0x4C }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ CommonSharedConstants::VK_WIN_BOTH }, Shortcut() }, std::wstring()) });
            // Case 5: Validate the element when selecting L (0x4C) on second dropdown of second column of Win+Empty shortcut
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ CommonSharedConstants::VK_WIN_BOTH, 0x4C }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), std::vector<int32_t>{ CommonSharedConstants::VK_WIN_BOTH } }, std::wstring()) });
            // Case 6: Validate the element when selecting L (0x4C) on second dropdown of second column of hybrid Win+Empty shortcut
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ CommonSharedConstants::VK_WIN_BOTH, 0x4C }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), std::vector<int32_t>{ CommonSharedConstants::VK_WIN_BOTH } }, std::wstring()) });
            // Case 7: Validate the element when selecting LWin (VK_LWIN) on first dropdown of first column of Empty+L shortcut
            testCases.push_back({ 0, 0, 0, std::vector<int32_t>{ VK_LWIN, 0x4C }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ 0x4C }, Shortcut() }, std::wstring()) });
            // Case 8: Validate the element when selecting LWin (VK_LWIN) on first dropdown of second column of Empty+L shortcut
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ VK_LWIN, 0x4C }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), std::vector<int32_t>{ 0x4C } }, std::wstring()) });
            // Case 9: Validate the element when selecting LWin (VK_LWIN) on first dropdown of second column of hybrid Empty+L shortcut
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ VK_LWIN, 0x4C }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), std::vector<int32_t>{ 0x4C } }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is invalid
                Assert::AreEqual(true, result.first == ShortcutErrorType::WinL);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns WinL error on setting a drop down to null or none on a column resulting in Win+L
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnWinLError_OnSettingDropDownToNullOrNoneOnColumnResultingInWinL)
        {
            

            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting Null (-1) on second dropdown of first column of LWin + Ctrl + L shortcut
            testCases.push_back({ 0, 0, 2, std::vector<int32_t>{ VK_LWIN, -1, 0x4C }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_LWIN, VK_CONTROL, 0x4C }, Shortcut() }, std::wstring()) });
            // Case 2: Validate the element when selecting Null (-1) on second dropdown of second column of LWin + Ctrl + L shortcut
            testCases.push_back({ 0, 1, 2, std::vector<int32_t>{ VK_LWIN, -1, 0x4C }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), std::vector<int32_t>{ VK_LWIN, VK_CONTROL, 0x4C } }, std::wstring()) });
            // Case 3: Validate the element when selecting Null (-1) on second dropdown of second column of hybrid LWin + Ctrl + L shortcut
            testCases.push_back({ 0, 1, 2, std::vector<int32_t>{ VK_LWIN, -1, 0x4C }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), std::vector<int32_t>{ VK_LWIN, VK_CONTROL, 0x4C } }, std::wstring()) });
            // Case 4: Validate the element when selecting None (0) on second dropdown of first column of LWin + Ctrl + L shortcut
            testCases.push_back({ 0, 0, 2, std::vector<int32_t>{ VK_LWIN, 0, 0x4C }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_LWIN, VK_CONTROL, 0x4C }, Shortcut() }, std::wstring()) });
            // Case 5: Validate the element when selecting None (0) on second dropdown of second column of LWin + Ctrl + L shortcut
            testCases.push_back({ 0, 1, 2, std::vector<int32_t>{ VK_LWIN, 0, 0x4C }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), std::vector<int32_t>{ VK_LWIN, VK_CONTROL, 0x4C } }, std::wstring()) });
            // Case 6: Validate the element when selecting None (0) on second dropdown of second column of hybrid LWin + Ctrl + L shortcut
            testCases.push_back({ 0, 1, 2, std::vector<int32_t>{ VK_LWIN, 0, 0x4C }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), std::vector<int32_t>{ VK_LWIN, VK_CONTROL, 0x4C } }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is invalid
                Assert::AreEqual(true, result.first == ShortcutErrorType::WinL);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns CtrlAltDel error on setting a drop down to Ctrl, Alt or Del on a column resulting in Ctrl+Alt+Del
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnCtrlAltDelError_OnSettingDropDownToCtrlAltOrDelOnColumnResultingInCtrlAltDel)
        {
            

            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting Del (VK_DELETE) on third dropdown of first column of Ctrl+Alt+Empty shortcut
            testCases.push_back({ 0, 0, 2, std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_DELETE }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_MENU }, Shortcut() }, std::wstring()) });
            // Case 2: Validate the element when selecting Del (VK_DELETE) on third dropdown of second column of Ctrl+Alt+Empty shortcut
            testCases.push_back({ 0, 1, 2, std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_DELETE }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), std::vector<int32_t>{ VK_CONTROL, VK_MENU } }, std::wstring()) });
            // Case 3: Validate the element when selecting Del (VK_DELETE) on third dropdown of second column of hybrid Ctrl+Alt+Empty shortcut
            testCases.push_back({ 0, 1, 2, std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_DELETE }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), std::vector<int32_t>{ VK_CONTROL, VK_MENU } }, std::wstring()) });
            // Case 4: Validate the element when selecting Alt (VK_MENU) on second dropdown of first column of Ctrl+Empty+Del shortcut
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_DELETE }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_DELETE }, Shortcut() }, std::wstring()) });
            // Case 5: Validate the element when selecting Alt (VK_MENU) on second dropdown of second column of Ctrl+Empty+Del shortcut
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_DELETE }, std::wstring(), false, std::make_pair(RemapBufferItem{ Shortcut(), std::vector<int32_t>{ VK_CONTROL, VK_DELETE } }, std::wstring()) });
            // Case 6: Validate the element when selecting Alt (VK_MENU) on second dropdown of second column of hybrid Ctrl+Empty+Del shortcut
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, VK_MENU, VK_DELETE }, std::wstring(), true, std::make_pair(RemapBufferItem{ Shortcut(), std::vector<int32_t>{ VK_CONTROL, VK_DELETE } }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is invalid
                Assert::AreEqual(true, result.first == ShortcutErrorType::CtrlAltDel);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns MapToSameKey error on setting hybrid second column to match first column in a remap keys table
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnMapToSameKeyError_OnSettingHybridSecondColumnToFirstColumnInKeyTable)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1: Validate the element when selecting A (0x41) on first dropdown of empty hybrid second column
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ 0x41, -1, -1 }, std::wstring(), true, std::make_pair(RemapBufferItem{ (DWORD)0x41, (DWORD)0 }, std::wstring()) });
            // Case 2: Validate the element when selecting A (0x41) on second dropdown of empty hybrid second column
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ -1, 0x41, -1 }, std::wstring(), true, std::make_pair(RemapBufferItem{ (DWORD)0x41, (DWORD)0 }, std::wstring()) });
            // Case 3: Validate the element when selecting A (0x41) on third dropdown of empty hybrid second column
            testCases.push_back({ 0, 1, 2, std::vector<int32_t>{ -1, -1, 0x41 }, std::wstring(), true, std::make_pair(RemapBufferItem{ (DWORD)0x41, (DWORD)0 }, std::wstring()) });
            // Case 4: Validate the element when selecting A (0x41) on first dropdown of hybrid second column with key
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ 0x41 }, std::wstring(), true, std::make_pair(RemapBufferItem{ (DWORD)0x41, (DWORD)0x43 }, std::wstring()) });
            // Case 5: Validate the element when selecting Null (-1) on first dropdown of hybrid second column with shortcut
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ -1, 0x41 }, std::wstring(), true, std::make_pair(RemapBufferItem{ (DWORD)0x41, std::vector<int32_t>{ VK_CONTROL, 0x41 } }, std::wstring()) });
            // Case 6: Validate the element when selecting None (0) on first dropdown of hybrid second column with shortcut
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ 0, 0x41 }, std::wstring(), true, std::make_pair(RemapBufferItem{ (DWORD)0x41, std::vector<int32_t>{ VK_CONTROL, 0x41 } }, std::wstring()) });
            // Case 7: Validate the element when selecting Null (-1) on second dropdown of hybrid second column with shortcut
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ -1, VK_CONTROL }, std::wstring(), true, std::make_pair(RemapBufferItem{ (DWORD)VK_CONTROL, std::vector<int32_t>{ VK_CONTROL, 0x41 } }, std::wstring()) });
            // Case 8: Validate the element when selecting None (0) on second dropdown of hybrid second column with shortcut
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ 0, VK_CONTROL }, std::wstring(), true, std::make_pair(RemapBufferItem{ (DWORD)VK_CONTROL, std::vector<int32_t>{ VK_CONTROL, 0x41 } }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is invalid
                Assert::AreEqual(true, result.first == ShortcutErrorType::MapToSameKey);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns MapToSameShortcut error on setting one column to match the other and both are valid 3 key shortcuts
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnMapToSameShortcutError_OnSettingOneColumnToTheOtherAndBothAreValid3KeyShortcuts)
        {
            

            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1 : Validate the element when selecting C (0x43) on third dropdown of first column with Ctrl+Shift+Empty
            testCases.push_back({ 0, 0, 2, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 } }, std::wstring()) });
            // Case 2 : Validate the element when selecting C (0x43) on third dropdown of second column with Ctrl+Shift+Empty
            testCases.push_back({ 0, 1, 2, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT } }, std::wstring()) });
            // Case 3 : Validate the element when selecting C (0x43) on third dropdown of second column with hybrid Ctrl+Shift+Empty
            testCases.push_back({ 0, 1, 2, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT } }, std::wstring()) });
            // Case 4 : Validate the element when selecting Shift (VK_SHIFT) on second dropdown of first column with Ctrl+Empty+C
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 } }, std::wstring()) });
            // Case 5 : Validate the element when selecting Shift (VK_SHIFT) on second dropdown of second column with Ctrl+Empty+C
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x43 } }, std::wstring()) });
            // Case 6 : Validate the element when selecting Shift (VK_SHIFT) on second dropdown of second column with hybrid Ctrl+Empty+C
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x43 } }, std::wstring()) });
            // Case 7 : Validate the element when selecting Shift (VK_SHIFT) on first dropdown of first column with Empty+Ctrl+C
            testCases.push_back({ 0, 0, 0, std::vector<int32_t>{ VK_SHIFT, VK_CONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 } }, std::wstring()) });
            // Case 8 : Validate the element when selecting Shift (VK_SHIFT) on first dropdown of second column with Empty+Ctrl+C
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ VK_SHIFT, VK_CONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x43 } }, std::wstring()) });
            // Case 9 : Validate the element when selecting Shift (VK_SHIFT) on first dropdown of second column with hybrid Empty+Ctrl+C
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ VK_SHIFT, VK_CONTROL, 0x43 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x43 } }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is invalid
                Assert::AreEqual(true, result.first == ShortcutErrorType::MapToSameShortcut);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns MapToSameShortcut error on setting one column to match the other and both are valid 2 key shortcuts
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnMapToSameShortcutError_OnSettingOneColumnToTheOtherAndBothAreValid2KeyShortcuts)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1 : Validate the element when selecting C (0x43) on third dropdown of first column with Ctrl+Empty+Empty
            testCases.push_back({ 0, 0, 2, std::vector<int32_t>{ VK_CONTROL, -1, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL }, std::vector<int32_t>{ VK_CONTROL, 0x43 } }, std::wstring()) });
            // Case 2 : Validate the element when selecting C (0x43) on third dropdown of second column with Ctrl+Empty+Empty
            testCases.push_back({ 0, 1, 2, std::vector<int32_t>{ VK_CONTROL, -1, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL } }, std::wstring()) });
            // Case 3 : Validate the element when selecting C (0x43) on third dropdown of second column with hybrid Ctrl+Empty+Empty
            testCases.push_back({ 0, 1, 2, std::vector<int32_t>{ VK_CONTROL, -1, 0x43 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL } }, std::wstring()) });
            // Case 4 : Validate the element when selecting C (0x43) on second dropdown of first column with Ctrl+Empty+Empty
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>{ VK_CONTROL, 0x43, -1 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL }, std::vector<int32_t>{ VK_CONTROL, 0x43 } }, std::wstring()) });
            // Case 5 : Validate the element when selecting C (0x43) on second dropdown of second column with Ctrl+Empty+Empty
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, 0x43, -1 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL } }, std::wstring()) });
            // Case 6 : Validate the element when selecting C (0x43) on second dropdown of second column with hybrid Ctrl+Empty+Empty
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, 0x43, -1 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL } }, std::wstring()) });
            // Case 7 : Validate the element when selecting Ctrl (VK_CONTROL) on first dropdown of first column with Empty+Empty+C
            testCases.push_back({ 0, 0, 0, std::vector<int32_t>{ VK_CONTROL, -1, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x43 } }, std::wstring()) });
            // Case 8 : Validate the element when selecting Ctrl (VK_CONTROL) on first dropdown of second column with Empty+Empty+C
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ VK_CONTROL, -1, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ 0x43 } }, std::wstring()) });
            // Case 9 : Validate the element when selecting Ctrl (VK_CONTROL) on first dropdown of second column with hybrid Empty+Empty+C
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ VK_CONTROL, -1, 0x43 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ 0x43 } }, std::wstring()) });
            // Case 10 : Validate the element when selecting Ctrl (VK_CONTROL) on second dropdown of first column with Empty+Empty+C
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>{ -1, VK_CONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x43 } }, std::wstring()) });
            // Case 11 : Validate the element when selecting Ctrl (VK_CONTROL) on second dropdown of second column with Empty+Empty+C
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ -1, VK_CONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ 0x43 } }, std::wstring()) });
            // Case 12 : Validate the element when selecting Ctrl (VK_CONTROL) on second dropdown of second column with hybrid Empty+Empty+C
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ -1, VK_CONTROL, 0x43 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ 0x43 } }, std::wstring()) });
            // Case 13 : Validate the element when selecting C (0x43) on second dropdown of first column with Ctrl+A
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x41 }, std::vector<int32_t>{ VK_CONTROL, 0x43 } }, std::wstring()) });
            // Case 14 : Validate the element when selecting C (0x43) on second dropdown of second column with Ctrl+A
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x41 } }, std::wstring()) });
            // Case 15 : Validate the element when selecting C (0x43) on second dropdown of second column with hybrid Ctrl+A
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x41 } }, std::wstring()) });
            // Case 16 : Validate the element when selecting Ctrl (VK_CONTROL) on first dropdown of first column with Alt+C
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_MENU, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x43 } }, std::wstring()) });
            // Case 17 : Validate the element when selecting Ctrl (VK_CONTROL) on first dropdown of second column with Alt+C
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_MENU, 0x43 } }, std::wstring()) });
            // Case 18 : Validate the element when selecting Ctrl (VK_CONTROL) on first dropdown of second column with hybrid Alt+C
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_MENU, 0x43 } }, std::wstring()) });
            // Case 19 : Validate the element when selecting Null (-1)  on second dropdown of first column with Ctrl+Shift+C
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>{ VK_CONTROL, -1, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x43 } }, std::wstring()) });
            // Case 20 : Validate the element when selecting Null (-1)  on second dropdown of second column with Ctrl+Shift+C
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, -1, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 } }, std::wstring()) });
            // Case 21 : Validate the element when selecting Null (-1)  on second dropdown of second column with hybrid Ctrl+Shift+C
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, -1, 0x43 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 } }, std::wstring()) });
            // Case 22 : Validate the element when selecting None (0)  on second dropdown of first column with Ctrl+Shift+C
            testCases.push_back({ 0, 0, 1, std::vector<int32_t>{ VK_CONTROL, 0, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x43 } }, std::wstring()) });
            // Case 23 : Validate the element when selecting None (0)  on second dropdown of second column with Ctrl+Shift+C
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, 0, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 } }, std::wstring()) });
            // Case 24 : Validate the element when selecting None (0)  on second dropdown of second column with hybrid Ctrl+Shift+C
            testCases.push_back({ 0, 1, 1, std::vector<int32_t>{ VK_CONTROL, 0, 0x43 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 } }, std::wstring()) });
            // Case 25 : Validate the element when selecting Null (-1)  on first dropdown of first column with Shift+Ctrl+C
            testCases.push_back({ 0, 0, 0, std::vector<int32_t>{ -1, VK_CONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_SHIFT, VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x43 } }, std::wstring()) });
            // Case 26 : Validate the element when selecting Null (-1)  on first dropdown of second column with Shift+Ctrl+C
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ -1, VK_CONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_SHIFT, VK_CONTROL, 0x43 } }, std::wstring()) });
            // Case 27 : Validate the element when selecting Null (-1)  on first dropdown of second column with hybrid Shift+Ctrl+C
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ -1, VK_CONTROL, 0x43 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_SHIFT, VK_CONTROL, 0x43 } }, std::wstring()) });
            // Case 28 : Validate the element when selecting None (0)  on first dropdown of first column with Shift+Ctrl+C
            testCases.push_back({ 0, 0, 0, std::vector<int32_t>{ 0, VK_CONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_SHIFT, VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_CONTROL, 0x43 } }, std::wstring()) });
            // Case 29 : Validate the element when selecting None (0)  on first dropdown of second column with Shift+Ctrl+C
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ 0, VK_CONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_SHIFT, VK_CONTROL, 0x43 } }, std::wstring()) });
            // Case 30 : Validate the element when selecting None (0)  on first dropdown of second column with hybrid Shift+Ctrl+C
            testCases.push_back({ 0, 1, 0, std::vector<int32_t>{ 0, VK_CONTROL, 0x43 }, std::wstring(), true, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::vector<int32_t>{ VK_SHIFT, VK_CONTROL, 0x43 } }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is invalid
                Assert::AreEqual(true, result.first == ShortcutErrorType::MapToSameShortcut);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns SameShortcutPreviouslyMapped error on setting first column to match first column in another row with same target app and both are valid 3 key shortcuts
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnSameShortcutPreviouslyMappedError_OnSettingFirstColumnToFirstColumnInAnotherRowWithSameTargetAppAndBothAreValid3KeyShortcuts)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1 : Validate the element when selecting C (0x43) on third dropdown of first column with Ctrl+Shift+Empty
            testCases.push_back({ 1, 0, 2, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT }, Shortcut() }, std::wstring()) });
            // Case 2 : Validate the element when selecting Shift (VK_SHIFT) on second dropdown of first column with Ctrl+Empty+C
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, Shortcut() }, std::wstring()) });
            // Case 3 : Validate the element when selecting Shift (VK_SHIFT) on first dropdown of first column with Empty+Ctrl+C
            testCases.push_back({ 1, 0, 0, std::vector<int32_t>{ VK_SHIFT, VK_CONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, Shortcut() }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                // Ctrl+Shift+C remapped
                remapBuffer.push_back(std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, Shortcut() }, std::wstring()));
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is invalid
                Assert::AreEqual(true, result.first == ShortcutErrorType::SameShortcutPreviouslyMapped);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns no error on setting first column to match first column in another row with different target app and both are valid 3 key shortcuts
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoError_OnSettingFirstColumnToFirstColumnInAnotherRowWithDifferentTargetAppAndBothAreValid3KeyShortcuts)
        {
            

            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1 : Validate the element when selecting C (0x43) on third dropdown of first column with Ctrl+Shift+Empty for testApp2
            testCases.push_back({ 1, 0, 2, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT }, Shortcut() }, testApp2) });
            // Case 2 : Validate the element when selecting Shift (VK_SHIFT) on second dropdown of first column with Ctrl+Empty+C for testApp2
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, Shortcut() }, testApp2) });
            // Case 3 : Validate the element when selecting Shift (VK_SHIFT) on first dropdown of first column with Empty+Ctrl+C for testApp2
            testCases.push_back({ 1, 0, 0, std::vector<int32_t>{ VK_SHIFT, VK_CONTROL, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, Shortcut() }, testApp2) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                // Ctrl+Shift+C remapped for testApp1
                remapBuffer.push_back(std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, Shortcut() }, testApp1));
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is valid
                Assert::AreEqual(true, result.first == ShortcutErrorType::NoError);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns ConflictingModifierShortcut error on setting first column to conflict with first column in another row with same target app and both are valid 3 key shortcuts
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnConflictingModifierShortcutError_OnSettingFirstColumnToConflictWithFirstColumnInAnotherRowWithSameTargetAppAndBothAreValid3KeyShortcuts)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1 : Validate the element when selecting C (0x43) on third dropdown of first column with LCtrl+Shift+Empty
            testCases.push_back({ 1, 0, 2, std::vector<int32_t>{ VK_LCONTROL, VK_SHIFT, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_LCONTROL, VK_SHIFT }, Shortcut() }, std::wstring()) });
            // Case 2 : Validate the element when selecting Shift (VK_SHIFT) on second dropdown of first column with LCtrl+Empty+C
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ VK_LCONTROL, VK_SHIFT, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_LCONTROL, 0x43 }, Shortcut() }, std::wstring()) });
            // Case 3 : Validate the element when selecting LShift (VK_LSHIFT) on first dropdown of first column with Empty+Ctrl+C
            testCases.push_back({ 1, 0, 0, std::vector<int32_t>{ VK_LSHIFT, VK_CONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, Shortcut() }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                // Ctrl+Shift+C remapped
                remapBuffer.push_back(std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, Shortcut() }, std::wstring()));
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is invalid
                Assert::AreEqual(true, result.first == ShortcutErrorType::ConflictingModifierShortcut);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns no error on setting first column to conflict with first column in another row with different target app and both are valid 3 key shortcuts
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoError_OnSettingFirstColumnToConflictWithFirstColumnInAnotherRowWithDifferentTargetAppAndBothAreValid3KeyShortcuts)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1 : Validate the element when selecting C (0x43) on third dropdown of first column with LCtrl+Shift+Empty for testApp2
            testCases.push_back({ 1, 0, 2, std::vector<int32_t>{ VK_LCONTROL, VK_SHIFT, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_LCONTROL, VK_SHIFT }, Shortcut() }, testApp2) });
            // Case 2 : Validate the element when selecting Shift (VK_SHIFT) on second dropdown of first column with LCtrl+Empty+C for testApp2
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ VK_LCONTROL, VK_SHIFT, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_LCONTROL, 0x43 }, Shortcut() }, testApp2) });
            // Case 3 : Validate the element when selecting LShift (VK_LSHIFT) on first dropdown of first column with Empty+Ctrl+C for testApp2
            testCases.push_back({ 1, 0, 0, std::vector<int32_t>{ VK_LSHIFT, VK_CONTROL, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, Shortcut() }, testApp2) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                // Ctrl+Shift+C remapped for testApp1
                remapBuffer.push_back(std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, Shortcut() }, testApp1));
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is valid
                Assert::AreEqual(true, result.first == ShortcutErrorType::NoError);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns SameShortcutPreviouslyMapped error on setting first column to match first column in another row with same target app and both are valid 2 key shortcuts
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnSameShortcutPreviouslyMappedError_OnSettingFirstColumnToFirstColumnInAnotherRowWithSameTargetAppAndBothAreValid2KeyShortcuts)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1 : Validate the element when selecting C (0x43) on second dropdown of first column with Ctrl+Empty
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL }, Shortcut() }, std::wstring()) });
            // Case 2 : Validate the element when selecting C (0x43) on third dropdown of first column with Ctrl+Empty+Empty
            testCases.push_back({ 1, 0, 2, std::vector<int32_t>{ VK_CONTROL, -1, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL }, Shortcut() }, std::wstring()) });
            // Case 3 : Validate the element when selecting C (0x43) on second dropdown of first column with Ctrl+Empty+Empty
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ VK_CONTROL, 0x43, -1 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL }, Shortcut() }, std::wstring()) });
            // Case 4 : Validate the element when selecting Ctrl (VK_CONTROL) on first dropdown of first column with Empty+C
            testCases.push_back({ 1, 0, 0, std::vector<int32_t>{ VK_CONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ 0x43 }, Shortcut() }, std::wstring()) });
            // Case 5 : Validate the element when selecting Ctrl (VK_CONTROL) on first dropdown of first column with Empty+Empty+C
            testCases.push_back({ 1, 0, 0, std::vector<int32_t>{ VK_CONTROL, -1, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ 0x43 }, Shortcut() }, std::wstring()) });
            // Case 6 : Validate the element when selecting Ctrl (VK_CONTROL) on second dropdown of first column with Empty+Empty+C
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ -1, VK_CONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ 0x43 }, Shortcut() }, std::wstring()) });
            // Case 7 : Validate the element when selecting Null (-1) on second dropdown of first column with Ctrl+Shift+C
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ VK_CONTROL, -1, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, Shortcut() }, std::wstring()) });
            // Case 8 : Validate the element when selecting Null (-1) on first dropdown of first column with Shift+Ctrl+C
            testCases.push_back({ 1, 0, 0, std::vector<int32_t>{ -1, VK_CONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_SHIFT, VK_CONTROL, 0x43 }, Shortcut() }, std::wstring()) });
            // Case 9 : Validate the element when selecting None (0) on second dropdown of first column with Ctrl+Shift+C
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ VK_CONTROL, 0, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, Shortcut() }, std::wstring()) });
            // Case 10 : Validate the element when selecting None (0) on first dropdown of first column with Shift+Ctrl+C
            testCases.push_back({ 1, 0, 0, std::vector<int32_t>{ 0, VK_CONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_SHIFT, VK_CONTROL, 0x43 }, Shortcut() }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                // Ctrl+C remapped
                remapBuffer.push_back(std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, Shortcut() }, std::wstring()));
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is invalid
                Assert::AreEqual(true, result.first == ShortcutErrorType::SameShortcutPreviouslyMapped);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns no error on setting first column to match first column in another row with different target app and both are valid 2 key shortcuts
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoError_OnSettingFirstColumnToFirstColumnInAnotherRowWithDifferentTargetAppAndBothAreValid2KeyShortcuts)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1 : Validate the element when selecting C (0x43) on second dropdown of first column with Ctrl+Empty for testApp2
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ VK_CONTROL, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL }, Shortcut() }, testApp2) });
            // Case 2 : Validate the element when selecting C (0x43) on third dropdown of first column with Ctrl+Empty+Empty for testApp2
            testCases.push_back({ 1, 0, 2, std::vector<int32_t>{ VK_CONTROL, -1, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL }, Shortcut() }, testApp2) });
            // Case 3 : Validate the element when selecting C (0x43) on second dropdown of first column with Ctrl+Empty+Empty for testApp2
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ VK_CONTROL, 0x43, -1 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL }, Shortcut() }, testApp2) });
            // Case 4 : Validate the element when selecting Ctrl (VK_CONTROL) on first dropdown of first column with Empty+C for testApp2
            testCases.push_back({ 1, 0, 0, std::vector<int32_t>{ VK_CONTROL, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ 0x43 }, Shortcut() }, testApp2) });
            // Case 5 : Validate the element when selecting Ctrl (VK_CONTROL) on first dropdown of first column with Empty+Empty+C for testApp2
            testCases.push_back({ 1, 0, 0, std::vector<int32_t>{ VK_CONTROL, -1, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ 0x43 }, Shortcut() }, testApp2) });
            // Case 6 : Validate the element when selecting Ctrl (VK_CONTROL) on second dropdown of first column with Empty+Empty+C for testApp2
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ -1, VK_CONTROL, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ 0x43 }, Shortcut() }, testApp2) });
            // Case 7 : Validate the element when selecting Null (-1) on second dropdown of first column with Ctrl+Shift+C for testApp2
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ VK_CONTROL, -1, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, Shortcut() }, testApp2) });
            // Case 8 : Validate the element when selecting Null (-1) on first dropdown of first column with Shift+Ctrl+C for testApp2
            testCases.push_back({ 1, 0, 0, std::vector<int32_t>{ -1, VK_CONTROL, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_SHIFT, VK_CONTROL, 0x43 }, Shortcut() }, testApp2) });
            // Case 9 : Validate the element when selecting None (0) on second dropdown of first column with Ctrl+Shift+C for testApp2
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ VK_CONTROL, 0, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, VK_SHIFT, 0x43 }, Shortcut() }, testApp2) });
            // Case 10 : Validate the element when selecting None (0) on first dropdown of first column with Shift+Ctrl+C for testApp2
            testCases.push_back({ 1, 0, 0, std::vector<int32_t>{ 0, VK_CONTROL, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_SHIFT, VK_CONTROL, 0x43 }, Shortcut() }, testApp2) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                // Ctrl+C remapped for testApp1
                remapBuffer.push_back(std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, Shortcut() }, testApp1));
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is valid
                Assert::AreEqual(true, result.first == ShortcutErrorType::NoError);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns ConflictingModifierShortcut error on setting first column to conflict with first column in another row with same target app and both are valid 2 key shortcuts
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnConflictingModifierShortcutError_OnSettingFirstColumnToConflictWithFirstColumnInAnotherRowWithSameTargetAppAndBothAreValid2KeyShortcuts)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1 : Validate the element when selecting C (0x43) on second dropdown of first column with LCtrl+Empty
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ VK_LCONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_LCONTROL }, Shortcut() }, std::wstring()) });
            // Case 2 : Validate the element when selecting C (0x43) on third dropdown of first column with LCtrl+Empty+Empty
            testCases.push_back({ 1, 0, 2, std::vector<int32_t>{ VK_LCONTROL, -1, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_LCONTROL }, Shortcut() }, std::wstring()) });
            // Case 3 : Validate the element when selecting C (0x43) on second dropdown of first column with LCtrl+Empty+Empty
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ VK_LCONTROL, 0x43, -1 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_LCONTROL }, Shortcut() }, std::wstring()) });
            // Case 4 : Validate the element when selecting LCtrl (VK_LCONTROL) on first dropdown of first column with Empty+C
            testCases.push_back({ 1, 0, 0, std::vector<int32_t>{ VK_LCONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ 0x43 }, Shortcut() }, std::wstring()) });
            // Case 5 : Validate the element when selecting LCtrl (VK_LCONTROL) on first dropdown of first column with Empty+Empty+C
            testCases.push_back({ 1, 0, 0, std::vector<int32_t>{ VK_LCONTROL, -1, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ 0x43 }, Shortcut() }, std::wstring()) });
            // Case 6 : Validate the element when selecting LCtrl (VK_LCONTROL) on second dropdown of first column with Empty+Empty+C
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ -1, VK_LCONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ 0x43 }, Shortcut() }, std::wstring()) });
            // Case 7 : Validate the element when selecting Null (-1) on second dropdown of first column with LCtrl+Shift+C
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ VK_LCONTROL, -1, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_LCONTROL, VK_SHIFT, 0x43 }, Shortcut() }, std::wstring()) });
            // Case 8 : Validate the element when selecting Null (-1) on first dropdown of first column with Shift+LCtrl+C
            testCases.push_back({ 1, 0, 0, std::vector<int32_t>{ -1, VK_LCONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_SHIFT, VK_LCONTROL, 0x43 }, Shortcut() }, std::wstring()) });
            // Case 9 : Validate the element when selecting None (0) on second dropdown of first column with LCtrl+Shift+C
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ VK_LCONTROL, 0, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_LCONTROL, VK_SHIFT, 0x43 }, Shortcut() }, std::wstring()) });
            // Case 10 : Validate the element when selecting None (0) on first dropdown of first column with Shift+LCtrl+C
            testCases.push_back({ 1, 0, 0, std::vector<int32_t>{ 0, VK_LCONTROL, 0x43 }, std::wstring(), false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_SHIFT, VK_LCONTROL, 0x43 }, Shortcut() }, std::wstring()) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                // Ctrl+C remapped
                remapBuffer.push_back(std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, Shortcut() }, std::wstring()));
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is invalid
                Assert::AreEqual(true, result.first == ShortcutErrorType::ConflictingModifierShortcut);
            });
        }

        // Test if the ValidateShortcutBufferElement method returns no error on setting first column to conflict with first column in another row with different target app and both are valid 2 key shortcuts
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnNoError_OnSettingFirstColumnToConflictWithFirstColumnInAnotherRowWithDifferentTargetAppAndBothAreValid2KeyShortcuts)
        {
            std::vector<ValidateShortcutBufferElementArgs> testCases;
            // Case 1 : Validate the element when selecting C (0x43) on second dropdown of first column with LCtrl+Empty for testApp2
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ VK_LCONTROL, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_LCONTROL }, Shortcut() }, testApp2) });
            // Case 2 : Validate the element when selecting C (0x43) on third dropdown of first column with LCtrl+Empty+Empty for testApp2
            testCases.push_back({ 1, 0, 2, std::vector<int32_t>{ VK_LCONTROL, -1, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_LCONTROL }, Shortcut() }, testApp2) });
            // Case 3 : Validate the element when selecting C (0x43) on second dropdown of first column with LCtrl+Empty+Empty for testApp2
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ VK_LCONTROL, 0x43, -1 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_LCONTROL }, Shortcut() }, testApp2) });
            // Case 4 : Validate the element when selecting LCtrl (VK_LCONTROL) on first dropdown of first column with Empty+C for testApp2
            testCases.push_back({ 1, 0, 0, std::vector<int32_t>{ VK_LCONTROL, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ 0x43 }, Shortcut() }, testApp2) });
            // Case 5 : Validate the element when selecting LCtrl (VK_LCONTROL) on first dropdown of first column with Empty+Empty+C for testApp2
            testCases.push_back({ 1, 0, 0, std::vector<int32_t>{ VK_LCONTROL, -1, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ 0x43 }, Shortcut() }, testApp2) });
            // Case 6 : Validate the element when selecting LCtrl (VK_LCONTROL) on second dropdown of first column with Empty+Empty+C for testApp2
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ -1, VK_LCONTROL, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ 0x43 }, Shortcut() }, testApp2) });
            // Case 7 : Validate the element when selecting Null (-1) on second dropdown of first column with LCtrl+Shift+C for testApp2
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ VK_LCONTROL, -1, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_LCONTROL, VK_SHIFT, 0x43 }, Shortcut() }, testApp2) });
            // Case 8 : Validate the element when selecting Null (-1) on first dropdown of first column with Shift+LCtrl+C for testApp2
            testCases.push_back({ 1, 0, 0, std::vector<int32_t>{ -1, VK_LCONTROL, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_SHIFT, VK_LCONTROL, 0x43 }, Shortcut() }, testApp2) });
            // Case 9 : Validate the element when selecting None (0) on second dropdown of first column with LCtrl+Shift+C for testApp2
            testCases.push_back({ 1, 0, 1, std::vector<int32_t>{ VK_LCONTROL, 0, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_LCONTROL, VK_SHIFT, 0x43 }, Shortcut() }, testApp2) });
            // Case 10 : Validate the element when selecting None (0) on first dropdown of first column with Shift+LCtrl+C for testApp2
            testCases.push_back({ 1, 0, 0, std::vector<int32_t>{ 0, VK_LCONTROL, 0x43 }, testApp2, false, std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_SHIFT, VK_LCONTROL, 0x43 }, Shortcut() }, testApp2) });

            RunTestCases(testCases, [this](const ValidateShortcutBufferElementArgs& testCase) {
                // Arrange
                RemapBuffer remapBuffer;
                // Ctrl+C remapped for testApp1
                remapBuffer.push_back(std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_CONTROL, 0x43 }, Shortcut() }, testApp1));
                remapBuffer.push_back(testCase.bufferRow);

                // Act
                std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(testCase.elementRowIndex, testCase.elementColIndex, testCase.indexOfDropDownLastModified, testCase.selectedCodesOnDropDowns, testCase.targetAppNameInTextBox, testCase.isHybridColumn, remapBuffer, true);

                // Assert that the element is valid
                Assert::AreEqual(true, result.first == ShortcutErrorType::NoError);
            });
        }

        // Return error on Disable as second modifier key or action key
        TEST_METHOD (ValidateShortcutBufferElement_ShouldReturnDisableAsActionKeyError_OnSettingSecondDropdownAsDisable)
        {
            // Arrange
            RemapBuffer remapBuffer;
            remapBuffer.push_back(std::make_pair(RemapBufferItem{ std::vector<int32_t>{ VK_SHIFT, CommonSharedConstants::VK_DISABLED }, Shortcut() }, testApp1));
            std::vector<int32_t> selectedCodes = { 
                VK_SHIFT,
                CommonSharedConstants::VK_DISABLED
            };

            // Act
            std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> result = BufferValidationHelpers::ValidateShortcutBufferElement(0, 1, 1, selectedCodes, testApp1, true, remapBuffer, true);

            // Assert
            Assert::AreEqual(true, result.first == ShortcutErrorType::ShortcutDisableAsActionKey);
            Assert::AreEqual(true, result.second == BufferValidationHelpers::DropDownAction::NoAction);
        }
    };
}
