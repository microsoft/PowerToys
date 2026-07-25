// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Observable host-side projection for the in-box performance overview.
/// The instance and its bound native visual tree stay alive while DataJson
/// updates mutate these properties in place.
/// </summary>
public sealed partial class ContentPerformanceOverviewViewModel(
    IFormContent form,
    WeakReference<IPageContext> context) : ContentViewModel(context)
{
    private const int SupportedSchemaVersion = 1;

    private readonly ExtensionObject<IFormContent> _formModel = new(form);

    private string _titleText = string.Empty;
    private string _statusText = string.Empty;
    private string _heroMetric = string.Empty;
    private string _heroLabelText = string.Empty;
    private string _heroValueText = string.Empty;
    private string _cpuLabelText = string.Empty;
    private string _cpuDetailText = string.Empty;
    private int _cpuPercent;
    private string _gpuLabelText = string.Empty;
    private string _gpuDetailText = string.Empty;
    private int _gpuPercent;
    private string _memoryLabelText = string.Empty;
    private string _memoryDetailText = string.Empty;
    private int _memoryPercent;
    private string _diskLabelText = string.Empty;
    private string _diskDetailText = string.Empty;
    private int _diskPercent;
    private string _networkLabelText = string.Empty;
    private string _networkDetailText = string.Empty;
    private int _networkPercent;

    public string TitleText => _titleText;

    public string StatusText => _statusText;

    public string HeroMetric => _heroMetric;

    public string HeroLabelText => _heroLabelText;

    public string HeroValueText => _heroValueText;

    public string CpuLabelText => _cpuLabelText;

    public string CpuDetailText => _cpuDetailText;

    public int CpuPercent => _cpuPercent;

    public string GpuLabelText => _gpuLabelText;

    public string GpuDetailText => _gpuDetailText;

    public int GpuPercent => _gpuPercent;

    public string MemoryLabelText => _memoryLabelText;

    public string MemoryDetailText => _memoryDetailText;

    public int MemoryPercent => _memoryPercent;

    public string DiskLabelText => _diskLabelText;

    public string DiskDetailText => _diskDetailText;

    public int DiskPercent => _diskPercent;

    public string NetworkLabelText => _networkLabelText;

    public string NetworkDetailText => _networkDetailText;

    public int NetworkPercent => _networkPercent;

    public static bool IsPerformanceOverview(IFormContent content) =>
        string.Equals(
            content.TemplateJson,
            NativeFormContentTypes.PerformanceOverview,
            StringComparison.Ordinal);

    public override void InitializeProperties()
    {
        var model = _formModel.Unsafe;
        if (model is null)
        {
            return;
        }

        model.PropChanged += Model_PropChanged;
        ApplyDataJsonIfPresent(model.DataJson);
    }

    private void Model_PropChanged(object sender, IPropChangedEventArgs args)
    {
        if (!string.Equals(args.PropertyName, nameof(IFormContent.DataJson), StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            var model = _formModel.Unsafe;
            if (model is not null)
            {
                ApplyDataJsonIfPresent(model.DataJson);
            }
        }
        catch (Exception ex)
        {
            ShowException(ex);
        }
    }

    private void ApplyDataJsonIfPresent(string dataJson)
    {
        if (!string.IsNullOrWhiteSpace(dataJson))
        {
            ApplyDataJson(dataJson);
        }
    }

    private void ApplyDataJson(string dataJson)
    {
        var data = JsonNode.Parse(dataJson) as JsonObject
            ?? throw new JsonException("The native performance overview payload must be a JSON object.");

        var schemaVersion = GetRequiredInt(data, "schemaVersion");
        if (schemaVersion != SupportedSchemaVersion)
        {
            throw new JsonException($"Unsupported native performance overview schema version: {schemaVersion}.");
        }

        // Read and validate the complete sample before mutating any backing
        // fields so a malformed update cannot leave the observable state
        // partially advanced.
        var titleText = GetRequiredString(data, "titleText");
        var statusText = GetRequiredString(data, "statusText");
        var heroMetric = GetRequiredString(data, "heroMetric");
        var heroLabelText = GetRequiredString(data, "heroLabelText");
        var heroValueText = GetRequiredString(data, "heroValueText");
        var cpuLabelText = GetRequiredString(data, "cpuLabelText");
        var cpuDetailText = GetRequiredString(data, "cpuDetailText");
        var cpuPercent = GetPercent(data, "cpuPercent");
        var gpuLabelText = GetRequiredString(data, "gpuLabelText");
        var gpuDetailText = GetRequiredString(data, "gpuDetailText");
        var gpuPercent = GetPercent(data, "gpuPercent");
        var memoryLabelText = GetRequiredString(data, "memoryLabelText");
        var memoryDetailText = GetRequiredString(data, "memoryDetailText");
        var memoryPercent = GetPercent(data, "memoryPercent");
        var diskLabelText = GetRequiredString(data, "diskLabelText");
        var diskDetailText = GetRequiredString(data, "diskDetailText");
        var diskPercent = GetPercent(data, "diskPercent");
        var networkLabelText = GetRequiredString(data, "networkLabelText");
        var networkDetailText = GetRequiredString(data, "networkDetailText");
        var networkPercent = GetPercent(data, "networkPercent");

        List<string> changedProperties = new(20);

        SetValue(ref _titleText, titleText, nameof(TitleText), changedProperties);
        SetValue(ref _statusText, statusText, nameof(StatusText), changedProperties);
        SetValue(ref _heroMetric, heroMetric, nameof(HeroMetric), changedProperties);
        SetValue(ref _heroLabelText, heroLabelText, nameof(HeroLabelText), changedProperties);
        SetValue(ref _heroValueText, heroValueText, nameof(HeroValueText), changedProperties);

        SetValue(ref _cpuLabelText, cpuLabelText, nameof(CpuLabelText), changedProperties);
        SetValue(ref _cpuDetailText, cpuDetailText, nameof(CpuDetailText), changedProperties);
        SetValue(ref _cpuPercent, cpuPercent, nameof(CpuPercent), changedProperties);

        SetValue(ref _gpuLabelText, gpuLabelText, nameof(GpuLabelText), changedProperties);
        SetValue(ref _gpuDetailText, gpuDetailText, nameof(GpuDetailText), changedProperties);
        SetValue(ref _gpuPercent, gpuPercent, nameof(GpuPercent), changedProperties);

        SetValue(ref _memoryLabelText, memoryLabelText, nameof(MemoryLabelText), changedProperties);
        SetValue(ref _memoryDetailText, memoryDetailText, nameof(MemoryDetailText), changedProperties);
        SetValue(ref _memoryPercent, memoryPercent, nameof(MemoryPercent), changedProperties);

        SetValue(ref _diskLabelText, diskLabelText, nameof(DiskLabelText), changedProperties);
        SetValue(ref _diskDetailText, diskDetailText, nameof(DiskDetailText), changedProperties);
        SetValue(ref _diskPercent, diskPercent, nameof(DiskPercent), changedProperties);

        SetValue(ref _networkLabelText, networkLabelText, nameof(NetworkLabelText), changedProperties);
        SetValue(ref _networkDetailText, networkDetailText, nameof(NetworkDetailText), changedProperties);
        SetValue(ref _networkPercent, networkPercent, nameof(NetworkPercent), changedProperties);

        if (changedProperties.Count > 0)
        {
            UpdateProperty(changedProperties.ToArray());
        }
    }

    private static string GetRequiredString(JsonObject data, string propertyName) =>
        data[propertyName]?.GetValue<string>()
        ?? throw new JsonException($"Missing native performance overview property: {propertyName}.");

    private static int GetRequiredInt(JsonObject data, string propertyName) =>
        data[propertyName]?.GetValue<int>()
        ?? throw new JsonException($"Missing native performance overview property: {propertyName}.");

    private static int GetPercent(JsonObject data, string propertyName) =>
        Math.Clamp(GetRequiredInt(data, propertyName), 0, 100);

    private static void SetValue<T>(
        ref T target,
        T value,
        string propertyName,
        ICollection<string> changedProperties)
    {
        if (!EqualityComparer<T>.Default.Equals(target, value))
        {
            target = value;
            changedProperties.Add(propertyName);
        }
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        var model = _formModel.Unsafe;
        if (model is not null)
        {
            model.PropChanged -= Model_PropChanged;
        }
    }
}
