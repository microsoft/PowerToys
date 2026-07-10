// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    /// <summary>
    /// A read-only projection of a single script parameter as emitted by
    /// <c>PowerScripts.Host.exe list --json</c> (mirrors <c>ScriptParameter</c> in the manifest model).
    /// Shown under each script in the Settings list so users can see, at a glance, what a
    /// community-authored script asks for before they run it.
    /// </summary>
    public sealed class PowerScriptParameterItem
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>One of: "string", "int", "bool", "choice".</summary>
        public string Type { get; set; } = "string";

        public string Label { get; set; }

        public string Description { get; set; }

        public string Default { get; set; }

        public List<string> Options { get; set; } = new();

        public int? Min { get; set; }

        public int? Max { get; set; }

        /// <summary>The label to show (explicit label if set, otherwise the parameter name).</summary>
        public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? Name : Label;

        /// <summary>True when the parameter has help text worth showing.</summary>
        public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

        /// <summary>Friendly, human-readable type name.</summary>
        public string TypeDisplay => (Type ?? string.Empty).ToLowerInvariant() switch
        {
            "choice" => "Choice",
            "bool" => "On/off",
            "int" => "Number",
            _ => "Text",
        };

        /// <summary>
        /// A compact one-line summary of the parameter's type plus its options, range and default,
        /// e.g. "Choice · one of: Small, Medium, Large · default: Medium".
        /// </summary>
        public string TypeDetailDisplay
        {
            get
            {
                var parts = new List<string> { TypeDisplay };

                if (Options is { Count: > 0 })
                {
                    parts.Add("one of: " + string.Join(", ", Options));
                }

                if (Min.HasValue || Max.HasValue)
                {
                    string min = Min.HasValue ? Min.Value.ToString(CultureInfo.InvariantCulture) : "…";
                    string max = Max.HasValue ? Max.Value.ToString(CultureInfo.InvariantCulture) : "…";
                    parts.Add($"range {min}–{max}");
                }

                if (!string.IsNullOrWhiteSpace(Default))
                {
                    parts.Add("default: " + Default);
                }

                return string.Join("  ·  ", parts);
            }
        }
    }
}
