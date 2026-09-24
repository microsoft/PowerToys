// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"

#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "MockedInput.h"
#include "TestHelpers.h"
#include <keyboardmanager/KeyboardManagerEngineLibrary/EditorSuspensionState.h>
#include <keyboardmanager/KeyboardManagerEngineLibrary/KeyboardEventHandlers.h>
#include <thread>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RemappingLogicTests
{
    TEST_CLASS (EditorSuspensionTests)
    {
    private:
        KeyboardManagerInput::MockedInput input;
        State state;
        EditorSuspensionState suspension;
        bool editorOpen = false;

        void SendKey(WORD key, bool keyUp = false)
        {
            input.SendVirtualInput({ { .type = INPUT_KEYBOARD, .ki = { .wVk = key, .dwFlags = keyUp ? KEYEVENTF_KEYUP : 0UL } } });
        }

        void HoldModifierBeforeHook(WORD key)
        {
            input.SetKeyboardState(key, true);
            input.SetKeyboardState(Helpers::GetCombinedKey(key), true);
            suspension.AddInitiallyPressedModifier(key);
        }

        void VerifyInitiallyHeldShortcut(bool appSpecific, bool openEditor)
        {
            Shortcut source(L"17;65");
            Shortcut target(L"18;86");
            if (appSpecific)
            {
                state.AddAppSpecificShortcut(L"notepad.exe", source, target);
                input.SetForegroundProcess(L"notepad.exe");
            }
            else
            {
                state.AddOSLevelShortcut(source, target);
            }

            HoldModifierBeforeHook(VK_LCONTROL);
            SendKey('A');
            Assert::IsTrue(input.GetVirtualKeyState(VK_MENU));
            Assert::IsTrue(input.GetVirtualKeyState('V'));

            SendKey('A', true);
            Assert::IsFalse(input.GetVirtualKeyState('V'));
            Assert::IsTrue(input.GetVirtualKeyState(VK_MENU));
            Assert::IsFalse(suspension.IsIdle());

            editorOpen = openEditor;
            SendKey(VK_LCONTROL, true);
            Assert::IsFalse(input.GetVirtualKeyState(VK_MENU));
            Assert::IsFalse(input.GetVirtualKeyState(VK_CONTROL));
            Assert::IsTrue(suspension.IsIdle());
            Assert::IsFalse(state.CheckShortcutRemapInvoked(appSpecific ? std::optional<std::wstring>(L"notepad.exe") : std::nullopt));

            if (openEditor)
            {
                SendKey('B');
                Assert::IsTrue(suspension.IsSuspended());
            }
        }

        bool SkipKey(DWORD key, WPARAM message, bool requested, DWORD scanCode = 0, DWORD flags = 0, ULONG_PTR extraInfo = 0)
        {
            KBDLLHOOKSTRUCT keyData{};
            keyData.vkCode = key;
            keyData.scanCode = scanCode;
            keyData.flags = flags;
            keyData.dwExtraInfo = extraInfo;
            LowlevelKeyboardEvent event{};
            event.lParam = &keyData;
            event.wParam = message;
            return suspension.ShouldSkipRemapping(event, requested);
        }

    public:
        TEST_METHOD_INITIALIZE(Initialize)
        {
            TestHelpers::ResetTestEnv(input, state);
            suspension.Reset();
            editorOpen = false;
            input.SetHookProc([this](LowlevelKeyboardEvent* event) -> intptr_t {
                bool skipSingleKeyRemapping = false;
                const bool skip = suspension.ShouldSkipRemapping(*event, editorOpen, &skipSingleKeyRemapping);
                if (event->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
                {
                    return 1;
                }

                if (skip)
                {
                    return 0;
                }

                if (!skipSingleKeyRemapping && KeyboardEventHandlers::HandleSingleKeyRemapEvent(input, event, state) == 1)
                {
                    return 1;
                }

                if (KeyboardEventHandlers::HandleAppSpecificShortcutRemapEvent(input, event, state) == 1)
                {
                    return 1;
                }

                return KeyboardEventHandlers::HandleOSLevelShortcutRemapEvent(input, event, state);
            });
        }

        TEST_METHOD (OpeningEditorWhileRemappedKeyHeld_ReleasesRemappedModifier)
        {
            state.AddSingleKeyRemap(VK_F8, static_cast<DWORD>(VK_LCONTROL));
            SendKey(VK_F8);
            Assert::IsTrue(input.GetVirtualKeyState(VK_LCONTROL));

            editorOpen = true;
            SendKey(VK_F8, true);
            Assert::IsFalse(input.GetVirtualKeyState(VK_LCONTROL));
            Assert::IsFalse(input.GetVirtualKeyState(VK_F8));

            SendKey(VK_F8);
            Assert::IsTrue(input.GetVirtualKeyState(VK_F8));
            Assert::IsFalse(input.GetVirtualKeyState(VK_LCONTROL));
        }

        TEST_METHOD (ClosingEditorWhileKeyHeld_PassesThroughMatchingRelease)
        {
            state.AddSingleKeyRemap(VK_F8, static_cast<DWORD>(VK_LCONTROL));
            editorOpen = true;
            SendKey(VK_F8);
            Assert::IsTrue(input.GetVirtualKeyState(VK_F8));

            editorOpen = false;
            SendKey(VK_F8, true);
            Assert::IsFalse(input.GetVirtualKeyState(VK_F8));
            Assert::IsFalse(input.GetVirtualKeyState(VK_LCONTROL));

            SendKey(VK_F8);
            Assert::IsTrue(input.GetVirtualKeyState(VK_LCONTROL));
            Assert::IsFalse(input.GetVirtualKeyState(VK_F8));
        }

        TEST_METHOD (OpeningEditorWhileShortcutHeld_ReleasesTargetAndSourceModifiers)
        {
            Shortcut source;
            source.SetKey(VK_CONTROL);
            source.SetKey('A');
            Shortcut target;
            target.SetKey(VK_MENU);
            target.SetKey('V');
            state.AddOSLevelShortcut(source, target);

            SendKey(VK_LCONTROL);
            SendKey('A');
            Assert::IsTrue(input.GetVirtualKeyState(VK_MENU));
            Assert::IsTrue(input.GetVirtualKeyState('V'));

            editorOpen = true;
            SendKey('A', true);
            SendKey(VK_LCONTROL, true);
            Assert::IsFalse(input.GetVirtualKeyState(VK_MENU));
            Assert::IsFalse(input.GetVirtualKeyState('V'));
            Assert::IsFalse(input.GetVirtualKeyState(VK_CONTROL));

            SendKey('B');
            Assert::IsTrue(suspension.IsSuspended());
        }

        TEST_METHOD (RepeatedKeyDowns_DoNotDelaySuspensionAfterOneRelease)
        {
            Assert::IsFalse(SkipKey(VK_F8, WM_KEYDOWN, false));
            Assert::IsFalse(SkipKey(VK_F8, WM_KEYDOWN, true));
            Assert::IsFalse(SkipKey(VK_F8, WM_KEYDOWN, true));
            Assert::IsFalse(SkipKey(VK_F8, WM_KEYUP, true));
            Assert::IsTrue(SkipKey('A', WM_KEYDOWN, true));
        }

        TEST_METHOD (SystemKeyMessages_KeepTheModeOfTheirMatchingKeyDown)
        {
            Assert::IsFalse(SkipKey(VK_LMENU, WM_SYSKEYDOWN, false));
            Assert::IsFalse(SkipKey(VK_LMENU, WM_SYSKEYUP, true));
            Assert::IsTrue(SkipKey(VK_LMENU, WM_SYSKEYDOWN, true));
            Assert::IsTrue(SkipKey(VK_LMENU, WM_SYSKEYUP, false));
            Assert::IsFalse(SkipKey(VK_LMENU, WM_SYSKEYDOWN, false));
        }

        TEST_METHOD (NumpadVirtualKeyChanges_StillReleaseTheSamePhysicalKey)
        {
            Assert::IsFalse(SkipKey(VK_NUMPAD1, WM_KEYDOWN, false, 0x4f));
            Assert::IsFalse(SkipKey(VK_END, WM_KEYUP, true, 0x4f));
            Assert::IsTrue(SkipKey('A', WM_KEYDOWN, true, 0x1e));
        }

        TEST_METHOD (ExtendedKeys_DoNotReleaseNonExtendedKeysWithTheSameScanCode)
        {
            Assert::IsFalse(SkipKey(VK_RETURN, WM_KEYDOWN, false, 0x1c));
            Assert::IsFalse(SkipKey(VK_RETURN, WM_KEYDOWN, true, 0x1c, LLKHF_EXTENDED));
            Assert::IsFalse(SkipKey(VK_RETURN, WM_KEYUP, true, 0x1c, LLKHF_EXTENDED));
            Assert::IsFalse(SkipKey('A', WM_KEYDOWN, true, 0x1e));
            Assert::IsFalse(SkipKey('A', WM_KEYUP, true, 0x1e));
            Assert::IsFalse(SkipKey(VK_RETURN, WM_KEYUP, true, 0x1c));
            Assert::IsTrue(SkipKey('B', WM_KEYDOWN, true, 0x30));
        }

        TEST_METHOD (InjectedReleaseAfterLastSourceRelease_DoesNotChangeMode)
        {
            Assert::IsFalse(SkipKey(VK_F8, WM_KEYDOWN, false));
            Assert::IsFalse(SkipKey(VK_F8, WM_KEYUP, true));
            Assert::IsFalse(SkipKey(VK_LCONTROL, WM_KEYUP, true, 0, 0, KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG));
            Assert::IsTrue(SkipKey('A', WM_KEYDOWN, true));
        }

        TEST_METHOD (ReplayedNumpadEnter_DoesNotCreateAPhantomHeldKey)
        {
            Assert::IsFalse(SkipKey(VK_RETURN, WM_KEYDOWN, false, 0x1c, LLKHF_EXTENDED));
            Assert::IsFalse(SkipKey(VK_RETURN, WM_KEYDOWN, true, 0x1c, 0, KeyboardManagerConstants::KEYBOARDMANAGER_REPLAY_FLAG));
            Assert::IsFalse(SkipKey(VK_RETURN, WM_KEYUP, true, 0x1c, LLKHF_EXTENDED));
            Assert::IsTrue(SkipKey('A', WM_KEYDOWN, true));
        }

        TEST_METHOD (HookRestart_DoesNotRemapAReleaseWhoseKeyDownWasNotObserved)
        {
            Assert::IsFalse(SkipKey(VK_F8, WM_KEYDOWN, false));
            suspension.Reset();
            Assert::IsTrue(SkipKey(VK_F8, WM_KEYUP, false));
            Assert::IsTrue(SkipKey('A', WM_KEYDOWN, true));
        }

        TEST_METHOD (InitiallyHeldModifier_ReleasesGlobalShortcutTarget)
        {
            VerifyInitiallyHeldShortcut(false, false);
        }

        TEST_METHOD (InitiallyHeldModifier_ReleasesAppSpecificShortcutTarget)
        {
            VerifyInitiallyHeldShortcut(true, false);
        }

        TEST_METHOD (InitiallyHeldModifier_FinishesShortcutBeforeEditorSuspends)
        {
            VerifyInitiallyHeldShortcut(false, true);
        }

        TEST_METHOD (InitiallyHeldModifier_DoesNotReleaseAnUnrelatedSingleKeyTarget)
        {
            state.AddSingleKeyRemap(VK_LCONTROL, static_cast<DWORD>(VK_F8));
            HoldModifierBeforeHook(VK_LCONTROL);
            input.SetKeyboardState(VK_F8, true);

            SendKey(VK_LCONTROL, true);
            Assert::IsFalse(input.GetVirtualKeyState(VK_CONTROL));
            Assert::IsTrue(input.GetVirtualKeyState(VK_F8));
        }

        TEST_METHOD (InitiallyHeldModifier_NewDownStartsANormalSingleKeyRemap)
        {
            state.AddSingleKeyRemap(VK_LCONTROL, static_cast<DWORD>(VK_F8));
            HoldModifierBeforeHook(VK_LCONTROL);

            SendKey(VK_LCONTROL);
            Assert::IsTrue(input.GetVirtualKeyState(VK_F8));
            SendKey(VK_LCONTROL, true);
            Assert::IsFalse(input.GetVirtualKeyState(VK_F8));
            Assert::IsTrue(suspension.IsIdle());
        }

        TEST_METHOD (InitiallyHeldModifiers_DrainBothSidesBeforeSuspension)
        {
            suspension.AddInitiallyPressedModifier(VK_LCONTROL);
            suspension.AddInitiallyPressedModifier(VK_RCONTROL);

            Assert::IsFalse(SkipKey(VK_LCONTROL, WM_KEYUP, true, 0x1d));
            Assert::IsFalse(suspension.IsIdle());
            Assert::IsFalse(SkipKey(VK_RCONTROL, WM_KEYUP, true, 0x1d, LLKHF_EXTENDED));
            Assert::IsTrue(suspension.IsIdle());
            Assert::IsTrue(SkipKey('A', WM_KEYDOWN, true));
        }

        TEST_METHOD (InitiallyHeldModifier_AlreadyOpenEditorDoesNotStartRemapping)
        {
            state.AddOSLevelShortcut(Shortcut(L"17;65"), Shortcut(L"18;86"));
            editorOpen = true;
            suspension.Reset(true);
            HoldModifierBeforeHook(VK_LCONTROL);

            SendKey('A');
            SendKey('A', true);
            SendKey(VK_LCONTROL, true);
            Assert::IsFalse(input.GetVirtualKeyState(VK_MENU));
            Assert::IsFalse(input.GetVirtualKeyState('V'));
            Assert::IsTrue(suspension.IsIdle());
        }

        TEST_METHOD (CaptureReadiness_AllowsDownstreamRecorderToCaptureTheFirstKeyDown)
        {
            HANDLE readyEvent = CreateEvent(nullptr, true, true, nullptr);
            Assert::IsNotNull(readyEvent);
            {
                EditorCaptureReadyScope publishCaptureReady(suspension, readyEvent, true);
                Assert::IsTrue(SkipKey(VK_F8, WM_KEYDOWN, true));

                // The downstream recorder sees readiness for the gesture that preceded
                // this down, allowing it to capture this first key instead of losing it.
                Assert::IsTrue(WaitForSingleObject(readyEvent, 0) == WAIT_OBJECT_0);
            }

            Assert::IsTrue(WaitForSingleObject(readyEvent, 0) == WAIT_TIMEOUT);
            CloseHandle(readyEvent);
        }

        TEST_METHOD (CaptureReadiness_WaitsForLastKeyUpAndInjectedReleasesToFinish)
        {
            HANDLE readyEvent = CreateEvent(nullptr, true, false, nullptr);
            Assert::IsNotNull(readyEvent);
            Assert::IsFalse(SkipKey(VK_F8, WM_KEYDOWN, false));
            {
                EditorCaptureReadyScope publishCaptureReady(suspension, readyEvent, true);
                Assert::IsFalse(SkipKey(VK_F8, WM_KEYUP, true));
                {
                    // A recursive generated release must not publish readiness before
                    // the outer source key-up has passed through the rest of the chain.
                    EditorCaptureReadyScope injectedRelease(suspension, readyEvent, false);
                    Assert::IsFalse(SkipKey(VK_LCONTROL, WM_KEYUP, true, 0, 0, KeyboardManagerConstants::KEYBOARDMANAGER_SINGLEKEY_FLAG));
                }

                Assert::IsTrue(WaitForSingleObject(readyEvent, 0) == WAIT_TIMEOUT);
            }

            Assert::IsTrue(WaitForSingleObject(readyEvent, 0) == WAIT_OBJECT_0);
            CloseHandle(readyEvent);
        }

        TEST_METHOD (CaptureReadiness_AlsoDrainsKeysPressedWhileSuspended)
        {
            HANDLE readyEvent = CreateEvent(nullptr, true, true, nullptr);
            Assert::IsNotNull(readyEvent);
            {
                EditorCaptureReadyScope publishCaptureReady(suspension, readyEvent, true);
                Assert::IsTrue(SkipKey(VK_F8, WM_KEYDOWN, true));
            }

            Assert::IsTrue(WaitForSingleObject(readyEvent, 0) == WAIT_TIMEOUT);
            {
                EditorCaptureReadyScope publishCaptureReady(suspension, readyEvent, true);
                Assert::IsTrue(SkipKey(VK_F8, WM_KEYUP, true));
                Assert::IsTrue(WaitForSingleObject(readyEvent, 0) == WAIT_TIMEOUT);
            }

            Assert::IsTrue(WaitForSingleObject(readyEvent, 0) == WAIT_OBJECT_0);
            CloseHandle(readyEvent);
        }

        TEST_METHOD (EditorMutex_RecoversWhenOwningThreadExitsWithoutCleanup)
        {
            HANDLE mutex = CreateMutex(nullptr, false, nullptr);
            HANDLE acquiredEvent = CreateEvent(nullptr, true, false, nullptr);
            HANDLE exitEvent = CreateEvent(nullptr, true, false, nullptr);
            Assert::IsNotNull(mutex);
            Assert::IsNotNull(acquiredEvent);
            Assert::IsNotNull(exitEvent);

            bool ownerAcquired = false;
            std::thread owner([&] {
                ownerAcquired = WaitForSingleObject(mutex, 1000) == WAIT_OBJECT_0;
                SetEvent(acquiredEvent);
                WaitForSingleObject(exitEvent, 5000);
                // Deliberately abandon ownership, like an editor terminated without cleanup.
            });

            const bool signaled = WaitForSingleObject(acquiredEvent, 3000) == WAIT_OBJECT_0;
            const bool editorRunning = IsEditorMutexOwned(mutex);
            SetEvent(exitEvent);
            owner.join();
            const bool editorStillRunning = IsEditorMutexOwned(mutex);
            bool releasedAfterProbe = false;
            std::thread probe([&] {
                releasedAfterProbe = WaitForSingleObject(mutex, 0) == WAIT_OBJECT_0;
                if (releasedAfterProbe)
                {
                    ReleaseMutex(mutex);
                }
            });
            probe.join();

            CloseHandle(exitEvent);
            CloseHandle(acquiredEvent);
            CloseHandle(mutex);
            Assert::IsTrue(signaled);
            Assert::IsTrue(ownerAcquired);
            Assert::IsTrue(editorRunning);
            Assert::IsFalse(editorStillRunning);
            Assert::IsTrue(releasedAfterProbe);
        }
    };
}
