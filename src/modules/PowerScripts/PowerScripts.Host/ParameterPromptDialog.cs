// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Forms;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Host;

/// <summary>
/// A small, dynamically built dialog that collects <see cref="ScriptParameter"/> values before a
/// script runs. It is only shown when the manifest opts in via
/// <see cref="PowerScriptManifest.PromptForParameters"/>, so scripts without parameters (or without
/// the opt-in) behave exactly as before.
///
/// Each parameter renders as the control best suited to its type:
/// choice → dropdown, bool → checkbox, int → numeric up/down, string → text box.
/// </summary>
internal static class ParameterPromptDialog
{
    /// <summary>
    /// Shows the prompt for the given parameters, pre-filled from <paramref name="initialValues"/>.
    /// Returns <c>true</c> and the collected values when the user confirms, or <c>false</c> when the
    /// user cancels (in which case the script must not run).
    /// </summary>
    public static bool TryPrompt(
        PowerScriptManifest manifest,
        IReadOnlyDictionary<string, string?> initialValues,
        out Dictionary<string, string?> values)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        values = new Dictionary<string, string?>(StringComparer.Ordinal);

        using var form = new Form
        {
            Text = string.IsNullOrWhiteSpace(manifest.Name) ? manifest.Id : manifest.Name,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MinimizeBox = false,
            MaximizeBox = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(12),
            ShowInTaskbar = true,
            TopMost = true,
        };

        var layout = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));

        if (!string.IsNullOrWhiteSpace(manifest.Description))
        {
            var header = new Label
            {
                Text = manifest.Description,
                AutoSize = true,
                MaximumSize = new System.Drawing.Size(360, 0),
                Margin = new Padding(3, 3, 3, 10),
            };
            layout.Controls.Add(header, 0, layout.RowCount);
            layout.SetColumnSpan(header, 2);
            layout.RowCount++;
        }

        var controls = new List<(ScriptParameter Param, Control Control)>();
        foreach (var p in manifest.Parameters)
        {
            var label = new Label
            {
                Text = p.DisplayLabel,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 8, 8, 3),
            };

            initialValues.TryGetValue(p.Name, out var initial);
            var control = BuildControl(p, initial);

            layout.Controls.Add(label, 0, layout.RowCount);
            layout.Controls.Add(control, 1, layout.RowCount);
            layout.RowCount++;
            controls.Add((p, control));

            if (!string.IsNullOrWhiteSpace(p.Description))
            {
                var help = new Label
                {
                    Text = p.Description,
                    AutoSize = true,
                    ForeColor = System.Drawing.SystemColors.GrayText,
                    MaximumSize = new System.Drawing.Size(360, 0),
                    Margin = new Padding(3, 0, 3, 8),
                };
                layout.Controls.Add(help, 1, layout.RowCount);
                layout.RowCount++;
            }
        }

        var okButton = new Button { Text = "Run", DialogResult = DialogResult.OK, AutoSize = true };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0, 8, 0, 0),
        };
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(okButton);

        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };
        root.Controls.Add(layout, 0, 0);
        root.Controls.Add(buttons, 0, 1);
        form.Controls.Add(root);
        form.AcceptButton = okButton;
        form.CancelButton = cancelButton;

        if (form.ShowDialog() != DialogResult.OK)
        {
            return false;
        }

        foreach (var (param, control) in controls)
        {
            values[param.Name] = ReadValue(param, control);
        }

        return true;
    }

    private static Control BuildControl(ScriptParameter p, string? initial)
    {
        if (p.IsChoice)
        {
            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 250,
                Anchor = AnchorStyles.Left,
            };
            foreach (var option in p.Options)
            {
                combo.Items.Add(option);
            }

            var selected = initial ?? p.Default;
            if (selected is not null && combo.Items.Contains(selected))
            {
                combo.SelectedItem = selected;
            }
            else if (combo.Items.Count > 0)
            {
                combo.SelectedIndex = 0;
            }

            return combo;
        }

        if (p.IsBool)
        {
            var value = initial ?? p.Default;
            return new CheckBox
            {
                Checked = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase),
                Anchor = AnchorStyles.Left,
                AutoSize = true,
            };
        }

        if (p.IsInt)
        {
            var numeric = new NumericUpDown
            {
                Minimum = p.Min ?? int.MinValue,
                Maximum = p.Max ?? int.MaxValue,
                Width = 250,
                Anchor = AnchorStyles.Left,
            };
            var value = initial ?? p.Default;
            if (int.TryParse(value, out var parsed))
            {
                numeric.Value = Math.Clamp(parsed, numeric.Minimum, numeric.Maximum);
            }

            return numeric;
        }

        return new TextBox
        {
            Text = initial ?? p.Default ?? string.Empty,
            Width = 250,
            Anchor = AnchorStyles.Left,
        };
    }

    private static string ReadValue(ScriptParameter p, Control control) => control switch
    {
        ComboBox combo => combo.SelectedItem?.ToString() ?? string.Empty,
        CheckBox check => check.Checked ? "true" : "false",
        NumericUpDown numeric => ((int)numeric.Value).ToString(System.Globalization.CultureInfo.InvariantCulture),
        TextBox text => text.Text,
        _ => string.Empty,
    };
}
