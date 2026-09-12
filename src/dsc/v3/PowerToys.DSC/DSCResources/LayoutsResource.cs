// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using ManagedCommon;
using PowerToys.DSC.Models;
using PowerToys.DSC.Models.FunctionData;
using PowerToys.DSC.Properties;

namespace PowerToys.DSC.DSCResources;

/// <summary>
/// Represents the DSC resource for managing the FancyZones layouts: custom
/// layouts, layout templates, layout hotkeys and default layouts. Each section
/// of the desired state replaces the whole layout file it maps to; sections
/// that are omitted are left unchanged.
/// </summary>
public sealed class LayoutsResource : BaseResource
{
    private static readonly CompositeFormat FailedToWriteManifests = CompositeFormat.Parse(Resources.FailedToWriteManifests);
    private static readonly CompositeFormat InvalidLayoutsError = CompositeFormat.Parse(Resources.InvalidLayoutsError);
    private static readonly CompositeFormat LayoutsWarning = CompositeFormat.Parse(Resources.LayoutsWarning);

    public const string ResourceName = "layouts";

    public LayoutsResource(string? module)
        : base(ResourceName, module)
    {
    }

    /// <inheritdoc/>
    public override bool ExportState(string? input)
    {
        var data = new LayoutsFunctionData();
        data.GetState();
        WriteWarnings(data);
        WriteJsonOutputLine(data.Output.ToJson());
        return true;
    }

    /// <inheritdoc/>
    public override bool GetState(string? input)
    {
        return ExportState(input);
    }

    /// <inheritdoc/>
    public override bool SetState(string? input)
    {
        var data = CreateFunctionDataWithInput(input);
        if (data == null)
        {
            return false;
        }

        data.GetState();
        if (!ValidateInput(data))
        {
            return false;
        }

        // Capture the diff before updating the output
        var diff = data.GetDiffJson();

        // Only call Set if the desired state is different from the current state
        if (!data.TestState())
        {
            data.SetState();

            // Report the canonical form of the applied layouts as the new state
            data.Output.Layouts = data.GetDesiredState();
        }

        WriteJsonOutputLine(data.Output.ToJson());
        WriteJsonOutputLine(diff);
        return true;
    }

    /// <inheritdoc/>
    public override bool TestState(string? input)
    {
        var data = CreateFunctionDataWithInput(input);
        if (data == null)
        {
            return false;
        }

        data.GetState();
        if (!ValidateInput(data))
        {
            return false;
        }

        data.Output.InDesiredState = data.TestState();

        WriteJsonOutputLine(data.Output.ToJson());
        WriteJsonOutputLine(data.GetDiffJson());
        return true;
    }

    /// <inheritdoc/>
    public override bool Schema()
    {
        var data = new LayoutsFunctionData();
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
        var module = string.IsNullOrEmpty(Module) ? nameof(ModuleType.FancyZones) : Module;
        var manifest = GenerateManifest(module);

        if (!string.IsNullOrEmpty(outputDir))
        {
            try
            {
                File.WriteAllText(Path.Combine(outputDir, $"microsoft.powertoys.{module}.layouts.dsc.resource.json"), manifest);
            }
            catch (Exception ex)
            {
                var errorMessage = string.Format(CultureInfo.InvariantCulture, FailedToWriteManifests, outputDir, ex.Message);
                WriteMessageOutputLine(DscMessageLevel.Error, errorMessage);
                return false;
            }
        }
        else
        {
            WriteJsonOutputLine(manifest);
        }

        return true;
    }

    /// <inheritdoc/>
    public override IList<string> GetSupportedModules()
    {
        return [nameof(ModuleType.FancyZones)];
    }

    /// <summary>
    /// Creates the function data from the provided input, writing an error
    /// and returning null when the input is missing or malformed.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>The function data, or null when the input is invalid.</returns>
    private LayoutsFunctionData? CreateFunctionDataWithInput(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            WriteMessageOutputLine(DscMessageLevel.Error, Resources.InputEmptyOrNullError);
            return null;
        }

        try
        {
            return new LayoutsFunctionData(input);
        }
        catch (JsonException ex)
        {
            WriteMessageOutputLine(DscMessageLevel.Error, string.Format(CultureInfo.InvariantCulture, InvalidLayoutsError, ex.Message));
            return null;
        }
    }

    /// <summary>
    /// Validates the input against the current state, surfacing the warnings
    /// collected so far and writing the validation errors, if any.
    /// </summary>
    /// <param name="data">The function data whose state was read.</param>
    /// <returns>True when the input is valid; otherwise false.</returns>
    private bool ValidateInput(LayoutsFunctionData data)
    {
        var errors = data.ValidateInput();
        WriteWarnings(data);

        foreach (var error in errors)
        {
            WriteMessageOutputLine(DscMessageLevel.Error, string.Format(CultureInfo.InvariantCulture, InvalidLayoutsError, error));
        }

        return errors.Count == 0;
    }

    /// <summary>
    /// Surfaces the warnings collected while reading the current state
    /// (entries that could not be loaded and were skipped) and validating
    /// the input through the DSC warning channel.
    /// </summary>
    /// <param name="data">The function data whose state was read.</param>
    private void WriteWarnings(LayoutsFunctionData data)
    {
        foreach (var warning in data.Warnings)
        {
            WriteMessageOutputLine(DscMessageLevel.Warning, string.Format(CultureInfo.InvariantCulture, LayoutsWarning, warning));
        }
    }

    /// <summary>
    /// Generate a DSC resource JSON manifest for the specified module.
    /// </summary>
    /// <param name="module">The name of the module for which to generate the manifest.</param>
    /// <returns>A JSON string representing the DSC resource manifest.</returns>
    private static string GenerateManifest(string module)
    {
        // Note: The description is not localized because the generated
        // manifest file will be part of the package
        return new DscManifest($"{module}Layouts", "0.1.0")
            .AddDescription($"Allows management of the {module} layouts (custom layouts, layout templates, layout hotkeys and default layouts) via the DSC v3 command line interface protocol.")
            .AddStdinMethod("export", ["export", "--module", module, "--resource", ResourceName])
            .AddStdinMethod("get", ["get", "--module", module, "--resource", ResourceName])
            .AddJsonInputMethod("set", "--input", ["set", "--module", module, "--resource", ResourceName], implementsPretest: true, stateAndDiff: true)
            .AddJsonInputMethod("test", "--input", ["test", "--module", module, "--resource", ResourceName], stateAndDiff: true)
            .AddCommandMethod("schema", ["schema", "--module", module, "--resource", ResourceName])
            .ToJson();
    }
}
