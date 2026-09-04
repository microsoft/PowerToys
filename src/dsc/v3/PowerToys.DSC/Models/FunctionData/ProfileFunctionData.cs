// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.DSC.Models.KeyboardManager;
using PowerToys.DSC.Models.ResourceObjects;

namespace PowerToys.DSC.Models.FunctionData;

/// <summary>
/// Function data for the Keyboard Manager profile DSC resource. Reads and
/// writes the remapping profile file selected by the module's active
/// configuration and signals the Keyboard Manager engine to reload after a
/// change.
/// </summary>
public sealed class ProfileFunctionData : BaseFunctionData
{
    // Named event the Keyboard Manager engine listens on to reload its
    // configuration; see SettingsEventName in KeyboardManagerConstants.h.
    public const string SettingsEventName = "PowerToys_KeyboardManager_Event_Settings";

    private static readonly SettingsUtils _settingsUtils = SettingsUtils.Default;
    private readonly Func<bool> _isProcessElevated;

    // Structural problems with the input JSON (missing or null required
    // members) detected before the model is materialized.
    private readonly IList<string> _inputErrors;

    // The stored profile is serialized without null properties to match the
    // shape written by the C++ editor; the engine's JSON reader throws on
    // null-valued properties, which would make it skip the entry.
    private static readonly JsonSerializerOptions _profileSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Gets the desired state provided as input, if any.
    /// </summary>
    public ProfileResourceObject Input { get; }

    /// <summary>
    /// Gets the current state read from the profile file.
    /// </summary>
    public ProfileResourceObject Output { get; }

    /// <summary>
    /// Gets the warnings collected while reading the current profile.
    /// </summary>
    public IList<string> Warnings { get; } = [];

    public ProfileFunctionData(string? input = null, Func<bool>? isProcessElevated = null)
    {
        _isProcessElevated = isProcessElevated ?? (() =>
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        });
        Output = new();
        Input = new();
        _inputErrors = [];

        if (string.IsNullOrEmpty(input))
        {
            return;
        }

        // Deserialization does not enforce [Required] or non-nullable
        // annotations: "{}" would leave the empty default profile (and erase
        // every remapping on Set) and null members would dereference later.
        // Check the shape of the JSON before materializing the model.
        var node = JsonNode.Parse(input);
        _inputErrors = ValidateInputStructure(node);
        if (_inputErrors.Count == 0)
        {
            Input = node.Deserialize<ProfileResourceObject>() ?? new();
        }
    }

    /// <summary>
    /// Validates the input profile.
    /// </summary>
    /// <returns>The list of validation errors; empty when the input is valid.</returns>
    public IList<string> ValidateInput()
    {
        return _inputErrors.Count > 0 ? _inputErrors : KbmProfileConverter.Validate(Input.Profile);
    }

    /// <summary>
    /// Reads the current profile file into the output state.
    /// </summary>
    public void GetState()
    {
        var profile = _settingsUtils.GetSettingsOrDefault<KeyboardManagerProfile>(
            KeyboardManagerSettings.ModuleName, GetProfileFileName());
        Output.Profile = KbmProfileConverter.FromProfile(profile, Warnings);
    }

    /// <summary>
    /// Writes the desired profile to the profile file and signals the
    /// Keyboard Manager engine to reload. Failing to signal is not an error;
    /// the profile is loaded on the next PowerToys start.
    /// </summary>
    /// <returns>True when the running engine was signaled; otherwise false.</returns>
    public bool SetState()
    {
        if (_isProcessElevated())
        {
            throw new UnauthorizedAccessException("Keyboard Manager profiles must be applied from a non-elevated process.");
        }

        // Ensure the module settings exist so the engine can resolve the
        // active configuration; without it LoadSettings() bails out early.
        if (!_settingsUtils.SettingsExists(KeyboardManagerSettings.ModuleName))
        {
            var settings = new KeyboardManagerSettings();
            _settingsUtils.SaveSettings(settings.ToJsonString(), KeyboardManagerSettings.ModuleName);
        }

        var profile = KbmProfileConverter.ToProfile(Input.Profile);
        var profileJson = JsonSerializer.Serialize(profile, _profileSerializerOptions);
        _settingsUtils.SaveSettings(profileJson, KeyboardManagerSettings.ModuleName, GetProfileFileName());

        return SignalSettingsChangedEvent();
    }

    /// <summary>
    /// Tests whether the desired state matches the current state, comparing
    /// the canonical form of both profiles.
    /// </summary>
    /// <returns>True if the states match; otherwise false.</returns>
    public bool TestState()
    {
        var input = JsonSerializer.SerializeToNode(KbmProfileConverter.Canonicalize(Input.Profile));
        var output = JsonSerializer.SerializeToNode(Output.Profile);
        return JsonNode.DeepEquals(input, output);
    }

    /// <summary>
    /// Gets the difference between the desired and the current state.
    /// </summary>
    /// <returns>A JSON array with the differing property names.</returns>
    public JsonArray GetDiffJson()
    {
        var diff = new JsonArray();
        if (!TestState())
        {
            diff.Add(ProfileResourceObject.ProfileJsonPropertyName);
        }

        return diff;
    }

    /// <summary>
    /// Gets the schema for the profile resource object.
    /// </summary>
    /// <returns>The JSON schema string.</returns>
    public string Schema()
    {
        return GenerateSchema<ProfileResourceObject>();
    }

    /// <summary>
    /// Gets the profile file name selected by the module's active
    /// configuration, e.g. "default.json".
    /// </summary>
    /// <returns>The profile file name.</returns>
    private static string GetProfileFileName()
    {
        var settings = _settingsUtils.GetSettingsOrDefault<KeyboardManagerSettings>(KeyboardManagerSettings.ModuleName);
        var activeConfiguration = settings.Properties?.ActiveConfiguration?.Value;
        return $"{(string.IsNullOrEmpty(activeConfiguration) ? "default" : activeConfiguration)}.json";
    }

    /// <summary>
    /// Checks that the input JSON has the required shape: a "profile" object
    /// whose optional "keys" and "shortcuts" members are arrays of objects.
    /// Type mismatches inside the entries are left to the deserializer.
    /// </summary>
    /// <param name="node">The parsed input JSON.</param>
    /// <returns>The list of structural errors; empty when the shape is valid.</returns>
    private static IList<string> ValidateInputStructure(JsonNode? node)
    {
        var errors = new List<string>();
        if (node is not JsonObject root)
        {
            errors.Add("input must be a JSON object");
            return errors;
        }

        if (!root.TryGetPropertyValue(ProfileResourceObject.ProfileJsonPropertyName, out var profileNode) || profileNode == null)
        {
            errors.Add($"'{ProfileResourceObject.ProfileJsonPropertyName}' is required");
            return errors;
        }

        if (profileNode is not JsonObject profile)
        {
            errors.Add($"'{ProfileResourceObject.ProfileJsonPropertyName}' must be an object");
            return errors;
        }

        foreach (var listName in new[] { "keys", "shortcuts" })
        {
            if (!profile.TryGetPropertyValue(listName, out var listNode))
            {
                continue;
            }

            var context = $"{ProfileResourceObject.ProfileJsonPropertyName}.{listName}";
            if (listNode is not JsonArray list)
            {
                errors.Add($"'{context}' must be an array");
                continue;
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] is not JsonObject)
                {
                    errors.Add($"'{context}[{i.ToString(CultureInfo.InvariantCulture)}]' must be an object");
                }
            }
        }

        return errors;
    }

    /// <summary>
    /// Signals the named event the Keyboard Manager engine listens on so a
    /// running instance reloads the profile immediately. Mirrors the signal
    /// in MappingConfiguration::SaveSettingsToFile. The event is only opened,
    /// never created: when no engine is running there is nothing to signal,
    /// and creating it here would make the signal look successful.
    /// </summary>
    /// <returns>True if a running engine was signaled; otherwise false.</returns>
    private static bool SignalSettingsChangedEvent()
    {
        try
        {
            if (!EventWaitHandle.TryOpenExisting(SettingsEventName, out var settingsEvent))
            {
                return false;
            }

            using (settingsEvent)
            {
                return settingsEvent.Set();
            }
        }
        catch (Exception)
        {
            return false;
        }
    }
}
