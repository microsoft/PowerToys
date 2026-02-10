// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CmdPal.Core.Common;
using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CmdPal.Ext.Shell;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation.Collections;
using Windows.Win32.Foundation;

namespace Microsoft.CmdPal.Ext.Run;

internal static class RunDialogHelpers
{
#pragma warning disable SA1310 // Field names should not contain underscore
    private static readonly HRESULT E_WAITTIMEOUT = new(-2147024638); // 0x80070102
#pragma warning restore SA1310 // Field names should not contain underscore

    public static ListItem? CreateListItemForCommandString(
        string query,
        IRunHistoryService nativeService,
        ITelemetryService? telemetryService)
    {
        var itemTimer = new Stopwatch();
        itemTimer.Start();

        var searchText = query.Trim();
        var expanded = Environment.ExpandEnvironmentVariables(searchText);
        searchText = expanded;
        if (string.IsNullOrEmpty(searchText) || string.IsNullOrWhiteSpace(searchText))
        {
            return null;
        }

        ListItem? li = null;

        ParseCommandlineResult? parseResult = null;
        var timedOut = false;

        try
        {
            // Use Task.Run with timeout - this will actually timeout even if the sync operations don't respond to cancellation
            var pathResolutionTask = Task.Run(
                () => { parseResult = nativeService.ParseCommandline(searchText, string.Empty); });

            // Wait for either completion or timeout
            timedOut = !pathResolutionTask.Wait(TimeSpan.FromMilliseconds(200));
        }
        catch (TimeoutException)
        {
            // Timeout occurred
            timedOut = true;
        }
        catch (OperationCanceledException)
        {
            // Timeout occurred (from Wait())
            timedOut = true;
        }

        var parseTime = itemTimer.ElapsedMilliseconds;

        // If we did complete the parse...
        if (parseResult is ParseCommandlineResult res)
        {
            if (res.Result != 0)
            {
                // ... and the result wasn't S_OK? Then this text isn't
                // something we can understand as a commandline.
                CoreLogger.LogDebug($"ParseCommandline failed for '{searchText}' with HRESULT: 0x{res.Result:X}");
            }
            else
            {
                // ... and we did decipher the text as a commandline? Great!
                // Turn it into something we can use. We'll make a web command,
                // or a file item, depending on if the commandline is a URI or
                // not.
                li = CreateListItemForCommandResult(res);
            }
        }
        else
        {
            // Or, we didn't succeed in parsing the commandline due to timeout.
            // In that case, just make a best-effort RunExeItem with the
            // original query.
            var exe = query;
            var args = string.Empty;
            li = new RunExeItem(exe, args, exe, null, null);
        }

        if (li is not null)
        {
            li.TextToSuggest = query;
        }

        itemTimer.Stop();

        telemetryService?.LogEvent("LoadHistoryItem", new PropertySet()
        {
                { "type", li?.GetType().Name ?? "null" },
                { "timedOut", timedOut },
                { "totalMs", itemTimer.ElapsedMilliseconds },
                { "parseMs", parseTime },
                { "isUri", parseResult?.IsUri },
                { "target", parseResult?.FilePath ?? query },
                { "args", parseResult?.Arguments },
                { "parseResult", parseResult?.Result ?? E_WAITTIMEOUT },
        });

        return li;
    }

    internal static ListItem? CreateListItemForCommandResult(ParseCommandlineResult res)
    {
        if (res.Result != 0)
        {
            return null;
        }

        ListItem? li = null;
        if (res.IsUri)
        {
            li = new ListItem();
            li.Command = new OpenUrlWithHistoryCommand(res.FilePath, null, null);
            li.Title = res.FilePath;
        }
        else
        {
            var exe = res.FilePath;
            var args = res.Arguments;
            li = new RunExeItem(exe, args, exe, null, null);
        }

        return li;
    }
}
