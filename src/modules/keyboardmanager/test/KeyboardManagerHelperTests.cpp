#include "pch.h"
#include "CppUnitTest.h"
#include <keyboardmanager/common/Helpers.h>
#include "TestHelpers.h"
#include <common/keyboard_layout.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace KeyboardManagerCommonTests
{
    // Tests for methods in the KeyboardManagerHelper namespace
    TEST_CLASS (KeyboardManagerHelperTests)
    {
    public:
        TEST_METHOD_INITIALIZE(InitializeTestEnv)
        {
        }

        // Test if the DoKeysOverlap method returns SameKeyPreviouslyMapped on passing the same key for both arguments
        TEST_METHOD (DoKeysOverlap_ShouldReturnSameKeyPreviouslyMapped_OnPassingSameKeyForBothArguments)
        {
            // Arrange
            DWORD key1 = 0x41;
            DWORD key2 = key1;

            // Act
            auto result = KeyboardManagerHelper::DoKeysOverlap(key1, key2);

            // Assert
            Assert::IsTrue(result == KeyboardManagerHelper::ErrorType::SameKeyPreviouslyMapped);
        }

        // Test if the DoKeysOverlap method returns ConflictingModifierKey on passing left modifier and common modifier
        TEST_METHOD (DoKeysOverlap_ShouldReturnConflictingModifierKey_OnPassingLeftModifierAndCommonModifierOfSameType)
        {
            // Arrange
            DWORD key1 = VK_LCONTROL;
            DWORD key2 = VK_CONTROL;

            // Act
            auto result = KeyboardManagerHelper::DoKeysOverlap(key1, key2);

            // Assert
            Assert::IsTrue(result == KeyboardManagerHelper::ErrorType::ConflictingModifierKey);
        }

        // Test if the DoKeysOverlap method returns ConflictingModifierKey on passing right modifier and common modifier
        TEST_METHOD (DoKeysOverlap_ShouldReturnConflictingModifierKey_OnPassingRightModifierAndCommonModifierOfSameType)
        {
            // Arrange
            DWORD key1 = VK_RCONTROL;
            DWORD key2 = VK_CONTROL;

            // Act
            auto result = KeyboardManagerHelper::DoKeysOverlap(key1, key2);

            // Assert
            Assert::IsTrue(result == KeyboardManagerHelper::ErrorType::ConflictingModifierKey);
        }

        // Test if the DoKeysOverlap method returns NoError on passing left modifier and right modifier
        TEST_METHOD (DoKeysOverlap_ShouldReturnNoError_OnPassingLeftModifierAndRightModifierOfSameType)
        {
            // Arrange
            DWORD key1 = VK_LCONTROL;
            DWORD key2 = VK_RCONTROL;

            // Act
            auto result = KeyboardManagerHelper::DoKeysOverlap(key1, key2);

            // Assert
            Assert::IsTrue(result == KeyboardManagerHelper::ErrorType::NoError);
        }

        // Test if the DoKeysOverlap method returns NoError on passing keys of different types
        TEST_METHOD (DoKeysOverlap_ShouldReturnNoError_OnPassingKeysOfDifferentTypes)
        {
            // Arrange
            DWORD key1 = VK_CONTROL;
            DWORD key2 = VK_SHIFT;

            // Act
            auto result = KeyboardManagerHelper::DoKeysOverlap(key1, key2);

            // Assert
            Assert::IsTrue(result == KeyboardManagerHelper::ErrorType::NoError);
        }

        // Test if the DoKeysOverlap method returns NoError on passing different action keys
        TEST_METHOD (DoKeysOverlap_ShouldReturnNoError_OnPassingDifferentActionKeys)
        {
            // Arrange
            DWORD key1 = 0x41;
            DWORD key2 = 0x42;

            // Act
            auto result = KeyboardManagerHelper::DoKeysOverlap(key1, key2);

            // Assert
            Assert::IsTrue(result == KeyboardManagerHelper::ErrorType::NoError);
        }

        // Test if the CheckRepeatedModifier method returns true on passing vector with same modifier repeated
        TEST_METHOD (CheckRepeatedModifier_ShouldReturnTrue_OnPassingSameModifierRepeated)
        {
            // Arrange
            std::vector<DWORD> keyList = LayoutMap().GetKeyCodeList(true);
            std::vector<DWORD> keys = { VK_CONTROL, VK_CONTROL, 0x41 };

            // Act
            bool result = KeyboardManagerHelper::CheckRepeatedModifier(keys, TestHelpers::GetDropDownIndexFromDropDownList(VK_CONTROL, keyList), keyList);

            // Assert
            Assert::IsTrue(result);
        }

        // Test if the CheckRepeatedModifier method returns true on passing vector with conflicting modifier repeated
        TEST_METHOD (CheckRepeatedModifier_ShouldReturnTrue_OnPassingConflictingModifierRepeated)
        {
            // Arrange
            std::vector<DWORD> keyList = LayoutMap().GetKeyCodeList(true);
            std::vector<DWORD> keys = { VK_CONTROL, VK_LCONTROL, 0x41 };

            // Act
            bool result = KeyboardManagerHelper::CheckRepeatedModifier(keys, TestHelpers::GetDropDownIndexFromDropDownList(VK_LCONTROL, keyList), keyList);

            // Assert
            Assert::IsTrue(result);
        }

        // Test if the CheckRepeatedModifier method returns false on passing vector with different modifiers
        TEST_METHOD (CheckRepeatedModifier_ShouldReturnFalse_OnPassingDifferentModifiers)
        {
            // Arrange
            std::vector<DWORD> keyList = LayoutMap().GetKeyCodeList(true);
            std::vector<DWORD> keys = { VK_CONTROL, VK_SHIFT, 0x41 };

            // Act
            bool result = KeyboardManagerHelper::CheckRepeatedModifier(keys, TestHelpers::GetDropDownIndexFromDropDownList(VK_SHIFT, keyList), keyList);

            // Assert
            Assert::IsFalse(result);
        }

        // Test if the GetKeyCodesFromSelectedIndices method ignores -1 and 0 from argument in return value
        TEST_METHOD (GetKeyCodesFromSelectedIndices_ShouldIgnoreMinus1And0_OnPassingArgumentWithMinus1Or0)
        {
            // Arrange
            std::vector<DWORD> keyList = LayoutMap().GetKeyCodeList(true);
            std::vector<int32_t> indices = { -1, 0, -1, 0 };

            // Act
            auto result = KeyboardManagerHelper::GetKeyCodesFromSelectedIndices(indices, keyList);

            // Assert
            Assert::IsTrue(result.empty());
        }

        // Test if the GetKeyCodesFromSelectedIndices method returns vector with key codes from vector with indices as per key code list
        TEST_METHOD (GetKeyCodesFromSelectedIndices_ShouldReturnVectorWithKeyCodes_OnPassingVectorWithIndicesAsPerKeyCodeList)
        {
            // Arrange
            std::vector<DWORD> keyList = LayoutMap().GetKeyCodeList(true);
            std::vector<int32_t> indices = { TestHelpers::GetDropDownIndexFromDropDownList(0x41, keyList), TestHelpers::GetDropDownIndexFromDropDownList(0x42, keyList) };

            // Act
            auto result = KeyboardManagerHelper::GetKeyCodesFromSelectedIndices(indices, keyList);

            // Assert
            Assert::IsTrue(result == std::vector<DWORD>{ 0x41, 0x42 });
        }
    };
}
