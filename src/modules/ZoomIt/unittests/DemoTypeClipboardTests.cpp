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

namespace DemoTypeClipboardTests
{
    TEST_CLASS (ClipboardTextTests)
    {
    public:
        TEST_METHOD_INITIALIZE(Initialize)
            {
                canOpen = hasText = hasData = canLock = true;
                openCount = closeCount = lockCount = unlockCount = dataCount = 0;
                clipboardCalls.clear();
                g_Clipboard = false;
                g_LastClipboardSeq = 0;
                g_Text.clear();
                SetText(L"[start]Example");
            }

            TEST_METHOD_CLEANUP(Cleanup)
                {
                    delete[] g_ClipboardCache;
                    g_ClipboardCache = nullptr;
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
                    SetText(std::wstring(L"[start]First\0Unused", 19), false);
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
                    for (const auto* text : { L"", L"[", L"x", L"[start", L"[start]", L"xstart]", L"xxxxxxxxstart]Text", L"[Start]Text" })
                    {
                        SetText(text);
                        Assert::IsFalse(GetDemoTypeClipboard(), text);
                        Assert::IsFalse(g_Clipboard);
                    }
                }

                TEST_METHOD (AcceptsDemoTypePrefixWithDifferentFirstCharacter)
                {
                    for (const auto* text : { L"[start]Example", L"xstart]Example", L"!start]Example" })
                    {
                        SetText(text);
                        Assert::IsTrue(GetDemoTypeClipboard(), text);
                        Assert::AreEqual(L"Example", g_Text.c_str());
                    }
                }

                TEST_METHOD (RejectsEveryPrefixLengthWithoutScriptText)
                {
                    for (const auto* prefix : { L"[start]", L"xstart]" })
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
                    Assert::IsNull(GetClipboard());
                    Assert::AreEqual(static_cast<DWORD>(0), GetClipboardSequence());
                    Assert::AreEqual(0, closeCount);
                    Assert::AreEqual(0, dataCount);
                    Assert::AreEqual(0, lockCount);
                }

                TEST_METHOD (MissingFormatOrDataClosesWithoutLocking)
                {
                    hasText = false;
                    Assert::IsNull(GetClipboard());
                    Assert::AreEqual(0, dataCount);
                    hasText = true;
                    hasData = false;
                    Assert::IsNull(GetClipboard());
                    Assert::AreEqual(2, closeCount);
                    Assert::AreEqual(0, lockCount);
                    Assert::AreEqual(0, unlockCount);
                }

                TEST_METHOD (RejectsUnsupportedSizesWithoutLocking)
                {
                    for (const SIZE_T size : { SIZE_T{ 0 }, SIZE_T{ 1 }, SIZE_T{ MAX_INPUT_SIZE + 1 } })
                    {
                        clipboardBytes = size;
                        Assert::IsNull(GetClipboard());
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
                    Assert::IsNull(GetClipboard());
                    Assert::AreEqual(1, closeCount);
                    Assert::AreEqual(1, lockCount);
                    Assert::AreEqual(0, unlockCount);
                }
    };
}
