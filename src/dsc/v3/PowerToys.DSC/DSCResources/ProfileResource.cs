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
using PowerToys.DSC.Models.KeyboardManager;
using PowerToys.DSC.Properties;

namespace PowerToys.DSC.DSCResources;

/// <summary>
/// Represents the DSC resource for managing the Keyboard Manager remapping
/// profile (key and shortcut remappings). Applying the resource replaces the
/// whole profile with the desired state.
/// </summary>
public sealed class ProfileResource : BaseResource
{
    private static readonly CompositeFormat FailedToWriteManifests = CompositeFormat.Parse(Resources.FailedToWriteManifests);
    private static readonly CompositeFormat InvalidProfileError = CompositeFormat.Parse(Resources.InvalidProfileError);

    public const string ResourceName = "profile";

    public ProfileResource(string? module)
        : base(ResourceName, module)
    {
    }

    /// <inheritdoc/>
    public override bool ExportState(string? input)
    {
        var data = new ProfileFunctionData();
        data.GetState();
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

        // Capture the diff before updating the output
        var diff = data.GetDiffJson();

        // Only call Set if the desired state is different from the current state
        if (!data.TestState())
        {
            if (!data.SetState())
            {
                WriteMessageOutputLine(DscMessageLevel.Info, Resources.FailedToSignalSettingsEvent);
            }

            // Report the canonical form of the applied profile as the new state
            data.Output.Profile = KbmProfileConverter.Canonicalize(data.Input.Profile);
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
        data.Output.InDesiredState = data.TestState();

        WriteJsonOutputLine(data.Output.ToJson());
        WriteJsonOutputLine(data.GetDiffJson());
        return true;
    }

    /// <inheritdoc/>
    public override bool Schema()
    {
        var data = new ProfileFunctionData();
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
        var module = string.IsNullOrEmpty(Module) ? nameof(ModuleType.KeyboardManager) : Module;
        var manifest = GenerateManifest(module);

        if (!string.IsNullOrEmpty(outputDir))
        {
            try
            {
                File.WriteAllText(Path.Combine(outputDir, $"microsoft.powertoys.{module}.profile.dsc.resource.json"), manifest);
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
        return [nameof(ModuleType.KeyboardManager)];
    }

    /// <summary>
    /// Creates the function data from the provided input, writing an error
    /// and returning null when the input is missing or invalid.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>The function data, or null when the input is invalid.</returns>
    private ProfileFunctionData? CreateFunctionDataWithInput(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            WriteMessageOutputLine(DscMessageLevel.Error, Resources.InputEmptyOrNullError);
            return null;
        }

        ProfileFunctionData data;
        try
        {
            data = new ProfileFunctionData(input);
        }
        catch (JsonException ex)
        {
            WriteMessageOutputLine(DscMessageLevel.Error, string.Format(CultureInfo.InvariantCulture, InvalidProfileError, ex.Message));
            return null;
        }

        var errors = data.ValidateInput();
        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                WriteMessageOutputLine(DscMessageLevel.Error, string.Format(CultureInfo.InvariantCulture, InvalidProfileError, error));
            }

            return null;
        }

        return data;
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
        return new DscManifest($"{module}Profile", "0.1.0")
            .AddDescription($"Allows management of the {module} remapping profile (key and shortcut remappings) via the DSC v3 command line interface protocol.")
            .AddStdinMethod("export", ["export", "--module", module, "--resource", ResourceName])
            .AddStdinMethod("get", ["get", "--module", module, "--resource", ResourceName])
            .AddJsonInputMethod("set", "--input", ["set", "--module", module, "--resource", ResourceName], implementsPretest: true, stateAndDiff: true)
            .AddJsonInputMethod("test", "--input", ["test", "--module", module, "--resource", ResourceName], stateAndDiff: true)
            .AddCommandMethod("schema", ["schema", "--module", module, "--resource", ResourceName])
            .ToJson();
    }
}
