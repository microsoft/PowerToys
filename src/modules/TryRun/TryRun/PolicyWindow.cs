// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun;

internal sealed class PolicyWindow : Window
{
    private readonly bool linux;
    private readonly bool captureAvailable;
    private readonly Dictionary<string, Func<string>> readers = [];
    private readonly TabControl tabs = new();
    private readonly TextBlock error = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
    private PolicySettings draft;

    internal Dictionary<string, Control> Editors { get; } = [];

    internal PolicySettings? Result { get; private set; }

    internal PolicyWindow(PolicySettings settings, bool linux, bool captureAvailable)
    {
        this.linux = linux;
        this.captureAvailable = captureAvailable;
        draft = settings.Clone();
        Title = "Run permissions — Try Run";
        Width = 920;
        Height = 780;
        MinWidth = 740;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = SystemColors.ControlBrush;
        var root = new DockPanel { Margin = new Thickness(24) };
        Content = root;
        var header = new StackPanel();
        header.Children.Add(new TextBlock { Text = "Run permissions", FontSize = 24, FontWeight = FontWeights.SemiBold });
        header.Children.Add(new TextBlock
        {
            Text = $"Configure this {(linux ? "Linux / WSLC" : "Windows / ProcessContainer")} run. Each option has a default and can be reset. Nothing runs when you apply these settings.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 12),
        });
        var presets = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        presets.Children.Add(Button("Restore defaults", () => Replace(PolicySettings.Defaults(linux))));
        presets.Children.Add(Button("File processing", () =>
        {
            var values = PolicySettings.Defaults(linux);
            values.Values["timeoutMs"] = "300000";
            Replace(values);
        }));
        presets.Children.Add(Button("Network task", () =>
        {
            var values = PolicySettings.Defaults(linux);
            values.Values["allowOutbound"] = "true";
            values.Values["timeoutMs"] = "300000";
            Replace(values);
        }));
        header.Children.Add(presets);
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        var footer = new StackPanel();
        footer.Children.Add(error);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = Button("Cancel", () => DialogResult = false);
        cancel.IsCancel = true;
        actions.Children.Add(cancel);
        actions.Children.Add(Button("Apply permissions", () =>
        {
            try
            {
                Result = ReadSettings();
                DialogResult = true;
            }
            catch (Exception exception)
            {
                error.Text = exception.Message;
            }
        }));
        footer.Children.Add(actions);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        root.Children.Add(tabs);
        Build();
    }

    internal PolicySettings ReadSettings()
    {
        var result = draft.Clone();
        foreach (var (key, read) in readers)
        {
            result.Values[key] = read();
        }

        result.Validate(linux);
        if (result.Enabled("captureEnabled") && !captureAvailable)
        {
            throw new InvalidOperationException("Native MXC access capture is unavailable on this host.");
        }

        return result;
    }

    private void Replace(PolicySettings settings)
    {
        draft = settings;
        Build();
        error.Text = "Preset applied to this draft. Review the options, then Apply permissions.";
    }

    private void Build()
    {
        var selected = tabs.SelectedIndex;
        tabs.Items.Clear();
        readers.Clear();
        Editors.Clear();
        foreach (var group in PolicySettings.Fields.GroupBy(field => field.Group))
        {
            var panel = new StackPanel { Margin = new Thickness(16) };
            tabs.Items.Add(new TabItem { Header = group.Key, Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
            foreach (var field in group)
            {
                var section = new StackPanel { Margin = new Thickness(0, 0, 0, 22) };
                panel.Children.Add(section);
                var heading = new DockPanel();
                var reset = Button("Reset", () =>
                {
                    // Keep all other edits, including nested rules.
                    try
                    {
                        foreach (var (key, read) in readers)
                        {
                            if (key != field.Key)
                            {
                                draft.Values[key] = read();
                            }
                        }

                        draft.Values[field.Key] = field.DefaultFor(linux);
                        Build();
                    }
                    catch (Exception exception)
                    {
                        error.Text = exception.Message;
                    }
                });
                DockPanel.SetDock(reset, Dock.Right);
                heading.Children.Add(reset);
                heading.Children.Add(new TextBlock { Text = field.Title, FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
                section.Children.Add(heading);
                var defaultText = field.DefaultFor(linux);
                section.Children.Add(new TextBlock
                {
                    Text = "Default: " + (defaultText.Length == 0 || defaultText == "[]" ? "None" : defaultText.Replace("\n", " · ", StringComparison.Ordinal)),
                    Opacity = 0.7,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 3, 0, 6),
                });
                Control editor;
                if (field.Kind == "bool")
                {
                    var box = new CheckBox { Content = "Enabled", IsChecked = draft.Enabled(field.Key) };
                    editor = box;
                    readers[field.Key] = () => box.IsChecked == true ? "true" : "false";
                    box.Checked += (_, _) => RefreshAvailability();
                    box.Unchecked += (_, _) => RefreshAvailability();
                }
                else if (field.Kind == "choice")
                {
                    var box = new ComboBox { ItemsSource = field.Choices, SelectedItem = draft.Get(field.Key, linux), MinHeight = 30 };
                    editor = box;
                    readers[field.Key] = () => box.SelectedItem as string ?? string.Empty;
                    box.SelectionChanged += (_, _) => RefreshAvailability();
                }
                else if (field.Kind == "rules")
                {
                    var rules = new NetworkRulesEditor(draft.Rules(field.Key));
                    editor = rules;
                    readers[field.Key] = () => JsonSerializer.Serialize(rules.ReadRules());
                }
                else
                {
                    var multiline = field.Kind is "lines" or "paths";
                    var box = new TextBox
                    {
                        Text = draft.Get(field.Key, linux),
                        MinHeight = multiline ? 74 : 30,
                        MaxHeight = multiline ? 150 : 32,
                        MaxLength = 16384,
                        AcceptsReturn = multiline,
                        VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.NoWrap,
                    };
                    editor = box;
                    readers[field.Key] = () => box.Text;
                }

                Editors[field.Key] = editor;
                AutomationProperties.SetAutomationId(editor, "Policy_" + field.Key);
                AutomationProperties.SetName(editor, field.Title);
                section.Children.Add(editor);
                var supported = field.AppliesTo(linux);
                section.Children.Add(new TextBlock
                {
                    Text = supported ? field.Help : $"Unavailable for {(linux ? "Linux / WSLC" : "Windows / ProcessContainer")}. " + field.Help,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 6, 0, 0),
                    Opacity = 0.8,
                });
                reset.IsEnabled = supported;
            }
        }

        tabs.SelectedIndex = Math.Max(0, selected);
        RefreshAvailability();
    }

    private void RefreshAvailability()
    {
        if (Editors.Count != PolicySettings.Fields.Count)
        {
            return;
        }

        foreach (var field in PolicySettings.Fields)
        {
            Editors[field.Key].IsEnabled = field.AppliesTo(linux);
        }

        var directional = readers["networkMode"]() == "Directional";
        Editors["networkMode"].IsEnabled = !linux;
        foreach (var key in new[] { "egressDefault", "egressAllow", "egressDeny", "ingressDefault", "hostLoopback", "networkProxy", "allowedProxyPeer" })
        {
            Editors[key].IsEnabled = !linux && directional;
        }

        foreach (var key in new[] { "allowOutbound", "allowLocalNetwork", "allowedHosts", "blockedHosts", "proxyKind" })
        {
            Editors[key].IsEnabled &= !directional;
        }

        Editors["proxyPort"].IsEnabled = !linux && !directional && readers["proxyKind"]() == "Host port";
        Editors["proxyUrl"].IsEnabled = !directional && readers["proxyKind"]() == "URL";
        Editors["captureEnabled"].IsEnabled = !linux && captureAvailable;
        foreach (var key in new[] { "captureMode", "captureOutputPath", "retainEtl" })
        {
            Editors[key].IsEnabled = !linux && captureAvailable && readers["captureEnabled"]() == "true";
        }
    }

    internal static Button Button(string label, Action action)
    {
        var button = new Button { Content = label, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0), MinHeight = 32 };
        button.Click += (_, _) => action();
        return button;
    }
}
