#include "pch.h"
#include <CppUnitTest.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace
{
    std::vector<wchar_t> clipboardText;
    std::vector<std::wstring> clipboardCalls;
    SIZE_T clipboardBytes = 0;
    bool canOpen = true;
    bool hasText = true;
    bool hasData = true;
    bool canLock = true;
    int openCount = 0;
    int closeCount = 0;
    int lockCount = 0;
    int unlockCount = 0;
    int dataCount = 0;
    DWORD fixtureSequence = 1;

    BOOL TestOpenClipboard(HWND)
    {
        clipboardCalls.push_back(L"OpenClipboard");
        ++openCount;
        return canOpen;
    }

    BOOL TestCloseClipboard()
    {
        clipboardCalls.push_back(L"CloseClipboard");
        ++closeCount;
        return TRUE;
    }

    BOOL TestIsClipboardFormatAvailable(UINT format)
    {
        Assert::AreEqual(static_cast<UINT>(CF_UNICODETEXT), format);
        return hasText;
    }

    HANDLE TestGetClipboardData(UINT format)
    {
        Assert::AreEqual(static_cast<UINT>(CF_UNICODETEXT), format);
        ++dataCount;
        return hasData ? reinterpret_cast<HANDLE>(1) : nullptr;
    }

    SIZE_T TestGlobalSize(HGLOBAL)
    {
        return clipboardBytes;
    }

    LPVOID TestGlobalLock(HGLOBAL)
    {
        clipboardCalls.push_back(L"GlobalLock");
        ++lockCount;
        return canLock ? clipboardText.data() : nullptr;
    }

    BOOL TestGlobalUnlock(HGLOBAL)
    {
        clipboardCalls.push_back(L"GlobalUnlock");
        ++unlockCount;
        return TRUE;
    }

    DWORD TestGetClipboardSequenceNumber()
    {
        return fixtureSequence;
    }

    void SetText(const std::wstring& text, bool terminated = true)
    {
        clipboardText.assign(text.begin(), text.end());
        if (terminated)
        {
            clipboardText.push_back(L'\0');
        }
        clipboardBytes = clipboardText.size() * sizeof(wchar_t);
        ++fixtureSequence;
    }

    std::vector<INPUT> sentInputs;
    std::vector<UINT> sendInputBatches;
    std::vector<UINT> sendInputLimits;
    size_t sendInputCall = 0;
    bool keyDownState[256] = {};
    HWND foregroundWindow = nullptr;
    int callNextCount = 0;
    int postQuitCount = 0;

    UINT TestSendInput(UINT count, LPINPUT inputs, int)
    {
        sendInputBatches.push_back(count);
        UINT sent = count;
        if (sendInputCall < sendInputLimits.size())
        {
            sent = count < sendInputLimits[sendInputCall] ? count : sendInputLimits[sendInputCall];
        }
        ++sendInputCall;
        sentInputs.insert(sentInputs.end(), inputs, inputs + sent);
        return sent;
    }

    SHORT TestGetKeyState(int vk)
    {
        return keyDownState[vk & 0xFF] ? static_cast<SHORT>(0x8000) : 0;
    }

    HWND TestGetForegroundWindow()
    {
        return foregroundWindow;
    }

    LRESULT TestCallNextHookEx(HHOOK, int, WPARAM, LPARAM)
    {
        ++callNextCount;
        return 0;
    }

    void TestPostQuitMessage(int)
    {
        ++postQuitCount;
    }
}

// Compile the actual reader and prefix handling against isolated clipboard data.
#define OpenClipboard TestOpenClipboard
#define CloseClipboard TestCloseClipboard
#define IsClipboardFormatAvailable TestIsClipboardFormatAvailable
#define GetClipboardData TestGetClipboardData
#define GlobalSize TestGlobalSize
#define GlobalLock TestGlobalLock
#define GlobalUnlock TestGlobalUnlock
#define GetClipboardSequenceNumber TestGetClipboardSequenceNumber
// Observe injections and hook decisions without touching real input.
#define SendInput TestSendInput
#define GetKeyState TestGetKeyState
#define GetAsyncKeyState TestGetKeyState
#define GetForegroundWindow TestGetForegroundWindow
#define CallNextHookEx TestCallNextHookEx
#define PostQuitMessage TestPostQuitMessage
// Keep production CRT diagnostics separate from the test code.
#pragma warning(push)
#pragma warning(disable : 4996)
#include "..\ZoomIt\DemoType.cpp"
#pragma warning(pop)
#undef OpenClipboard
#undef CloseClipboard
#undef IsClipboardFormatAvailable
#undef GetClipboardData
#undef GlobalSize
#undef GlobalLock
#undef GlobalUnlock
#undef GetClipboardSequenceNumber
#undef SendInput
#undef GetKeyState
#undef GetAsyncKeyState
#undef GetForegroundWindow
#undef CallNextHookEx
#undef PostQuitMessage

namespace DemoTypeClipboardTests
{
    TEST_CLASS (ClipboardTextTests)
    {
    public:
        TEST_METHOD_INITIALIZE(Initialize)
            {
                canOpen = hasText = hasData = canLock = true;
                fixtureSequence = 1;
                openCount = closeCount = lockCount = unlockCount = dataCount = 0;
                clipboardCalls.clear();
                g_Clipboard = false;
                g_LastClipboardSeq = 0;
                g_Text.clear();
                SetText(L"[start]Example");
            }

            TEST_METHOD_CLEANUP(Cleanup)
                {
                    g_ClipboardCache.reset();
                    g_Clipboard = false;
                    g_Text.clear();
                }

                TEST_METHOD (PreservesUnicodeAndIndependentCopy)
                {
                    SetText(L"[start]\u4F60\u597D \U0001F600\r\n\tEnd");
                    std::unique_ptr<wchar_t[]> copy(GetClipboard());
                    Assert::IsNotNull(copy.get());
                    Assert::AreEqual(L"[start]\u4F60\u597D \U0001F600\r\n\tEnd", copy.get());
                    clipboardText[0] = L'X';
                    Assert::AreEqual(L'[', copy[0]);
                    Assert::AreEqual(1, closeCount);
                    Assert::AreEqual(1, unlockCount);
                }

                TEST_METHOD (TerminatesTextWithoutTrailingNull)
                {
                    SetText(L"[start]\u4F60\u597D \U0001F600", false);
                    std::unique_ptr<wchar_t[]> copy(GetClipboard());
                    Assert::AreEqual(L"[start]\u4F60\u597D \U0001F600", copy.get());
                    Assert::AreEqual(0, static_cast<int>(copy[clipboardText.size()]));
                    SetText(L"[start]Example", false);
                    Assert::IsTrue(GetDemoTypeClipboard());
                    Assert::AreEqual(L"Example", g_Text.c_str());
                    Assert::AreEqual(0, static_cast<int>(g_ClipboardCache[clipboardText.size()]));
                    Assert::AreEqual(openCount, closeCount);
                    Assert::AreEqual(2, unlockCount);
                }

                TEST_METHOD (StopsAtFirstNull)
                {
                    SetText(std::wstring(L"[start]First") + L'\0' + L"Unused", false);
                    Assert::IsTrue(GetDemoTypeClipboard());
                    Assert::AreEqual(L"First", g_Text.c_str());
                }

                TEST_METHOD (IgnoresIncompleteTrailingCodeUnit)
                {
                    SetText(L"[start]TextX", false);
                    --clipboardBytes;
                    std::unique_ptr<wchar_t[]> copy(GetClipboard());
                    Assert::AreEqual(L"[start]Text", copy.get());
                }

                TEST_METHOD (RejectsShortOrIncorrectPrefix)
                {
                    for (const auto* text : { L"", L"[", L" ", L"[start", L"[start]", L" start]", L"        start]Text", L"[Start]Text" })
                    {
                        SetText(text);
                        Assert::IsFalse(GetDemoTypeClipboard(), text);
                        Assert::IsFalse(g_Clipboard);
                    }
                }

                TEST_METHOD (AcceptsDemoTypePrefixWithDifferentFirstCharacter)
                {
                    for (const auto* text : { L"[start]Example", L" start]Example", L"!start]Example" })
                    {
                        SetText(text);
                        Assert::IsTrue(GetDemoTypeClipboard(), text);
                        Assert::AreEqual(L"Example", g_Text.c_str());
                    }
                }

                TEST_METHOD (RejectsEveryPrefixLengthWithoutScriptText)
                {
                    for (const auto* prefix : { L"[start]", L" start]" })
                    {
                        for (size_t length = 0; length <= 7; ++length)
                        {
                            SetText(std::wstring(prefix, length));
                            Assert::IsFalse(GetDemoTypeClipboard());
                        }
                    }
                }

                TEST_METHOD (DoesNotReuseTextAfterRejectedClipboard)
                {
                    Assert::IsTrue(GetDemoTypeClipboard());
                    SetText(L"Other text");
                    Assert::IsFalse(GetDemoTypeClipboard());
                    Assert::IsTrue(g_Text.empty());
                }

                TEST_METHOD (ReusesUnchangedClipboardCache)
                {
                    Assert::IsTrue(GetDemoTypeClipboard());
                    const int reads = dataCount;
                    Assert::IsTrue(GetDemoTypeClipboard());
                    Assert::AreEqual(reads, dataCount);
                }

                TEST_METHOD (FailedOpenDoesNotReadOrClose)
                {
                    canOpen = false;
                    Assert::IsNull(GetClipboard().get());
                    Assert::AreEqual(static_cast<DWORD>(0), GetClipboardSequence());
                    Assert::AreEqual(0, closeCount);
                    Assert::AreEqual(0, dataCount);
                    Assert::AreEqual(0, lockCount);
                }

                TEST_METHOD (MissingFormatOrDataClosesWithoutLocking)
                {
                    hasText = false;
                    Assert::IsNull(GetClipboard().get());
                    Assert::AreEqual(0, dataCount);
                    hasText = true;
                    hasData = false;
                    Assert::IsNull(GetClipboard().get());
                    Assert::AreEqual(2, closeCount);
                    Assert::AreEqual(0, lockCount);
                    Assert::AreEqual(0, unlockCount);
                }

                TEST_METHOD (RejectsUnsupportedSizesWithoutLocking)
                {
                    for (const SIZE_T size : { SIZE_T{ 0 }, SIZE_T{ 1 }, SIZE_T{ MAX_INPUT_SIZE + 1 } })
                    {
                        clipboardBytes = size;
                        Assert::IsNull(GetClipboard().get());
                    }
                    Assert::AreEqual(3, closeCount);
                    Assert::AreEqual(0, lockCount);
                    Assert::AreEqual(0, unlockCount);
                }

                TEST_METHOD (AcceptsClipboardTextAtMaximumByteSize)
                {
                    const std::wstring text(MAX_INPUT_SIZE / sizeof(wchar_t) - 1, L'A');
                    SetText(text);
                    std::unique_ptr<wchar_t[]> copy(GetClipboard());
                    Assert::IsNotNull(copy.get());
                    Assert::AreEqual(text.c_str(), copy.get());
                    Assert::AreEqual(1, unlockCount);
                    Assert::AreEqual(1, closeCount);
                }

                TEST_METHOD (UnlocksClipboardDataBeforeClosingClipboard)
                {
                    std::unique_ptr<wchar_t[]> copy(GetClipboard());
                    Assert::IsNotNull(copy.get());
                    const std::vector<std::wstring> expectedCalls{
                        L"OpenClipboard", L"GlobalLock", L"GlobalUnlock", L"CloseClipboard"
                    };
                    Assert::IsTrue(expectedCalls == clipboardCalls, L"Expected open, lock, unlock, then close.");
                }

                TEST_METHOD (FailedLockClosesWithoutUnlocking)
                {
                    canLock = false;
                    Assert::IsNull(GetClipboard().get());
                    Assert::AreEqual(1, closeCount);
                    Assert::AreEqual(1, lockCount);
                    Assert::AreEqual(0, unlockCount);
                }
    };

    TEST_CLASS (InputHookTests)
    {
        static bool IsKeyUp(const INPUT& input)
        {
            return (input.ki.dwFlags & KEYEVENTF_KEYUP) != 0;
        }

        static void AssertKey(const INPUT& input, WORD vk, bool keyUp)
        {
            Assert::AreEqual(static_cast<DWORD>(INPUT_KEYBOARD), input.type);
            Assert::AreEqual(vk, input.ki.wVk);
            Assert::AreEqual(keyUp, IsKeyUp(input));
            Assert::AreEqual(DEMOTYPE_INJECTION_TAG, input.ki.dwExtraInfo);
        }

        // Mirrors the system: key state only changes for events the hook passes on.
        static LRESULT SendHookEvent(DWORD vk, bool keyUp, bool injected = false)
        {
            KBDLLHOOKSTRUCT event{};
            event.vkCode = vk;
            event.flags = (keyUp ? LLKHF_UP : 0) | (injected ? LLKHF_INJECTED : 0);
            event.dwExtraInfo = injected ? DEMOTYPE_INJECTION_TAG : 0;
            const LRESULT result = DemoTypeHookProc(HC_ACTION, keyUp ? WM_KEYUP : WM_KEYDOWN, reinterpret_cast<LPARAM>(&event));
            if (result == 0)
            {
                keyDownState[vk] = !keyUp;
            }
            return result;
        }

        static void StartHook(int state, bool userDriven)
        {
            g_HookState = state;
            g_UserDriven = userDriven;
            g_SeekingBaseline = false;
        }

    public:
        TEST_METHOD_INITIALIZE(Initialize)
        {
            sentInputs.clear();
            sendInputBatches.clear();
            sendInputLimits.clear();
            sendInputCall = 0;
            std::fill(std::begin(keyDownState), std::end(keyDownState), false);
            std::fill(std::begin(g_SwallowedKeys), std::end(g_SwallowedKeys), false);
            foregroundWindow = reinterpret_cast<HWND>(1);
            g_hWndFocus = foregroundWindow;
            callNextCount = postQuitCount = 0;
            g_PendingInjections = 0;
            g_PendingAdvances = 0;
            g_SeekingBaseline = false;
            g_Notepad = false;
            g_UserDriven = false;
            g_End = false;
            g_Kill = false;
            g_HookState = INACTIVE_STATE;
        }

        TEST_METHOD_CLEANUP(Cleanup)
        {
            g_HookState = INACTIVE_STATE;
            g_Kill = false;
            g_UserDriven = false;
            g_hWndFocus = nullptr;
        }

        TEST_METHOD (ChordIsInjectedAsOneBatchReleasedInReverse)
        {
            SendKeyChord({ { VK_CONTROL, NULL }, { 'V', NULL } });

            Assert::AreEqual(size_t{ 1 }, sendInputBatches.size());
            Assert::AreEqual(size_t{ 4 }, sentInputs.size());
            AssertKey(sentInputs[0], VK_CONTROL, false);
            AssertKey(sentInputs[1], 'V', false);
            AssertKey(sentInputs[2], 'V', true);
            AssertKey(sentInputs[3], VK_CONTROL, true);
            Assert::AreEqual(4, g_PendingInjections.load());
        }

        TEST_METHOD (PartiallyInjectedChordStillReleasesEveryKey)
        {
            sendInputLimits = { 1 };

            SendKeyChord({ { VK_CONTROL, NULL }, { 'V', NULL } });

            Assert::AreEqual(size_t{ 3 }, sentInputs.size());
            AssertKey(sentInputs[0], VK_CONTROL, false);
            AssertKey(sentInputs[1], 'V', true);
            AssertKey(sentInputs[2], VK_CONTROL, true);
            Assert::AreEqual(3, g_PendingInjections.load());
        }

        TEST_METHOD (RetriesUnsentKeyUps)
        {
            g_Notepad = true;
            sendInputLimits = { 1, 1, 1 };

            SendKeyChord({ { VK_SHIFT, NULL }, { VK_LEFT, NULL } });

            const std::vector<UINT> expectedBatches{ 1, 1, 2, 1 };
            Assert::IsTrue(expectedBatches == sendInputBatches);
            Assert::AreEqual(size_t{ 4 }, sentInputs.size());
            AssertKey(sentInputs[2], VK_LEFT, true);
            AssertKey(sentInputs[3], VK_SHIFT, true);
            Assert::AreEqual(static_cast<DWORD>(KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP), sentInputs[2].ki.dwFlags);
        }

        TEST_METHOD (UnicodeKeyPressIsInjectedAsPacketPair)
        {
            SendUnicodeKeyPress(L'\u4F60');

            Assert::AreEqual(size_t{ 2 }, sentInputs.size());
            Assert::AreEqual(static_cast<DWORD>(KEYEVENTF_UNICODE), sentInputs[0].ki.dwFlags);
            Assert::AreEqual(static_cast<DWORD>(KEYEVENTF_UNICODE | KEYEVENTF_KEYUP), sentInputs[1].ki.dwFlags);
            Assert::AreEqual(static_cast<WORD>(L'\u4F60'), sentInputs[1].ki.wScan);
        }

        TEST_METHOD (BlockModifierKeysOnlyReleasesHeldModifiers)
        {
            keyDownState[VK_LCONTROL] = true;
            keyDownState[VK_RSHIFT] = true;

            BlockModifierKeys();

            Assert::AreEqual(size_t{ 1 }, sendInputBatches.size());
            Assert::AreEqual(size_t{ 2 }, sentInputs.size());
            AssertKey(sentInputs[0], VK_RSHIFT, true);
            AssertKey(sentInputs[1], VK_LCONTROL, true);
        }

        TEST_METHOD (HookActivatesOnceInjectedModifierReleasesArrive)
        {
            StartHook(INIT_STATE, false);
            keyDownState[VK_LCONTROL] = true;
            keyDownState[VK_RSHIFT] = true;
            BlockModifierKeys();

            Assert::AreEqual(0LL, static_cast<long long>(SendHookEvent(VK_RSHIFT, true, true)));
            Assert::AreEqual(INIT_STATE, g_HookState.load());
            Assert::AreEqual(0LL, static_cast<long long>(SendHookEvent(VK_LCONTROL, true, true)));
            Assert::AreEqual(ACTIVE_STATE, g_HookState.load());
            Assert::AreEqual(0, g_PendingInjections.load());
        }

        TEST_METHOD (UserDrivenKeystrokeQueuesAdvanceWithoutInjecting)
        {
            StartHook(ACTIVE_STATE, true);

            Assert::AreEqual(1LL, static_cast<long long>(SendHookEvent('A', false)));
            Assert::AreEqual(0, g_PendingAdvances.load());
            Assert::AreEqual(1LL, static_cast<long long>(SendHookEvent('A', true)));

            Assert::AreEqual(1, g_PendingAdvances.load());
            Assert::IsTrue(sentInputs.empty());
            Assert::AreEqual(0, callNextCount);
        }

        TEST_METHOD (ReleaseOfKeyHeldBeforeStartPassesWithoutAdvancing)
        {
            StartHook(ACTIVE_STATE, true);
            keyDownState['D'] = true;

            Assert::AreEqual(0LL, static_cast<long long>(SendHookEvent('D', true)));

            Assert::IsFalse(keyDownState['D']);
            Assert::AreEqual(0, g_PendingAdvances.load());
        }

        TEST_METHOD (AutorepeatOfHeldKeyDoesNotAdvance)
        {
            StartHook(ACTIVE_STATE, true);
            keyDownState['D'] = true;

            Assert::AreEqual(1LL, static_cast<long long>(SendHookEvent('D', false)));
            Assert::AreEqual(0LL, static_cast<long long>(SendHookEvent('D', true)));

            Assert::AreEqual(0, g_PendingAdvances.load());
        }

        TEST_METHOD (KeystrokesWhileSeekingBaselineDoNotAdvance)
        {
            StartHook(ACTIVE_STATE, true);
            g_SeekingBaseline = true;

            Assert::AreEqual(1LL, static_cast<long long>(SendHookEvent('A', false)));
            Assert::AreEqual(1LL, static_cast<long long>(SendHookEvent('A', true)));

            Assert::AreEqual(0, g_PendingAdvances.load());
        }

        TEST_METHOD (BlockStateNeverStrandsALogicallyDownKey)
        {
            StartHook(BLOCK_STATE, false);
            keyDownState[VK_LCONTROL] = true;

            Assert::AreEqual(0LL, static_cast<long long>(SendHookEvent(VK_LCONTROL, true, true)));
            Assert::IsFalse(keyDownState[VK_LCONTROL]);

            Assert::AreEqual(1LL, static_cast<long long>(SendHookEvent('B', false)));
            Assert::AreEqual(1LL, static_cast<long long>(SendHookEvent('B', true)));
            Assert::IsFalse(keyDownState['B']);
        }

        TEST_METHOD (UserEscapeRequestsKillAndPassesThrough)
        {
            StartHook(ACTIVE_STATE, true);

            Assert::AreEqual(0LL, static_cast<long long>(SendHookEvent(VK_ESCAPE, false)));

            Assert::IsTrue(g_Kill.load());
            Assert::AreEqual(BLOCK_STATE, g_HookState.load());
            Assert::AreEqual(1, postQuitCount);
            Assert::AreEqual(0LL, static_cast<long long>(SendHookEvent(VK_ESCAPE, true)));
        }

        TEST_METHOD (FocusChangeBlocksInjectionsButNotKillState)
        {
            StartHook(ACTIVE_STATE, false);
            foregroundWindow = reinterpret_cast<HWND>(2);

            Assert::AreEqual(1LL, static_cast<long long>(SendHookEvent('X', false, true)));
            Assert::IsTrue(g_Kill.load());
            Assert::AreEqual(BLOCK_STATE, g_HookState.load());
            Assert::AreEqual(0, postQuitCount);

            KillDemoTypeHook();
            BlockDemoTypeHook();
            Assert::AreEqual(KILL_STATE, g_HookState.load());
        }

        TEST_METHOD (UserDrivenSpaceAfterEndRequestsKill)
        {
            StartHook(ACTIVE_STATE, true);
            g_End = true;

            Assert::AreEqual(1LL, static_cast<long long>(SendHookEvent(VK_SPACE, false)));
            Assert::AreEqual(1LL, static_cast<long long>(SendHookEvent(VK_SPACE, true)));

            Assert::IsTrue(g_Kill.load());
            Assert::AreEqual(1, postQuitCount);
            Assert::AreEqual(0, g_PendingAdvances.load());
        }
    };
}
