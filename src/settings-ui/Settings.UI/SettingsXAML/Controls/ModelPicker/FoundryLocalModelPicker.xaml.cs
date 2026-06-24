// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using LanguageModelProvider;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls;

public sealed partial class FoundryLocalModelPicker : UserControl
{
    private INotifyCollectionChanged _cachedModelsSubscription;
    private INotifyCollectionChanged _downloadableModelsSubscription;
    private bool _suppressSelection;

    public FoundryLocalModelPicker()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateVisualStates();
    }

    public delegate void ModelSelectionChangedEventHandler(object sender, ModelDetails model);

    public delegate void DownloadRequestedEventHandler(object sender, object payload);

    public delegate void LoadRequestedEventHandler(object sender);

    public event ModelSelectionChangedEventHandler SelectionChanged;

    public event LoadRequestedEventHandler LoadRequested;

    public IEnumerable<ModelDetails> CachedModels
    {
        get => (IEnumerable<ModelDetails>)GetValue(CachedModelsProperty);
        set => SetValue(CachedModelsProperty, value);
    }

    public static readonly DependencyProperty CachedModelsProperty =
        DependencyProperty.Register(nameof(CachedModels), typeof(IEnumerable<ModelDetails>), typeof(FoundryLocalModelPicker), new PropertyMetadata(null, OnCachedModelsChanged));

    public IEnumerable DownloadableModels
    {
        get => (IEnumerable)GetValue(DownloadableModelsProperty);
        set => SetValue(DownloadableModelsProperty, value);
    }

    public static readonly DependencyProperty DownloadableModelsProperty =
        DependencyProperty.Register(nameof(DownloadableModels), typeof(IEnumerable), typeof(FoundryLocalModelPicker), new PropertyMetadata(null, OnDownloadableModelsChanged));

    public ModelDetails SelectedModel
    {
        get => (ModelDetails)GetValue(SelectedModelProperty);
        set => SetValue(SelectedModelProperty, value);
    }

    public static readonly DependencyProperty SelectedModelProperty =
        DependencyProperty.Register(nameof(SelectedModel), typeof(ModelDetails), typeof(FoundryLocalModelPicker), new PropertyMetadata(null, OnSelectedModelChanged));

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(FoundryLocalModelPicker), new PropertyMetadata(false, OnStatePropertyChanged));

    public bool IsAvailable
    {
        get => (bool)GetValue(IsAvailableProperty);
        set => SetValue(IsAvailableProperty, value);
    }

    public static readonly DependencyProperty IsAvailableProperty =
        DependencyProperty.Register(nameof(IsAvailable), typeof(bool), typeof(FoundryLocalModelPicker), new PropertyMetadata(false, OnStatePropertyChanged));

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public static readonly DependencyProperty StatusTextProperty =
        DependencyProperty.Register(nameof(StatusText), typeof(string), typeof(FoundryLocalModelPicker), new PropertyMetadata(string.Empty, OnStatePropertyChanged));

    public bool HasCachedModels => CachedModels?.Any() ?? false;

    public bool HasDownloadableModels => DownloadableModels?.Cast<object>().Any() ?? false;

    public void RequestLoad()
    {
        if (IsLoading)
        {
            // Allow refresh requests to continue even if already loading by cancelling via host.
        }
        else
        {
            IsLoading = true;
        }

        IsAvailable = false;
        StatusText = "Loading Foundry Local status...";
        LoadRequested?.Invoke(this);
    }

    private static void OnCachedModelsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FoundryLocalModelPicker)d;
        control.SubscribeToCachedModels(e.OldValue as IEnumerable<ModelDetails>, e.NewValue as IEnumerable<ModelDetails>);
        control.UpdateVisualStates();
    }

    private static void OnDownloadableModelsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FoundryLocalModelPicker)d;
        control.SubscribeToDownloadableModels(e.OldValue as IEnumerable, e.NewValue as IEnumerable);
        control.UpdateVisualStates();
    }

    private static void OnSelectedModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FoundryLocalModelPicker)d;
        if (control._suppressSelection)
        {
            return;
        }

        try
        {
            control._suppressSelection = true;
            if (control.CachedModelsComboBox is not null)
            {
                control.CachedModelsComboBox.SelectedItem = e.NewValue;
            }
        }
        finally
        {
            control._suppressSelection = false;
        }

        control.UpdateSelectedModelDetails();
    }

    private static void OnStatePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FoundryLocalModelPicker)d;
        control.UpdateVisualStates();
    }

    private void SubscribeToCachedModels(IEnumerable<ModelDetails> oldValue, IEnumerable<ModelDetails> newValue)
    {
        if (_cachedModelsSubscription is not null)
        {
            _cachedModelsSubscription.CollectionChanged -= CachedModels_CollectionChanged;
            _cachedModelsSubscription = null;
        }

        if (newValue is INotifyCollectionChanged observable)
        {
            observable.CollectionChanged += CachedModels_CollectionChanged;
            _cachedModelsSubscription = observable;
        }
    }

    private void SubscribeToDownloadableModels(IEnumerable oldValue, IEnumerable newValue)
    {
        if (_downloadableModelsSubscription is not null)
        {
            _downloadableModelsSubscription.CollectionChanged -= DownloadableModels_CollectionChanged;
            _downloadableModelsSubscription = null;
        }

        if (newValue is INotifyCollectionChanged observable)
        {
            observable.CollectionChanged += DownloadableModels_CollectionChanged;
            _downloadableModelsSubscription = observable;
        }
    }

    private void CachedModels_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateVisualStates();
    }

    private void DownloadableModels_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateVisualStates();
    }

    private void CachedModelsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection)
        {
            return;
        }

        try
        {
            _suppressSelection = true;
            var selected = CachedModelsComboBox.SelectedItem as ModelDetails;
            SetValue(SelectedModelProperty, selected);
            SelectionChanged?.Invoke(this, selected);
        }
        finally
        {
            _suppressSelection = false;
        }

        UpdateSelectedModelDetails();
    }

    private void UpdateSelectedModelDetails()
    {
        if (SelectedModelDetailsPanel is null || SelectedModelDescriptionText is null || SelectedModelTagsPanel is null)
        {
            return;
        }

        if (!HasCachedModels || SelectedModel is not ModelDetails model)
        {
            SelectedModelDetailsPanel.Visibility = Visibility.Collapsed;
            SelectedModelDescriptionText.Text = string.Empty;
            SelectedModelTagsPanel.Children.Clear();
            SelectedModelTagsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        SelectedModelDetailsPanel.Visibility = Visibility.Visible;
        SelectedModelDescriptionText.Text = string.IsNullOrWhiteSpace(model.Description)
            ? "No description provided."
            : model.Description;

        SelectedModelTagsPanel.Children.Clear();

        AddTag(GetModelSizeText(model.Size));
        AddTag(GetLicenseShortText(model.License), model.License);

        foreach (var deviceTag in GetDeviceTags(model.HardwareAccelerators))
        {
            AddTag(deviceTag);
        }

        SelectedModelTagsPanel.Visibility = SelectedModelTagsPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        void AddTag(string text, string tooltip = null)
        {
            if (string.IsNullOrWhiteSpace(text) || SelectedModelTagsPanel is null)
            {
                return;
            }

            Border tag = new();
            if (Resources.TryGetValue("TagBorderStyle", out var borderStyleObj) && borderStyleObj is Style borderStyle)
            {
                tag.Style = borderStyle;
            }

            TextBlock label = new()
            {
                Text = text,
            };

            if (Resources.TryGetValue("TagTextStyle", out var textStyleObj) && textStyleObj is Style textStyle)
            {
                label.Style = textStyle;
            }

            tag.Child = label;

            if (!string.IsNullOrWhiteSpace(tooltip))
            {
                ToolTipService.SetToolTip(tag, new TextBlock
                {
                    Text = tooltip,
                    TextWrapping = TextWrapping.Wrap,
                });
            }

            SelectedModelTagsPanel.Children.Add(tag);
        }
    }

    private void LaunchFoundryModelListButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ProcessStartInfo processInfo = new()
            {
                FileName = "powershell.exe",
                Arguments = "-NoExit -Command \"foundry model list\"",
                UseShellExecute = true,
            };

            Process.Start(processInfo);
            StatusText = "Opening PowerShell and running 'foundry model list'...";
        }
        catch (Exception ex)
        {
            StatusText = $"Unable to start PowerShell. {ex.Message}";
            Debug.WriteLine($"[FoundryLocalModelPicker] Failed to run 'foundry model list': {ex}");
        }
    }

    private void RefreshModelsButton_Click(object sender, RoutedEventArgs e)
    {
        RequestLoad();
    }

    private void UpdateVisualStates()
    {
        LoadingIndicator.IsActive = IsLoading;

        if (IsLoading)
        {
            VisualStateManager.GoToState(this, "ShowLoading", true);
        }
        else if (!IsAvailable)
        {
            VisualStateManager.GoToState(this, "ShowNotAvailable", true);
        }
        else
        {
            VisualStateManager.GoToState(this, "ShowModels", true);
        }

        if (LoadingStatusTextBlock is not null)
        {
            LoadingStatusTextBlock.Text = string.IsNullOrWhiteSpace(StatusText)
                ? "Loading Foundry Local status..."
                : StatusText;
        }

        NoModelsPanel.Visibility = HasCachedModels ? Visibility.Collapsed : Visibility.Visible;
        if (CachedModelsComboBox is not null)
        {
            CachedModelsComboBox.Visibility = HasCachedModels ? Visibility.Visible : Visibility.Collapsed;
            CachedModelsComboBox.IsEnabled = HasCachedModels;
        }

        UpdateSelectedModelDetails();

        Bindings.Update();
    }

    public static string GetModelSizeText(long size)
    {
        if (size <= 0)
        {
            return string.Empty;
        }

        const long kiloByte = 1024;
        const long megaByte = kiloByte * 1024;
        const long gigaByte = megaByte * 1024;

        if (size >= gigaByte)
        {
            return $"{size / (double)gigaByte:0.##} GB";
        }

        if (size >= megaByte)
        {
            return $"{size / (double)megaByte:0.##} MB";
        }

        if (size >= kiloByte)
        {
            return $"{size / (double)kiloByte:0.##} KB";
        }

        return $"{size} B";
    }

    public static Visibility GetModelSizeVisibility(long size)
    {
        return size > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public static IEnumerable<string> GetDeviceTags(IReadOnlyCollection<HardwareAccelerator> accelerators)
    {
        if (accelerators is null || accelerators.Count == 0)
        {
            return Array.Empty<string>();
        }

        HashSet<string> tags = new(StringComparer.OrdinalIgnoreCase);

        foreach (var accelerator in accelerators)
        {
            switch (accelerator)
            {
                case HardwareAccelerator.CPU:
                    tags.Add("CPU");
                    break;
                case HardwareAccelerator.GPU:
                case HardwareAccelerator.DML:
                    tags.Add("GPU");
                    break;
                case HardwareAccelerator.NPU:
                case HardwareAccelerator.QNN:
                    tags.Add("NPU");
                    break;
            }
        }

        return tags.Count > 0 ? tags.ToArray() : Array.Empty<string>();
    }

    public static Visibility GetDeviceVisibility(IReadOnlyCollection<HardwareAccelerator> accelerators)
    {
        return GetDeviceTags(accelerators).Any() ? Visibility.Visible : Visibility.Collapsed;
    }

    public static string GetLicenseShortText(string license)
    {
        if (string.IsNullOrWhiteSpace(license))
        {
            return string.Empty;
        }

        var trimmed = license.Trim();
        int separatorIndex = trimmed.IndexOfAny(['(', '[', ':']);
        if (separatorIndex > 0)
        {
            trimmed = trimmed[..separatorIndex].Trim();
        }

        if (trimmed.Length > 24)
        {
            trimmed = $"{trimmed[..24].TrimEnd()}…";
        }

        return trimmed;
    }

    public static Visibility GetLicenseVisibility(string license)
    {
        return string.IsNullOrWhiteSpace(license) ? Visibility.Collapsed : Visibility.Visible;
    }
}
