// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#include "pch.h"
#include <WindowProperties/WorkspacesWindowPropertyUtils.h>
#include <wil/resource.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace WorkspacesWindowProperties;

namespace WorkspacesLibUnitTests
{
    TEST_CLASS(WindowPropertyTests)
    {
        static wil::unique_hwnd CreateMessageWindow()
        {
            wil::unique_hwnd window{ CreateWindowExW(0, L"STATIC", nullptr, 0, 0, 0, 0, 0, HWND_MESSAGE, nullptr, nullptr, nullptr) };
            Assert::IsTrue(window.get() != nullptr);
            return window;
        }

        static auto LegacyWords(const wchar_t* id)
        {
            GUID guid{};
            Assert::IsTrue(SUCCEEDED(CLSIDFromString(id, &guid)));
            std::array<ULONG_PTR, sizeof(GUID) / sizeof(ULONG_PTR)> words{};
            std::memcpy(words.data(), &guid, sizeof(guid));
            return words;
        }

        static std::wstring LegacyName(size_t index)
        {
            return L"PowerToys_WorkspacesAppId" + std::to_wstring(index);
        }

    public:
        TEST_METHOD(BracedAndUnbracedIdsRoundTripThroughTheExistingWindowProperties)
        {
            auto window = CreateMessageWindow();
            const std::wstring first = L"{00112233-4455-6677-8899-AABBCCDDEEFF}";
            StampWorkspacesGuidProperty(window.get(), L"00112233-4455-6677-8899-aabbccddeeff");
            Assert::AreEqual(first, GetGuidFromHwnd(window.get()));

            // Existing snapshot readers still see the same raw words under the same names.
            const auto words = LegacyWords(first.c_str());
            for (size_t i = 0; i < words.size(); ++i)
            {
                Assert::IsTrue(reinterpret_cast<ULONG_PTR>(GetPropW(window.get(), LegacyName(i).c_str())) == words[i]);
            }

            StampWorkspacesGuidProperty(window.get(), L"{abcdef12-3456-7890-1234-56789abcdef0}");
            Assert::AreEqual(std::wstring(L"{ABCDEF12-3456-7890-1234-56789ABCDEF0}"), GetGuidFromHwnd(window.get()));
        }

        TEST_METHOD(ReadsLegacyZeroWordsOnlyWhenEveryPropertyIsPresent)
        {
            auto window = CreateMessageWindow();
            // EnumPropsExW may also supply unrelated integer atoms instead of string pointers.
            Assert::IsTrue(SetPropW(window.get(), MAKEINTRESOURCEW(0x61), reinterpret_cast<HANDLE>(1)) != FALSE);
            Assert::IsTrue(GetGuidFromHwnd(window.get()).empty());

            const std::wstring id = L"{00000000-0000-0000-0123-456789ABCDEF}";
            const auto words = LegacyWords(id.c_str());
            for (size_t i = 0; i < words.size(); ++i)
            {
                Assert::IsTrue(SetPropW(window.get(), LegacyName(i).c_str(), reinterpret_cast<HANDLE>(words[i])) != FALSE);
            }
            Assert::AreEqual(id, GetGuidFromHwnd(window.get()));

            RemovePropW(window.get(), LegacyName(0).c_str());
            Assert::IsTrue(GetGuidFromHwnd(window.get()).empty());

            StampWorkspacesGuidProperty(window.get(), L"00000000-0000-0000-0000-000000000000");
            Assert::AreEqual(std::wstring(L"{00000000-0000-0000-0000-000000000000}"), GetGuidFromHwnd(window.get()));
            RemovePropW(window.get(), LegacyName(words.size() - 1).c_str());
            Assert::IsTrue(GetGuidFromHwnd(window.get()).empty());
            RemovePropW(window.get(), MAKEINTRESOURCEW(0x61));
        }

        TEST_METHOD(MalformedIdsDoNotReplaceAnExistingStamp)
        {
            auto window = CreateMessageWindow();
            const std::wstring id = L"{01234567-89AB-CDEF-0000-000000000000}";
            StampWorkspacesGuidProperty(window.get(), id);
            for (const auto* invalid : { L"", L"not-a-guid", L"01234567-89ab-cdef-0000-00000000000Z", L"(01234567-89ab-cdef-0000-000000000000)" })
            {
                StampWorkspacesGuidProperty(window.get(), invalid);
                Assert::AreEqual(id, GetGuidFromHwnd(window.get()));
            }
        }
    };
}
