// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
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

    public event ModelSelectionChangedEventHandler SelectionChanged;

    public event DownloadRequestedEventHandler DownloadRequested;

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
            control.CachedModelsListView.SelectedItem = e.NewValue;
        }
        finally
        {
            control._suppressSelection = false;
        }
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

    private void CachedModelsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelection)
        {
            return;
        }

        try
        {
            _suppressSelection = true;
            var selected = CachedModelsListView.SelectedItem as ModelDetails;
            SetValue(SelectedModelProperty, selected);
            SelectionChanged?.Invoke(this, selected);
        }
        finally
        {
            _suppressSelection = false;
        }
    }

    private void DownloadItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            DownloadRequested?.Invoke(this, button.Tag);
            DownloadModelsFlyout?.Hide();
        }
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

        NoModelsPanel.Visibility = HasCachedModels ? Visibility.Collapsed : Visibility.Visible;
        DownloadSection.Visibility = HasDownloadableModels ? Visibility.Visible : Visibility.Collapsed;
        StatusTextBlock.Text = string.IsNullOrWhiteSpace(StatusText) ? string.Empty : StatusText;

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
