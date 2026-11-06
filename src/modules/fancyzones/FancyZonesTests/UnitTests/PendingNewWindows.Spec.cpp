#include "pch.h"

#include <FancyZonesLib/PendingNewWindows.h>

#include <CppUnitTestLogger.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    TEST_CLASS (PendingNewWindowsUnitTests)
    {
        static constexpr unsigned long long SettleDelay = 100;
        const HWND window1 = reinterpret_cast<HWND>(0x1001);
        const HWND window2 = reinterpret_cast<HWND>(0x1002);

        TEST_METHOD (Empty)
        {
            PendingNewWindows pending(SettleDelay);

            Assert::IsTrue(pending.Empty());
            Assert::IsFalse(pending.NextSettleDelay(0).has_value());
            Assert::IsTrue(pending.TakeSettled(1000).empty());
        }

        TEST_METHOD (WindowNotSettledBeforeDelay)
        {
            PendingNewWindows pending(SettleDelay);
            pending.Add(window1, 1000);

            Assert::IsTrue(pending.TakeSettled(1000 + SettleDelay - 1).empty());
            Assert::IsFalse(pending.Empty());
        }

        TEST_METHOD (WindowSettledAfterDelay)
        {
            PendingNewWindows pending(SettleDelay);
            pending.Add(window1, 1000);

            const auto settled = pending.TakeSettled(1000 + SettleDelay);
            Assert::AreEqual(static_cast<size_t>(1), settled.size());
            Assert::IsTrue(settled[0] == window1);
            Assert::IsTrue(pending.Empty());
        }

        TEST_METHOD (WindowTakenOnlyOnce)
        {
            PendingNewWindows pending(SettleDelay);
            pending.Add(window1, 1000);

            Assert::AreEqual(static_cast<size_t>(1), pending.TakeSettled(1000 + SettleDelay).size());
            Assert::IsTrue(pending.TakeSettled(1000 + 2 * SettleDelay).empty());
        }

        TEST_METHOD (NewEventRestartsSettlePeriod)
        {
            // e.g. the window is created and shown right after
            PendingNewWindows pending(SettleDelay);
            pending.Add(window1, 1000);
            pending.Add(window1, 1050);

            Assert::IsTrue(pending.TakeSettled(1000 + SettleDelay).empty());
            Assert::AreEqual(static_cast<size_t>(1), pending.TakeSettled(1050 + SettleDelay).size());
        }

        TEST_METHOD (RemovedWindowNotReturned)
        {
            // the window was destroyed before it settled
            PendingNewWindows pending(SettleDelay);
            pending.Add(window1, 1000);
            pending.Remove(window1);

            Assert::IsTrue(pending.Empty());
            Assert::IsTrue(pending.TakeSettled(1000 + SettleDelay).empty());
        }

        TEST_METHOD (OnlySettledWindowsTaken)
        {
            PendingNewWindows pending(SettleDelay);
            pending.Add(window1, 1000);
            pending.Add(window2, 1060);

            const auto settled = pending.TakeSettled(1000 + SettleDelay);
            Assert::AreEqual(static_cast<size_t>(1), settled.size());
            Assert::IsTrue(settled[0] == window1);
            Assert::IsFalse(pending.Empty());
        }

        TEST_METHOD (NextSettleDelayIsShortestRemaining)
        {
            PendingNewWindows pending(SettleDelay);
            pending.Add(window1, 1000);
            pending.Add(window2, 1060);

            Assert::AreEqual(40ULL, pending.NextSettleDelay(1060).value());

            pending.TakeSettled(1100);
            Assert::AreEqual(60ULL, pending.NextSettleDelay(1100).value());
        }

        TEST_METHOD (NextSettleDelayIsZeroWhenOverdue)
        {
            PendingNewWindows pending(SettleDelay);
            pending.Add(window1, 1000);

            Assert::AreEqual(0ULL, pending.NextSettleDelay(1000 + 3 * SettleDelay).value());
        }

        TEST_METHOD (TakeAllReturnsUnsettledWindows)
        {
            PendingNewWindows pending(SettleDelay);
            pending.Add(window1, 1000);
            pending.Add(window2, 1060);

            Assert::AreEqual(static_cast<size_t>(2), pending.TakeAll().size());
            Assert::IsTrue(pending.Empty());
        }
    };
}
