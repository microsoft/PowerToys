// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerToys.MacroCommon.Models;

namespace Microsoft.PowerToys.Settings.UI.Views;

public sealed partial class MacroEditDialog : ContentDialog
{
    public MacroEditViewModel ViewModel { get; }

    public MacroEditDialog(MacroEditViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    private DataTemplate? _stepTemplate;

    private DataTemplate GetStepTemplate()
    {
        _stepTemplate ??= StepsList.ItemTemplate;
        return _stepTemplate!;
    }

    private void SubStepsList_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ListView list)
        {
            list.ItemTemplate = GetStepTemplate();
        }
    }

    private void AddStep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string tag)
        {
            StepType type = Enum.Parse<StepType>(tag);
            ViewModel.AddStep(type);
        }
    }

    private void DeleteStep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is MacroStepViewModel step)
        {
            ViewModel.DeleteStep(step);
        }
    }

    private void AddSubStep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is MacroStepViewModel parent)
        {
            ViewModel.AddSubStep(parent, StepType.PressKey);
        }
    }
}
