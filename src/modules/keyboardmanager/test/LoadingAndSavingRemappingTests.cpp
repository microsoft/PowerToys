#include "pch.h"
#include "CppUnitTest.h"
#include "MockedInput.h"
#include <keyboardmanager/common/KeyboardManagerState.h>
#include <keyboardmanager/ui/LoadingAndSavingRemappingHelper.h>
#include "TestHelpers.h"
#include "../common/shared_constants.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RemappingUITests
{
    // Tests for MockedInput test helper - to ensure simulated keyboard input behaves as expected
    TEST_CLASS (LoadingAndSavingRemappingTests)
    {
    public:
        TEST_METHOD_INITIALIZE(InitializeTestEnv)
        {
        }

        // Test if the CheckIfRemappingsAreValid method is successful when no remaps are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnNoError_OnPassingNoRemaps)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Assert that remapping set is valid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the CheckIfRemappingsAreValid method is successful when valid key to key remaps are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnNoError_OnPassingValidKeyToKeyRemaps)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Remap A to B and B to C
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ 0x41, 0x42 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ 0x42, 0x43 }), std::wstring()));

            // Assert that remapping set is valid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the CheckIfRemappingsAreValid method is successful when valid key to shortcut remaps are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnNoError_OnPassingValidKeyToShortcutRemaps)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Remap A to Ctrl+V and B to Alt+Tab
            Shortcut s1;
            s1.SetKey(VK_CONTROL);
            s1.SetKey(0x56);
            Shortcut s2;
            s2.SetKey(VK_MENU);
            s2.SetKey(VK_TAB);
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ 0x41, s1 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ 0x42, s2 }), std::wstring()));

            // Assert that remapping set is valid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the CheckIfRemappingsAreValid method is successful when valid shortcut to key remaps are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnNoError_OnPassingValidShortcutToKeyRemaps)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Remap Ctrl+V to A and Alt+Tab to B
            Shortcut s1;
            s1.SetKey(VK_CONTROL);
            s1.SetKey(0x56);
            Shortcut s2;
            s2.SetKey(VK_MENU);
            s2.SetKey(VK_TAB);
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ s1, 0x41 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ s2, 0x42 }), std::wstring()));

            // Assert that remapping set is valid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the CheckIfRemappingsAreValid method is successful when valid shortcut to shortcut remaps are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnNoError_OnPassingValidShortcutToShortcutRemaps)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Remap Ctrl+V to Ctrl+D and Alt+Tab to Win+A
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            src1.SetKey(0x56);
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x44);
            Shortcut src2;
            src2.SetKey(VK_MENU);
            src2.SetKey(VK_TAB);
            Shortcut dest2;
            dest2.SetKey(CommonSharedConstants::VK_WIN_BOTH);
            dest2.SetKey(0x41);
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ src1, dest1 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ src2, dest2 }), std::wstring()));

            // Assert that remapping set is valid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the CheckIfRemappingsAreValid method is successful when valid remaps are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnNoError_OnPassingValidRemapsOfAllTypes)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Remap Ctrl+V to Ctrl+D, Alt+Tab to A, A to B and B to Win+A
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            src1.SetKey(0x56);
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x44);
            Shortcut src2;
            src2.SetKey(VK_MENU);
            src2.SetKey(VK_TAB);
            Shortcut dest2;
            dest2.SetKey(CommonSharedConstants::VK_WIN_BOTH);
            dest2.SetKey(0x41);
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ src1, dest1 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ src2, 0x41 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ 0x41, 0x42 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ 0x42, dest2 }), std::wstring()));

            // Assert that remapping set is valid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the CheckIfRemappingsAreValid method is unsuccessful when remaps with null keys are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnRemapUnsuccessful_OnPassingRemapsWithNullKeys)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Remap A to NULL
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ 0x41, NULL }), std::wstring()));

            // Assert that remapping set is invalid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == KeyboardManagerHelper::ErrorType::RemapUnsuccessful);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the CheckIfRemappingsAreValid method is unsuccessful when remaps with invalid shortcuts are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnRemapUnsuccessfulr_OnPassingRemapsWithNullKeys)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Remap A to incomplete shortcut (Ctrl)
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ 0x41, src1 }), std::wstring()));

            // Assert that remapping set is invalid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == KeyboardManagerHelper::ErrorType::RemapUnsuccessful);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the CheckIfRemappingsAreValid method is unsuccessful when remaps with the same key remapped twice are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnRemapUnsuccessful_OnPassingRemapsWithSameKeyRemappedTwice)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Remap A to B and A to Ctrl+C
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            src1.SetKey(0x43);
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ 0x41, 0x42 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ 0x41, src1 }), std::wstring()));

            // Assert that remapping set is invalid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == KeyboardManagerHelper::ErrorType::RemapUnsuccessful);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the CheckIfRemappingsAreValid method is unsuccessful when remaps with the same shortcut remapped twice are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnRemapUnsuccessful_OnPassingRemapsWithSameShortcutRemappedTwice)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Remap Ctrl+A to B and Ctrl+A to Ctrl+V
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            src1.SetKey(0x41);
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x56);
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ src1, 0x42 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ src1, dest1 }), std::wstring()));

            // Assert that remapping set is invalid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == KeyboardManagerHelper::ErrorType::RemapUnsuccessful);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the CheckIfRemappingsAreValid method is unsuccessful when app specific remaps with the same shortcut remapped twice for the same target app are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnRemapUnsuccessful_OnPassingAppSpecificRemapsWithSameShortcutRemappedTwiceForTheSameTargetApp)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Remap Ctrl+A to B and Ctrl+A to Ctrl+V for msedge
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            src1.SetKey(0x41);
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x56);
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ src1, 0x42 }), L"msedge"));
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ src1, dest1 }), L"msedge"));

            // Assert that remapping set is invalid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == KeyboardManagerHelper::ErrorType::RemapUnsuccessful);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the CheckIfRemappingsAreValid method is successful when app specific remaps with the same shortcut remapped twice for different target apps are passed
        TEST_METHOD (CheckIfRemappingsAreValid_ShouldReturnNoError_OnPassingAppSpecificRemapsWithSameShortcutRemappedTwiceForDifferentTargetApps)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Remap Ctrl+A to B for msedge and Ctrl+A to Ctrl+V for outlook
            Shortcut src1;
            src1.SetKey(VK_CONTROL);
            src1.SetKey(0x41);
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x56);
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ src1, 0x42 }), L"msedge"));
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ src1, dest1 }), L"outlook"));

            // Assert that remapping set is valid
            bool isSuccess = (LoadingAndSavingRemappingHelper::CheckIfRemappingsAreValid(remapBuffer) == KeyboardManagerHelper::ErrorType::NoError);
            Assert::AreEqual(true, isSuccess);
        }

        // Test if the GetOrphanedKeys method return an empty vector on passing no remaps
        TEST_METHOD (GetOrphanedKeys_ShouldReturnEmptyVector_OnPassingNoRemaps)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Assert that there are no orphaned keys
            Assert::AreEqual(true, LoadingAndSavingRemappingHelper::GetOrphanedKeys(remapBuffer).empty());
        }

        // Test if the GetOrphanedKeys method return one orphaned on passing one key remap
        TEST_METHOD (GetOrphanedKeys_ShouldReturnOneOrphanedKey_OnPassingOneKeyRemap)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Remap A to B
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ 0x41, 0x42 }), std::wstring()));

            // Assert that only A is orphaned
            Assert::AreEqual((size_t)1, LoadingAndSavingRemappingHelper::GetOrphanedKeys(remapBuffer).size());
            Assert::AreEqual((DWORD)0x41, LoadingAndSavingRemappingHelper::GetOrphanedKeys(remapBuffer)[0]);
        }

        // Test if the GetOrphanedKeys method return an empty vector on passing swapped key remaps
        TEST_METHOD (GetOrphanedKeys_ShouldReturnEmptyVector_OnPassingSwappedKeyRemap)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Remap A to B and B to A
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ 0x41, 0x42 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ 0x42, 0x41 }), std::wstring()));

            // Assert that there are no orphaned keys
            Assert::AreEqual(true, LoadingAndSavingRemappingHelper::GetOrphanedKeys(remapBuffer).empty());
        }

        // Test if the GetOrphanedKeys method return one orphaned on passing two key remaps where one key is mapped to a remapped key
        TEST_METHOD (GetOrphanedKeys_ShouldReturnOneOrphanedKey_OnPassingTwoKeyRemapsWhereOneKeyIsMappedToARemappedKey)
        {
            std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>> remapBuffer;

            // Remap A to Ctrl+B and C to A
            Shortcut dest1;
            dest1.SetKey(VK_CONTROL);
            dest1.SetKey(0x42);
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ 0x41, dest1 }), std::wstring()));
            remapBuffer.push_back(std::make_pair(std::vector<std::variant<DWORD, Shortcut>>({ 0x43, 0x41 }), std::wstring()));

            // Assert that only C is orphaned
            Assert::AreEqual((size_t)1, LoadingAndSavingRemappingHelper::GetOrphanedKeys(remapBuffer).size());
            Assert::AreEqual((DWORD)0x43, LoadingAndSavingRemappingHelper::GetOrphanedKeys(remapBuffer)[0]);
        }

        // Test if the PreProcessRemapTable method combines all the modifier pairs when the left and right modifiers are remapped to the same target
        TEST_METHOD (PreProcessRemapTable_ShouldCombineAllPairs_OnPassingLeftAndRightModifiersRemappedToTheSameTarget)
        {
            std::unordered_map<DWORD, std::variant<DWORD, Shortcut>> remapTable;

            // Remap LCtrl and RCtrl to A, LAlt and RAlt to B, LShift and RShift to C, LWin and RWin to D
            remapTable[VK_LCONTROL] = 0x41;
            remapTable[VK_RCONTROL] = 0x41;
            remapTable[VK_LMENU] = 0x42;
            remapTable[VK_RMENU] = 0x42;
            remapTable[VK_LSHIFT] = 0x43;
            remapTable[VK_RSHIFT] = 0x43;
            remapTable[VK_LWIN] = 0x44;
            remapTable[VK_RWIN] = 0x44;

            // Pre process table
            LoadingAndSavingRemappingHelper::PreProcessRemapTable(remapTable);

            // Expected Ctrl remapped to A, Alt to B, Shift to C, Win to D
            std::unordered_map<DWORD, std::variant<DWORD, Shortcut>> expectedTable;
            expectedTable[VK_CONTROL] = 0x41;
            expectedTable[VK_MENU] = 0x42;
            expectedTable[VK_SHIFT] = 0x43;
            expectedTable[CommonSharedConstants::VK_WIN_BOTH] = 0x44;

            bool areTablesEqual = (expectedTable == remapTable);
            Assert::AreEqual(true, areTablesEqual);
        }

        // Test if the PreProcessRemapTable method does not combines any of the modifier pairs when the left and right modifiers are remapped to different targets
        TEST_METHOD (PreProcessRemapTable_ShouldNotCombineAnyPairs_OnPassingLeftAndRightModifiersRemappedToTheDifferentTargets)
        {
            std::unordered_map<DWORD, std::variant<DWORD, Shortcut>> remapTable;

            // Remap left modifiers to A and right modifiers to B
            remapTable[VK_LCONTROL] = 0x41;
            remapTable[VK_RCONTROL] = 0x42;
            remapTable[VK_LMENU] = 0x41;
            remapTable[VK_RMENU] = 0x42;
            remapTable[VK_LSHIFT] = 0x41;
            remapTable[VK_RSHIFT] = 0x42;
            remapTable[VK_LWIN] = 0x41;
            remapTable[VK_RWIN] = 0x42;

            // Pre process table
            LoadingAndSavingRemappingHelper::PreProcessRemapTable(remapTable);

            // Expected unchanged table
            std::unordered_map<DWORD, std::variant<DWORD, Shortcut>> expectedTable;
            expectedTable[VK_LCONTROL] = 0x41;
            expectedTable[VK_RCONTROL] = 0x42;
            expectedTable[VK_LMENU] = 0x41;
            expectedTable[VK_RMENU] = 0x42;
            expectedTable[VK_LSHIFT] = 0x41;
            expectedTable[VK_RSHIFT] = 0x42;
            expectedTable[VK_LWIN] = 0x41;
            expectedTable[VK_RWIN] = 0x42;

            bool areTablesEqual = (expectedTable == remapTable);
            Assert::AreEqual(true, areTablesEqual);
        }
    };
}
