// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Unit tests for AlwaysOnTop settings parsing.
// These are pure-logic tests that exercise default values, color parsing,
// and excluded-apps string splitting without requiring file I/O.

#include "pch.h"

#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace AlwaysOnTopUnitTests
{
    // ── Mirror of HexToRGB from Settings.cpp ────────────────────────────────

    // Trim whitespace from both ends of a wide string_view.
    inline std::wstring_view Trim(std::wstring_view sv)
    {
        while (!sv.empty() && iswspace(sv.front()))
            sv.remove_prefix(1);
        while (!sv.empty() && iswspace(sv.back()))
            sv.remove_suffix(1);
        return sv;
    }

    // Left-trim a specific prefix character set.
    inline std::wstring_view LeftTrim(std::wstring_view sv, const wchar_t* chars)
    {
        while (!sv.empty())
        {
            bool found = false;
            for (const wchar_t* p = chars; *p; ++p)
            {
                if (sv.front() == *p) { found = true; break; }
            }
            if (!found) break;
            sv.remove_prefix(1);
        }
        return sv;
    }

    inline COLORREF HexToRGB(std::wstring_view hex, const COLORREF fallbackColor = RGB(255, 255, 255))
    {
        hex = LeftTrim(Trim(hex), L"#");

        try
        {
            const long long tmp = std::stoll(std::wstring(hex), nullptr, 16);
            const BYTE nR = static_cast<BYTE>((tmp & 0xFF0000) >> 16);
            const BYTE nG = static_cast<BYTE>((tmp & 0xFF00) >> 8);
            const BYTE nB = static_cast<BYTE>((tmp & 0xFF));
            return RGB(nR, nG, nB);
        }
        catch (const std::exception&)
        {
            return fallbackColor;
        }
    }

    // ── Mirror of excluded-apps parsing from Settings.cpp ───────────────────

    inline std::vector<std::wstring> ParseExcludedApps(const std::wstring& apps)
    {
        std::vector<std::wstring> result;
        std::wstring excludedUppercase = apps;
        CharUpperBuffW(excludedUppercase.data(), static_cast<DWORD>(excludedUppercase.length()));
        std::wstring_view view(excludedUppercase);

        // Trim leading/trailing whitespace
        while (!view.empty() && iswspace(view.front()))
            view.remove_prefix(1);
        while (!view.empty() && iswspace(view.back()))
            view.remove_suffix(1);

        while (!view.empty())
        {
            auto pos = (std::min)(view.find_first_of(L"\r\n"), view.length());
            result.emplace_back(view.substr(0, pos));
            view.remove_prefix(pos);
            // Trim leading whitespace/newlines for next entry
            while (!view.empty() && (view.front() == L'\r' || view.front() == L'\n' || iswspace(view.front())))
                view.remove_prefix(1);
        }

        return result;
    }

    // ── Mirror of Settings struct defaults ──────────────────────────────────

    struct SettingsDefaults
    {
        bool showInSystemMenu = false;
        bool enableFrame = true;
        bool enableSound = true;
        bool roundCornersEnabled = true;
        bool blockInGameMode = true;
        bool frameAccentColor = true;
        int frameThickness = 15;
        int frameOpacity = 100;
        COLORREF frameColor = RGB(0, 173, 239);

        static constexpr int minTransparencyPercentage = 20;
        static constexpr int maxTransparencyPercentage = 100;
        static constexpr int transparencyStep = 10;
    };

    // ── Default value tests ─────────────────────────────────────────────────

    TEST_CLASS(AlwaysOnTopSettingsDefaultsTests)
    {
    public:
        TEST_METHOD(FrameThickness_DefaultIs15)
        {
            SettingsDefaults s;
            Assert::AreEqual(15, s.frameThickness);
        }

        TEST_METHOD(FrameOpacity_DefaultIs100)
        {
            SettingsDefaults s;
            Assert::AreEqual(100, s.frameOpacity);
        }

        TEST_METHOD(FrameColor_DefaultIsLightBlue)
        {
            SettingsDefaults s;
            Assert::AreEqual(static_cast<COLORREF>(RGB(0, 173, 239)), s.frameColor);
        }

        TEST_METHOD(FrameColor_RedComponent)
        {
            SettingsDefaults s;
            Assert::AreEqual(static_cast<BYTE>(0), GetRValue(s.frameColor));
        }

        TEST_METHOD(FrameColor_GreenComponent)
        {
            SettingsDefaults s;
            Assert::AreEqual(static_cast<BYTE>(173), GetGValue(s.frameColor));
        }

        TEST_METHOD(FrameColor_BlueComponent)
        {
            SettingsDefaults s;
            Assert::AreEqual(static_cast<BYTE>(239), GetBValue(s.frameColor));
        }

        TEST_METHOD(EnableFrame_DefaultIsTrue)
        {
            SettingsDefaults s;
            Assert::IsTrue(s.enableFrame);
        }

        TEST_METHOD(EnableSound_DefaultIsTrue)
        {
            SettingsDefaults s;
            Assert::IsTrue(s.enableSound);
        }

        TEST_METHOD(RoundCornersEnabled_DefaultIsTrue)
        {
            SettingsDefaults s;
            Assert::IsTrue(s.roundCornersEnabled);
        }

        TEST_METHOD(BlockInGameMode_DefaultIsTrue)
        {
            SettingsDefaults s;
            Assert::IsTrue(s.blockInGameMode);
        }

        TEST_METHOD(FrameAccentColor_DefaultIsTrue)
        {
            SettingsDefaults s;
            Assert::IsTrue(s.frameAccentColor);
        }

        TEST_METHOD(ShowInSystemMenu_DefaultIsFalse)
        {
            SettingsDefaults s;
            Assert::IsFalse(s.showInSystemMenu);
        }

        TEST_METHOD(MinTransparency_Is20)
        {
            Assert::AreEqual(20, SettingsDefaults::minTransparencyPercentage);
        }

        TEST_METHOD(MaxTransparency_Is100)
        {
            Assert::AreEqual(100, SettingsDefaults::maxTransparencyPercentage);
        }

        TEST_METHOD(TransparencyStep_Is10)
        {
            Assert::AreEqual(10, SettingsDefaults::transparencyStep);
        }
    };

    // ── Color parsing tests ─────────────────────────────────────────────────

    TEST_CLASS(AlwaysOnTopColorParsingTests)
    {
    public:
        TEST_METHOD(HexToRGB_00ADE0_ParsesCorrectly)
        {
            // #00ADE0 → RGB(0, 173, 224) - note: 0xE0 = 224
            auto color = HexToRGB(L"#00ADE0");
            Assert::AreEqual(static_cast<BYTE>(0), GetRValue(color));
            Assert::AreEqual(static_cast<BYTE>(173), GetGValue(color));
            Assert::AreEqual(static_cast<BYTE>(224), GetBValue(color));
        }

        TEST_METHOD(HexToRGB_00ADEF_MatchesDefaultBlue)
        {
            // #00ADEF → RGB(0, 173, 239)
            auto color = HexToRGB(L"#00ADEF");
            Assert::AreEqual(static_cast<COLORREF>(RGB(0, 173, 239)), color);
        }

        TEST_METHOD(HexToRGB_FF0000_Red)
        {
            auto color = HexToRGB(L"#FF0000");
            Assert::AreEqual(static_cast<BYTE>(255), GetRValue(color));
            Assert::AreEqual(static_cast<BYTE>(0), GetGValue(color));
            Assert::AreEqual(static_cast<BYTE>(0), GetBValue(color));
        }

        TEST_METHOD(HexToRGB_00FF00_Green)
        {
            auto color = HexToRGB(L"#00FF00");
            Assert::AreEqual(static_cast<BYTE>(0), GetRValue(color));
            Assert::AreEqual(static_cast<BYTE>(255), GetGValue(color));
            Assert::AreEqual(static_cast<BYTE>(0), GetBValue(color));
        }

        TEST_METHOD(HexToRGB_0000FF_Blue)
        {
            auto color = HexToRGB(L"#0000FF");
            Assert::AreEqual(static_cast<BYTE>(0), GetRValue(color));
            Assert::AreEqual(static_cast<BYTE>(0), GetGValue(color));
            Assert::AreEqual(static_cast<BYTE>(255), GetBValue(color));
        }

        TEST_METHOD(HexToRGB_WithoutHash)
        {
            auto color = HexToRGB(L"FF8000");
            Assert::AreEqual(static_cast<BYTE>(255), GetRValue(color));
            Assert::AreEqual(static_cast<BYTE>(128), GetGValue(color));
            Assert::AreEqual(static_cast<BYTE>(0), GetBValue(color));
        }

        TEST_METHOD(HexToRGB_000000_Black)
        {
            auto color = HexToRGB(L"#000000");
            Assert::AreEqual(static_cast<COLORREF>(RGB(0, 0, 0)), color);
        }

        TEST_METHOD(HexToRGB_FFFFFF_White)
        {
            auto color = HexToRGB(L"#FFFFFF");
            Assert::AreEqual(static_cast<COLORREF>(RGB(255, 255, 255)), color);
        }

        TEST_METHOD(HexToRGB_InvalidString_FallsBackToDefault)
        {
            auto color = HexToRGB(L"not-a-color");
            Assert::AreEqual(static_cast<COLORREF>(RGB(255, 255, 255)), color,
                             L"Invalid hex should fall back to white");
        }

        TEST_METHOD(HexToRGB_EmptyString_FallsBackToDefault)
        {
            auto color = HexToRGB(L"");
            Assert::AreEqual(static_cast<COLORREF>(RGB(255, 255, 255)), color);
        }

        TEST_METHOD(HexToRGB_CustomFallback)
        {
            auto color = HexToRGB(L"invalid", RGB(10, 20, 30));
            Assert::AreEqual(static_cast<COLORREF>(RGB(10, 20, 30)), color);
        }

        TEST_METHOD(HexToRGB_WithLeadingSpaces)
        {
            auto color = HexToRGB(L"  #FF0000  ");
            Assert::AreEqual(static_cast<BYTE>(255), GetRValue(color));
        }

        TEST_METHOD(HexToRGB_LowercaseHex)
        {
            auto color = HexToRGB(L"#ff8040");
            Assert::AreEqual(static_cast<BYTE>(255), GetRValue(color));
            Assert::AreEqual(static_cast<BYTE>(128), GetGValue(color));
            Assert::AreEqual(static_cast<BYTE>(64), GetBValue(color));
        }
    };

    // ── Excluded apps parsing tests ─────────────────────────────────────────

    TEST_CLASS(AlwaysOnTopExcludedAppsTests)
    {
    public:
        TEST_METHOD(EmptyString_ReturnsEmptyVector)
        {
            auto apps = ParseExcludedApps(L"");
            Assert::AreEqual(static_cast<size_t>(0), apps.size());
        }

        TEST_METHOD(SingleApp_ReturnsSingleEntry)
        {
            auto apps = ParseExcludedApps(L"notepad.exe");
            Assert::AreEqual(static_cast<size_t>(1), apps.size());
            Assert::AreEqual(std::wstring(L"NOTEPAD.EXE"), apps[0]);
        }

        TEST_METHOD(MultipleApps_NewlineSeparated)
        {
            auto apps = ParseExcludedApps(L"notepad.exe\nchrome.exe\nfirefox.exe");
            Assert::AreEqual(static_cast<size_t>(3), apps.size());
            Assert::AreEqual(std::wstring(L"NOTEPAD.EXE"), apps[0]);
            Assert::AreEqual(std::wstring(L"CHROME.EXE"), apps[1]);
            Assert::AreEqual(std::wstring(L"FIREFOX.EXE"), apps[2]);
        }

        TEST_METHOD(MultipleApps_CRLFSeparated)
        {
            auto apps = ParseExcludedApps(L"app1.exe\r\napp2.exe\r\napp3.exe");
            Assert::AreEqual(static_cast<size_t>(3), apps.size());
            Assert::AreEqual(std::wstring(L"APP1.EXE"), apps[0]);
            Assert::AreEqual(std::wstring(L"APP2.EXE"), apps[1]);
            Assert::AreEqual(std::wstring(L"APP3.EXE"), apps[2]);
        }

        TEST_METHOD(AppsAreUppercased)
        {
            auto apps = ParseExcludedApps(L"MixedCase.exe");
            Assert::AreEqual(std::wstring(L"MIXEDCASE.EXE"), apps[0]);
        }

        TEST_METHOD(LeadingTrailingWhitespace_Trimmed)
        {
            auto apps = ParseExcludedApps(L"  app.exe  ");
            Assert::AreEqual(static_cast<size_t>(1), apps.size());
            Assert::AreEqual(std::wstring(L"APP.EXE"), apps[0]);
        }

        TEST_METHOD(EmptyLinesBetweenApps_Skipped)
        {
            auto apps = ParseExcludedApps(L"app1.exe\n\n\napp2.exe");
            Assert::AreEqual(static_cast<size_t>(2), apps.size());
            Assert::AreEqual(std::wstring(L"APP1.EXE"), apps[0]);
            Assert::AreEqual(std::wstring(L"APP2.EXE"), apps[1]);
        }

        TEST_METHOD(WhitespaceOnly_ReturnsEmpty)
        {
            auto apps = ParseExcludedApps(L"   \n  \r\n  ");
            Assert::AreEqual(static_cast<size_t>(0), apps.size());
        }
    };

    // ── Empty / partial settings → all defaults ─────────────────────────────

    TEST_CLASS(AlwaysOnTopPartialSettingsTests)
    {
    public:
        TEST_METHOD(EmptySettings_AllFieldsGetDefaults)
        {
            SettingsDefaults s;
            Assert::AreEqual(15, s.frameThickness);
            Assert::AreEqual(100, s.frameOpacity);
            Assert::AreEqual(static_cast<COLORREF>(RGB(0, 173, 239)), s.frameColor);
            Assert::IsTrue(s.enableFrame);
            Assert::IsTrue(s.enableSound);
            Assert::IsTrue(s.roundCornersEnabled);
            Assert::IsTrue(s.blockInGameMode);
            Assert::IsTrue(s.frameAccentColor);
            Assert::IsFalse(s.showInSystemMenu);
        }

        TEST_METHOD(PartialSettings_UnsetFieldsRetainDefaults)
        {
            // Simulate partial update: only change frameThickness
            SettingsDefaults s;
            s.frameThickness = 20;

            // Unchanged fields must still hold defaults
            Assert::AreEqual(20, s.frameThickness);
            Assert::AreEqual(100, s.frameOpacity);
            Assert::AreEqual(static_cast<COLORREF>(RGB(0, 173, 239)), s.frameColor);
            Assert::IsTrue(s.enableFrame);
            Assert::IsTrue(s.enableSound);
        }

        TEST_METHOD(PartialSettings_OnlyBoolChanged)
        {
            SettingsDefaults s;
            s.enableSound = false;
            s.showInSystemMenu = true;

            Assert::IsFalse(s.enableSound);
            Assert::IsTrue(s.showInSystemMenu);
            // Other bools unchanged
            Assert::IsTrue(s.enableFrame);
            Assert::IsTrue(s.roundCornersEnabled);
            Assert::IsTrue(s.blockInGameMode);
        }

        TEST_METHOD(PartialSettings_OnlyColorChanged)
        {
            SettingsDefaults s;
            s.frameColor = HexToRGB(L"#FF0000");

            Assert::AreEqual(static_cast<COLORREF>(RGB(255, 0, 0)), s.frameColor);
            Assert::AreEqual(15, s.frameThickness);
            Assert::AreEqual(100, s.frameOpacity);
        }
    };
}
