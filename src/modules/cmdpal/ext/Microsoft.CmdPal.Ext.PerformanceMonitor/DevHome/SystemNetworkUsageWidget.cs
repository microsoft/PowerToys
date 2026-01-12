// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Nodes;
using CoreWidgetProvider.Helpers;
using CoreWidgetProvider.Widgets.Enums;
using Microsoft.Windows.Widgets.Providers;

namespace CoreWidgetProvider.Widgets;

internal sealed partial class SystemNetworkUsageWidget : CoreWidget, IDisposable
{
    private DataManager _dataManager;

    private static Dictionary<string, string> Templates { get; set; } = new();

    private int _networkIndex;

    public SystemNetworkUsageWidget()
        : base()
    {
        _dataManager = new(DataType.Network, UpdateWidget);
    }

    private string SpeedToString(float cpuSpeed)
    {
        _dataManager = new(DataType.Network, UpdateWidget);
        return string.Format(CultureInfo.InvariantCulture, "{0:0.00} GHz", cpuSpeed / 1000);
    }

    private string FloatToPercentString(float value)
    {
        return ((int)(value * 100)).ToString(CultureInfo.InvariantCulture) + "%";
    }

    private string BytesToBitsPerSecString(float value)
    {
        // Bytes to bits
        value *= 8;

        // bits to Kbits
        value /= 1024;
        if (value < 1024)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.0} Kbps", value);
        }

        // Kbits to Mbits
        value /= 1024;
        return string.Format(CultureInfo.InvariantCulture, "{0:0.0} Mbps", value);
    }

    public override void LoadContentData()
    {
        // Log.Debug("Getting network Data");
        try
        {
            var networkData = new JsonObject();

            var currentData = _dataManager.GetNetworkStats();

            var netName = currentData.GetNetworkName(_networkIndex);
            var networkStats = currentData.GetNetworkUsage(_networkIndex);

            networkData.Add("networkUsage", FloatToPercentString(networkStats.Usage));
            networkData.Add("netSent", BytesToBitsPerSecString(networkStats.Sent));
            networkData.Add("netReceived", BytesToBitsPerSecString(networkStats.Received));
            networkData.Add("networkName", netName);
            networkData.Add("netGraphUrl", currentData.CreateNetImageUrl(_networkIndex));
            networkData.Add("chartHeight", ChartHelper.ChartHeight + "px");
            networkData.Add("chartWidth", ChartHelper.ChartWidth + "px");

            DataState = WidgetDataState.Okay;
            ContentData = networkData.ToJsonString();
        }
        catch (Exception e)
        {
            // Log.Error(e, "Error retrieving data.");
            var content = new JsonObject
            {
                { "errorMessage", e.Message },
            };
            ContentData = content.ToJsonString();
            DataState = WidgetDataState.Failed;
            return;
        }
    }

    public override string GetTemplatePath(WidgetPageState page)
    {
        return page switch
        {
            WidgetPageState.Content => @"Widgets\Templates\SystemNetworkUsageTemplate.json",
            WidgetPageState.Loading => @"Widgets\Templates\SystemNetworkUsageTemplate.json",
            _ => throw new NotImplementedException(),
        };
    }

    public override string GetData(WidgetPageState page)
    {
        return page switch
        {
            WidgetPageState.Content => ContentData,
            WidgetPageState.Loading => EmptyJson,

            // In case of unknown state default to empty data
            _ => EmptyJson,
        };
    }

    private void HandlePrevNetwork(WidgetActionInvokedArgs args)
    {
        _networkIndex = _dataManager.GetNetworkStats().GetPrevNetworkIndex(_networkIndex);
        UpdateWidget();
    }

    private void HandleNextNetwork(WidgetActionInvokedArgs args)
    {
        _networkIndex = _dataManager.GetNetworkStats().GetNextNetworkIndex(_networkIndex);
        UpdateWidget();
    }

    public override void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)
    {
        var verb = GetWidgetActionForVerb(actionInvokedArgs.Verb);

        // Log.Debug($"ActionInvoked: {verb}");
        switch (verb)
        {
            case WidgetAction.PrevItem:
                HandlePrevNetwork(actionInvokedArgs);
                break;

            case WidgetAction.NextItem:
                HandleNextNetwork(actionInvokedArgs);
                break;

            case WidgetAction.Unknown:
                // Log.Error($"Unknown verb: {actionInvokedArgs.Verb}");
                break;
        }
    }

    protected override void SetActive()
    {
        ActivityState = WidgetActivityState.Active;
        Page = WidgetPageState.Content;
        if (ContentData == EmptyJson)
        {
            LoadContentData();
        }

        _dataManager.Start();

        LogCurrentState();
        UpdateWidget();
    }

    protected override void SetInactive()
    {
        _dataManager.Stop();

        ActivityState = WidgetActivityState.Inactive;

        LogCurrentState();
    }

    protected override void SetDeleted()
    {
        _dataManager.Stop();

        SetState(string.Empty);
        ActivityState = WidgetActivityState.Unknown;
        LogCurrentState();
    }

    public void Dispose()
    {
        _dataManager.Dispose();
    }
}
