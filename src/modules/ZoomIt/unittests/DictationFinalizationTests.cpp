#include <CppUnitTest.h>

#include <atomic>
#include <chrono>
#include <thread>

#include "DictationFinalization.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace DictationFinalizationTests
{
    TEST_CLASS(DictationFinalizationWaitTests)
    {
    public:
        TEST_METHOD(ReturnsWhenFinalizationCompletes)
        {
            bool finalizing = true;

            const auto result = WaitForDictationFinalization(
                std::chrono::seconds( 1 ),
                [&] { return finalizing; },
                [] { return false; },
                [&]( std::chrono::milliseconds ) {
                    finalizing = false;
                } );

            Assert::AreEqual(
                static_cast<int>(DictationFinalizationWaitResult::Completed),
                static_cast<int>(result) );
        }

        TEST_METHOD(ReturnsWhenCancellationIsRequested)
        {
            const auto result = WaitForDictationFinalization(
                std::chrono::seconds( 1 ),
                [] { return true; },
                [] { return true; },
                []( std::chrono::milliseconds ) {} );

            Assert::AreEqual(
                static_cast<int>(DictationFinalizationWaitResult::Cancelled),
                static_cast<int>(result) );
        }

        TEST_METHOD(TimesOutWithoutAnUnboundedWait)
        {
            int updateCount = 0;
            const auto started = std::chrono::steady_clock::now();

            const auto result = WaitForDictationFinalization(
                std::chrono::milliseconds( 30 ),
                [] { return true; },
                [] { return false; },
                []( std::chrono::milliseconds duration ) {
                    std::this_thread::sleep_for( duration );
                },
                [&] {
                    ++updateCount;
                } );

            const auto elapsed = std::chrono::steady_clock::now() - started;
            Assert::AreEqual(
                static_cast<int>(DictationFinalizationWaitResult::TimedOut),
                static_cast<int>(result) );
            Assert::IsTrue( updateCount > 0 );
            Assert::IsTrue( elapsed < std::chrono::seconds( 1 ) );
        }
    };
}
