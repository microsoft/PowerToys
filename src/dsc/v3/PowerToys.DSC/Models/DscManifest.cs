// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace PowerToys.DSC.Models;

/// <summary>
/// Class for building a DSC manifest for PowerToys resources.
/// </summary>
public sealed class DscManifest
{
    private const string Schema = "https://aka.ms/dsc/schemas/v3/bundled/resource/manifest.vscode.json";
    private const string Executable = @"..\PowerToys.DSC.exe";

    private readonly string _type;
    private readonly string _version;
    private readonly JsonObject _manifest;

    public DscManifest(string type, string version)
    {
        _type = type;
        _version = version;
        _manifest = new JsonObject
        {
            ["$schema"] = Schema,
            ["type"] = $"Microsoft.PowerToys/{_type}",
            ["version"] = _version,
            ["tags"] = new JsonArray("PowerToys"),
        };
    }

    /// <summary>
    /// Adds a description to the manifest.
    /// </summary>
    /// <param name="description">The description to add.</param>
    /// <returns>Returns the current instance of <see cref="DscManifest"/>.</returns>
    public DscManifest AddDescription(string description)
    {
        _manifest["description"] = description;
        return this;
    }

    /// <summary>
    /// Adds a method to the manifest with the specified executable and arguments.
    /// </summary>
    /// <param name="method">The name of the method to add.</param>
    /// <param name="inputArg">The input argument for the method</param>
    /// <param name="args">The list of arguments for the method.</param>
    /// <param name="implementsPretest">Whether the method implements a pretest.</param>
    /// <param name="stateAndDiff">Whether the method returns state and diff.</param>
    /// <returns>Returns the current instance of <see cref="DscManifest"/>.</returns>
    public DscManifest AddJsonInputMethod(string method, string inputArg, List<string> args, bool? implementsPretest = null, bool? stateAndDiff = null)
    {
        var argsJson = CreateJsonArray(args);
        argsJson.Add(new JsonObject
        {
            ["jsonInputArg"] = inputArg,
            ["mandatory"] = true,
        });
        var methodObject = AddMethod(argsJson, implementsPretest, stateAndDiff);
        _manifest[method] = methodObject;
        return this;
    }

    /// <summary>
    /// Adds a method to the manifest that reads from standard input (stdin).
    /// </summary>
    /// <param name="method">The name of the method to add.</param>
    /// <param name="args">The list of arguments for the method.</param>
    /// <param name="implementsPretest">Whether the method implements a pretest.</param>
    /// <param name="stateAndDiff">Whether the method returns state and diff.</param>
    /// <returns>Returns the current instance of <see cref="DscManifest"/>.</returns>
    public DscManifest AddStdinMethod(string method, List<string> args, bool? implementsPretest = null, bool? stateAndDiff = null)
    {
        var argsJson = CreateJsonArray(args);
        var methodObject = AddMethod(argsJson, implementsPretest, stateAndDiff);
        methodObject["input"] = "stdin";
        _manifest[method] = methodObject;
        return this;
    }

    /// <summary>
    /// Adds a command method to the manifest.
    /// </summary>
    /// <param name="method">The name of the method to add.</param>
    /// <param name="args">The list of arguments for the method.</param>
    /// <returns>Returns the current instance of <see cref="DscManifest"/>.</returns>
    public DscManifest AddCommandMethod(string method, List<string> args)
    {
        _manifest[method] = new JsonObject
        {
            ["command"] = AddMethod(CreateJsonArray(args)),
        };
        return this;
    }

    /// <summary>
    /// Gets the JSON representation of the manifest.
    /// </summary>
    /// <returns>Returns the JSON string of the manifest.</returns>
    public string ToJson()
    {
        return _manifest.ToJsonString(new() { WriteIndented = true });
    }

    /// <summary>
    /// Add a method to the manifest with the specified arguments.
    /// </summary>
    /// <param name="args">The list of arguments for the method.</param>
    /// <param name="implementsPretest">Whether the method implements a pretest.</param>
    /// <param name="stateAndDiff">Whether the method returns state and diff.</param>
    /// <returns>Returns the method object.</returns>
    private JsonObject AddMethod(JsonArray args, bool? implementsPretest = null, bool? stateAndDiff = null)
    {
        var methodObject = new JsonObject
        {
            ["executable"] = Executable,
            ["args"] = args,
        };

        if (implementsPretest.HasValue)
        {
            methodObject["implementsPretest"] = implementsPretest.Value;
        }

        if (stateAndDiff.HasValue)
        {
            methodObject["return"] = stateAndDiff.Value ? "stateAndDiff" : "state";
        }

        return methodObject;
    }

    /// <summary>
    /// Creates a JSON array from a list of strings.
    /// </summary>
    /// <param name="args">The list of strings to convert.</param>
    /// <returns>Returns the JSON array.</returns>
    private JsonArray CreateJsonArray(List<string> args)
    {
        var jsonArray = new JsonArray();
        foreach (var arg in args)
        {
            jsonArray.Add(arg);
        }

        return jsonArray;
    }
}
