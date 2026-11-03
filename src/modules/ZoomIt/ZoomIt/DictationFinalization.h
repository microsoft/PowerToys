#pragma once

#include <algorithm>
#include <chrono>
#include <functional>

enum class DictationFinalizationWaitResult
{
    Completed,
    Cancelled,
    TimedOut,
};

template<typename FinalizingPredicate, typename CancellationPredicate, typename WaitFunction>
DictationFinalizationWaitResult WaitForDictationFinalization(
    std::chrono::milliseconds timeout,
    FinalizingPredicate&& isFinalizing,
    CancellationPredicate&& isCancellationRequested,
    WaitFunction&& wait,
    const std::function<void()>& waitCallback = {})
{
    const auto deadline = std::chrono::steady_clock::now() + timeout;
    while( isFinalizing() )
    {
        if( waitCallback )
        {
            waitCallback();
        }
        if( isCancellationRequested() )
        {
            return DictationFinalizationWaitResult::Cancelled;
        }

        const auto now = std::chrono::steady_clock::now();
        if( now >= deadline )
        {
            return DictationFinalizationWaitResult::TimedOut;
        }

        const auto remaining =
            std::chrono::duration_cast<std::chrono::milliseconds>( deadline - now );
        wait( (std::min)( std::chrono::milliseconds( 50 ), remaining ) );
    }

    return DictationFinalizationWaitResult::Completed;
}
