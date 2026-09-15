// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun;

internal sealed class FileAccessGrantWindow : Window
{
    internal FileAccessGrantWindow(FileReadGrant grant, ExecutionRequest request)
    {
        Title = "Review file access — Try Run";
        Width = 680;
        Height = 540;
        MinWidth = 540;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = SystemColors.ControlBrush;
        var root = new DockPanel { Margin = new Thickness(24) };
        Content = root;
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        var cancel = new Button { Content = "Keep blocked", IsCancel = true, Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(0, 0, 12, 0) };
        actions.Children.Add(cancel);
        ApplyButton = new Button { Content = "Apply and run again", Padding = new Thickness(16, 8, 16, 8) };
        ApplyButton.Click += (_, _) => DialogResult = true;
        actions.Children.Add(ApplyButton);
        DockPanel.SetDock(actions, Dock.Bottom);
        root.Children.Add(actions);
        var content = new StackPanel();
        content.Children.Add(new TextBlock { Text = "Allow reading this file?", FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 16) });
        EvidenceText = new TextBlock
        {
            Text = grant.FromNativeDenial ? "MXC recorded a blocked read of this file." : "You selected this file. MXC has not confirmed a blocked read of it. This is a new permission you are choosing to grant.",
            TextWrapping = TextWrapping.Wrap,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12),
        };
        content.Children.Add(EvidenceText);
        Preview = new TextBox
        {
            Text = $"Previous program: {request.ApplicationPath ?? request.FileRelativePath ?? "Inline script"}\n\nFile:\n{grant.Path}\n\nPermission change:\n+ Read-only access to this file\n\nAll other permissions stay as submitted for the previous run. The file's contents become available to the program under those permissions. No write access or containing-folder grant is added.\n\nThe previous command and arguments will run again in a fresh workspace. Selected inputs are copied again from their source paths. Review or export any previous results before discarding them. Other errors may remain.",
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            BorderThickness = new Thickness(0),
            Background = SystemColors.ControlBrush,
        };
        AutomationProperties.SetName(Preview, "Read-only permission change preview");
        content.Children.Add(Preview);
        content.Children.Add(new TextBlock { Text = "This grant applies to the retry. Your configuration draft is unchanged.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) });
        content.Children.Add(new TextBlock { Text = "Previous command details", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 8) });
        content.Children.Add(new TextBox
        {
            Text = $"Arguments (one per line):\n{string.Join('\n', request.Arguments)}\n\nInline script:\n{request.Script}\n\n{request.Policy!.DescribeNetwork(false)}",
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
        });
        root.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
    }

    internal Button ApplyButton { get; }

    internal TextBox Preview { get; }

    internal TextBlock EvidenceText { get; }
}
