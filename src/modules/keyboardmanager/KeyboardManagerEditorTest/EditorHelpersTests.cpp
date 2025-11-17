#include "pch.h"

// Suppressing 26466 - Don't use static_cast downcasts - in CppUnitTest.h
#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include <keyboardmanager/KeyboardManagerEditorLibrary/ShortcutErrorType.h>
#include <keyboardmanager/common/Helpers.h>
#include <common/interop/keyboard_layout.h>
#include <keyboardmanager/KeyboardManagerEditorLibrary/EditorHelpers.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace EditorHelpersTests
{
    TEST_CLASS (EditorHelpersTests)
    {
    public:
        // Test if the DoKeysOverlap method returns SameKeyPreviouslyMapped on passing the same key for both arguments
        TEST_METHOD (DoKeysOverlap_ShouldReturnSameKeyPreviouslyMapped_OnPassingSameKeyForBothArguments)
        {
            // Arrange
            DWORD key1 = 0x41;
            DWORD key2 = key1;

            // Act
            auto result = EditorHelpers::DoKeysOverlap(key1, key2);

            // Assert
            Assert::IsTrue(result == ShortcutErrorType::SameKeyPreviouslyMapped);
        }

        // Test if the DoKeysOverlap method returns ConflictingModifierKey on passing left modifier and common modifier
        TEST_METHOD (DoKeysOverlap_ShouldReturnConflictingModifierKey_OnPassingLeftModifierAndCommonModifierOfSameType)
        {
            // Arrange
            DWORD key1 = VK_LCONTROL;
            DWORD key2 = VK_CONTROL;

            // Act
            auto result = EditorHelpers::DoKeysOverlap(key1, key2);

            // Assert
            Assert::IsTrue(result == ShortcutErrorType::ConflictingModifierKey);
        }

        // Test if the DoKeysOverlap method returns ConflictingModifierKey on passing right modifier and common modifier
        TEST_METHOD (DoKeysOverlap_ShouldReturnConflictingModifierKey_OnPassingRightModifierAndCommonModifierOfSameType)
        {
            // Arrange
            DWORD key1 = VK_RCONTROL;
            DWORD key2 = VK_CONTROL;

            // Act
            auto result = EditorHelpers::DoKeysOverlap(key1, key2);

            // Assert
            Assert::IsTrue(result == ShortcutErrorType::ConflictingModifierKey);
        }

        // Test if the DoKeysOverlap method returns NoError on passing left modifier and right modifier
        TEST_METHOD (DoKeysOverlap_ShouldReturnNoError_OnPassingLeftModifierAndRightModifierOfSameType)
        {
            // Arrange
            DWORD key1 = VK_LCONTROL;
            DWORD key2 = VK_RCONTROL;

            // Act
            auto result = EditorHelpers::DoKeysOverlap(key1, key2);

            // Assert
            Assert::IsTrue(result == ShortcutErrorType::NoError);
        }

        // Test if the DoKeysOverlap method returns NoError on passing keys of different types
        TEST_METHOD (DoKeysOverlap_ShouldReturnNoError_OnPassingKeysOfDifferentTypes)
        {
            // Arrange
            DWORD key1 = VK_CONTROL;
            DWORD key2 = VK_SHIFT;

            // Act
            auto result = EditorHelpers::DoKeysOverlap(key1, key2);

            // Assert
            Assert::IsTrue(result == ShortcutErrorType::NoError);
        }

        // Test if the DoKeysOverlap method returns NoError on passing different action keys
        TEST_METHOD (DoKeysOverlap_ShouldReturnNoError_OnPassingDifferentActionKeys)
        {
            // Arrange
            DWORD key1 = 0x41;
            DWORD key2 = 0x42;

            // Act
            auto result = EditorHelpers::DoKeysOverlap(key1, key2);

            // Assert
            Assert::IsTrue(result == ShortcutErrorType::NoError);
        }

        // Test if the CheckRepeatedModifier method returns true on passing vector with same modifier repeated
        TEST_METHOD (CheckRepeatedModifier_ShouldReturnTrue_OnPassingSameModifierRepeated)
        {
            // Arrange
            std::vector<int32_t> keys = { VK_CONTROL, VK_CONTROL, 0x41 };

            // Act
            bool result = EditorHelpers::CheckRepeatedModifier(keys, VK_CONTROL);

            // Assert
            Assert::IsTrue(result);
        }

        // Test if the CheckRepeatedModifier method returns true on passing vector with conflicting modifier repeated
        TEST_METHOD (CheckRepeatedModifier_ShouldReturnTrue_OnPassingConflictingModifierRepeated)
        {
            // Arrange
            std::vector<int32_t> keys = { VK_CONTROL, VK_LCONTROL, 0x41 };

            // Act
            bool result = EditorHelpers::CheckRepeatedModifier(keys, VK_LCONTROL);

            // Assert
            Assert::IsTrue(result);
        }

        // Test if the CheckRepeatedModifier method returns false on passing vector with different modifiers
        TEST_METHOD (CheckRepeatedModifier_ShouldReturnFalse_OnPassingDifferentModifiers)
        {
            // Arrange
            std::vector<int32_t> keys = { VK_CONTROL, VK_SHIFT, 0x41 };

            // Act
            bool result = EditorHelpers::CheckRepeatedModifier(keys, VK_SHIFT);

            // Assert
            Assert::IsFalse(result);
        }

        
        // Test if the IsValidShortcut method returns false on passing shortcut with null action key
        TEST_METHOD (IsValidShortcut_ShouldReturnFalse_OnPassingShortcutWithNullActionKey)
        {
            // Arrange
            Shortcut s;
            s.SetKey(NULL);

            // Act
            bool result = EditorHelpers::IsValidShortcut(s);

            // Assert
            Assert::IsFalse(result);
        }

        // Test if the IsValidShortcut method returns false on passing shortcut with only action key
        TEST_METHOD (IsValidShortcut_ShouldReturnFalse_OnPassingShortcutWithOnlyActionKey)
        {
            // Arrange
            Shortcut s;
            s.SetKey(0x41);

            // Act
            bool result = EditorHelpers::IsValidShortcut(s);

            // Assert
            Assert::IsFalse(result);
        }

        // Test if the IsValidShortcut method returns false on passing shortcut with only modifier keys
        TEST_METHOD (IsValidShortcut_ShouldReturnFalse_OnPassingShortcutWithOnlyModifierKeys)
        {
            // Arrange
            Shortcut s;
            s.SetKey(VK_CONTROL);
            s.SetKey(VK_SHIFT);

            // Act
            bool result = EditorHelpers::IsValidShortcut(s);

            // Assert
            Assert::IsFalse(result);
        }

        // Test if the IsValidShortcut method returns true on passing shortcut with modifier and action key
        TEST_METHOD (IsValidShortcut_ShouldReturnFalse_OnPassingShortcutWithModifierAndActionKey)
        {
            // Arrange
            Shortcut s;
            s.SetKey(VK_CONTROL);
            s.SetKey(0x41);

            // Act
            bool result = EditorHelpers::IsValidShortcut(s);

            // Assert
            Assert::IsTrue(result);
        }

        // Test if the DoKeysOverlap method returns NoError on passing invalid shortcut for one of the arguments
        TEST_METHOD (DoKeysOverlap_ShouldReturnNoError_OnPassingInvalidShortcutForOneOfTheArguments)
        {
            // Arrange
            Shortcut s1(std::vector<int32_t>{ NULL });
            Shortcut s2(std::vector<int32_t>{ VK_CONTROL, 0x41 });

            // Act
            auto result = EditorHelpers::DoShortcutsOverlap(s1, s2);

            // Assert
            Assert::IsTrue(result == ShortcutErrorType::NoError);
        }

        // Test if the DoKeysOverlap method returns SameShortcutPreviouslyMapped on passing same shortcut for both arguments
        TEST_METHOD (DoKeysOverlap_ShouldReturnSameShortcutPreviouslyMapped_OnPassingSameShortcutForBothArguments)
        {
            // Arrange
            Shortcut s1(std::vector<int32_t>{ VK_CONTROL, 0x41 });
            Shortcut s2 = s1;

            // Act
            auto result = EditorHelpers::DoShortcutsOverlap(s1, s2);

            // Assert
            Assert::IsTrue(result == ShortcutErrorType::SameShortcutPreviouslyMapped);
        }

        // Test if the DoKeysOverlap method returns NoError on passing shortcuts with different action keys
        TEST_METHOD (DoKeysOverlap_ShouldReturnNoError_OnPassingShortcutsWithDifferentActionKeys)
        {
            // Arrange
            Shortcut s1(std::vector<int32_t>{ VK_CONTROL, 0x42 });
            Shortcut s2(std::vector<int32_t>{ VK_CONTROL, 0x41 });

            // Act
            auto result = EditorHelpers::DoShortcutsOverlap(s1, s2);

            // Assert
            Assert::IsTrue(result == ShortcutErrorType::NoError);
        }

        // Test if the DoKeysOverlap method returns NoError on passing shortcuts with different modifiers
        TEST_METHOD (DoKeysOverlap_ShouldReturnNoError_OnPassingShortcutsWithDifferentModifiers)
        {
            // Arrange
            Shortcut s1(std::vector<int32_t>{ VK_CONTROL, 0x42 });
            Shortcut s2(std::vector<int32_t>{ VK_SHIFT, 0x42 });

            // Act
            auto result = EditorHelpers::DoShortcutsOverlap(s1, s2);

            // Assert
            Assert::IsTrue(result == ShortcutErrorType::NoError);
        }

        // Test if the DoKeysOverlap method returns ConflictingModifierShortcut on passing shortcuts with left modifier and common modifier
        TEST_METHOD (DoKeysOverlap_ShouldReturnConflictingModifierShortcut_OnPassingShortcutsWithLeftModifierAndCommonModifierOfSameType)
        {
            // Arrange
            Shortcut s1(std::vector<int32_t>{ VK_LCONTROL, 0x42 });
            Shortcut s2(std::vector<int32_t>{ VK_CONTROL, 0x42 });

            // Act
            auto result = EditorHelpers::DoShortcutsOverlap(s1, s2);

            // Assert
            Assert::IsTrue(result == ShortcutErrorType::ConflictingModifierShortcut);
        }

        // Test if the DoKeysOverlap method returns ConflictingModifierShortcut on passing shortcuts with right modifier and common modifier
        TEST_METHOD (DoKeysOverlap_ShouldReturnConflictingModifierShortcut_OnPassingShortcutsWithRightModifierAndCommonModifierOfSameType)
        {
            // Arrange
            Shortcut s1(std::vector<int32_t>{ VK_RCONTROL, 0x42 });
            Shortcut s2(std::vector<int32_t>{ VK_CONTROL, 0x42 });

            // Act
            auto result = EditorHelpers::DoShortcutsOverlap(s1, s2);

            // Assert
            Assert::IsTrue(result == ShortcutErrorType::ConflictingModifierShortcut);
        }

        // Test if the DoKeysOverlap method returns ConflictingModifierShortcut on passing shortcuts with left modifier and right modifier
        TEST_METHOD (DoKeysOverlap_ShouldReturnConflictingModifierShortcut_OnPassingShortcutsWithLeftModifierAndRightModifierOfSameType)
        {
            // Arrange
            Shortcut s1(std::vector<int32_t>{ VK_LCONTROL, 0x42 });
            Shortcut s2(std::vector<int32_t>{ VK_RCONTROL, 0x42 });

            // Act
            auto result = EditorHelpers::DoShortcutsOverlap(s1, s2);

            // Assert
            Assert::IsTrue(result == ShortcutErrorType::NoError);
        }
    };
}
