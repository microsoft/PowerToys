// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
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
    IRecipient<BeginInvokeMessage>,
    IRecipient<CmdPalInvokeResultMessage>,
    IRecipient<FetchItemsMetricsMessage>
{
    public TelemetryForwarder()
    {
        WeakReferenceMessenger.Default.Register<BeginInvokeMessage>(this);
        WeakReferenceMessenger.Default.Register<CmdPalInvokeResultMessage>(this);
        WeakReferenceMessenger.Default.Register<FetchItemsMetricsMessage>(this);
    }

    public void Receive(CmdPalInvokeResultMessage message)
    {
        PowerToysTelemetry.Log.WriteEvent(new CmdPalInvokeResult(message.Kind));
    }

    public void Receive(BeginInvokeMessage message)
    {
        PowerToysTelemetry.Log.WriteEvent(new BeginInvoke());
    }

    public void Receive(FetchItemsMetricsMessage message)
    {
        PowerToysTelemetry.Log.WriteEvent(new FetchItemsMetrics(message.ItemCount, message.GetItemsTime, message.InitializeItemsTime));
    }
}
