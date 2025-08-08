// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace PowerToys.DSC.Models;

internal sealed class Manifest
{
    private const string Schema = "https://aka.ms/dsc/schemas/v3/bundled/resource/manifest.vscode.json";
    private const string Executable = "PowerToys.Dsc";
    private readonly string _type;
    private readonly string _version;
    private readonly JsonObject _manifest;

    public Manifest(string type, string version)
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

    public Manifest AddDescription(string description)
    {
        _manifest["description"] = description;
        return this;
    }

    public Manifest AddJsonInputMethod(string method, string inputArg, List<string> args, bool? implementsPretest = null, bool? stateAndDiff = null)
    {
        var argsJson = ToJsonArray(args);
        argsJson.Add(new JsonObject
        {
            ["jsonInputArg"] = inputArg,
            ["mandatory"] = true,
        });
        var methodObject = AddMethod(argsJson, implementsPretest, stateAndDiff);
        _manifest[method] = methodObject;
        return this;
    }

    public Manifest AddStdinMethod(string method, List<string> args, bool? implementsPretest = null, bool? stateAndDiff = null)
    {
        var argsJson = ToJsonArray(args);
        var methodObject = AddMethod(argsJson, implementsPretest, stateAndDiff);
        methodObject["input"] = "stdin";
        _manifest[method] = methodObject;
        return this;
    }

    public Manifest AddCommandMethod(string method, List<string> args)
    {
        _manifest[method] = new JsonObject
        {
            ["command"] = AddMethod(ToJsonArray(args)),
        };
        return this;
    }

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

    public string ToJson()
    {
        return _manifest.ToJsonString(new() { WriteIndented = true });
    }

    private JsonArray ToJsonArray(List<string> args)
    {
        var jsonArray = new JsonArray();
        foreach (var arg in args)
        {
            jsonArray.Add(arg);
        }

        return jsonArray;
    }
}
