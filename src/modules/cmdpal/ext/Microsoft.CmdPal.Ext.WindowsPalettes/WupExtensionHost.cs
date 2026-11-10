// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;
using Windows.UI.Palettes;
using WinRT;

namespace Microsoft.CmdPal.Ext.WindowsPalettes;

[WinRTRuntimeClassName("Windows.UI.Palettes.PaletteExtensionHost")]
internal sealed partial class WupExtensionHost(Action requestUnload) : IPaletteExtensionHost
{
    private event TypedEventHandler<PaletteExtensionHost, object>? DismissedEvent;

    public IAsyncAction ReportStatusAsync(PaletteStatus status)
    {
        var progress = status.Progress is null
            ? null
            : new ProgressState
            {
                IsIndeterminate = status.Progress.IsIndeterminate,
                ProgressPercent = (uint)Math.Clamp(status.Progress.Progress * 100, 0, 100),
            };
        var text = status.Message ?? string.Empty;
        var state = status.Severity switch
        {
            PaletteSeverity.Success => MessageState.Success,
            PaletteSeverity.Warning => MessageState.Warning,
            PaletteSeverity.Error => MessageState.Error,
            _ => MessageState.Info,
        };

        // Sources (e.g. Windows Search's "still indexing") repeat the same status on many queries, and every
        // session gets a new host. Show each distinct status once and update it in place after that, rather
        // than stacking duplicate messages in Command Palette.
        var host = ExtensionHost.Host;
        if (host is null)
        {
            return Task.CompletedTask.AsAsyncAction();
        }

        StatusMessage message;
        lock (ShownStatuses)
        {
            if (ShownStatuses.TryGetValue((text, state), out var shown))
            {
                shown.Progress = progress;
                return Task.CompletedTask.AsAsyncAction();
            }

            message = new StatusMessage { Message = text, State = state, Progress = progress };
            ShownStatuses[(text, state)] = message;
        }

        return host.ShowStatus(message, StatusContext.Extension);
    }

    private static readonly Dictionary<(string Text, MessageState State), StatusMessage> ShownStatuses = [];

    public IAsyncAction ReportStatusAsync(string message, PaletteSeverity severity) =>
        ReportStatusAsync(new PaletteStatus(message, severity));

    public IAsyncAction ReportStatusAsync(string message, PaletteSeverity severity, PaletteProgressState progress) =>
        ReportStatusAsync(new PaletteStatus(message, severity, progress));

    public IAsyncAction ReportStatusAsync(
        string message,
        PaletteSeverity severity,
        PaletteProgressState progress,
        PaletteItemView item) => ReportStatusAsync(new PaletteStatus(message, severity, progress, item));

    public PaletteSystemInvoker GetSystemInvoker(SystemInvokerKind kind) => new(kind);

    public PaletteSystemInvoker GetSystemInvoker(
        SystemInvokerKind kind,
        IReadOnlyDictionary<string, object> context) => context is null ? new(kind) : new(kind, context);

    public event TypedEventHandler<PaletteExtensionHost, object> Dismissed
    {
        add => DismissedEvent += value;
        remove => DismissedEvent -= value;
    }

    internal void RaiseDismissed()
    {
        var handler = DismissedEvent;
        if (handler is null)
        {
            return;
        }

        var hostPointer = MarshalInspectable<object>.FromManaged(this);
        try
        {
            var projectedHost = MarshalInspectable<PaletteExtensionHost>.FromAbi(hostPointer);
            handler.Invoke(projectedHost, null!);
        }
        catch (Exception ex)
        {
            WupDiagnostics.Write($"Dismissed handler failed: 0x{ex.HResult:X8} {ex.Message}");
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.Release(hostPointer);
        }
    }

    public void RequestUnload() => requestUnload();
}
