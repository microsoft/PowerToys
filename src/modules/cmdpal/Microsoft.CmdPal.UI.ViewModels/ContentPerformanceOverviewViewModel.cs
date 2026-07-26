// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.Input;
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
    WeakReference<IPageContext> context,
    Action<string>? commandInvoker = null) : ContentViewModel(context)
{
    private const int SupportedSchemaVersion = 1;

    private readonly ExtensionObject<IFormContent> _formModel = new(form);
    private readonly Action<string>? _commandInvoker = commandInvoker;

    private string _titleText = string.Empty;
    private string _statusText = string.Empty;
    private string _heroMetric = string.Empty;
    private string _heroLabelText = string.Empty;
    private string _heroValueText = string.Empty;
    private string _cpuLabelText = string.Empty;
    private string _cpuDetailText = string.Empty;
    private int _cpuPercent;
    private string _gpuLabelText = string.Empty;
    private string _gpuAdapterName = string.Empty;
    private bool _canSwitchGpu;
    private string _gpuDetailText = string.Empty;
    private int _gpuPercent;
    private string _memoryLabelText = string.Empty;
    private string _memoryDetailText = string.Empty;
    private int _memoryPercent;
    private string _diskLabelText = string.Empty;
    private string _diskDetailText = string.Empty;
    private int _diskPercent;
    private string _diskReadLabelText = string.Empty;
    private string _diskWriteLabelText = string.Empty;
    private string _diskReadText = string.Empty;
    private string _diskWriteText = string.Empty;
    private int _diskReadPercent;
    private int _diskWritePercent;
    private string _networkLabelText = string.Empty;
    private string _networkAdapterName = string.Empty;
    private bool _canSwitchNetwork;
    private string _networkDetailText = string.Empty;
    private int _networkPercent;
    private string _networkInLabelText = string.Empty;
    private string _networkOutLabelText = string.Empty;
    private string _networkInText = string.Empty;
    private string _networkOutText = string.Empty;
    private int _networkInPercent;
    private int _networkOutPercent;
    private string _previousGpuCommandText = string.Empty;
    private string _nextGpuCommandText = string.Empty;
    private string _previousNetworkCommandText = string.Empty;
    private string _nextNetworkCommandText = string.Empty;

    public string TitleText => _titleText;

    public string StatusText => _statusText;

    public string HeroMetric => _heroMetric;

    public string HeroLabelText => _heroLabelText;

    public string HeroValueText => _heroValueText;

    public string CpuLabelText => _cpuLabelText;

    public string CpuDetailText => _cpuDetailText;

    public int CpuPercent => _cpuPercent;

    public string GpuLabelText => _gpuLabelText;

    public string GpuAdapterName => _gpuAdapterName;

    public bool CanSwitchGpu => _canSwitchGpu;

    public string GpuDetailText => _gpuDetailText;

    public int GpuPercent => _gpuPercent;

    public string MemoryLabelText => _memoryLabelText;

    public string MemoryDetailText => _memoryDetailText;

    public int MemoryPercent => _memoryPercent;

    public string DiskLabelText => _diskLabelText;

    public string DiskDetailText => _diskDetailText;

    public int DiskPercent => _diskPercent;

    public string DiskReadLabelText => _diskReadLabelText;

    public string DiskWriteLabelText => _diskWriteLabelText;

    public string DiskReadText => _diskReadText;

    public string DiskWriteText => _diskWriteText;

    public int DiskReadPercent => _diskReadPercent;

    public int DiskWritePercent => _diskWritePercent;

    public string NetworkLabelText => _networkLabelText;

    public string NetworkAdapterName => _networkAdapterName;

    public bool CanSwitchNetwork => _canSwitchNetwork;

    public string NetworkDetailText => _networkDetailText;

    public int NetworkPercent => _networkPercent;

    public string NetworkInLabelText => _networkInLabelText;

    public string NetworkOutLabelText => _networkOutLabelText;

    public string NetworkInText => _networkInText;

    public string NetworkOutText => _networkOutText;

    public int NetworkInPercent => _networkInPercent;

    public int NetworkOutPercent => _networkOutPercent;

    public string PreviousGpuCommandText => _previousGpuCommandText;

    public string NextGpuCommandText => _nextGpuCommandText;

    public string PreviousNetworkCommandText => _previousNetworkCommandText;

    public string NextNetworkCommandText => _nextNetworkCommandText;

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
        var gpuAdapterName = GetOptionalString(data, "gpuAdapterName");
        var canSwitchGpu = GetOptionalBool(data, "canSwitchGpu");
        var gpuDetailText = GetRequiredString(data, "gpuDetailText");
        var gpuPercent = GetPercent(data, "gpuPercent");
        var memoryLabelText = GetRequiredString(data, "memoryLabelText");
        var memoryDetailText = GetRequiredString(data, "memoryDetailText");
        var memoryPercent = GetPercent(data, "memoryPercent");
        var diskLabelText = GetRequiredString(data, "diskLabelText");
        var diskDetailText = GetRequiredString(data, "diskDetailText");
        var diskPercent = GetPercent(data, "diskPercent");
        var diskReadLabelText = GetOptionalString(data, "diskReadLabelText");
        var diskWriteLabelText = GetOptionalString(data, "diskWriteLabelText");
        var diskReadText = GetOptionalString(data, "diskReadText");
        var diskWriteText = GetOptionalString(data, "diskWriteText");
        var diskReadPercent = GetOptionalPercent(data, "diskReadPercent", diskPercent);
        var diskWritePercent = GetOptionalPercent(data, "diskWritePercent", 0);
        var networkLabelText = GetRequiredString(data, "networkLabelText");
        var networkAdapterName = GetOptionalString(data, "networkAdapterName");
        var canSwitchNetwork = GetOptionalBool(data, "canSwitchNetwork");
        var networkDetailText = GetRequiredString(data, "networkDetailText");
        var networkPercent = GetPercent(data, "networkPercent");
        var networkInLabelText = GetOptionalString(data, "networkInLabelText");
        var networkOutLabelText = GetOptionalString(data, "networkOutLabelText");
        var networkInText = GetOptionalString(data, "networkInText");
        var networkOutText = GetOptionalString(data, "networkOutText");
        var networkInPercent = GetOptionalPercent(data, "networkInPercent", networkPercent);
        var networkOutPercent = GetOptionalPercent(data, "networkOutPercent", 0);
        var previousGpuCommandText = GetOptionalString(data, "previousGpuCommandText");
        var nextGpuCommandText = GetOptionalString(data, "nextGpuCommandText");
        var previousNetworkCommandText = GetOptionalString(data, "previousNetworkCommandText");
        var nextNetworkCommandText = GetOptionalString(data, "nextNetworkCommandText");

        List<string> changedProperties = new(42);

        SetValue(ref _titleText, titleText, nameof(TitleText), changedProperties);
        SetValue(ref _statusText, statusText, nameof(StatusText), changedProperties);
        SetValue(ref _heroMetric, heroMetric, nameof(HeroMetric), changedProperties);
        SetValue(ref _heroLabelText, heroLabelText, nameof(HeroLabelText), changedProperties);
        SetValue(ref _heroValueText, heroValueText, nameof(HeroValueText), changedProperties);

        SetValue(ref _cpuLabelText, cpuLabelText, nameof(CpuLabelText), changedProperties);
        SetValue(ref _cpuDetailText, cpuDetailText, nameof(CpuDetailText), changedProperties);
        SetValue(ref _cpuPercent, cpuPercent, nameof(CpuPercent), changedProperties);

        SetValue(ref _gpuLabelText, gpuLabelText, nameof(GpuLabelText), changedProperties);
        SetValue(ref _gpuAdapterName, gpuAdapterName, nameof(GpuAdapterName), changedProperties);
        SetValue(ref _canSwitchGpu, canSwitchGpu, nameof(CanSwitchGpu), changedProperties);
        SetValue(ref _gpuDetailText, gpuDetailText, nameof(GpuDetailText), changedProperties);
        SetValue(ref _gpuPercent, gpuPercent, nameof(GpuPercent), changedProperties);

        SetValue(ref _memoryLabelText, memoryLabelText, nameof(MemoryLabelText), changedProperties);
        SetValue(ref _memoryDetailText, memoryDetailText, nameof(MemoryDetailText), changedProperties);
        SetValue(ref _memoryPercent, memoryPercent, nameof(MemoryPercent), changedProperties);

        SetValue(ref _diskLabelText, diskLabelText, nameof(DiskLabelText), changedProperties);
        SetValue(ref _diskDetailText, diskDetailText, nameof(DiskDetailText), changedProperties);
        SetValue(ref _diskPercent, diskPercent, nameof(DiskPercent), changedProperties);
        SetValue(ref _diskReadLabelText, string.IsNullOrEmpty(diskReadLabelText) ? diskLabelText : diskReadLabelText, nameof(DiskReadLabelText), changedProperties);
        SetValue(ref _diskWriteLabelText, string.IsNullOrEmpty(diskWriteLabelText) ? diskLabelText : diskWriteLabelText, nameof(DiskWriteLabelText), changedProperties);
        SetValue(ref _diskReadText, string.IsNullOrEmpty(diskReadText) ? diskDetailText : diskReadText, nameof(DiskReadText), changedProperties);
        SetValue(ref _diskWriteText, diskWriteText, nameof(DiskWriteText), changedProperties);
        SetValue(ref _diskReadPercent, diskReadPercent, nameof(DiskReadPercent), changedProperties);
        SetValue(ref _diskWritePercent, diskWritePercent, nameof(DiskWritePercent), changedProperties);

        SetValue(ref _networkLabelText, networkLabelText, nameof(NetworkLabelText), changedProperties);
        SetValue(ref _networkAdapterName, networkAdapterName, nameof(NetworkAdapterName), changedProperties);
        SetValue(ref _canSwitchNetwork, canSwitchNetwork, nameof(CanSwitchNetwork), changedProperties);
        SetValue(ref _networkDetailText, networkDetailText, nameof(NetworkDetailText), changedProperties);
        SetValue(ref _networkPercent, networkPercent, nameof(NetworkPercent), changedProperties);
        SetValue(ref _networkInLabelText, string.IsNullOrEmpty(networkInLabelText) ? networkLabelText : networkInLabelText, nameof(NetworkInLabelText), changedProperties);
        SetValue(ref _networkOutLabelText, string.IsNullOrEmpty(networkOutLabelText) ? networkLabelText : networkOutLabelText, nameof(NetworkOutLabelText), changedProperties);
        SetValue(ref _networkInText, string.IsNullOrEmpty(networkInText) ? networkDetailText : networkInText, nameof(NetworkInText), changedProperties);
        SetValue(ref _networkOutText, networkOutText, nameof(NetworkOutText), changedProperties);
        SetValue(ref _networkInPercent, networkInPercent, nameof(NetworkInPercent), changedProperties);
        SetValue(ref _networkOutPercent, networkOutPercent, nameof(NetworkOutPercent), changedProperties);
        SetValue(ref _previousGpuCommandText, previousGpuCommandText, nameof(PreviousGpuCommandText), changedProperties);
        SetValue(ref _nextGpuCommandText, nextGpuCommandText, nameof(NextGpuCommandText), changedProperties);
        SetValue(ref _previousNetworkCommandText, previousNetworkCommandText, nameof(PreviousNetworkCommandText), changedProperties);
        SetValue(ref _nextNetworkCommandText, nextNetworkCommandText, nameof(NextNetworkCommandText), changedProperties);

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

    private static string GetOptionalString(JsonObject data, string propertyName) =>
        data[propertyName]?.GetValue<string>() ?? string.Empty;

    private static bool GetOptionalBool(JsonObject data, string propertyName) =>
        data[propertyName]?.GetValue<bool>() ?? false;

    private static int GetPercent(JsonObject data, string propertyName) =>
        Math.Clamp(GetRequiredInt(data, propertyName), 0, 100);

    private static int GetOptionalPercent(JsonObject data, string propertyName, int fallback) =>
        data[propertyName] is null
            ? fallback
            : Math.Clamp(data[propertyName]!.GetValue<int>(), 0, 100);

    [RelayCommand]
    private void PreviousGpu() => _commandInvoker?.Invoke(NativePerformanceOverviewCommandIds.PreviousGpu);

    [RelayCommand]
    private void NextGpu() => _commandInvoker?.Invoke(NativePerformanceOverviewCommandIds.NextGpu);

    [RelayCommand]
    private void PreviousNetwork() => _commandInvoker?.Invoke(NativePerformanceOverviewCommandIds.PreviousNetwork);

    [RelayCommand]
    private void NextNetwork() => _commandInvoker?.Invoke(NativePerformanceOverviewCommandIds.NextNetwork);

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
