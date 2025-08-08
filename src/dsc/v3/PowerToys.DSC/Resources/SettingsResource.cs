// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using PowerToys.DSC.Models;

namespace PowerToys.DSC.Resources;

internal sealed class SettingsResource : BaseResource
{
    public const string AppModule = "App";
    public const string ResourceName = "settings";
    private readonly Dictionary<string, Func<string?, ISettingsFunctionData>> _moduleFunctionData;

    public string ModuleOrDefault => Module ?? AppModule;

    public SettingsResource(string? module)
        : base(module)
    {
        _moduleFunctionData = new()
        {
            { AppModule,                                    CreateModuleFunctionData<GeneralSettings> },
            { nameof(ModuleType.AdvancedPaste),             CreateModuleFunctionData<AdvancedPasteSettings> },
            { nameof(ModuleType.AlwaysOnTop),               CreateModuleFunctionData<AlwaysOnTopSettings> },
            { nameof(ModuleType.Awake),                     CreateModuleFunctionData<AwakeSettings> },
            { nameof(ModuleType.ColorPicker),               CreateModuleFunctionData<ColorPickerSettings> },
            { nameof(ModuleType.CropAndLock),               CreateModuleFunctionData<CropAndLockSettings> },
            { nameof(ModuleType.EnvironmentVariables),      CreateModuleFunctionData<EnvironmentVariablesSettings> },
            { nameof(ModuleType.FancyZones),                CreateModuleFunctionData<FancyZonesSettings> },
            { nameof(ModuleType.FileLocksmith),             CreateModuleFunctionData<FileLocksmithSettings> },
            { nameof(ModuleType.FindMyMouse),               CreateModuleFunctionData<FindMyMouseSettings> },
            { nameof(ModuleType.Hosts),                     CreateModuleFunctionData<HostsSettings> },
            { nameof(ModuleType.ImageResizer),              CreateModuleFunctionData<ImageResizerSettings> },
            { nameof(ModuleType.KeyboardManager),           CreateModuleFunctionData<KeyboardManagerSettings> },
            { nameof(ModuleType.MouseHighlighter),          CreateModuleFunctionData<MouseHighlighterSettings> },
            { nameof(ModuleType.MouseJump),                 CreateModuleFunctionData<MouseJumpSettings> },
            { nameof(ModuleType.MousePointerCrosshairs),    CreateModuleFunctionData<MousePointerCrosshairsSettings> },
            { nameof(ModuleType.MouseWithoutBorders),       CreateModuleFunctionData<MouseWithoutBordersSettings> },
            { nameof(ModuleType.NewPlus),                   CreateModuleFunctionData<NewPlusSettings> },
            { nameof(ModuleType.Peek),                      CreateModuleFunctionData<PeekSettings> },
            { nameof(ModuleType.PowerRename),               CreateModuleFunctionData<PowerRenameSettings> },
            { nameof(ModuleType.PowerLauncher),             CreateModuleFunctionData<PowerLauncherSettings> },
            { nameof(ModuleType.PowerAccent),               CreateModuleFunctionData<PowerAccentSettings> },
            { nameof(ModuleType.RegistryPreview),           CreateModuleFunctionData<RegistryPreviewSettings> },
            { nameof(ModuleType.MeasureTool),               CreateModuleFunctionData<MeasureToolSettings> },
            { nameof(ModuleType.ShortcutGuide),             CreateModuleFunctionData<ShortcutGuideSettings> },
            { nameof(ModuleType.PowerOCR),                  CreateModuleFunctionData<PowerOcrSettings> },
            { nameof(ModuleType.Workspaces),                CreateModuleFunctionData<WorkspacesSettings> },
            { nameof(ModuleType.ZoomIt),                    CreateModuleFunctionData<ZoomItSettings> },
        };
    }

    public override bool Export(string? input)
    {
        var data = CreateFunctionData();
        data.Get();
        this.WriteJsonOutputLine(data.GetOutput().ToJson());
        return true;
    }

    public override bool Get(string? input)
    {
        return this.Export(input);
    }

    public override bool Set(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            WriteMessageOutputLine(DscMessageLevel.Error, "Input cannot be null.");
            return false;
        }

        var data = CreateFunctionData(input);
        data.Get();

        // Capture the diff before updating the output
        var diff = data.DiffJson();

        if (!data.Test())
        {
            var inputSettings = data.GetInput().GetSettings();
            data.GetOutput().SetSettings(inputSettings);
            data.Set();
        }

        WriteJsonOutputLine(data.GetOutput().ToJson());
        WriteJsonOutputLine(diff);
        return true;
    }

    public override bool Test(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            WriteMessageOutputLine(DscMessageLevel.Error, "Input cannot be null.");
            return false;
        }

        var data = CreateFunctionData(input);

        data.Get();
        data.GetOutput().SetInDesiredState(data.Test());

        this.WriteJsonOutputLine(data.GetOutput().ToJson());
        this.WriteJsonOutputLine(data.DiffJson());
        return true;
    }

    public override bool Schema()
    {
        var data = CreateFunctionData();
        WriteJsonOutputLine(data.Schema());
        return true;
    }

    public override bool Manifest(string? outputDir)
    {
        List<(string Name, string Manifest)> manifests = [];
        if (!string.IsNullOrEmpty(Module))
        {
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

    private string GenerateManifest(string module)
    {
        return new Manifest($"{module}Settings", "0.1.0")
            .AddDescription($"Allows management of {module} settings state via the DSC v3 command line interface protocol.")
            .AddStdinMethod("export", ["export", "--module", module, "--resource", "settings"])
            .AddStdinMethod("get", ["get", "--module", module, "--resource", "settings"])
            .AddJsonInputMethod("set", "--input", ["set", "--module", module, "--resource", "settings"], implementsPretest: true, stateAndDiff: true)
            .AddJsonInputMethod("test", "--input", ["test", "--module", module, "--resource", "settings"], implementsPretest: true, stateAndDiff: true)
            .AddCommandMethod("schema", ["schema", "--module", module, "--resource", "settings"])
            .ToJson();
    }

    public override IList<string> GetSupportedModules()
    {
        return [.. _moduleFunctionData.Keys.Order()];
    }

    public ISettingsFunctionData CreateFunctionData(string? input = null)
    {
        Debug.Assert(_moduleFunctionData.ContainsKey(ModuleOrDefault), "Module should be supported by the resource.");
        return _moduleFunctionData[ModuleOrDefault](input);
    }

    private ISettingsFunctionData CreateModuleFunctionData<TSettingsConfig>(string? input)
    where TSettingsConfig : ISettingsConfig, new()
    {
        return new SettingsFunctionData<TSettingsConfig>(input);
    }
}
