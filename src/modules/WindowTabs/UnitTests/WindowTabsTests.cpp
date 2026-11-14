// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <windows.h>
#include <roapi.h>
#include <CppUnitTest.h>
#include "../GroupModel.h"
#include "../Session.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace WindowTabs;

namespace WindowTabsUnitTests
{
    // clang-format off
    TEST_CLASS(WindowTabsTests)
    {
        bool m_apartmentInitialized = false;
    public:
        TEST_METHOD(RestorationMatchesReopenedWindowsInSavedOrder)
        {
            SavedWindow editor{ L"C:\\editor.exe", L"Editor", L"Document" };
            SavedWindow browser{ L"C:\\browser.exe", L"Browser", L"Research" };
            SavedGroup group{ { editor, browser }, 1 };
            auto ids = MatchGroup(group, { { 99, browser }, { 42, editor } });
            Assert::IsTrue(ids == std::vector<WindowId>{ 42, 99 });
        }
        TEST_METHOD(RestorationLeavesMissingAndAmbiguousWindowsUntouched)
        {
            SavedWindow editor{ L"C:\\editor.exe", L"Editor", L"Document" };
            SavedWindow browser{ L"C:\\browser.exe", L"Browser", L"Research" };
            SavedGroup group{ { editor, browser }, 0 };
            Assert::IsTrue(MatchGroup(group, { { 42, editor } }).empty());
            Assert::IsTrue(MatchGroup(group, { { 42, editor }, { 43, editor }, { 99, browser } }).empty());
            browser.title = L"Changed";
            Assert::IsTrue(MatchGroup(group, { { 42, editor }, { 99, browser } }).empty());
        }
        TEST_METHOD_INITIALIZE(Initialize)
        {
            const auto result = RoInitialize(RO_INIT_MULTITHREADED);
            m_apartmentInitialized = SUCCEEDED(result);
            if (!m_apartmentInitialized && result != RPC_E_CHANGED_MODE)
                winrt::throw_hresult(result);
        }
        TEST_METHOD_CLEANUP(Cleanup)
        {
            if (m_apartmentInitialized) RoUninitialize();
        }

        TEST_METHOD(RejectsDuplicateAndNullWindows)
        {
            Group group;
            Assert::IsFalse(group.Add(0));
            Assert::IsTrue(group.Add(10));
            Assert::IsFalse(group.Add(10));
            Assert::IsTrue(group.active == 10);
        }
        TEST_METHOD(ReorderingPreservesSelection)
        {
            Group group{ { 1, 2, 3 }, 2 };
            Assert::IsTrue(group.Reorder(2, 0));
            Assert::IsTrue(group.tabs == std::vector<WindowId>{ 2, 1, 3 });
            Assert::IsTrue(group.active == 2);
            Assert::IsFalse(group.Reorder(3, 3));
            Assert::IsFalse(group.Reorder(4, 0));
        }
        TEST_METHOD(DetachingSelectedTabSelectsItsNeighbor)
        {
            Group group{ { 1, 2, 3 }, 2 };
            group.Remove(2);
            Assert::IsTrue(group.active == 3);
            group.Remove(3);
            Assert::IsTrue(group.active == 1);
            group.Remove(1);
            Assert::IsTrue(group.active == 0);
        }
        TEST_METHOD(RemovingUnselectedOrMissingTabPreservesSelection)
        {
            Group group{ { 1, 2, 3 }, 2 };
            group.Remove(7);
            group.Remove(1);
            Assert::IsTrue(group.active == 2);
            Assert::IsFalse(group.Select(8));
        }
        TEST_METHOD(CyclingWrapsInBothDirections)
        {
            Group group{ { 1, 2, 3 }, 3 };
            Assert::IsTrue(group.Cycle(true) == 1);
            group.Select(1);
            Assert::IsTrue(group.Cycle(false) == 3);
            Assert::IsTrue(Group{}.Cycle(true) == 0);
        }
        TEST_METHOD(GroupSizeIsBounded)
        {
            Group group;
            for (WindowId id = 1; id <= 32; ++id)
            Assert::IsTrue(group.Add(id));
            Assert::IsFalse(group.Add(33));
        }
        TEST_METHOD(SessionRoundTripsUnicodeTitlesAndSelection)
        {
            SavedGroup saved{ { { L"C:\\Apps\\editor.exe", L"Editor", L"文書 – editor" }, { L"C:\\Apps\\browser.exe", L"Browser", L"Research" } }, 1 };
            auto parsed = ParseSession(SerializeSession({ saved }));
            Assert::AreEqual(size_t{ 1 }, parsed.size());
            Assert::IsTrue(parsed[0].tabs == saved.tabs);
            Assert::AreEqual(size_t{ 1 }, parsed[0].selected);
        }
        TEST_METHOD(RejectsMalformedOrFutureSessions)
        {
            for (auto text : { L"", L"null", L"{", L"[]", L"{\"version\":2,\"groups\":[]}", L"{\"version\":1,\"groups\":[null]}" })
            Assert::IsTrue(ParseSession(text).empty());
        }
        TEST_METHOD(RejectsInvalidSelectionAndDuplicateIdentities)
        {
            SavedWindow window{ L"C:\\app.exe", L"Window", L"Title" };
            SavedWindow other{ L"C:\\other.exe", L"OtherWindow", L"Other title" };
            Assert::IsTrue(ParseSession(SerializeSession({ { { window, other }, 2 } })).empty());
            Assert::IsTrue(ParseSession(SerializeSession({ { { window, window }, 0 } })).empty());
            Assert::IsTrue(ParseSession(SerializeSession({ { { window }, 0 } })).empty());
        }
        TEST_METHOD(RejectsOversizedSessionAndFields)
        {
            Assert::IsTrue(ParseSession(std::wstring(512 * 1024 + 1, L' ')).empty());
            SavedWindow window{ L"C:\\app.exe", L"Window", std::wstring(4097, L'a') };
            SavedWindow other{ L"C:\\other.exe", L"OtherWindow", L"Other title" };
            Assert::IsTrue(ParseSession(SerializeSession({ { { window, other }, 0 } })).empty());
        }
    };
}
