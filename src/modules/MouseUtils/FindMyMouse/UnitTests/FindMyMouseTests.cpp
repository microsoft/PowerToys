// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Unit tests for FindMyMouse shake detection and activation guard logic.
//
// The C++ shake detection lives inside the private SuperSonar<D> template in
// FindMyMouse.cpp. To test the algorithm without pulling in the full Win32
// surface, we replicate the core algorithm in a standalone ShakeDetector class
// that matches the production logic (same merging, pruning, and threshold
// math). The tests mirror the Rust test suite in
// src/rust/libs/findmymouse-core/src/shake_detector.rs and
// src/rust/libs/findmymouse-core/src/activation_guard.rs.

#include "pch.h"

#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include <cmath>
#include <cstdint>
#include <string>
#include <vector>
#include <algorithm>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FindMyMouseUnitTests
{
    // ── Standalone ShakeDetector (mirrors C++ DetectShake / OnSonarMouseInput) ──

    struct PointerRecentMovement
    {
        int64_t dx;
        int64_t dy;
        uint64_t tick;
    };

    static int8_t GetSign(int64_t n)
    {
        if (n > 0) return 1;
        if (n < 0) return -1;
        return 0;
    }

    class ShakeDetector
    {
    public:
        ShakeDetector(int shakeMinimumDistance, int shakeIntervalMs, int shakeFactor)
            : m_shakeMinimumDistance(shakeMinimumDistance)
            , m_shakeIntervalMs(static_cast<uint64_t>(shakeIntervalMs))
            , m_shakeFactor(shakeFactor)
        {
        }

        // Feed a relative mouse movement. Returns true if shake is detected.
        bool Feed(int64_t dx, int64_t dy, uint64_t tick)
        {
            if (dx == 0 && dy == 0)
                return false;

            if (!m_history.empty())
            {
                auto& last = m_history.back();
                if (GetSign(last.dx) == GetSign(dx) && GetSign(last.dy) == GetSign(dy))
                {
                    // Same direction — merge.
                    last.dx += dx;
                    last.dy += dy;
                    return false;
                }
            }

            // Direction changed (or first movement).
            m_history.push_back({ dx, dy, tick });

            if (m_history.size() >= 2)
                return DetectShake(tick);

            return false;
        }

        void Clear()
        {
            m_history.clear();
        }

    private:
        bool DetectShake(uint64_t now)
        {
            uint64_t shakeStartTick = (now > m_shakeIntervalMs) ? (now - m_shakeIntervalMs) : 0;

            // Prune old movements.
            std::erase_if(m_history, [shakeStartTick](const PointerRecentMovement& m) {
                return m.tick < shakeStartTick;
            });

            double distanceTravelled = 0.0;
            int64_t currentX = 0, currentY = 0;
            int64_t minX = 0, maxX = 0, minY = 0, maxY = 0;

            for (const auto& m : m_history)
            {
                currentX += m.dx;
                currentY += m.dy;
                distanceTravelled += std::sqrt(
                    static_cast<double>(m.dx) * m.dx +
                    static_cast<double>(m.dy) * m.dy);
                minX = (std::min)(currentX, minX);
                maxX = (std::max)(currentX, maxX);
                minY = (std::min)(currentY, minY);
                maxY = (std::max)(currentY, maxY);
            }

            if (distanceTravelled < m_shakeMinimumDistance)
                return false;

            double rectW = static_cast<double>(maxX - minX);
            double rectH = static_cast<double>(maxY - minY);
            double diagonal = std::sqrt(rectW * rectW + rectH * rectH);

            if (diagonal > 0.0 && distanceTravelled / diagonal > (m_shakeFactor / 100.0))
            {
                m_history.clear();
                return true;
            }

            return false;
        }

        std::vector<PointerRecentMovement> m_history;
        int m_shakeMinimumDistance;
        uint64_t m_shakeIntervalMs;
        int m_shakeFactor;
    };

    // ── Activation guard (mirrors Rust activation_guard / C++ StartSonar guards) ──

    enum class ActivationCheck
    {
        Allow,
        BlockedByGameMode,
        BlockedByExcludedApp,
    };

    static ActivationCheck CanActivate(
        bool doNotActivateOnGameMode,
        const std::vector<std::wstring>& excludedApps,
        bool isGameMode,
        const std::wstring& foregroundApp)
    {
        if (doNotActivateOnGameMode && isGameMode)
            return ActivationCheck::BlockedByGameMode;

        if (!excludedApps.empty() && !foregroundApp.empty())
        {
            std::wstring appUpper = foregroundApp;
            for (auto& ch : appUpper)
                ch = static_cast<wchar_t>(towupper(ch));

            for (const auto& excluded : excludedApps)
            {
                if (appUpper.find(excluded) != std::wstring::npos)
                    return ActivationCheck::BlockedByExcludedApp;
            }
        }

        return ActivationCheck::Allow;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Shake Detection Tests
    // ═══════════════════════════════════════════════════════════════════════════

    TEST_CLASS(ShakeDetectorTests)
    {
    public:

        // ── Rapid back-and-forth horizontal shake triggers ──

        TEST_METHOD(RapidHorizontalShakeTriggers)
        {
            ShakeDetector det(1000, 1000, 400);
            uint64_t tick = 1000;
            bool triggered = false;

            for (int i = 0; i < 30; ++i)
            {
                int64_t dx = (i % 2 == 0) ? 200 : -200;
                tick += 20;
                if (det.Feed(dx, 0, tick))
                {
                    triggered = true;
                    break;
                }
            }

            Assert::IsTrue(triggered, L"Rapid horizontal shake should trigger detection");
        }

        // ── Slow movement doesn't trigger ──

        TEST_METHOD(SlowMovementDoesNotTrigger)
        {
            ShakeDetector det(1000, 1000, 400);

            for (int i = 0; i < 50; ++i)
            {
                bool triggered = det.Feed(10, 0, 1000 + static_cast<uint64_t>(i) * 100);
                Assert::IsFalse(triggered, L"Slow unidirectional movement should not trigger");
            }
        }

        // ── Circular motion doesn't trigger ──

        TEST_METHOD(CircularMotionDoesNotTrigger)
        {
            ShakeDetector det(500, 1000, 500);

            const int64_t dirs[][2] = {
                {100, 0}, {70, 70}, {0, 100}, {-70, 70},
                {-100, 0}, {-70, -70}, {0, -100}, {70, -70},
            };

            uint64_t tick = 1000;
            for (int cycle = 0; cycle < 2; ++cycle)
            {
                for (const auto& d : dirs)
                {
                    tick += 20;
                    bool triggered = det.Feed(d[0], d[1], tick);
                    Assert::IsFalse(triggered, L"Circular motion should not trigger");
                }
            }
        }

        // ── Distance below minimum doesn't trigger ──

        TEST_METHOD(DistanceBelowMinimumDoesNotTrigger)
        {
            ShakeDetector det(100000, 1000, 400);

            uint64_t tick = 1000;
            for (int i = 0; i < 20; ++i)
            {
                int64_t dx = (i % 2 == 0) ? 100 : -100;
                tick += 20;
                bool triggered = det.Feed(dx, 0, tick);
                Assert::IsFalse(triggered, L"Should not trigger when total distance is below minimum");
            }
        }

        // ── Old movements are pruned ──

        TEST_METHOD(OldMovementsArePruned)
        {
            ShakeDetector det(500, 1000, 400);

            det.Feed(200, 0, 100);
            det.Feed(-200, 0, 200);

            // Jump forward 2 seconds — old data pruned.
            bool triggered = det.Feed(50, 0, 2200);
            Assert::IsFalse(triggered, L"Old movements should be pruned");

            triggered = det.Feed(-50, 0, 2250);
            Assert::IsFalse(triggered, L"Pruned history should not contribute to detection");
        }

        // ── Same-direction movements are merged ──

        TEST_METHOD(SameDirectionMovementsMerged)
        {
            ShakeDetector det(1000, 1000, 400);

            det.Feed(10, 0, 1000);
            det.Feed(10, 0, 1010);
            det.Feed(10, 0, 1020);

            bool triggered = det.Feed(-10, 0, 1030);
            Assert::IsFalse(triggered, L"Small movements shouldn't trigger");
        }

        // ── Vertical shake also triggers ──

        TEST_METHOD(RapidVerticalShakeTriggers)
        {
            ShakeDetector det(1000, 1000, 400);
            uint64_t tick = 1000;
            bool triggered = false;

            for (int i = 0; i < 30; ++i)
            {
                int64_t dy = (i % 2 == 0) ? 200 : -200;
                tick += 20;
                if (det.Feed(0, dy, tick))
                {
                    triggered = true;
                    break;
                }
            }

            Assert::IsTrue(triggered, L"Rapid vertical shake should trigger detection");
        }

        // ── Zero movement is ignored ──

        TEST_METHOD(ZeroMovementIgnored)
        {
            ShakeDetector det(1000, 1000, 400);
            bool triggered = det.Feed(0, 0, 1000);
            Assert::IsFalse(triggered, L"Zero movement should be ignored");
        }

        // ── Clear resets state ──

        TEST_METHOD(ClearResetsState)
        {
            ShakeDetector det(1000, 1000, 400);
            det.Feed(200, 0, 1000);
            det.Feed(-200, 0, 1020);
            det.Clear();

            bool triggered = det.Feed(50, 0, 1040);
            Assert::IsFalse(triggered, L"After clear, history should be empty");
        }

        // ── High shake factor requires more extreme shaking ──

        TEST_METHOD(HighShakeFactorHarderToTrigger)
        {
            ShakeDetector det(1000, 1000, 10000);
            uint64_t tick = 1000;
            bool triggered = false;

            for (int i = 0; i < 30; ++i)
            {
                int64_t dx = (i % 2 == 0) ? 200 : -200;
                tick += 20;
                if (det.Feed(dx, 0, tick))
                {
                    triggered = true;
                    break;
                }
            }

            Assert::IsFalse(triggered, L"Very high shake factor should make it harder to trigger");
        }

        // ── Stationary mouse: no activation ──

        TEST_METHOD(StationaryMouseNoActivation)
        {
            ShakeDetector det(1000, 1000, 400);

            // Feed only zero-movement events.
            for (int i = 0; i < 20; ++i)
            {
                bool triggered = det.Feed(0, 0, 1000 + static_cast<uint64_t>(i) * 50);
                Assert::IsFalse(triggered, L"Stationary mouse should not trigger activation");
            }
        }
    };

    // ═══════════════════════════════════════════════════════════════════════════
    // Activation Guard Tests
    // ═══════════════════════════════════════════════════════════════════════════

    TEST_CLASS(ActivationGuardTests)
    {
    public:

        // ── Game mode off allows activation ──

        TEST_METHOD(GameModeOffAllowsActivation)
        {
            auto result = CanActivate(true, {}, false, L"");
            Assert::IsTrue(result == ActivationCheck::Allow, L"Should allow when game mode is off");
        }

        // ── Game mode on with setting enabled blocks ──

        TEST_METHOD(GameModeOnWithSettingEnabledBlocks)
        {
            auto result = CanActivate(true, {}, true, L"");
            Assert::IsTrue(result == ActivationCheck::BlockedByGameMode, L"Should block when game mode on and setting enabled");
        }

        // ── Game mode on with setting disabled allows ──

        TEST_METHOD(GameModeOnWithSettingDisabledAllows)
        {
            auto result = CanActivate(false, {}, true, L"");
            Assert::IsTrue(result == ActivationCheck::Allow, L"Should allow when game mode setting is disabled");
        }

        // ── No excluded apps allows any foreground ──

        TEST_METHOD(NoExcludedAppsAllowsAnyForeground)
        {
            auto result = CanActivate(true, {}, false, L"NOTEPAD.EXE");
            Assert::IsTrue(result == ActivationCheck::Allow, L"Should allow when no excluded apps");
        }

        // ── Excluded app blocks activation ──

        TEST_METHOD(ExcludedAppBlocksActivation)
        {
            std::vector<std::wstring> excluded = { L"NOTEPAD.EXE" };
            auto result = CanActivate(true, excluded, false, L"NOTEPAD.EXE");
            Assert::IsTrue(result == ActivationCheck::BlockedByExcludedApp, L"Should block excluded app");
        }

        // ── Excluded app case-insensitive ──

        TEST_METHOD(ExcludedAppCaseInsensitive)
        {
            std::vector<std::wstring> excluded = { L"NOTEPAD.EXE" };
            auto result = CanActivate(true, excluded, false, L"notepad.exe");
            Assert::IsTrue(result == ActivationCheck::BlockedByExcludedApp, L"Should match case-insensitively");
        }

        // ── Excluded app partial path match ──

        TEST_METHOD(ExcludedAppPartialPathMatch)
        {
            std::vector<std::wstring> excluded = { L"NOTEPAD.EXE" };
            auto result = CanActivate(true, excluded, false, L"C:\\WINDOWS\\SYSTEM32\\NOTEPAD.EXE");
            Assert::IsTrue(result == ActivationCheck::BlockedByExcludedApp, L"Should match partial path");
        }

        // ── Non-excluded app allows ──

        TEST_METHOD(NonExcludedAppAllows)
        {
            std::vector<std::wstring> excluded = { L"NOTEPAD.EXE" };
            auto result = CanActivate(true, excluded, false, L"CALC.EXE");
            Assert::IsTrue(result == ActivationCheck::Allow, L"Non-excluded app should be allowed");
        }

        // ── Multiple excluded apps ──

        TEST_METHOD(MultipleExcludedAppsSecondMatches)
        {
            std::vector<std::wstring> excluded = { L"NOTEPAD.EXE", L"CALC.EXE" };
            auto result = CanActivate(true, excluded, false, L"CALC.EXE");
            Assert::IsTrue(result == ActivationCheck::BlockedByExcludedApp, L"Second excluded app should match");
        }

        // ── Empty foreground app not blocked ──

        TEST_METHOD(EmptyForegroundAppNotBlocked)
        {
            std::vector<std::wstring> excluded = { L"NOTEPAD.EXE" };
            auto result = CanActivate(true, excluded, false, L"");
            Assert::IsTrue(result == ActivationCheck::Allow, L"Empty foreground should not be blocked");
        }

        // ── Game mode checked before excluded apps ──

        TEST_METHOD(GameModeCheckedBeforeExcludedApps)
        {
            std::vector<std::wstring> excluded = { L"NOTEPAD.EXE" };
            auto result = CanActivate(true, excluded, true, L"NOTEPAD.EXE");
            Assert::IsTrue(result == ActivationCheck::BlockedByGameMode, L"Game mode should take priority");
        }
    };
}
