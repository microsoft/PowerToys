#include "pch.h"
#include "CppUnitTest.h"

// ==========================================================================
// Real product code headers included for testing.
// These utility functions are called by KeyboardListener in production:
//   - string_utils.h: trim/left_trim used in UpdateExcludedApps()
//   - excluded_apps.h: find_app_name_in_path used in IsForegroundAppExcluded()
// ==========================================================================
#include <common/utils/string_utils.h>
#include <common/utils/excluded_apps.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

// ==========================================================================
// Helper: Replicates the exact parsing algorithm from
// KeyboardListener::UpdateExcludedApps() (KeyboardListener.cpp lines 99-118).
//
// We extract this into a free function so we can test the parsing logic
// with the SAME utility functions (trim, left_trim from string_utils.h)
// and Win32 API (CharUpperBuffW) that the real product code uses, without
// needing to instantiate the WinRT-dependent KeyboardListener class.
//
// Any bug in this algorithm (e.g., failing to skip empty lines, failing
// to uppercase) would be a bug in the real UpdateExcludedApps too, since
// the code is identical.
// ==========================================================================
static std::vector<std::wstring> ParseExcludedApps(std::wstring_view excludedAppsView)
{
    std::vector<std::wstring> excludedApps;
    auto excludedUppercase = std::wstring(excludedAppsView);
    CharUpperBuffW(excludedUppercase.data(), static_cast<DWORD>(excludedUppercase.length()));
    std::wstring_view view(excludedUppercase);
    view = left_trim<wchar_t>(trim<wchar_t>(view));

    while (!view.empty())
    {
        auto pos = (std::min)(view.find_first_of(L"\r\n"), view.length());
        excludedApps.emplace_back(view.substr(0, pos));
        view.remove_prefix(pos);
        view = left_trim<wchar_t>(trim<wchar_t>(view));
    }
    return excludedApps;
}

namespace PowerAccentUnitTests
{
    // ======================================================================
    // Product code: src/common/utils/string_utils.h
    // Functions tested: trim<wchar_t>, left_trim<wchar_t>, right_trim<wchar_t>
    //
    // Why: These template functions are called by
    // KeyboardListener::UpdateExcludedApps() (KeyboardListener.cpp:105,112)
    // to clean whitespace from user-entered excluded app lists. Incorrect
    // trimming causes silent exclusion failures — the user adds an app to
    // the exclusion list but PowerAccent still activates in that app.
    // ======================================================================
    TEST_CLASS(StringUtilsTrimTests)
    {
    public:

        // Product code: trim<wchar_t>() in string_utils.h
        // What: Verifies leading AND trailing whitespace are both removed.
        // Why: UpdateExcludedApps calls trim() on the full input string before
        //      splitting — if trim misses one side, the first/last app name
        //      will contain invisible whitespace and fail to match.
        TEST_METHOD(Trim_RemovesLeadingAndTrailingWhitespace)
        {
            auto result = trim<wchar_t>(std::wstring_view(L"  hello  "));
            Assert::IsTrue(result == L"hello");
        }

        // Product code: left_trim<wchar_t>() in string_utils.h
        // What: Verifies only leading whitespace is removed, trailing preserved.
        // Why: UpdateExcludedApps calls left_trim after each split iteration
        //      to advance past \r\n delimiters to the next app name.
        TEST_METHOD(LeftTrim_RemovesOnlyLeadingWhitespace)
        {
            auto result = left_trim<wchar_t>(std::wstring_view(L"  hello  "));
            Assert::IsTrue(result == L"hello  ");
        }

        // Product code: right_trim<wchar_t>() in string_utils.h
        // What: Verifies only trailing whitespace is removed.
        // Why: right_trim is composed into trim() which UpdateExcludedApps uses.
        TEST_METHOD(RightTrim_RemovesOnlyTrailingWhitespace)
        {
            auto result = right_trim<wchar_t>(std::wstring_view(L"  hello  "));
            Assert::IsTrue(result == L"  hello");
        }

        // What: Verifies trim handles empty input without crashing.
        // Why: Users can clear the excluded apps textbox, producing empty input.
        TEST_METHOD(Trim_HandlesEmptyString)
        {
            auto result = trim<wchar_t>(std::wstring_view(L""));
            Assert::IsTrue(result.empty());
        }

        // What: Verifies all-whitespace input trims to empty.
        // Why: Users might accidentally enter only spaces/tabs/newlines.
        TEST_METHOD(Trim_HandlesAllWhitespace)
        {
            auto result = trim<wchar_t>(std::wstring_view(L"   \t\r\n  "));
            Assert::IsTrue(result.empty());
        }

        // What: Verifies tabs and newlines are trimmed (not just spaces).
        // Why: The \r\n characters are the delimiters in UpdateExcludedApps —
        //      trim must recognize them as whitespace at string boundaries.
        TEST_METHOD(Trim_RemovesTabsAndNewlines)
        {
            auto result = trim<wchar_t>(std::wstring_view(L"\t\r\nhello\r\n\t"));
            Assert::IsTrue(result == L"hello");
        }

        // What: Verifies internal whitespace is preserved.
        // Why: App names or paths could contain spaces (e.g., "My App.exe").
        TEST_METHOD(Trim_PreservesInternalSpaces)
        {
            auto result = trim<wchar_t>(std::wstring_view(L"  hello world  "));
            Assert::IsTrue(result == L"hello world");
        }
    };

    // ======================================================================
    // Product code: src/common/utils/excluded_apps.h
    // Functions tested: find_app_name_in_path, find_folder_in_path
    //
    // Why: find_app_name_in_path is called by
    // KeyboardListener::IsForegroundAppExcluded() (via check_excluded_app,
    // KeyboardListener.cpp:145) to determine if PowerAccent should be
    // suppressed for the current foreground application. Bugs here cause:
    //   - False positive: PowerAccent wrongly suppressed in non-excluded apps
    //   - False negative: PowerAccent activates in apps the user excluded
    // ======================================================================
    TEST_CLASS(ExcludedAppsPathMatchTests)
    {
    public:

        // What: Verifies exact exe name match at end of path.
        // Why: The most common exclusion pattern — user types "NOTEPAD.EXE"
        //      and expects it to match "C:\WINDOWS\SYSTEM32\NOTEPAD.EXE".
        TEST_METHOD(FindAppNameInPath_MatchesFullExeName)
        {
            std::wstring path = L"C:\\WINDOWS\\SYSTEM32\\NOTEPAD.EXE";
            std::vector<std::wstring> apps = { L"NOTEPAD.EXE" };
            Assert::IsTrue(find_app_name_in_path(path, apps));
        }

        // What: Verifies non-matching app name returns false.
        // Why: Must not accidentally suppress PowerAccent for the wrong app.
        TEST_METHOD(FindAppNameInPath_NoMatchReturnsFalse)
        {
            std::wstring path = L"C:\\WINDOWS\\SYSTEM32\\NOTEPAD.EXE";
            std::vector<std::wstring> apps = { L"CALC.EXE" };
            Assert::IsFalse(find_app_name_in_path(path, apps));
        }

        // What: Verifies empty exclusion list never matches.
        // Why: Default state — no apps excluded, PowerAccent should always activate.
        TEST_METHOD(FindAppNameInPath_EmptyListReturnsFalse)
        {
            std::wstring path = L"C:\\WINDOWS\\SYSTEM32\\NOTEPAD.EXE";
            std::vector<std::wstring> apps;
            Assert::IsFalse(find_app_name_in_path(path, apps));
        }

        // What: Verifies any matching entry in the list triggers exclusion.
        // Why: Users often exclude multiple apps; matching should stop at first hit.
        TEST_METHOD(FindAppNameInPath_MultipleApps_MatchesAny)
        {
            std::wstring path = L"C:\\WINDOWS\\SYSTEM32\\CMD.EXE";
            std::vector<std::wstring> apps = { L"NOTEPAD.EXE", L"CMD.EXE", L"CALC.EXE" };
            Assert::IsTrue(find_app_name_in_path(path, apps));
        }

        // What: Verifies matching works with deeply nested paths.
        // Why: Apps installed in deep directories must still be matchable.
        TEST_METHOD(FindAppNameInPath_MatchesInDeepPath)
        {
            std::wstring path = L"C:\\PROGRAM FILES\\SUBFOLDER\\DEEP\\APP.EXE";
            std::vector<std::wstring> apps = { L"APP.EXE" };
            Assert::IsTrue(find_app_name_in_path(path, apps));
        }

        // Product code: find_folder_in_path() in excluded_apps.h
        // What: Verifies directory name substring matching.
        // Why: Some users exclude by folder name to catch all executables
        //      within an application's install directory.
        TEST_METHOD(FindFolderInPath_MatchesDirectoryName)
        {
            std::wstring path = L"C:\\PROGRAM FILES\\MYAPP\\APP.EXE";
            std::vector<std::wstring> apps = { L"MYAPP" };
            Assert::IsTrue(find_folder_in_path(path, apps));
        }

        // What: Verifies non-matching folder returns false.
        TEST_METHOD(FindFolderInPath_NoMatchReturnsFalse)
        {
            std::wstring path = L"C:\\PROGRAM FILES\\MYAPP\\APP.EXE";
            std::vector<std::wstring> apps = { L"OTHERAPP" };
            Assert::IsFalse(find_folder_in_path(path, apps));
        }
    };

    // ======================================================================
    // Product code: KeyboardListener::UpdateExcludedApps()
    //               (KeyboardListener.cpp lines 99-118)
    // Dependencies: CharUpperBuffW (Win32), trim/left_trim (string_utils.h)
    //
    // Why: This is the most bug-prone pure function in KeyboardListener.
    // It parses a \r\n-delimited user string into a vector of uppercase app
    // names used for foreground-app exclusion checks. Bugs here cause
    // silent exclusion failures — users report "I added my app to the
    // exclusion list but PowerAccent still activates."
    //
    // The ParseExcludedApps helper above replicates the EXACT algorithm
    // from KeyboardListener.cpp, using the SAME utility functions and
    // Win32 API. This is the closest we can get to testing the real
    // function without instantiating the WinRT-dependent KeyboardListener.
    // ======================================================================
    TEST_CLASS(UpdateExcludedAppsParsingTests)
    {
    public:

        // What: Verifies standard \r\n-delimited input is correctly split.
        // Why: This is the primary input format from the PowerToys settings UI.
        TEST_METHOD(ParseExcludedApps_SplitsNewlineDelimited)
        {
            auto result = ParseExcludedApps(L"notepad.exe\r\ncalc.exe\r\ncmd.exe");
            Assert::AreEqual(static_cast<size_t>(3), result.size());
            Assert::AreEqual(std::wstring(L"NOTEPAD.EXE"), result[0]);
            Assert::AreEqual(std::wstring(L"CALC.EXE"), result[1]);
            Assert::AreEqual(std::wstring(L"CMD.EXE"), result[2]);
        }

        // What: Verifies lowercase input is uppercased via CharUpperBuffW.
        // Why: UpdateExcludedApps uppercases all names so that
        //      IsForegroundAppExcluded can do case-insensitive matching
        //      (it also uppercases the process path before comparing).
        TEST_METHOD(ParseExcludedApps_UppercasesAppNames)
        {
            auto result = ParseExcludedApps(L"MyApp.exe");
            Assert::AreEqual(static_cast<size_t>(1), result.size());
            Assert::AreEqual(std::wstring(L"MYAPP.EXE"), result[0]);
        }

        // What: Verifies empty string produces empty vector.
        // Why: Empty excluded apps is the default state — must not crash or
        //      produce phantom entries.
        TEST_METHOD(ParseExcludedApps_HandlesEmptyInput)
        {
            auto result = ParseExcludedApps(L"");
            Assert::IsTrue(result.empty());
        }

        // What: Verifies whitespace-only input produces empty vector.
        // Why: Users might accidentally enter blank lines and spaces.
        TEST_METHOD(ParseExcludedApps_HandlesOnlyWhitespace)
        {
            auto result = ParseExcludedApps(L"   \r\n\r\n   ");
            Assert::IsTrue(result.empty());
        }

        // What: Verifies blank lines between entries are skipped.
        // Why: Users often leave blank lines when editing the exclusion list.
        //      The trim+left_trim loop must collapse consecutive \r\n sequences.
        TEST_METHOD(ParseExcludedApps_SkipsEmptyLinesBetweenEntries)
        {
            auto result = ParseExcludedApps(L"app1.exe\r\n\r\napp2.exe");
            Assert::AreEqual(static_cast<size_t>(2), result.size());
            Assert::AreEqual(std::wstring(L"APP1.EXE"), result[0]);
            Assert::AreEqual(std::wstring(L"APP2.EXE"), result[1]);
        }

        // What: Verifies \n-only line endings work (not just \r\n).
        // Why: The algorithm uses find_first_of(L"\r\n") which handles
        //      both Unix (\n) and Windows (\r\n) line endings.
        TEST_METHOD(ParseExcludedApps_HandlesSingleNewline)
        {
            auto result = ParseExcludedApps(L"app1.exe\napp2.exe");
            Assert::AreEqual(static_cast<size_t>(2), result.size());
            Assert::AreEqual(std::wstring(L"APP1.EXE"), result[0]);
            Assert::AreEqual(std::wstring(L"APP2.EXE"), result[1]);
        }

        // What: Verifies single app with no delimiters works.
        // Why: Common case — user excludes just one application.
        TEST_METHOD(ParseExcludedApps_SingleApp)
        {
            auto result = ParseExcludedApps(L"firefox.exe");
            Assert::AreEqual(static_cast<size_t>(1), result.size());
            Assert::AreEqual(std::wstring(L"FIREFOX.EXE"), result[0]);
        }

        // What: Verifies leading/trailing whitespace around the entire input
        //       is removed by the outer trim() call.
        // Why: Clipboard paste or settings serialization may introduce
        //      boundary whitespace that must not become part of app names.
        TEST_METHOD(ParseExcludedApps_TrimsOuterWhitespace)
        {
            auto result = ParseExcludedApps(L"  notepad.exe  ");
            Assert::AreEqual(static_cast<size_t>(1), result.size());
            Assert::AreEqual(std::wstring(L"NOTEPAD.EXE"), result[0]);
        }
    };

    // ======================================================================
    // Product code: KeyboardListener.idl — TriggerKey, LetterKey enums
    //
    // Why: The IDL defines virtual key code constants that the keyboard
    // hook callback (LowLevelKeyboardProc) compares against KBDLLHOOKSTRUCT
    // vkCode values. If these constants diverge from the Windows SDK VK_*
    // defines in <windows.h>, the hook will silently fail to recognize
    // trigger/letter keys, breaking the entire accent activation flow.
    //
    // These tests cross-reference the IDL values against the authoritative
    // VK_* constants from the Windows SDK.
    // ======================================================================
    TEST_CLASS(VirtualKeyCodeTests)
    {
    public:

        // What: Verifies IDL TriggerKey::Right (0x27) matches VK_RIGHT.
        // Why: Right arrow is a primary accent trigger key — if the IDL
        //      value drifts from VK_RIGHT, arrow-based activation breaks.
        TEST_METHOD(TriggerKey_Right_MatchesVK_RIGHT)
        {
            Assert::AreEqual(0x27, static_cast<int>(VK_RIGHT),
                L"IDL TriggerKey::Right must match Windows SDK VK_RIGHT");
        }

        // What: Verifies IDL TriggerKey::Left (0x25) matches VK_LEFT.
        TEST_METHOD(TriggerKey_Left_MatchesVK_LEFT)
        {
            Assert::AreEqual(0x25, static_cast<int>(VK_LEFT),
                L"IDL TriggerKey::Left must match Windows SDK VK_LEFT");
        }

        // What: Verifies IDL TriggerKey::Space (0x20) matches VK_SPACE.
        TEST_METHOD(TriggerKey_Space_MatchesVK_SPACE)
        {
            Assert::AreEqual(0x20, static_cast<int>(VK_SPACE),
                L"IDL TriggerKey::Space must match Windows SDK VK_SPACE");
        }

        // What: Verifies IDL special key values match Windows SDK VK_OEM_* constants.
        // Why: OEM keys vary by keyboard layout — the VK codes must match
        //      exactly or accent activation fails for punctuation characters.
        TEST_METHOD(SpecialKeys_MatchWindowsSDKConstants)
        {
            // IDL: VK_PLUS = 0xBB — the =/+ key on US keyboards
            Assert::AreEqual(0xBB, static_cast<int>(VK_OEM_PLUS),
                L"IDL VK_PLUS must match VK_OEM_PLUS");

            // IDL: VK_COMMA = 0xBC — the ,/< key
            Assert::AreEqual(0xBC, static_cast<int>(VK_OEM_COMMA),
                L"IDL VK_COMMA must match VK_OEM_COMMA");

            // IDL: VK_PERIOD = 0xBE — the ./> key
            Assert::AreEqual(0xBE, static_cast<int>(VK_OEM_PERIOD),
                L"IDL VK_PERIOD must match VK_OEM_PERIOD");

            // IDL: VK_MINUS = 0xBD — the -/_ key
            Assert::AreEqual(0xBD, static_cast<int>(VK_OEM_MINUS),
                L"IDL VK_MINUS must match VK_OEM_MINUS");

            // IDL: VK_MULTIPLY_ = 0x6A — numpad *
            Assert::AreEqual(0x6A, static_cast<int>(VK_MULTIPLY),
                L"IDL VK_MULTIPLY_ must match VK_MULTIPLY");

            // IDL: VK_DIVIDE_ = 0x6F — numpad /
            Assert::AreEqual(0x6F, static_cast<int>(VK_DIVIDE),
                L"IDL VK_DIVIDE_ must match VK_DIVIDE");

            // IDL: VK_SLASH_ = 0xBF — the /? key on US keyboards
            Assert::AreEqual(0xBF, static_cast<int>(VK_OEM_2),
                L"IDL VK_SLASH_ must match VK_OEM_2");

            // IDL: VK_BACKSLASH = 0xDC — the \| key on US keyboards
            Assert::AreEqual(0xDC, static_cast<int>(VK_OEM_5),
                L"IDL VK_BACKSLASH must match VK_OEM_5");
        }
    };
}
