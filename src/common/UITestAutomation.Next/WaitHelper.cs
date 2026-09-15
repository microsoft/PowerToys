// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>Polling helpers for UI state that must remain true across consecutive observations.</summary>
public static class WaitHelper
{
    /// <summary>The final state of a stable wait, including its last observation or retryable exception.</summary>
    public readonly record struct StableWaitResult<T>(bool Succeeded, T? LastObservation, int ConsecutiveMatches, Exception? LastException = null);

    /// <summary>
    /// Poll <paramref name="observe"/> until <paramref name="isMatch"/> is true for
    /// <paramref name="requiredConsecutiveMatches"/> consecutive samples. A mismatch resets the
    /// sample count and invokes the optional recovery action. Exceptions propagate unless
    /// <paramref name="shouldRetryException"/> explicitly classifies them as transient.
    /// </summary>
    public static StableWaitResult<T> WaitForStable<T>(
        Func<T?> observe,
        Func<T?, bool> isMatch,
        int timeoutMS,
        int requiredConsecutiveMatches = 1,
        int pollIntervalMS = 100,
        Action<T?>? recover = null,
        Func<Exception, bool>? shouldRetryException = null)
    {
        ArgumentNullException.ThrowIfNull(observe);
        ArgumentNullException.ThrowIfNull(isMatch);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMS);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requiredConsecutiveMatches);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pollIntervalMS);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var consecutiveMatches = 0;
        T? lastObservation = default;
        Exception? lastException = null;

        while (stopwatch.ElapsedMilliseconds < timeoutMS)
        {
            try
            {
                lastObservation = observe();
                if (isMatch(lastObservation))
                {
                    consecutiveMatches++;
                    if (consecutiveMatches >= requiredConsecutiveMatches)
                    {
                        return new StableWaitResult<T>(true, lastObservation, consecutiveMatches);
                    }
                }
                else
                {
                    consecutiveMatches = 0;
                    recover?.Invoke(lastObservation);
                }

                lastException = null;
            }
            catch (Exception ex) when (shouldRetryException?.Invoke(ex) == true)
            {
                consecutiveMatches = 0;
                lastException = ex;
            }

            Thread.Sleep(pollIntervalMS);
        }

        return new StableWaitResult<T>(false, lastObservation, consecutiveMatches, lastException);
    }
}
