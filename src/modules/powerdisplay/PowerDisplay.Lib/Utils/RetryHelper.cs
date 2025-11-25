// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;

namespace PowerDisplay.Common.Utils
{
    /// <summary>
    /// Helper class for executing operations with retry logic.
    /// Provides a unified retry pattern for DDC/CI operations that may fail intermittently.
    /// </summary>
    public static class RetryHelper
    {
        /// <summary>
        /// Default delay between retry attempts in milliseconds.
        /// </summary>
        public const int DefaultRetryDelayMs = 100;

        /// <summary>
        /// Default maximum number of retry attempts.
        /// </summary>
        public const int DefaultMaxRetries = 3;

        /// <summary>
        /// Executes an async operation with retry logic.
        /// </summary>
        /// <typeparam name="T">The return type of the operation.</typeparam>
        /// <param name="operation">The async operation to execute.</param>
        /// <param name="isValid">Predicate to determine if the result is valid.</param>
        /// <param name="maxRetries">Maximum number of retry attempts (default: 3).</param>
        /// <param name="delayMs">Delay between retries in milliseconds (default: 100).</param>
        /// <param name="operationName">Optional name for logging purposes.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The result of the operation, or default if all retries failed.</returns>
        public static async Task<T?> ExecuteWithRetryAsync<T>(
            Func<Task<T?>> operation,
            Func<T?, bool> isValid,
            int maxRetries = DefaultMaxRetries,
            int delayMs = DefaultRetryDelayMs,
            string? operationName = null,
            CancellationToken cancellationToken = default)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var result = await operation();

                    if (isValid(result))
                    {
                        if (attempt > 0 && !string.IsNullOrEmpty(operationName))
                        {
                            Logger.LogDebug($"[Retry] {operationName} succeeded on attempt {attempt + 1}");
                        }

                        return result;
                    }

                    if (attempt < maxRetries - 1)
                    {
                        if (!string.IsNullOrEmpty(operationName))
                        {
                            Logger.LogWarning($"[Retry] {operationName} returned invalid result on attempt {attempt + 1}, retrying...");
                        }

                        await Task.Delay(delayMs, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (attempt < maxRetries - 1)
                    {
                        if (!string.IsNullOrEmpty(operationName))
                        {
                            Logger.LogWarning($"[Retry] {operationName} failed on attempt {attempt + 1}: {ex.Message}, retrying...");
                        }

                        await Task.Delay(delayMs, cancellationToken);
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(operationName))
                        {
                            Logger.LogWarning($"[Retry] {operationName} failed after {maxRetries} attempts: {ex.Message}");
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(operationName))
            {
                Logger.LogWarning($"[Retry] {operationName} failed after {maxRetries} attempts");
            }

            return default;
        }

        /// <summary>
        /// Executes a synchronous operation with retry logic.
        /// </summary>
        /// <typeparam name="T">The return type of the operation.</typeparam>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="isValid">Predicate to determine if the result is valid.</param>
        /// <param name="maxRetries">Maximum number of retry attempts (default: 3).</param>
        /// <param name="delayMs">Delay between retries in milliseconds (default: 100).</param>
        /// <param name="operationName">Optional name for logging purposes.</param>
        /// <returns>The result of the operation, or default if all retries failed.</returns>
        public static T? ExecuteWithRetry<T>(
            Func<T?> operation,
            Func<T?, bool> isValid,
            int maxRetries = DefaultMaxRetries,
            int delayMs = DefaultRetryDelayMs,
            string? operationName = null)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var result = operation();

                    if (isValid(result))
                    {
                        if (attempt > 0 && !string.IsNullOrEmpty(operationName))
                        {
                            Logger.LogDebug($"[Retry] {operationName} succeeded on attempt {attempt + 1}");
                        }

                        return result;
                    }

                    if (attempt < maxRetries - 1)
                    {
                        if (!string.IsNullOrEmpty(operationName))
                        {
                            Logger.LogWarning($"[Retry] {operationName} returned invalid result on attempt {attempt + 1}, retrying...");
                        }

                        Thread.Sleep(delayMs);
                    }
                }
                catch (Exception ex)
                {
                    if (attempt < maxRetries - 1)
                    {
                        if (!string.IsNullOrEmpty(operationName))
                        {
                            Logger.LogWarning($"[Retry] {operationName} failed on attempt {attempt + 1}: {ex.Message}, retrying...");
                        }

                        Thread.Sleep(delayMs);
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(operationName))
                        {
                            Logger.LogWarning($"[Retry] {operationName} failed after {maxRetries} attempts: {ex.Message}");
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(operationName))
            {
                Logger.LogWarning($"[Retry] {operationName} failed after {maxRetries} attempts");
            }

            return default;
        }
    }
}
