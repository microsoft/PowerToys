// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Core.Templates.CliModules;

internal abstract class PowerShellCliModule
{
    public abstract string Id { get; }

    public abstract string Name { get; }

    public abstract string Executable { get; }

    public virtual string IconName => "PowerToys";

    public abstract int Order { get; }

    public abstract IReadOnlyList<PowerShellCliTemplate> CreateRecipes();

    protected PowerShellCliTemplate Template(
        string id, string name, string description, string input, string arguments, params ScriptTemplateParameter[] parameters) =>
        CliRecipeBuilder.Template(id, name, description, input, Executable, arguments, parameters);

    protected PowerShellCliTemplate Query(
        string id, string name, string description, string input, params CliArgument[] arguments) =>
        Template(
            id,
            name,
            description,
            input,
            string.Join("\n", arguments.Select(argument => argument.Script)),
            arguments.Where(argument => argument.Parameter is not null).Select(argument => argument.Parameter!).ToArray());

    protected PowerShellCliTemplate Variant(
        string arguments, string id, string name, string description, string output, string prefix, params ScriptTemplateParameter[] parameters)
    {
        var template = Template(id, name, description, "none", prefix + "\n" + arguments, parameters);
        return output == "none" ? CliRecipeBuilder.AsAction(template) : template with { Output = output };
    }
}
