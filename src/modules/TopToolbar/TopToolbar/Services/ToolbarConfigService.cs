// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
            _configPath = configPath == string.Empty ? GetDefaultPath() : configPath;
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
        }

        public async Task<ToolbarConfig> LoadAsync()
        {
            if (!File.Exists(_configPath))
            {
                var def = CreateDefault();
                await SaveAsync(def);
                return def;
            }

            await using var stream = File.OpenRead(_configPath);
            var cfg = await JsonSerializer.DeserializeAsync<ToolbarConfig>(stream, JsonOptions);
            return cfg ?? new ToolbarConfig();
        }

        public async Task SaveAsync(ToolbarConfig config)
        {
            await using var stream = File.Create(_configPath);
            await JsonSerializer.SerializeAsync(stream, config, JsonOptions);
        }

        private static string GetDefaultPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "Microsoft", "PowerToys", "TopToolbar");
            return Path.Combine(dir, "toolbar.config.json");
        }

        private static ToolbarConfig CreateDefault()
        {
            return new ToolbarConfig
            {
                Groups =
                {
                    new ButtonGroup
                    {
                        Name = "Quick Access",
                        Buttons =
                        {
                            new ToolbarButton { Name = "Home", IconGlyph = "\uE80F", Action = new ToolbarAction { Command = "explorer.exe", Arguments = "shell:Home" } },
                            new ToolbarButton { Name = "Search", IconGlyph = "\uE721", Action = new ToolbarAction { Command = "explorer.exe", Arguments = "shell:SearchHome" } },
                            new ToolbarButton { Name = "Files", IconGlyph = "\uE8B7", Action = new ToolbarAction { Command = "explorer.exe" } },
                            new ToolbarButton { Name = "Calculator", IconGlyph = "\uE8EF", Action = new ToolbarAction { Command = "calc.exe" } },
                        },
                    },
                    new ButtonGroup
                    {
                        Name = "System",
                        Buttons =
                        {
                            new ToolbarButton { Name = "Display", IconGlyph = "\uE7F4", Action = new ToolbarAction { Command = "ms-settings:display" } },
                            new ToolbarButton { Name = "Sound", IconGlyph = "\uE767", Action = new ToolbarAction { Command = "ms-settings:sound" } },
                            new ToolbarButton { Name = "Network", IconGlyph = "\uE968", Action = new ToolbarAction { Command = "ms-settings:network" } },
                        },
                    },
                },
            };
        }
    }
}
