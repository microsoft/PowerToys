// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"

#include "KeystrokeFormatter.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace InputHighlighter;

namespace
{
#pragma warning(push)
#pragma warning(disable : 26497) // test helper; constexpr not required
    KeystrokeEvent MakeKey(uint32_t vk, char32_t ch, bool ctrl = false, bool alt = false, bool shift = false, bool win = false)
    {
        KeystrokeEvent e{};
        e.type = KeystrokeEventType::Down;
        e.vk = vk;
        e.ch = ch;
        e.mods = { ctrl, alt, shift, win };
        return e;
    }
#pragma warning(pop)
}

namespace MouseHighlighterKeystrokeTests
{
    TEST_CLASS(KeystrokeFormatterTests)
    {
    public:
        TEST_METHOD(PlainLetter_ShowsCharacter)
        {
            const auto e = MakeKey('A', U'a');
            Assert::AreEqual(std::wstring(L"a"), Formatter::Format(e));
            Assert::IsFalse(Formatter::IsShortcut(e));
        }

        TEST_METHOD(ShiftedCharacter_HidesShiftModifier)
        {
            // Shift is held to produce "!", but the glyph already implies it.
            const auto e = MakeKey('1', U'!', false, false, true, false);
            Assert::AreEqual(std::wstring(L"!"), Formatter::Format(e));
            // Any held modifier still classifies it as a shortcut.
            Assert::IsTrue(Formatter::IsShortcut(e));
        }

        TEST_METHOD(CtrlC_ShowsCombination)
        {
            // Ctrl+C yields a non-printable char, so ch is 0.
            const auto e = MakeKey('C', 0, true, false, false, false);
            Assert::AreEqual(std::wstring(L"Ctrl + C"), Formatter::Format(e));
            Assert::IsTrue(Formatter::IsShortcut(e));
        }

        TEST_METHOD(EnterKey_ShowsName_AndIsShortcut)
        {
            const auto e = MakeKey(VK_RETURN, 0);
            Assert::AreEqual(std::wstring(L"Enter"), Formatter::Format(e));
            Assert::IsTrue(Formatter::IsShortcut(e));
        }

        TEST_METHOD(ModifierKeyAlone_IsNotDuplicated)
        {
            // Pressing Ctrl by itself: modifier snapshot has Ctrl, key is Ctrl.
            const auto e = MakeKey(VK_CONTROL, 0, true, false, false, false);
            Assert::AreEqual(std::wstring(L"Ctrl"), Formatter::Format(e));
        }

        TEST_METHOD(ArrowKey_UsesGlyph)
        {
            const auto e = MakeKey(VK_LEFT, 0);
            Assert::AreEqual(std::wstring(L"\u2190"), Formatter::Format(e));
        }

        TEST_METHOD(WinPlusLetter_ShowsWinGlyph)
        {
            const auto e = MakeKey('D', U'd', false, false, false, true);
            Assert::AreEqual(std::wstring(L"\u229E + D"), Formatter::Format(e));
        }

        TEST_METHOD(KeyUp_ReturnsEmpty)
        {
            auto e = MakeKey('A', U'a');
            e.type = KeystrokeEventType::Up;
            Assert::AreEqual(std::wstring(L""), Formatter::Format(e));
        }

        TEST_METHOD(Whitespace_ReturnsEmpty)
        {
            const auto e = MakeKey(VK_SPACE, U' ');
            Assert::AreEqual(std::wstring(L""), Formatter::Format(e));
        }

        TEST_METHOD(IsCommandKey_Classification)
        {
            Assert::IsTrue(Formatter::IsCommandKey(VK_RETURN));
            Assert::IsTrue(Formatter::IsCommandKey(VK_F5));
            Assert::IsTrue(Formatter::IsCommandKey(VK_LEFT));
            Assert::IsFalse(Formatter::IsCommandKey('A'));
            Assert::IsFalse(Formatter::IsCommandKey('1'));
        }
    };
}
