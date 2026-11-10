// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerScripts.UI.ViewModels;

namespace PowerScripts.Editor.Controls;

public sealed partial class ScriptCard : UserControl
{
    public static readonly DependencyProperty ScriptProperty = DependencyProperty.Register(
        nameof(Script), typeof(PowerScriptListItem), typeof(ScriptCard), new PropertyMetadata(null, OnScriptChanged));

    public PowerScriptListItem Script
    {
        get => (PowerScriptListItem)GetValue(ScriptProperty);
        set => SetValue(ScriptProperty, value);
    }

    public ScriptCard()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdatePalette();
    }

    public static Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility InvertBoolToVisibility(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    private static void OnScriptChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var card = (ScriptCard)sender;
        card.Bindings.Update();
        card.UpdatePalette();
    }

    private void UpdatePalette()
    {
        var palette = Script?.Io?.Input switch
        {
            "image" => "Magenta",
            "audio" => "Teal",
            "text" => "Purple",
            "files" when Script.Io.Output == "files" => "Green",
            "files" => "Blue",
            _ when string.Equals(Script?.Runtime, "Python", StringComparison.OrdinalIgnoreCase) => "Magenta",
            _ when Script?.Io?.Output == "files" => "Green",
            _ => "Orange",
        };
        VisualStateManager.GoToState(this, palette, false);
    }

    private void Icon_ImageFailed(object sender, ExceptionRoutedEventArgs args)
    {
        if (Script is not null)
        {
            Logger.LogWarning($"Unable to display PowerScript icon '{Script.IconPath}': {args.ErrorMessage}");
            Script.MarkIconUnavailable();
        }
    }
}
