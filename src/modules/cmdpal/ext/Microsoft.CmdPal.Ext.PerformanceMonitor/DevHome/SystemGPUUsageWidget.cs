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

internal sealed partial class SystemGPUUsageWidget : CoreWidget, IDisposable
{
    private static Dictionary<string, string> Templates { get; set; } = new();

    private readonly DataManager _dataManager;

    private readonly string _gpuActiveEngType = "3D";

    private int _gpuActiveIndex;

    public SystemGPUUsageWidget()
        : base()
    {
        _dataManager = new(DataType.GPU, UpdateWidget);
    }

    private string SpeedToString(float cpuSpeed)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0:0.00} GHz", cpuSpeed / 1000);
    }

    private string FloatToPercentString(float value)
    {
        return ((int)(value * 100)).ToString(CultureInfo.InvariantCulture) + "%";
    }

    public override void LoadContentData()
    {
        // Log.Debug("Getting GPU Data");
        try
        {
            var gpuData = new JsonObject();

            var stats = _dataManager.GetGPUStats();
            var gpuName = stats.GetGPUName(_gpuActiveIndex);

            gpuData.Add("gpuUsage", FloatToPercentString(stats.GetGPUUsage(_gpuActiveIndex, _gpuActiveEngType)));
            gpuData.Add("gpuName", gpuName);
            gpuData.Add("gpuTemp", stats.GetGPUTemperature(_gpuActiveIndex));
            gpuData.Add("gpuGraphUrl", stats.CreateGPUImageUrl(_gpuActiveIndex));
            gpuData.Add("chartHeight", ChartHelper.ChartHeight + "px");
            gpuData.Add("chartWidth", ChartHelper.ChartWidth + "px");

            DataState = WidgetDataState.Okay;
            ContentData = gpuData.ToJsonString();
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
            WidgetPageState.Content => @"Widgets\Templates\SystemGPUUsageTemplate.json",
            WidgetPageState.Loading => @"Widgets\Templates\SystemGPUUsageTemplate.json",
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

    private void HandlePrevGPU(WidgetActionInvokedArgs args)
    {
        _gpuActiveIndex = _dataManager.GetGPUStats().GetPrevGPUIndex(_gpuActiveIndex);
        UpdateWidget();
    }

    private void HandleNextGPU(WidgetActionInvokedArgs args)
    {
        _gpuActiveIndex = _dataManager.GetGPUStats().GetNextGPUIndex(_gpuActiveIndex);
        UpdateWidget();
    }

    public override void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)
    {
        var verb = GetWidgetActionForVerb(actionInvokedArgs.Verb);

        // Log.Debug($"ActionInvoked: {verb}");
        switch (verb)
        {
            case WidgetAction.PrevItem:
                HandlePrevGPU(actionInvokedArgs);
                break;

            case WidgetAction.NextItem:
                HandleNextGPU(actionInvokedArgs);
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
