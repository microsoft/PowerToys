// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using TopToolbar.Models;

namespace TopToolbar.Services
{
    public class ToolbarConfigService
    {
        private readonly string _configPath;

        public string ConfigPath => _configPath;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            AllowTrailingCommas = true,
        };

        public ToolbarConfigService(string configPath = "")
        {
            _configPath = string.IsNullOrEmpty(configPath) ? GetDefaultPath() : configPath;
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        }

        public async Task<ToolbarConfig> LoadAsync()
        {
            ToolbarConfig config;

            if (!File.Exists(_configPath))
            {
                config = CreateDefault();
                EnsureDefaults(config);
                await SaveAsync(config);
                return config;
            }

            await using var stream = File.OpenRead(_configPath);
            config = await JsonSerializer.DeserializeAsync<ToolbarConfig>(stream, JsonOptions) ?? new ToolbarConfig();
            EnsureDefaults(config);
            return config;
        }

        public async Task SaveAsync(ToolbarConfig config)
        {
            EnsureDefaults(config);
            await using var stream = File.Create(_configPath);
            await JsonSerializer.SerializeAsync(stream, config, JsonOptions);
        }

        private static void EnsureDefaults(ToolbarConfig config)
        {
            config ??= new ToolbarConfig();
            config.Groups ??= new List<ButtonGroup>();
            config.Bindings ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in config.Groups)
            {
                group.Layout ??= new ToolbarGroupLayout();
                group.Providers ??= new ObservableCollection<string>();
                group.StaticActions ??= new ObservableCollection<string>();
                group.Buttons ??= new ObservableCollection<ToolbarButton>();
            }
        }

        private static string GetDefaultPath()
        {
            return AppPaths.ConfigFile;
        }

        private static ToolbarConfig CreateDefault()
        {
            var system = new ButtonGroup
            {
                Name = "System",
                Layout = new ToolbarGroupLayout
                {
                    Style = ToolbarGroupLayoutStyle.Icon,
                    Overflow = ToolbarGroupOverflowMode.Wrap,
                },
            };

            system.Buttons.Add(new ToolbarButton { Name = "Display", IconGlyph = "\uE7F4", Action = new ToolbarAction { Command = "ms-settings:display" } });
            system.Buttons.Add(new ToolbarButton { Name = "Sound", IconGlyph = "\uE767", Action = new ToolbarAction { Command = "ms-settings:sound" } });
            system.Buttons.Add(new ToolbarButton { Name = "Network", IconGlyph = "\uE968", Action = new ToolbarAction { Command = "ms-settings:network" } });

            return new ToolbarConfig
            {
                Groups =
                {
                    system,
                },
                Bindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Alt+1"] = "palette.open",
                    ["Ctrl+Space"] = "palette.open",
                    ["Alt+W"] = "workspace.switcher",
                },
            };
        }
    }
}
