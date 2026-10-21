// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Runs the {{prompt}} window's XAML on a thread of its own, started the first time a snippet asks
/// for something, so an engine that never prompts never loads XAML. It is never the keyboard hook's
/// thread: the hook keeps answering Windows while a prompt is open. The expansion worker waits here
/// for each answer, so prompts are asked one at a time, in order.
/// </summary>
internal static class PromptHost
{
    private const int StartTimeoutMs = 10_000;

    private static readonly object StartLock = new();
    private static volatile DispatcherQueue? _dispatcher;
    private static bool _unavailable;

    /// <summary>
    /// Asks <paramref name="request"/> in the prompt window. Returns false when the window can't be
    /// shown, so the caller can fall back to the classic box. Otherwise <paramref name="answer"/> is
    /// the answer, or null when the user cancelled.
    /// </summary>
    public static bool TryAsk(PromptRequest request, IntPtr target, out string? answer)
    {
        answer = null;
        DispatcherQueue? dispatcher = EnsureStarted();
        if (dispatcher is null)
        {
            return false;
        }

        using var done = new ManualResetEventSlim();
        string? result = null;
        bool opened = false;
        bool queued = dispatcher.TryEnqueue(() =>
        {
            try
            {
                var window = new PromptWindow(request, target, typed =>
                {
                    result = typed;
                    done.Set();
                });
                window.Open();
                opened = true;
            }
            catch (Exception)
            {
                // A window that can't open falls back to the classic box rather than losing the snippet.
                done.Set();
            }
        });

        if (!queued)
        {
            return false;
        }

        done.Wait();
        if (!opened)
        {
            return false;
        }

        answer = result;
        return true;
    }

    private static DispatcherQueue? EnsureStarted()
    {
        lock (StartLock)
        {
            if (_dispatcher is not null || _unavailable)
            {
                return _dispatcher;
            }

            // Not disposed: if starting takes longer than the timeout, the thread still sets it later.
            var ready = new ManualResetEventSlim();
            var thread = new Thread(() =>
            {
                try
                {
                    WinRT.ComWrappersSupport.InitializeComWrappers();
                    Application.Start(start =>
                    {
                        DispatcherQueue queue = DispatcherQueue.GetForCurrentThread();
                        SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(queue));
                        _ = new PromptApp();
                        _dispatcher = queue;
                        ready.Set();
                    });
                }
                catch (Exception)
                {
                    // Without the Windows App SDK runtime, prompts use the classic box.
                }
                finally
                {
                    ready.Set();
                }
            })
            {
                IsBackground = true,
                Name = "PowerToys Text Expander prompt",
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            if (!ready.Wait(StartTimeoutMs) || _dispatcher is null)
            {
                _unavailable = true;
            }

            return _dispatcher;
        }
    }
}
