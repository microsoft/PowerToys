// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.UI.Events;
using Microsoft.PowerToys.Telemetry;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// TelemetryForwarder is responsible for forwarding telemetry events from the
/// command palette core to PowerToys Telemetry.
/// This allows us to emit telemetry events as messages from the core,
/// and then handle them by logging to our PT telemetry provider.
///
/// We may in the future want to replace this with a more generic "ITelemetryService"
/// or something similar, but this works for now.
/// </summary>
internal sealed class TelemetryForwarder :
    ITelemetryService,
    IRecipient<BeginInvokeMessage>,
    IRecipient<CmdPalInvokeResultMessage>
{
    public TelemetryForwarder()
    {
        WeakReferenceMessenger.Default.Register<BeginInvokeMessage>(this);
        WeakReferenceMessenger.Default.Register<CmdPalInvokeResultMessage>(this);
    }

    public void Receive(CmdPalInvokeResultMessage message)
    {
        PowerToysTelemetry.Log.WriteEvent(new CmdPalInvokeResult(message.Kind));
    }

    public void Receive(BeginInvokeMessage message)
    {
        PowerToysTelemetry.Log.WriteEvent(new BeginInvoke());
    }

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
