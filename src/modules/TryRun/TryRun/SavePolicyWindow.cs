// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace PowerToys.TryRun;

internal sealed class SavePolicyWindow : Window
{
    private readonly TextBox nameBox;
    private readonly CheckBox updateBox;
    private readonly TextBlock error;

    public SavePolicyWindow(string? selectedName)
    {
        Title = "Save reusable policy";
        Width = 550;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Save run permissions", FontSize = 22, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Reuse this backend's permissions from Try Run, Command Palette, or the CLI. Programs, arguments, and copied inputs are selected separately for each run. Explicit host paths remain part of this policy.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 16) });
        nameBox = new TextBox { Text = selectedName ?? string.Empty, MaxLength = 80, MinHeight = 32, VerticalContentAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(nameBox, "Policy name");
        panel.Children.Add(new Label { Content = "Policy name", Target = nameBox });
        panel.Children.Add(nameBox);
        updateBox = new CheckBox { Content = "Update the selected policy (creates a new revision)", IsEnabled = selectedName is not null, Margin = new Thickness(0, 14, 0, 0) };
        panel.Children.Add(updateBox);
        error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
        panel.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(0, 0, 8, 0) };
        var save = new Button { Content = "Save policy", IsDefault = true, Padding = new Thickness(16, 8, 16, 8) };
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text))
            {
                error.Text = "Enter a policy name.";
                nameBox.Focus();
                return;
            }

            DialogResult = true;
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        panel.Children.Add(buttons);
        Content = panel;
    }

    public string PolicyName => nameBox.Text.Trim();

    public bool UpdateSelected => updateBox.IsChecked == true;
}
