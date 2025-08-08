// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using PowerToys.DSC.Models;

namespace PowerToys.DSC.Resources;

internal sealed class SettingsResource : BaseResource
{
    private sealed record ModuleActions(Func<string> Get, Action<string> Set, Func<string> Schema);

    public const string ResourceName = "settings";
    public const string AppModule = "App";
    private static readonly SettingsUtils _settingsUtils = new();
    private readonly Dictionary<string, ModuleActions> _moduleActions;

    public string ModuleOrDefault => Module ?? AppModule;

    public SettingsResource(string? module)
        : base(module)
    {
        _moduleActions = new Dictionary<string, ModuleActions>()
        {
            { AppModule,                                    CreateModuleActions<GeneralSettings>() },
            { nameof(ModuleType.AdvancedPaste),             CreateModuleActions<AdvancedPasteSettings>() },
            { nameof(ModuleType.AlwaysOnTop),               CreateModuleActions<AlwaysOnTopSettings>() },
            { nameof(ModuleType.Awake),                     CreateModuleActions<AwakeSettings>() },
            { nameof(ModuleType.ColorPicker),               CreateModuleActions<ColorPickerSettings>() },
            { nameof(ModuleType.CropAndLock),               CreateModuleActions<CropAndLockSettings>() },
            { nameof(ModuleType.EnvironmentVariables),      CreateModuleActions<EnvironmentVariablesSettings>() },
            { nameof(ModuleType.FancyZones),                CreateModuleActions<FancyZonesSettings>() },
            { nameof(ModuleType.FileLocksmith),             CreateModuleActions<FileLocksmithSettings>() },
            { nameof(ModuleType.FindMyMouse),               CreateModuleActions<FindMyMouseSettings>() },
            { nameof(ModuleType.Hosts),                     CreateModuleActions<HostsSettings>() },
            { nameof(ModuleType.ImageResizer),              CreateModuleActions<ImageResizerSettings>() },
            { nameof(ModuleType.KeyboardManager),           CreateModuleActions<KeyboardManagerSettings>() },
            { nameof(ModuleType.MouseHighlighter),          CreateModuleActions<MouseHighlighterSettings>() },
            { nameof(ModuleType.MouseJump),                 CreateModuleActions<MouseJumpSettings>() },
            { nameof(ModuleType.MousePointerCrosshairs),    CreateModuleActions<MousePointerCrosshairsSettings>() },
            { nameof(ModuleType.MouseWithoutBorders),       CreateModuleActions<MouseWithoutBordersSettings>() },
            { nameof(ModuleType.NewPlus),                   CreateModuleActions<NewPlusSettings>() },
            { nameof(ModuleType.Peek),                      CreateModuleActions<PeekSettings>() },
            { nameof(ModuleType.PowerRename),               CreateModuleActions<PowerRenameSettings>() },
            { nameof(ModuleType.PowerLauncher),             CreateModuleActions<PowerLauncherSettings>() },
            { nameof(ModuleType.PowerAccent),               CreateModuleActions<PowerAccentSettings>() },
            { nameof(ModuleType.RegistryPreview),           CreateModuleActions<RegistryPreviewSettings>() },
            { nameof(ModuleType.MeasureTool),               CreateModuleActions<MeasureToolSettings>() },
            { nameof(ModuleType.ShortcutGuide),             CreateModuleActions<ShortcutGuideSettings>() },
            { nameof(ModuleType.PowerOCR),                  CreateModuleActions<PowerOcrSettings>() },
            { nameof(ModuleType.Workspaces),                CreateModuleActions<WorkspacesSettings>() },
            { nameof(ModuleType.ZoomIt),                    CreateModuleActions<ZoomItSettings>() },
        };
    }

    public override bool Export(string? input)
    {
        return Get(input);
    }

    public override bool Get(string? input)
    {
        if (_moduleActions.TryGetValue(ModuleOrDefault, out ModuleActions? actions))
        {
            WriteJsonOutputLine(actions.Get());
            return true;
        }

        WriteMessageOutputLine(DscMessageLevel.Error, $"Unsupported module type: {Module}");
        return false;
    }

    public override bool Set(string? input)
    {
        if (input == null)
        {
            WriteMessageOutputLine(DscMessageLevel.Error, "Input cannot be null.");
            return false;
        }

        if (_moduleActions.TryGetValue(ModuleOrDefault, out ModuleActions? actions))
        {
            actions.Set(input!);
            return true;
        }

        WriteMessageOutputLine(DscMessageLevel.Error, $"Unsupported module type: {Module}");
        return false;
    }

    public override bool Test(string? input)
    {
        // Not implemented: Use default dsc test behavior
        return true;
    }

    public override bool Schema()
    {
        if (_moduleActions.TryGetValue(ModuleOrDefault, out ModuleActions? actions))
        {
            WriteJsonOutputLine(actions.Schema());
            return true;
        }

        WriteMessageOutputLine(DscMessageLevel.Error, $"Unsupported module type: {Module}");
        return false;
    }

    public override bool Manifest(string? outputDir)
    {
        List<(string Name, string Manifest)> manifests = [];
        if (!string.IsNullOrEmpty(Module))
        {
            if (!_moduleActions.ContainsKey(Module))
            {
                WriteMessageOutputLine(DscMessageLevel.Error, $"Unsupported module type: {Module}");
                return false;
            }

            manifests.Add((Module, GenerateManifest(Module)));
        }
        else
        {
            foreach (var module in GetSupportedModules())
            {
                manifests.Add((module, GenerateManifest(module)));
            }
        }

        if (!string.IsNullOrEmpty(outputDir))
        {
            foreach (var (name, manifest) in manifests)
            {
                File.WriteAllText(Path.Combine(outputDir, $"microsoft.powertoys.{name}.settings.dsc.resource.json"), manifest);
            }
        }
        else
        {
            foreach (var (_, manifest) in manifests)
            {
                WriteJsonOutputLine(manifest);
            }
        }

        return true;
    }

    private static string GetSettings<T>()
        where T : ISettingsConfig, new()
    {
        var settings = new T();
        var result = _settingsUtils.GetSettings<T>(settings.GetModuleName());
        return JsonSerializer.Serialize(result);
    }

    private static void SaveSettings<T>(string input)
        where T : ISettingsConfig, new()
    {
        var settings = new T();
        _settingsUtils.SaveSettings(input, settings.GetModuleName());
    }

    private static ModuleActions CreateModuleActions<TSettings>()
        where TSettings : ISettingsConfig, new()
    {
        return new(GetSettings<TSettings>, SaveSettings<TSettings>, GenerateSchema<SettingsResourceObject<TSettings>>);
    }

    private string GenerateManifest(string module)
    {
        var manifest = new JsonObject()
        {
            ["$schema"] = "https://aka.ms/dsc/schemas/v3/bundled/resource/manifest.vscode.json",
            ["description"] = $"Allows management of {module} settings state via the DSC v3 command line interface protocol.",
            ["type"] = $"Microsoft.PowerToys/{module}Settings",
            ["version"] = "0.1.0",
            ["tags"] = new JsonArray("PowerToys"),
            ["export"] = new JsonObject
            {
                ["executable"] = "PowerToys.Dsc",
                ["input"] = "stdin",
                ["args"] = new JsonArray("export", "--module", module, "--resource", "settings"),
            },
            ["get"] = new JsonObject
            {
                ["executable"] = "PowerToys.Dsc",
                ["input"] = "stdin",
                ["args"] = new JsonArray("get", "--module", module, "--resource", "settings"),
            },
            ["set"] = new JsonObject
            {
                ["executable"] = "PowerToys.Dsc",
                ["implementsPretest"] = false,
                ["return"] = "stateAndDiff",
                ["args"] = new JsonArray("set", "--module", module, "--resource", "settings", new JsonObject
                {
                    ["jsonInputArg"] = "--input",
                    ["mandatory"] = true,
                }),
            },
            ["schema"] = new JsonObject
            {
                ["command"] = new JsonObject
                {
                    ["executable"] = "PowerToys.Dsc",
                    ["args"] = new JsonArray("schema", "--module", module, "--resource", "settings"),
                },
            },
        };

        return manifest.ToJsonString(new() { WriteIndented = true });
    }

    public override IList<string> GetSupportedModules()
    {
        return [.. _moduleActions.Keys.Order()];
    }
}
