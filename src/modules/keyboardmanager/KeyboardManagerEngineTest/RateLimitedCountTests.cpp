#include "pch.h"

// Suppressing 26466 - Don't use static_cast downcasts - in CppUnitTest.h
#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include <keyboardmanager/KeyboardManagerEngineLibrary/RateLimitedCount.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace RemappingLogicTests
{
    // The engine logs "how many key-downs came from a keyboard that could not be identified" through
    // this, so that the log carries a count at most once per interval and nothing about the keys.
    TEST_CLASS (RateLimitedCountTests)
    {
    public:
        TEST_METHOD (FirstEvent_IsReportedAtOnce)
        {
            RateLimitedCount count(60000);
            Assert::AreEqual(1ull, count.Add(5000));
        }

        TEST_METHOD (EventsInsideTheInterval_AreNotReported)
        {
            RateLimitedCount count(60000);
            count.Add(5000);
            for (uint64_t t = 5001; t < 65000; t += 100)
            {
                Assert::AreEqual(0ull, count.Add(t));
            }
        }

        TEST_METHOD (FirstEventAfterTheInterval_ReportsTheTotalSinceTheLastReport)
        {
            RateLimitedCount count(60000);
            count.Add(5000);
            count.Add(6000);
            count.Add(7000);
            count.Add(64999);

            // The four events since the last report: three quiet ones and this one.
            Assert::AreEqual(4ull, count.Add(65000));

            // The count starts again after a report.
            Assert::AreEqual(0ull, count.Add(65001));
            Assert::AreEqual(2ull, count.Add(125000));
        }

        TEST_METHOD (FirstEventAtTimeZero_IsReported)
        {
            RateLimitedCount count(60000);
            Assert::AreEqual(1ull, count.Add(0));
            Assert::AreEqual(0ull, count.Add(1));
        }
    };
}
