// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.UI.Events;
using Microsoft.CommandPalette.Extensions;
using Microsoft.PowerToys.Telemetry;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// TelemetryForwarder is responsible for forwarding telemetry events from the
/// command palette to PowerToys Telemetry.
/// Listens to telemetry-specific messages from the core layer and logs them to PowerToys telemetry.
/// Also implements ITelemetryService for dependency injection in extensions.
/// </summary>
internal sealed class TelemetryForwarder :
    ITelemetryService,
    IRecipient<TelemetryBeginInvokeMessage>,
    IRecipient<TelemetryInvokeResultMessage>,
    IRecipient<TelemetryExtensionInvokedMessage>
{
    public TelemetryForwarder()
    {
        WeakReferenceMessenger.Default.Register<TelemetryBeginInvokeMessage>(this);
        WeakReferenceMessenger.Default.Register<TelemetryInvokeResultMessage>(this);
        WeakReferenceMessenger.Default.Register<TelemetryExtensionInvokedMessage>(this);
    }

    // Message handlers for telemetry events from core layer
    public void Receive(TelemetryBeginInvokeMessage message)
    {
        PowerToysTelemetry.Log.WriteEvent(new BeginInvoke());
    }

    public void Receive(TelemetryInvokeResultMessage message)
    {
        PowerToysTelemetry.Log.WriteEvent(new CmdPalInvokeResult(message.Kind));
    }

    public void Receive(TelemetryExtensionInvokedMessage message)
    {
        PowerToysTelemetry.Log.WriteEvent(new CmdPalExtensionInvoked(
            message.ExtensionId,
            message.CommandId,
            message.CommandName,
            message.Success,
            message.ExecutionTimeMs));

        // Increment session counter for commands executed
        if (App.Current.AppWindow is MainWindow mainWindow)
        {
            mainWindow.IncrementCommandsExecuted();
        }
    }

    // Static method for logging session duration from UI layer
    public static void LogSessionDuration(
        ulong durationMs,
        int commandsExecuted,
        int pagesVisited,
        string dismissalReason,
        int searchQueriesCount,
        int maxNavigationDepth,
        int errorCount)
    {
        PowerToysTelemetry.Log.WriteEvent(new CmdPalSessionDuration(
            durationMs,
            commandsExecuted,
            pagesVisited,
            dismissalReason,
            searchQueriesCount,
            maxNavigationDepth,
            errorCount));
    }

    // ITelemetryService implementation for dependency injection in extensions
    public void LogRunQuery(string query, int resultCount, ulong durationMs)
    {
        PowerToysTelemetry.Log.WriteEvent(new CmdPalRunQuery(query, resultCount, durationMs));
    }

    public void LogRunCommand(string command, bool asAdmin, bool success)
    {
        PowerToysTelemetry.Log.WriteEvent(new CmdPalRunCommand(command, asAdmin, success));
    }

    public void LogOpenUri(string uri, bool isWeb, bool success)
    {
        PowerToysTelemetry.Log.WriteEvent(new CmdPalOpenUri(uri, isWeb, success));
    }
}
