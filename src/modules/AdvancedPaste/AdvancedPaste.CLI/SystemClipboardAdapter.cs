// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Forms;
using AdvancedPaste.Core;

namespace AdvancedPaste.Cli;

internal sealed class SystemClipboardAdapter : IClipboardAdapter
{
    public string ReadText(HeadlessTransformFormat format)
        => RunOnSta(() =>
        {
            if (format == HeadlessTransformFormat.Markdown && Clipboard.ContainsText(TextDataFormat.Html))
            {
                return Clipboard.GetText(TextDataFormat.Html);
            }

            return Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
        });

    public void WriteText(string text)
        => RunOnSta(() =>
        {
            Clipboard.SetText(text);
            return true;
        });

    // Async entry points can resume on an MTA thread; clipboard calls always need STA.
    internal static T RunOnSta<T>(Func<T> operation)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return operation();
        }

        T? result = default;
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = operation();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
        return result!;
    }
}
