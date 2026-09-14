// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using ScreenTranslator.Helpers;

namespace ScreenTranslator.Keyboard;

public sealed class EventMonitor : IDisposable
{
    private const string ShowScreenTranslatorSharedEvent = "Local\\PowerToys_ScreenTranslator_ShowEvent-7f28d8a1-432a-4318-971c-4b5b7b05eb4c";
    private const string TerminateScreenTranslatorSharedEvent = "Local\\PowerToys_ScreenTranslator_TerminateEvent-93c6f4b2-5f6e-4123-b68a-2c49e7b41e98";

    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;

    public EventMonitor()
    {
        _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
    }

    public void Start()
    {
        NativeEventWaiter.WaitForEventLoop(
            ShowScreenTranslatorSharedEvent,
            () =>
            {
                Logger.LogInfo("Received SHOW_SCREEN_TRANSLATOR_SHARED_EVENT from runner.");
                _dispatcherQueue.TryEnqueue(() =>
                {
                    WindowManager.LaunchScreenTranslatorOnEveryScreen();
                });
            },
            _cancellationTokenSource.Token);

        NativeEventWaiter.WaitForEventLoop(
            TerminateScreenTranslatorSharedEvent,
            () =>
            {
                Logger.LogInfo("Received TERMINATE_SCREEN_TRANSLATOR_SHARED_EVENT from runner. Terminating.");
                _dispatcherQueue.TryEnqueue(() =>
                {
                    App.Shutdown();
                });
            },
            _cancellationTokenSource.Token);
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        GC.SuppressFinalize(this);
    }
}
