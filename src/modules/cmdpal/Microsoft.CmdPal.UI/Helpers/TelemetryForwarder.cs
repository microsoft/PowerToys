// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.Events;
using Microsoft.CmdPal.UI.ViewModels.Messages;
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
    IRecipient<TelemetryExtensionInvokedMessage>,
    IRecipient<TelemetryDockConfigurationMessage>,
    IRecipient<TelemetrySearchResultsMessage>,
    IRecipient<TelemetrySearchResultSelectedMessage>
{
    public TelemetryForwarder()
    {
        WeakReferenceMessenger.Default.Register<TelemetryBeginInvokeMessage>(this);
        WeakReferenceMessenger.Default.Register<TelemetryInvokeResultMessage>(this);
        WeakReferenceMessenger.Default.Register<TelemetryExtensionInvokedMessage>(this);
        WeakReferenceMessenger.Default.Register<TelemetryDockConfigurationMessage>(this);
        WeakReferenceMessenger.Default.Register<TelemetrySearchResultsMessage>(this);
        WeakReferenceMessenger.Default.Register<TelemetrySearchResultSelectedMessage>(this);
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

    public void Receive(TelemetryDockConfigurationMessage message)
    {
        PowerToysTelemetry.Log.WriteEvent(new CmdPalDockConfiguration(
            message.IsDockEnabled,
            message.DockSide,
            message.StartBands,
            message.CenterBands,
            message.EndBands));
    }

    public void Receive(TelemetrySearchResultsMessage message)
    {
        PowerToysTelemetry.Log.WriteEvent(new CmdPalSearchResults(
            message.QueryLength,
            message.ResultCount,
            message.NoResults,
            message.LatencyMs));
    }

    public void Receive(TelemetrySearchResultSelectedMessage message)
    {
        PowerToysTelemetry.Log.WriteEvent(new CmdPalSearchResultSelected(
            message.QueryLength,
            message.SelectedIndex,
            message.SelectedTier.ToString()));
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
        PowerToysTelemetry.Log.WriteEvent(new CmdPalRunQuery(resultCount, durationMs));
    }

    public void LogRunCommand(string command, bool asAdmin, bool success)
    {
        PowerToysTelemetry.Log.WriteEvent(new CmdPalRunCommand(asAdmin, success));
    }

    public void LogOpenUri(string uri, bool isWeb, bool success)
    {
        PowerToysTelemetry.Log.WriteEvent(new CmdPalOpenUri(isWeb, success));
    }

    public void LogEvent(string eventName, IDictionary<string, object>? properties = null)
    {
        if (string.IsNullOrEmpty(eventName))
        {
            throw new ArgumentNullException(nameof(eventName));
        }

        switch (eventName)
        {
            case "BuildListItems_PathResolution":
                PowerToysTelemetry.Log.WriteEvent(new CmdPalRunBuildListPathResolution(
                    GetBool(properties, "withLeadingTilde"),
                    GetBool(properties, "couldResolvePath"),
                    GetBool(properties, "isFile"),
                    GetLong(properties, "durationMs"),
                    GetInt(properties, "result")));
                break;

            case "CreatePathItems_Filtered":
                PowerToysTelemetry.Log.WriteEvent(new CmdPalRunCreatePathItemsFiltered(
                    GetInt(properties, "filteredCount")));
                break;

            case "BuildItemsForDirectory":
                PowerToysTelemetry.Log.WriteEvent(new CmdPalRunBuildItemsForDirectory(
                    GetInt(properties, "fileCount")));
                break;

            case "LoadHistory":
                PowerToysTelemetry.Log.WriteEvent(new CmdPalRunLoadHistory(
                    GetInt(properties, "itemsToLoad"),
                    GetInt(properties, "itemsLoaded"),
                    GetLong(properties, "durationMs")));
                break;

            case "LoadHistoryItem":
                PowerToysTelemetry.Log.WriteEvent(new CmdPalRunLoadHistoryItem(
                    GetBool(properties, "timedOut"),
                    GetLong(properties, "totalMs"),
                    GetLong(properties, "parseMs"),
                    GetBool(properties, "isUri"),
                    GetInt(properties, "parseResult")));
                break;

            default:
                // Only explicitly mapped events with privacy-reviewed payloads are forwarded.
                break;
        }
    }

    private static bool GetBool(IDictionary<string, object>? properties, string key)
    {
        if (properties != null && properties.TryGetValue(key, out var v) && v is bool b)
        {
            return b;
        }

        return false;
    }

    private static int GetInt(IDictionary<string, object>? properties, string key)
    {
        if (properties != null && properties.TryGetValue(key, out var v) && v is not null)
        {
            try
            {
                return Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        return 0;
    }

    private static long GetLong(IDictionary<string, object>? properties, string key)
    {
        if (properties != null && properties.TryGetValue(key, out var v) && v is not null)
        {
            try
            {
                return Convert.ToInt64(v, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        return 0;
    }
}
