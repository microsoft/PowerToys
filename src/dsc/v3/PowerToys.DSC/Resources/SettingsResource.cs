// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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
    private static readonly SettingsUtils _settingsUtils = new();
    private readonly Dictionary<ModuleType, ModuleActions> _moduleActions;

    public SettingsResource(ModuleType module)
        : base(module)
    {
        _moduleActions = new Dictionary<ModuleType, ModuleActions>()
        {
            { ModuleType.AdvancedPaste,             CreateModuleActions<AdvancedPasteSettings>() },
            { ModuleType.AlwaysOnTop,               CreateModuleActions<AlwaysOnTopSettings>() },
            { ModuleType.Awake,                     CreateModuleActions<AwakeSettings>() },
            { ModuleType.ColorPicker,               CreateModuleActions<ColorPickerSettings>() },
            { ModuleType.CropAndLock,               CreateModuleActions<CropAndLockSettings>() },
            { ModuleType.EnvironmentVariables,      CreateModuleActions<EnvironmentVariablesSettings>() },
            { ModuleType.FancyZones,                CreateModuleActions<FancyZonesSettings>() },
            { ModuleType.FileLocksmith,             CreateModuleActions<FileLocksmithSettings>() },
            { ModuleType.FindMyMouse,               CreateModuleActions<FindMyMouseSettings>() },
            { ModuleType.Hosts,                     CreateModuleActions<HostsSettings>() },
            { ModuleType.ImageResizer,              CreateModuleActions<ImageResizerSettings>() },
            { ModuleType.KeyboardManager,           CreateModuleActions<KeyboardManagerSettings>() },
            { ModuleType.MouseHighlighter,          CreateModuleActions<MouseHighlighterSettings>() },
            { ModuleType.MouseJump,                 CreateModuleActions<MouseJumpSettings>() },
            { ModuleType.MousePointerCrosshairs,    CreateModuleActions<MousePointerCrosshairsSettings>() },
            { ModuleType.MouseWithoutBorders,       CreateModuleActions<MouseWithoutBordersSettings>() },
            { ModuleType.NewPlus,                   CreateModuleActions<NewPlusSettings>() },
            { ModuleType.Peek,                      CreateModuleActions<PeekSettings>() },
            { ModuleType.PowerRename,               CreateModuleActions<PowerRenameSettings>() },
            { ModuleType.PowerLauncher,             CreateModuleActions<PowerLauncherSettings>() },
            { ModuleType.PowerAccent,               CreateModuleActions<PowerAccentSettings>() },
            { ModuleType.RegistryPreview,           CreateModuleActions<RegistryPreviewSettings>() },
            { ModuleType.MeasureTool,               CreateModuleActions<MeasureToolSettings>() },
            { ModuleType.ShortcutGuide,             CreateModuleActions<ShortcutGuideSettings>() },
            { ModuleType.PowerOCR,                  CreateModuleActions<PowerOcrSettings>() },
            { ModuleType.Workspaces,                CreateModuleActions<WorkspacesSettings>() },
            { ModuleType.ZoomIt,                    CreateModuleActions<ZoomItSettings>() },
        };
    }

    public override bool Export(string? input)
    {
        return Get(input);
    }

    public override bool Get(string? input)
    {
        if (_moduleActions.TryGetValue(Module, out ModuleActions? actions))
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

        if (_moduleActions.TryGetValue(Module, out ModuleActions? actions))
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
        if (_moduleActions.TryGetValue(Module, out ModuleActions? actions))
        {
            WriteJsonOutputLine(actions.Get());
            return true;
        }

        WriteMessageOutputLine(DscMessageLevel.Error, $"Unsupported module type: {Module}");
        return false;
    }

    public override bool Manifest(string? outputDir)
    {
        foreach (var moduleType in _moduleActions.Keys)
        {
            var manifest = GenerateManifest(moduleType);
            WriteJsonOutputLine(manifest);
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

    private string GenerateManifest(ModuleType moduleType)
    {
        var manifest = new JsonObject()
        {
            ["$schema"] = "https://aka.ms/dsc/schemas/v3/bundled/resource/manifest.vscode.json",
            ["description"] = $"Allows management of {moduleType} settings state via the DSC v3 command line interface protocol.",
            ["type"] = "Microsoft.PowerToys/AwakeSettings",
            ["version"] = "0.1.0",
            ["tag"] = new JsonArray("PowerToys"),
            ["export"] = new JsonObject
            {
                ["executable"] = "PowerToys.Dsc.exe",
                ["input"] = "stdin",
                ["args"] = new JsonArray("export", "--module", moduleType.ToString(), "--resource", "settings"),
            },
            ["get"] = new JsonObject
            {
                ["executable"] = "PowerToys.Dsc.exe",
                ["input"] = "stdin",
                ["args"] = new JsonArray("get", "--module", moduleType.ToString(), "--resource", "settings"),
            },
            ["set"] = new JsonObject
            {
                ["executable"] = "PowerToys.Dsc.exe",
                ["implementsPretest"] = false,
                ["return"] = "stateAndDiff",
                ["args"] = new JsonArray("set", "--module", moduleType.ToString(), "--resource", "settings", new JsonObject
                {
                    ["jsonInputArg"] = "--input",
                    ["mandatory"] = true,
                }),
            },
            ["schema"] = new JsonObject
            {
                ["command"] = new JsonObject
                {
                    ["executable"] = "PowerToys.Dsc.exe",
                    ["args"] = new JsonArray("schema", "--module", moduleType.ToString(), "--resource", "settings"),
                },
            },
        };

        return manifest.ToJsonString(new() { WriteIndented = false });
    }
}
