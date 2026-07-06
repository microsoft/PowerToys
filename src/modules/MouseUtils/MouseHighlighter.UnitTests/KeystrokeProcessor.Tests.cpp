// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"

#include "KeystrokeProcessor.h"

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
    TEST_CLASS(KeystrokeProcessorTests)
    {
    public:
        TEST_METHOD(Last5_AlwaysAdds)
        {
            KeystrokeProcessor p;
            auto r = p.Process(MakeKey('A', U'a'), DisplayMode::Last5);
            Assert::IsTrue(r.action == KeystrokeAction::Add);
            Assert::AreEqual(std::wstring(L"a"), r.text);
        }

        TEST_METHOD(SingleCharactersOnly_IgnoresShortcuts)
        {
            KeystrokeProcessor p;
            auto plain = p.Process(MakeKey('A', U'a'), DisplayMode::SingleCharactersOnly);
            Assert::IsTrue(plain.action == KeystrokeAction::Add);

            auto shortcut = p.Process(MakeKey('C', 0, true), DisplayMode::SingleCharactersOnly);
            Assert::IsTrue(shortcut.action == KeystrokeAction::None);
        }

        TEST_METHOD(ShortcutsOnly_IgnoresPlainCharacters)
        {
            KeystrokeProcessor p;
            auto plain = p.Process(MakeKey('A', U'a'), DisplayMode::ShortcutsOnly);
            Assert::IsTrue(plain.action == KeystrokeAction::None);

            auto shortcut = p.Process(MakeKey('C', 0, true), DisplayMode::ShortcutsOnly);
            Assert::IsTrue(shortcut.action == KeystrokeAction::Add);
            Assert::AreEqual(std::wstring(L"Ctrl + C"), shortcut.text);
        }

        TEST_METHOD(Stream_BuildsAndReplacesWord)
        {
            KeystrokeProcessor p;
            auto h = p.Process(MakeKey('H', U'h'), DisplayMode::Stream);
            Assert::IsTrue(h.action == KeystrokeAction::Add);
            Assert::AreEqual(std::wstring(L"h"), h.text);

            auto i = p.Process(MakeKey('I', U'i'), DisplayMode::Stream);
            Assert::IsTrue(i.action == KeystrokeAction::ReplaceLast);
            Assert::AreEqual(std::wstring(L"hi"), i.text);
        }

        TEST_METHOD(Stream_BackspaceEditsThenRemoves)
        {
            KeystrokeProcessor p;
            p.Process(MakeKey('H', U'h'), DisplayMode::Stream);
            p.Process(MakeKey('I', U'i'), DisplayMode::Stream);

            auto back1 = p.Process(MakeKey(VK_BACK, 0), DisplayMode::Stream);
            Assert::IsTrue(back1.action == KeystrokeAction::ReplaceLast);
            Assert::AreEqual(std::wstring(L"h"), back1.text);

            auto back2 = p.Process(MakeKey(VK_BACK, 0), DisplayMode::Stream);
            Assert::IsTrue(back2.action == KeystrokeAction::RemoveLast);

            auto back3 = p.Process(MakeKey(VK_BACK, 0), DisplayMode::Stream);
            Assert::IsTrue(back3.action == KeystrokeAction::None);
        }

        TEST_METHOD(Stream_ShortcutResetsBuffer)
        {
            KeystrokeProcessor p;
            p.Process(MakeKey('A', U'a'), DisplayMode::Stream); // buffer "a"

            auto shortcut = p.Process(MakeKey('S', 0, true), DisplayMode::Stream);
            Assert::IsTrue(shortcut.action == KeystrokeAction::Add);
            Assert::AreEqual(std::wstring(L"Ctrl + S"), shortcut.text);

            // Buffer was reset, so the next character starts a new pill.
            auto b = p.Process(MakeKey('B', U'b'), DisplayMode::Stream);
            Assert::IsTrue(b.action == KeystrokeAction::Add);
            Assert::AreEqual(std::wstring(L"b"), b.text);
        }
    };
}
