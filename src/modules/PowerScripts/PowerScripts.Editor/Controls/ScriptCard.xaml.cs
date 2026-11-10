// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerScripts.UI.ViewModels;

namespace PowerScripts.Editor.Controls;

public sealed partial class ScriptCard : UserControl
{
    public static readonly DependencyProperty ScriptProperty = DependencyProperty.Register(
        nameof(Script), typeof(PowerScriptListItem), typeof(ScriptCard), new PropertyMetadata(null, OnScriptChanged));

    private static readonly string[] Palettes = { "Blue", "Purple", "Green", "Orange", "Teal", "Magenta", "Slate" };

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
        // Decorative colors stay stable without encoding script capabilities or trust.
        uint hash = 0;
        foreach (var character in Script?.Id ?? string.Empty)
        {
            hash = unchecked((hash * 31) + character);
        }

        VisualStateManager.GoToState(this, Palettes[hash % (uint)Palettes.Length], false);
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
