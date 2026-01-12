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

internal sealed partial class SystemCPUUsageWidget : CoreWidget, IDisposable
{
    private static Dictionary<string, string> Templates { get; set; } = new();

    private readonly DataManager _dataManager;

    public SystemCPUUsageWidget()
        : base()
    {
        _dataManager = new(DataType.CPU, UpdateWidget);
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
        // Log.Debug("Getting CPU stats");
        try
        {
            var cpuData = new JsonObject();

            var currentData = _dataManager.GetCPUStats();

            cpuData.Add("cpuUsage", FloatToPercentString(currentData.CpuUsage));
            cpuData.Add("cpuSpeed", SpeedToString(currentData.CpuSpeed));
            cpuData.Add("cpuGraphUrl", currentData.CreateCPUImageUrl());
            cpuData.Add("chartHeight", ChartHelper.ChartHeight + "px");
            cpuData.Add("chartWidth", ChartHelper.ChartWidth + "px");

            cpuData.Add("cpuProc1", currentData.GetCpuProcessText(0));
            cpuData.Add("cpuProc2", currentData.GetCpuProcessText(1));
            cpuData.Add("cpuProc3", currentData.GetCpuProcessText(2));

            DataState = WidgetDataState.Okay;
            ContentData = cpuData.ToJsonString();
        }
        catch (Exception e)
        {
            // Log.Error(e, "Error retrieving stats.");
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
            WidgetPageState.Content => @"Widgets\Templates\SystemCPUUsageTemplate.json",
            WidgetPageState.Loading => @"Widgets\Templates\SystemCPUUsageTemplate.json",
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

    public override void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)
    {
        var verb = GetWidgetActionForVerb(actionInvokedArgs.Verb);

        // Log.Debug($"ActionInvoked: {verb}");
        var processIndex = -1;
        switch (verb)
        {
            case WidgetAction.CpuKill1:
                processIndex = 0;
                break;

            case WidgetAction.CpuKill2:
                processIndex = 1;
                break;

            case WidgetAction.CpuKill3:
                processIndex = 2;
                break;

            case WidgetAction.Unknown:
                // Log.Error($"Unknown verb: {actionInvokedArgs.Verb}");
                break;
        }

        if (processIndex != -1)
        {
            _dataManager.GetCPUStats().KillTopProcess(processIndex);
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
