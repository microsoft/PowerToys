// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "MouseButtonLockCore.h"

#include <vector>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace mousebuttonlock;

namespace
{
    // Records every InjectUp (each is a lock release) and can be configured to report failure
    // (e.g. a UIPI block on the injection).
    class FakeInjector : public IButtonUpInjector
    {
    public:
        std::vector<MouseButton> upCalls;
        bool succeed = true;

        bool InjectUp(MouseButton button) override
        {
            upCalls.push_back(button);
            return succeed;
        }
    };

    // Test baseline: RMB on, MMB off, move-cancel on at 5 px. The hold is pinned to 300 ms here so a
    // release at tick 400 reads as "past threshold" and the hold-mechanics tests below stay concise.
    // The shipping default is 1200 ms (see MouseButtonLockCore.h / DEFAULT_HOLD_DURATION_MS, matching
    // Windows ClickLock); tests that pivot on the exact threshold set holdDurationMs themselves.
    Settings DefaultSettings()
    {
        Settings s;
        s.holdDurationMs = 300;
        return s;
    }
}

namespace MouseButtonLockEngineTests
{
    TEST_CLASS(HoldToLock)
    {
    public:
        TEST_METHOD(DownIsNeverSuppressed)
        {
            FakeInjector inj;
            Engine e(inj);
            Assert::IsFalse(e.OnButtonDown(MouseButton::Right, 0, PointL{ 100, 100 }, DefaultSettings()));
        }

        TEST_METHOD(HoldPastThresholdLocksAndSuppressesUp)
        {
            FakeInjector inj;
            Engine e(inj);
            Settings s = DefaultSettings();

            e.OnButtonDown(MouseButton::Right, 0, PointL{ 100, 100 }, s);
            // The physical UP is suppressed so the button stays held without the original click ever
            // completing. Locking injects nothing; the held state is the suppressed up.
            Assert::IsTrue(e.OnButtonUp(MouseButton::Right, 400, s)); // held >= 300 ms -> suppress UP
            Assert::IsTrue(e.IsLocked(MouseButton::Right));
            Assert::AreEqual(static_cast<size_t>(0), inj.upCalls.size()); // locking does not inject
        }

        TEST_METHOD(ExactThresholdLocks)
        {
            FakeInjector inj;
            Engine e(inj);
            Settings s = DefaultSettings();
            s.holdDurationMs = 300;

            e.OnButtonDown(MouseButton::Right, 0, PointL{ 0, 0 }, s);
            Assert::IsTrue(e.OnButtonUp(MouseButton::Right, 300, s)); // elapsed == threshold -> lock
            Assert::IsTrue(e.IsLocked(MouseButton::Right));
        }

        TEST_METHOD(JustUnderThresholdDoesNotLock)
        {
            FakeInjector inj;
            Engine e(inj);
            Settings s = DefaultSettings();
            s.holdDurationMs = 300;

            e.OnButtonDown(MouseButton::Right, 0, PointL{ 0, 0 }, s);
            Assert::IsFalse(e.OnButtonUp(MouseButton::Right, 299, s)); // regular click
            Assert::IsFalse(e.IsLocked(MouseButton::Right));
        }

        TEST_METHOD(TapToReleaseInjectsUpAndSwallowsPairedUp)
        {
            FakeInjector inj;
            Engine e(inj);
            Settings s = DefaultSettings();

            e.OnButtonDown(MouseButton::Right, 0, PointL{ 0, 0 }, s);
            e.OnButtonUp(MouseButton::Right, 400, s);
            Assert::IsTrue(e.IsLocked(MouseButton::Right));

            // Next physical tap releases the lock: inject the balancing UP and suppress the DOWN.
            Assert::IsTrue(e.OnButtonDown(MouseButton::Right, 1000, PointL{ 0, 0 }, s)); // suppress DOWN
            Assert::IsFalse(e.IsLocked(MouseButton::Right));
            Assert::AreEqual(static_cast<size_t>(1), inj.upCalls.size());
            Assert::IsTrue(inj.upCalls[0] == MouseButton::Right);

            // The paired physical UP is swallowed so the app never sees an unbalanced up.
            Assert::IsTrue(e.OnButtonUp(MouseButton::Right, 1005, s));
        }

        TEST_METHOD(TapToReleaseInjectionFailureDropsLockAndPassesThrough)
        {
            FakeInjector inj;
            inj.succeed = false; // the release UP injection fails (e.g. a UIPI block)
            Engine e(inj);
            Settings s = DefaultSettings();

            e.OnButtonDown(MouseButton::Right, 0, PointL{ 0, 0 }, s);
            e.OnButtonUp(MouseButton::Right, 400, s); // locks (locking never injects)
            Assert::IsTrue(e.IsLocked(MouseButton::Right));

            // Release tap, injection fails: don't suppress, and drop the lock so state can't disagree.
            Assert::IsFalse(e.OnButtonDown(MouseButton::Right, 1000, PointL{ 0, 0 }, s));
            Assert::IsFalse(e.IsLocked(MouseButton::Right));
        }
    };

    TEST_CLASS(MoveCancel)
    {
    public:
        TEST_METHOD(MoveBeyondDeadzoneBeforeThresholdCancels)
        {
            FakeInjector inj;
            Engine e(inj);
            Settings s = DefaultSettings(); // 5 px dead-zone

            e.OnButtonDown(MouseButton::Right, 0, PointL{ 0, 0 }, s);
            e.OnMove(50, PointL{ 100, 100 }, s); // far move while still in the arming window
            Assert::IsFalse(e.OnButtonUp(MouseButton::Right, 400, s));
            Assert::IsFalse(e.IsLocked(MouseButton::Right));
        }

        TEST_METHOD(MoveWithinDeadzoneStillLocks)
        {
            FakeInjector inj;
            Engine e(inj);
            Settings s = DefaultSettings();

            e.OnButtonDown(MouseButton::Right, 0, PointL{ 0, 0 }, s);
            e.OnMove(50, PointL{ 3, 0 }, s); // 3 px < 5 px
            Assert::IsTrue(e.OnButtonUp(MouseButton::Right, 400, s)); // locks; up suppressed
            Assert::IsTrue(e.IsLocked(MouseButton::Right));
        }

        TEST_METHOD(MoveAfterThresholdCancelsLockByDefault)
        {
            FakeInjector inj;
            Engine e(inj);
            Settings s = DefaultSettings(); // dragLocksEnabled is false by default

            // A drag past the dead-zone after the threshold is still a drag (e.g. selecting text),
            // so it cancels the pending lock and the button-up passes through normally.
            e.OnButtonDown(MouseButton::Right, 0, PointL{ 0, 0 }, s);
            e.OnMove(350, PointL{ 500, 500 }, s); // past 300 ms, but a drag -> cancels
            Assert::IsFalse(e.OnButtonUp(MouseButton::Right, 400, s));
            Assert::IsFalse(e.IsLocked(MouseButton::Right));
        }

        TEST_METHOD(MoveAfterThresholdStillLocksWhenDragLocksEnabled)
        {
            FakeInjector inj;
            Engine e(inj);
            Settings s = DefaultSettings();
            s.dragLocksEnabled = true; // opt in to the hands-free camera-drag behavior

            e.OnButtonDown(MouseButton::Right, 0, PointL{ 0, 0 }, s);
            e.OnMove(350, PointL{ 500, 500 }, s); // past 300 ms -> armed, motion ignored
            Assert::IsTrue(e.OnButtonUp(MouseButton::Right, 400, s)); // locks; up suppressed
            Assert::IsTrue(e.IsLocked(MouseButton::Right));
        }

    };

    TEST_CLASS(ButtonsAndSettings)
    {
    public:
        TEST_METHOD(PressingAnotherButtonReleasesTheLock)
        {
            FakeInjector inj;
            Engine e(inj);
            Settings s = DefaultSettings();
            s.rmbEnabled = true;
            s.mmbEnabled = true;

            // Lock RMB.
            e.OnButtonDown(MouseButton::Right, 0, PointL{ 0, 0 }, s);
            e.OnButtonUp(MouseButton::Right, 400, s);
            Assert::IsTrue(e.IsLocked(MouseButton::Right));

            // A press of a different button (middle) releases the held right button (injecting its up)
            // so the mouse is never left stuck on the locked one.
            e.OnButtonDown(MouseButton::Middle, 500, PointL{ 0, 0 }, s);
            Assert::IsFalse(e.IsLocked(MouseButton::Right));
            Assert::AreEqual(static_cast<size_t>(1), inj.upCalls.size());
            Assert::IsTrue(inj.upCalls[0] == MouseButton::Right);
            // The middle tap itself is quick, so it does not lock.
            Assert::IsFalse(e.OnButtonUp(MouseButton::Middle, 550, s));
            Assert::IsFalse(e.IsLocked(MouseButton::Middle));
        }

        TEST_METHOD(DisabledButtonDoesNotLock)
        {
            FakeInjector inj;
            Engine e(inj);
            Settings s = DefaultSettings();
            s.mmbEnabled = false;

            e.OnButtonDown(MouseButton::Middle, 0, PointL{ 0, 0 }, s);
            Assert::IsFalse(e.OnButtonUp(MouseButton::Middle, 400, s));
            Assert::IsFalse(e.IsLocked(MouseButton::Middle));
        }

        TEST_METHOD(LeftButtonOffByDefault)
        {
            FakeInjector inj;
            Engine e(inj);
            Settings s = DefaultSettings(); // lmbEnabled defaults to false

            Assert::IsFalse(s.lmbEnabled);
            e.OnButtonDown(MouseButton::Left, 0, PointL{ 0, 0 }, s);
            Assert::IsFalse(e.OnButtonUp(MouseButton::Left, 400, s)); // held past threshold but disabled
            Assert::IsFalse(e.IsLocked(MouseButton::Left));
        }

        TEST_METHOD(LeftButtonLocksWhenEnabled)
        {
            FakeInjector inj;
            Engine e(inj);
            Settings s = DefaultSettings();
            s.lmbEnabled = true;

            e.OnButtonDown(MouseButton::Left, 0, PointL{ 0, 0 }, s);
            Assert::IsTrue(e.OnButtonUp(MouseButton::Left, 400, s)); // held >= 300 ms -> locks, up suppressed
            Assert::IsTrue(e.IsLocked(MouseButton::Left));

            // A left-lock is released by the injected LEFTUP path just like the other buttons.
            Assert::IsTrue(e.OnButtonDown(MouseButton::Left, 1000, PointL{ 0, 0 }, s));
            Assert::IsFalse(e.IsLocked(MouseButton::Left));
            Assert::AreEqual(static_cast<size_t>(1), inj.upCalls.size());
            Assert::IsTrue(inj.upCalls[0] == MouseButton::Left);
        }

        TEST_METHOD(LockedButtonIsIndependentUntilAnotherButtonIsPressed)
        {
            FakeInjector inj;
            Engine e(inj);
            Settings s = DefaultSettings();
            s.lmbEnabled = true;
            s.rmbEnabled = true;
            s.mmbEnabled = true;

            // Lock the left button.
            e.OnButtonDown(MouseButton::Left, 0, PointL{ 0, 0 }, s);
            e.OnButtonUp(MouseButton::Left, 400, s);
            Assert::IsTrue(e.IsLocked(MouseButton::Left));

            // A right-button press releases the held left button (any press frees a lock) and does not
            // itself lock on a quick tap.
            e.OnButtonDown(MouseButton::Right, 500, PointL{ 0, 0 }, s);
            Assert::IsFalse(e.IsLocked(MouseButton::Left));
            Assert::AreEqual(static_cast<size_t>(1), inj.upCalls.size());
            Assert::IsTrue(inj.upCalls[0] == MouseButton::Left);
            Assert::IsFalse(e.OnButtonUp(MouseButton::Right, 550, s));
            Assert::IsFalse(e.IsLocked(MouseButton::Right));
            Assert::IsFalse(e.IsLocked(MouseButton::Middle));
        }

        TEST_METHOD(EnforceEnabledReleasesNowDisabledButton)
        {
            FakeInjector inj;
            Engine e(inj);
            Settings s = DefaultSettings();

            e.OnButtonDown(MouseButton::Right, 0, PointL{ 0, 0 }, s);
            e.OnButtonUp(MouseButton::Right, 400, s);
            Assert::IsTrue(e.IsLocked(MouseButton::Right));

            s.rmbEnabled = false;
            e.EnforceEnabled(s);
            Assert::IsFalse(e.IsLocked(MouseButton::Right));
            Assert::AreEqual(static_cast<size_t>(1), inj.upCalls.size()); // released via the injector
        }

        TEST_METHOD(ReleaseAllReleasesLockedButtons)
        {
            FakeInjector inj;
            Engine e(inj);
            Settings s = DefaultSettings();

            e.OnButtonDown(MouseButton::Right, 0, PointL{ 0, 0 }, s);
            e.OnButtonUp(MouseButton::Right, 400, s);
            e.ReleaseAll();
            Assert::IsFalse(e.IsLocked(MouseButton::Right));
            Assert::AreEqual(static_cast<size_t>(1), inj.upCalls.size());
        }

        TEST_METHOD(ResetTransientClearsStaleHold)
        {
            FakeInjector inj;
            Engine e(inj);
            Settings s = DefaultSettings();

            // Begin a hold but never release (as if the module was disabled mid-hold).
            e.OnButtonDown(MouseButton::Right, 0, PointL{ 0, 0 }, s);
            // Re-enabling clears the transient state.
            e.ResetTransient();
            // A much later UP must NOT lock: the stale downTick / physicalDown were cleared.
            Assert::IsFalse(e.OnButtonUp(MouseButton::Right, 100000, s));
            Assert::IsFalse(e.IsLocked(MouseButton::Right));
        }
    };
}
