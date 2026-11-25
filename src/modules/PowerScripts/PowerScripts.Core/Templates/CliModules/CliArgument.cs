// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Core.Templates.CliModules;

internal sealed record CliArgument(string Script, ScriptTemplateParameter? Parameter = null)
{
    public static CliArgument Literal(params string[] values) =>
        new(values.Length == 1
            ? $"$__powerscriptArguments.Add({Quote(values[0])})"
            : "$__powerscriptArguments.AddRange([string[]]@(" + string.Join(", ", values.Select(Quote)) + "))");

    public static CliArgument Option(string flag, ScriptTemplateParameter parameter) =>
        new($"$__powerscriptArguments.AddRange([string[]]@({Quote(flag)}, {Value(parameter)}))", parameter);

    public static CliArgument Switch(string flag, ScriptTemplateParameter parameter) =>
        parameter.Schema.Type != "boolean"
            ? throw new ArgumentException("A CLI switch requires a boolean parameter.", nameof(parameter))
            : new($"if (${parameter.Name}) {{ $__powerscriptArguments.Add({Quote(flag)}) }}", parameter);

    public static CliArgument InputFiles() =>
        new("foreach ($__powerscriptFile in $Files) { $__powerscriptArguments.Add([IO.Path]::GetFullPath($__powerscriptFile)) }");

    private static string Value(ScriptTemplateParameter parameter) => parameter.Schema.Type switch
    {
        "integer" => $"${parameter.Name}.ToString([Globalization.CultureInfo]::InvariantCulture)",
        "string" => "$" + parameter.Name,
        _ => throw new ArgumentException("A value option requires a string or integer parameter.", nameof(parameter)),
    };

    private static string Quote(string value) => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
}
