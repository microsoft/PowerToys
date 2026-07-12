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
    // Records every InjectUp call and can be configured to report failure (e.g. a UIPI block).
    class FakeInjector : public IButtonUpInjector
    {
    public:
        std::vector<MouseButton> calls;
        bool succeed = true;

        bool InjectUp(MouseButton button) override
        {
            calls.push_back(button);
            return succeed;
        }
    };

    // Defaults mirror the shipping defaults: RMB on, MMB off, 300 ms hold, move-cancel on at 5 px.
    Settings DefaultSettings()
    {
        return Settings{};
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
            Assert::IsTrue(e.OnButtonUp(MouseButton::Right, 400, s)); // held >= 300 ms -> suppress UP
            Assert::IsTrue(e.IsLocked(MouseButton::Right));
            Assert::AreEqual(static_cast<size_t>(0), inj.calls.size()); // locking does not inject
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

            // Next physical tap releases the lock.
            Assert::IsTrue(e.OnButtonDown(MouseButton::Right, 1000, PointL{ 0, 0 }, s)); // suppress DOWN
            Assert::IsFalse(e.IsLocked(MouseButton::Right));
            Assert::AreEqual(static_cast<size_t>(1), inj.calls.size());
            Assert::IsTrue(inj.calls[0] == MouseButton::Right);

            // The paired physical UP is swallowed so the app never sees an unbalanced up.
            Assert::IsTrue(e.OnButtonUp(MouseButton::Right, 1005, s));
        }

        TEST_METHOD(TapToReleaseInjectionFailureDropsLockAndPassesThrough)
        {
            FakeInjector inj;
            inj.succeed = false; // simulate a UIPI block
            Engine e(inj);
            Settings s = DefaultSettings();

            e.OnButtonDown(MouseButton::Right, 0, PointL{ 0, 0 }, s);
            e.OnButtonUp(MouseButton::Right, 400, s);
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
            Assert::IsTrue(e.OnButtonUp(MouseButton::Right, 400, s));
            Assert::IsTrue(e.IsLocked(MouseButton::Right));
        }

        TEST_METHOD(MoveAfterThresholdStillLocks)
        {
            FakeInjector inj;
            Engine e(inj);
            Settings s = DefaultSettings();

            e.OnButtonDown(MouseButton::Right, 0, PointL{ 0, 0 }, s);
            e.OnMove(350, PointL{ 500, 500 }, s); // past 300 ms -> armed, motion ignored
            Assert::IsTrue(e.OnButtonUp(MouseButton::Right, 400, s));
            Assert::IsTrue(e.IsLocked(MouseButton::Right));
        }

        TEST_METHOD(MoveCancelDisabledIgnoresMotion)
        {
            FakeInjector inj;
            Engine e(inj);
            Settings s = DefaultSettings();
            s.moveCancelEnabled = false;

            e.OnButtonDown(MouseButton::Right, 0, PointL{ 0, 0 }, s);
            e.OnMove(50, PointL{ 500, 500 }, s);
            Assert::IsTrue(e.OnButtonUp(MouseButton::Right, 400, s));
            Assert::IsTrue(e.IsLocked(MouseButton::Right));
        }
    };

    TEST_CLASS(ButtonsAndSettings)
    {
    public:
        TEST_METHOD(RightAndMiddleAreIndependent)
        {
            FakeInjector inj;
            Engine e(inj);
            Settings s = DefaultSettings();
            s.rmbEnabled = true;
            s.mmbEnabled = true;

            // Lock RMB.
            e.OnButtonDown(MouseButton::Right, 0, PointL{ 0, 0 }, s);
            e.OnButtonUp(MouseButton::Right, 400, s);
            // Quick MMB tap, should not lock.
            e.OnButtonDown(MouseButton::Middle, 500, PointL{ 0, 0 }, s);
            Assert::IsFalse(e.OnButtonUp(MouseButton::Middle, 550, s));

            Assert::IsTrue(e.IsLocked(MouseButton::Right));
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
            Assert::IsTrue(e.OnButtonUp(MouseButton::Left, 400, s)); // held >= 300 ms -> suppress UP
            Assert::IsTrue(e.IsLocked(MouseButton::Left));

            // A left-lock is released by the injected LEFTUP path just like the other buttons.
            Assert::IsTrue(e.OnButtonDown(MouseButton::Left, 1000, PointL{ 0, 0 }, s));
            Assert::IsFalse(e.IsLocked(MouseButton::Left));
            Assert::AreEqual(static_cast<size_t>(1), inj.calls.size());
            Assert::IsTrue(inj.calls[0] == MouseButton::Left);
        }

        TEST_METHOD(LeftIsIndependentOfRightAndMiddle)
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
            // Quick right and middle taps must not lock, and must not disturb the left lock.
            e.OnButtonDown(MouseButton::Right, 500, PointL{ 0, 0 }, s);
            Assert::IsFalse(e.OnButtonUp(MouseButton::Right, 550, s));
            e.OnButtonDown(MouseButton::Middle, 600, PointL{ 0, 0 }, s);
            Assert::IsFalse(e.OnButtonUp(MouseButton::Middle, 650, s));

            Assert::IsTrue(e.IsLocked(MouseButton::Left));
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
            Assert::AreEqual(static_cast<size_t>(1), inj.calls.size()); // released via the injector
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
            Assert::AreEqual(static_cast<size_t>(1), inj.calls.size());
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
