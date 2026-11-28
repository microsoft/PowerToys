// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.Ext.TextExpander.Insertion;

/// <summary>
/// The window that owns the clipboard while a paste borrows it. Windows only lets a clipboard opened with an owner
/// be written after it's emptied, and sends the owner <c>WM_DESTROYCLIPBOARD</c> when the next app copies, so the
/// window lives on a thread of its own that keeps reading messages, as Text Expander's does. It's a message-only
/// window, created the first time a snippet is pasted or copied.
/// </summary>
internal sealed class ClipboardOwnerWindow
{
    private static readonly Lazy<ClipboardOwnerWindow> Shared = new(() => new ClipboardOwnerWindow(), LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly nint _handle;

    private ClipboardOwnerWindow()
    {
        var created = new TaskCompletionSource<nint>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => Run(created))
        {
            IsBackground = true,
            Name = "Text Expander snippets clipboard owner",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        _handle = created.Task.Wait(TimeSpan.FromSeconds(5)) ? created.Task.Result : 0;
    }

    /// <summary>Gets the owner window, or zero if it couldn't be created, in which case nothing is written to the clipboard.</summary>
    public static nint Handle => Shared.Value._handle;

    private static void Run(TaskCompletionSource<nint> created)
    {
        // A STATIC control needs no window class of its own, and HWND_MESSAGE keeps it out of sight and out of
        // broadcasts.
        var handle = NativeMethods.CreateWindowEx(0, "STATIC", null, 0, 0, 0, 0, 0, NativeMethods.HWND_MESSAGE, 0, 0, 0);
        created.TrySetResult(handle);
        if (handle == 0)
        {
            return;
        }

        while (NativeMethods.GetMessage(out var message, 0, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(in message);
            NativeMethods.DispatchMessage(in message);
        }
    }
}
