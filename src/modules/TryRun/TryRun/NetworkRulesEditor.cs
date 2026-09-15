// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun;

internal sealed class NetworkRulesEditor : ContentControl
{
    private readonly StackPanel panel = new();
    private readonly List<Func<PolicyNetworkRule>> readers = [];

    internal NetworkRulesEditor(IEnumerable<PolicyNetworkRule> rules)
    {
        var root = new StackPanel();
        Content = root;
        root.Children.Add(panel);
        root.Children.Add(PolicyWindow.Button("Add rule", () => AddRule(new PolicyNetworkRule())));
        foreach (var rule in rules)
        {
            AddRule(rule);
        }
    }

    internal PolicyNetworkRule[] ReadRules() => readers.Select(read => read()).ToArray();

    private void AddRule(PolicyNetworkRule rule)
    {
        var card = new StackPanel { Margin = new Thickness(8) };
        var border = new Border { BorderBrush = SystemColors.ActiveBorderBrush, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 8), Child = card };
        panel.Children.Add(border);
        var destinations = new List<Func<PolicyNetworkPeer>>();
        var ports = new List<Func<PolicyNetworkPort>>();
        Func<PolicyNetworkRule> readRule = () => new() { Destinations = destinations.Select(read => read()).ToArray(), Ports = ports.Select(read => read()).ToArray() };
        readers.Add(readRule);
        card.Children.Add(PolicyWindow.Button("Remove rule", () =>
        {
            readers.Remove(readRule);
            panel.Children.Remove(border);
        }));
        card.Children.Add(new TextBlock { Text = "Destinations — empty means any", Margin = new Thickness(0, 8, 0, 4) });
        var destinationPanel = new StackPanel();
        card.Children.Add(destinationPanel);
        void AddDestination(PolicyNetworkPeer peer)
        {
            var row = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
            destinationPanel.Children.Add(row);
            var cidr = Field(row, "IP/CIDR", peer.Cidr);
            var except = Field(row, "Except (comma-separated CIDRs)", string.Join(", ", peer.Except));
            Func<PolicyNetworkPeer> read = () => new() { Cidr = cidr.Text, Except = except.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) };
            destinations.Add(read);
            row.Children.Add(PolicyWindow.Button("Remove destination", () =>
            {
                destinations.Remove(read);
                destinationPanel.Children.Remove(row);
            }));
        }

        card.Children.Add(PolicyWindow.Button("Add destination", () => AddDestination(new PolicyNetworkPeer())));
        foreach (var peer in rule.Destinations)
        {
            AddDestination(peer);
        }

        card.Children.Add(new TextBlock { Text = "Ports — empty means any", Margin = new Thickness(0, 8, 0, 4) });
        var portPanel = new StackPanel();
        card.Children.Add(portPanel);
        void AddPort(PolicyNetworkPort port)
        {
            var row = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
            portPanel.Children.Add(row);
            row.Children.Add(new TextBlock { Text = "Protocol" });
            var protocol = new ComboBox { ItemsSource = new[] { "Any", "Tcp", "Udp", "Icmp" }, SelectedItem = port.Protocol, MinHeight = 28 };
            AutomationProperties.SetName(protocol, "Protocol");
            row.Children.Add(protocol);
            var first = Field(row, "First port (empty: any)", port.Port?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            var last = Field(row, "Last port (empty: single port)", port.EndPort?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
            Func<PolicyNetworkPort> read = () => new() { Protocol = (string)protocol.SelectedItem, Port = ParsePort(first.Text), EndPort = ParsePort(last.Text) };
            ports.Add(read);
            row.Children.Add(PolicyWindow.Button("Remove port selector", () =>
            {
                ports.Remove(read);
                portPanel.Children.Remove(row);
            }));
        }

        card.Children.Add(PolicyWindow.Button("Add port selector", () => AddPort(new PolicyNetworkPort())));
        foreach (var port in rule.Ports)
        {
            AddPort(port);
        }
    }

    private static TextBox Field(Panel panel, string label, string value)
    {
        var box = new TextBox { Text = value, MinHeight = 28, Margin = new Thickness(0, 3, 0, 6), MaxLength = 4096 };
        AutomationProperties.SetName(box, label);
        panel.Children.Add(new TextBlock { Text = label });
        panel.Children.Add(box);
        return box;
    }

    private static ushort? ParsePort(string value) => value.Length == 0 ? null : ushort.TryParse(value, out var port) && port != 0 ? port : throw new ArgumentException("Ports must be between 1 and 65535.");
}
