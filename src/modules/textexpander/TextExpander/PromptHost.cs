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
    public static bool TryAsk(PromptRequest request, IntPtr target, Action? accepted, out string? answer, CancellationToken cancellationToken = default)
        => TryShow(done => new PromptWindow(request, target, done).Open(), accepted, out answer, cancellationToken);

    /// <summary>
    /// Asks for <paramref name="choice"/> in the choice window. Returns false when the window can't be shown. Otherwise
    /// <paramref name="chosen"/> is what was picked, or null when the user cancelled.
    /// </summary>
    public static bool TryChoose(ClipboardChoice choice, IntPtr target, Action? accepted, out string? chosen, CancellationToken cancellationToken = default)
        => TryShow(done => new ClipboardChoiceWindow(choice, target, done).Open(), accepted, out chosen, cancellationToken);

    /// <summary>
    /// Opens a window on the prompt thread with <paramref name="open"/>, which passes the window what to call, once, with
    /// its result, and waits for it. <paramref name="accepted"/> runs on the prompt thread when the result isn't null.
    /// </summary>
    private static bool TryShow(Action<Action<string?>> open, Action? accepted, out string? answer, CancellationToken cancellationToken)
    {
        answer = null;
        DispatcherQueue? dispatcher = EnsureStarted(cancellationToken);
        if (dispatcher is null)
        {
            return false;
        }

        // A window can complete after shutdown released the worker. Its callback must not signal a disposed wait handle.
        var done = new TaskCompletionSource<(bool Opened, string? Answer)>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool queued = dispatcher.TryEnqueue(() =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                open(typed =>
                {
                    if (typed is not null && !cancellationToken.IsCancellationRequested)
                    {
                        accepted?.Invoke();
                    }

                    done.TrySetResult((Opened: true, Answer: typed));
                });
            }
            catch (Exception)
            {
                // A window that can't open falls back to the classic box rather than losing the snippet.
                done.TrySetResult((Opened: false, Answer: null));
            }
        });

        if (!queued)
        {
            return false;
        }

        (bool opened, string? result) = done.Task.WaitAsync(cancellationToken).GetAwaiter().GetResult();
        answer = result;
        return opened;
    }

    private static DispatcherQueue? EnsureStarted(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (StartLock)
        {
            if (_dispatcher is not null || _unavailable)
            {
                return _dispatcher;
            }

            var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
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
                        ready.TrySetResult(true);
                    });
                }
                catch (Exception)
                {
                    // Without the Windows App SDK runtime, prompts use the classic box.
                }
                finally
                {
                    ready.TrySetResult(true);
                }
            })
            {
                IsBackground = true,
                Name = "PowerToys Text Expander prompt",
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            try
            {
                _ = ready.Task.WaitAsync(TimeSpan.FromMilliseconds(StartTimeoutMs), cancellationToken).GetAwaiter().GetResult();
                _unavailable = _dispatcher is null;
            }
            catch (TimeoutException)
            {
                _unavailable = true;
            }

            return _dispatcher;
        }
    }
}
