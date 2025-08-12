// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using PowerToys.DSC.Models;
using PowerToys.DSC.Models.FunctionData;

namespace PowerToys.DSC.Resources;

/// <summary>
/// Represents the DSC resource for managing PowerToys settings.
/// </summary>
internal sealed class SettingsResource : BaseResource
{
    public const string AppModule = "App";
    public const string ResourceName = "settings";

    private readonly Dictionary<string, Func<string?, ISettingsFunctionData>> _moduleFunctionData;

    public string ModuleOrDefault => string.IsNullOrEmpty(Module) ? AppModule : Module;

    public SettingsResource(string? module)
        : base(ResourceName, module)
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

    /// <inheritdoc/>
    public override bool Export(string? input)
    {
        var data = CreateFunctionData();
        data.Get();
        WriteJsonOutputLine(data.Output.ToJson());
        return true;
    }

    /// <inheritdoc/>
    public override bool Get(string? input)
    {
        return Export(input);
    }

    /// <inheritdoc/>
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
        var diff = data.GetDiffJson();

        // Only call Set if the desired state is different from the current state
        if (!data.Test())
        {
            var inputSettings = data.Input.SettingsInternal;
            data.Output.SettingsInternal = inputSettings;
            data.Set();
        }

        WriteJsonOutputLine(data.Output.ToJson());
        WriteJsonOutputLine(diff);
        return true;
    }

    /// <inheritdoc/>
    public override bool Test(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            WriteMessageOutputLine(DscMessageLevel.Error, "Input cannot be null.");
            return false;
        }

        var data = CreateFunctionData(input);
        data.Get();
        data.Output.InDesiredState = data.Test();

        WriteJsonOutputLine(data.Output.ToJson());
        WriteJsonOutputLine(data.GetDiffJson());
        return true;
    }

    /// <inheritdoc/>
    public override bool Schema()
    {
        var data = CreateFunctionData();
        WriteJsonOutputLine(data.Schema());
        return true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// If an output directory is specified, write the manifests to files,
    /// otherwise output them to the console.
    /// </remarks>
    public override bool Manifest(string? outputDir)
    {
        var manifests = GenerateManifests();

        if (!string.IsNullOrEmpty(outputDir))
        {
            try
            {
                foreach (var (name, manifest) in manifests)
                {
                    File.WriteAllText(Path.Combine(outputDir, $"microsoft.powertoys.{name}.settings.dsc.resource.json"), manifest);
                }
            }
            catch (Exception ex)
            {
                WriteMessageOutputLine(DscMessageLevel.Error, $"Failed to write manifests to directory '{outputDir}': {ex.Message}");
                return false;
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

    /// <summary>
    /// Generates manifests for the specified module or all supported modules
    /// if no module is specified.
    /// </summary>
    /// <returns>A list of tuples containing the module name and its corresponding manifest JSON.</returns>
    private List<(string Name, string Manifest)> GenerateManifests()
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

        return manifests;
    }

    /// <summary>
    /// Gemerate a DSC resource JSON manifest for the specified module.
    /// </summary>
    /// <param name="module">The name of the module for which to generate the manifest.</param>
    /// <returns>A JSON string representing the DSC resource manifest.</returns>
    private string GenerateManifest(string module)
    {
        return new DscManifest($"{module}Settings", "0.1.0")
            .AddDescription($"Allows management of {module} settings state via the DSC v3 command line interface protocol.")
            .AddStdinMethod("export", ["export", "--module", module, "--resource", "settings"])
            .AddStdinMethod("get", ["get", "--module", module, "--resource", "settings"])
            .AddJsonInputMethod("set", "--input", ["set", "--module", module, "--resource", "settings"], implementsPretest: true, stateAndDiff: true)
            .AddJsonInputMethod("test", "--input", ["test", "--module", module, "--resource", "settings"], stateAndDiff: true)
            .AddCommandMethod("schema", ["schema", "--module", module, "--resource", "settings"])
            .ToJson();
    }

    /// <inheritdoc/>
    public override IList<string> GetSupportedModules()
    {
        return [.. _moduleFunctionData.Keys.Order()];
    }

    /// <summary>
    /// Creates the function data for the specified module or the default module if none is specified.
    /// </summary>
    /// <param name="input">The input string, if any.</param>
    /// <returns>An instance of <see cref="ISettingsFunctionData"/> for the specified module.</returns>
    public ISettingsFunctionData CreateFunctionData(string? input = null)
    {
        Debug.Assert(_moduleFunctionData.ContainsKey(ModuleOrDefault), "Module should be supported by the resource.");
        return _moduleFunctionData[ModuleOrDefault](input);
    }

    /// <summary>
    /// Creates the function data for a specific settings configuration type.
    /// </summary>
    /// <typeparam name="TSettingsConfig">The type of settings configuration to create function data for.</typeparam>
    /// <param name="input">The input string, if any.</param>
    /// <returns>An instance of <see cref="ISettingsFunctionData"/> for the specified settings configuration type.</returns>
    private ISettingsFunctionData CreateModuleFunctionData<TSettingsConfig>(string? input)
    where TSettingsConfig : ISettingsConfig, new()
    {
        return new SettingsFunctionData<TSettingsConfig>(input);
    }
}
