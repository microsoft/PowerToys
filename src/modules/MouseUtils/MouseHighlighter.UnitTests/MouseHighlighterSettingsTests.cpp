#include "pch.h"

#include <MouseHighlighter.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace MouseHighlighterUnitTests
{
    TEST_CLASS(MouseHighlighterSettingsTests)
    {
    public:
        TEST_METHOD(NormalizeMouseHighlighterSettings_SpotlightWithZeroFade_UsesRuntimeValues)
        {
            MouseHighlighterSettings settings;
            settings.leftButtonColor = winrt::Windows::UI::ColorHelper::FromArgb(128, 1, 2, 3);
            settings.rightButtonColor = winrt::Windows::UI::ColorHelper::FromArgb(129, 4, 5, 6);
            settings.alwaysColor = winrt::Windows::UI::ColorHelper::FromArgb(200, 7, 8, 9);
            settings.radius = 42;
            settings.fadeDelayMs = 0;
            settings.fadeDurationMs = 0;
            settings.spotlightMode = true;

            const auto runtimeSettings = NormalizeMouseHighlighterSettings(settings);

            Assert::AreEqual(42.0f, runtimeSettings.radius);
            Assert::AreEqual(MOUSE_HIGHLIGHTER_MIN_FADE_MS, runtimeSettings.fadeDelayMs);
            Assert::AreEqual(MOUSE_HIGHLIGHTER_MIN_FADE_MS, runtimeSettings.fadeDurationMs);
            Assert::IsFalse(runtimeSettings.leftPointerEnabled);
            Assert::IsFalse(runtimeSettings.rightPointerEnabled);
            Assert::IsTrue(runtimeSettings.alwaysPointerEnabled);
            Assert::IsTrue(runtimeSettings.spotlightMode);
            Assert::AreEqual(200, static_cast<int>(runtimeSettings.alwaysColor.A));
            Assert::AreEqual(7, static_cast<int>(runtimeSettings.alwaysColor.R));
            Assert::AreEqual(4, static_cast<int>(runtimeSettings.rightButtonColor.R));
        }
    };
}
