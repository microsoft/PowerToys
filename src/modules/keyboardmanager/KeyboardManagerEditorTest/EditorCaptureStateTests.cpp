// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"

#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include <keyboardmanager/common/EditorCaptureState.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace EditorCaptureTests
{
    TEST_CLASS (EditorCaptureStateTests)
    {
    private:
        EditorCaptureState captureState;

        bool Capture(DWORD key, WPARAM message, bool engineReady, ULONG_PTR extraInfo = 0)
        {
            KBDLLHOOKSTRUCT keyData{};
            keyData.vkCode = key;
            keyData.dwExtraInfo = extraInfo;
            LowlevelKeyboardEvent event{};
            event.lParam = &keyData;
            event.wParam = message;
            return captureState.ShouldCapture(event, engineReady);
        }

    public:
        TEST_METHOD_INITIALIZE(Initialize)
        {
            captureState.Reset();
        }

        TEST_METHOD (HeldKeyReleaseBeforeRecording_ReachesEngine)
        {
            // Shift was pressed outside the recorder. Its release must clear the
            // engine's held key before the next gesture can be recorded.
            Assert::IsFalse(Capture(VK_LSHIFT, WM_KEYUP, false));
            Assert::IsFalse(captureState.IsReady());
            Assert::IsTrue(Capture('A', WM_KEYDOWN, true));
            Assert::IsTrue(Capture('A', WM_KEYUP, false));
        }

        TEST_METHOD (CaptureReadiness_IsLatchedForTheRecordingSession)
        {
            Assert::IsTrue(Capture(VK_LCONTROL, WM_KEYDOWN, true));
            // An engine hook above the recorder sees these downs and becomes busy.
            Assert::IsTrue(Capture('A', WM_KEYDOWN, false));
            Assert::IsTrue(Capture('A', WM_KEYUP, false));
            Assert::IsTrue(Capture(VK_LCONTROL, WM_KEYUP, false));
        }

        TEST_METHOD (InjectedAndReplayedEvents_PassThroughWithoutStartingCapture)
        {
            Assert::IsFalse(Capture(VK_LSHIFT, WM_KEYUP, true, KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG));
            Assert::IsFalse(Capture('A', WM_KEYDOWN, true, KeyboardManagerConstants::KEYBOARDMANAGER_REPLAY_FLAG));
            Assert::IsFalse(captureState.IsReady());
            Assert::IsFalse(Capture(VK_LSHIFT, WM_KEYUP, false));
            Assert::IsTrue(Capture('B', WM_KEYDOWN, true));
            Assert::IsFalse(Capture('C', WM_KEYUP, false, KeyboardManagerConstants::KEYBOARDMANAGER_SHORTCUT_FLAG));
        }

        TEST_METHOD (FirstKeyUpWithReadiness_StillBelongsToPreviousGesture)
        {
            Assert::IsFalse(Capture(VK_LMENU, WM_SYSKEYUP, true));
            Assert::IsTrue(Capture('A', WM_KEYDOWN, false));
        }

        TEST_METHOD (NewRecordingSession_WaitsForHeldKeysAgain)
        {
            Assert::IsTrue(Capture('A', WM_KEYDOWN, true));
            Assert::IsTrue(Capture('A', WM_KEYUP, false));
            captureState.Reset();

            Assert::IsFalse(Capture(VK_LSHIFT, WM_KEYDOWN, false));
            Assert::IsFalse(Capture(VK_LSHIFT, WM_KEYUP, false));
            Assert::IsTrue(Capture('B', WM_KEYDOWN, true));
        }

        TEST_METHOD (EnterAndEscape_AreDeliveredToKeyDelayAfterThePriorGesture)
        {
            Assert::IsFalse(Capture(VK_LSHIFT, WM_KEYUP, false));
            Assert::IsTrue(Capture(VK_RETURN, WM_KEYDOWN, true));
            Assert::IsTrue(Capture(VK_RETURN, WM_KEYUP, false));
            Assert::IsTrue(Capture(VK_ESCAPE, WM_KEYDOWN, false));
            Assert::IsTrue(Capture(VK_ESCAPE, WM_KEYUP, false));
        }
    };
}
