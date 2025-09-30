// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using PowerToys.DSC.DSCResources;
using PowerToys.DSC.Options;
using PowerToys.DSC.Properties;

namespace PowerToys.DSC.Commands;

/// <summary>
/// Base class for all DSC commands.
/// </summary>
public abstract class BaseCommand : Command
{
    private static readonly CompositeFormat ModuleNotSupportedByResource = CompositeFormat.Parse(Resources.ModuleNotSupportedByResource);

    // Shared options for all commands
    private readonly ModuleOption _moduleOption;
    private readonly ResourceOption _resourceOption;
    private readonly InputOption _inputOption;

    // The dictionary of available resources and their factories.
    private static readonly Dictionary<string, Func<string?, BaseResource>> _resourceFactories = new()
    {
        { SettingsResource.ResourceName, module => new SettingsResource(module) },

        // Add other resources here
    };

    /// <summary>
    /// Gets the list of available DSC resources that can be used with the command.
    /// </summary>
    public static List<string> AvailableResources => [.._resourceFactories.Keys];

    /// <summary>
    /// Gets the DSC resource to be used by the command.
    /// </summary>
    protected BaseResource? Resource { get; private set; }

    /// <summary>
    /// Gets the input JSON provided by the user.
    /// </summary>
    protected string? Input { get; private set; }

    /// <summary>
    /// Gets the PowerToys module to be used by the command.
    /// </summary>
    protected string? Module { get; private set; }

    public BaseCommand(string name, string description)
        : base(name, description)
    {
        // Register the common options for all commands
        _moduleOption = new ModuleOption();
        AddOption(_moduleOption);

        _resourceOption = new ResourceOption(AvailableResources);
        AddOption(_resourceOption);

        _inputOption = new InputOption();
        AddOption(_inputOption);

        // Register the command handler
        this.SetHandler(CommandHandler);
    }

    /// <summary>
    /// Handles the command invocation.
    /// </summary>
    /// <param name="context">The invocation context containing the parsed command options.</param>
    public void CommandHandler(InvocationContext context)
    {
        Input = context.ParseResult.GetValueForOption(_inputOption);
        Module = context.ParseResult.GetValueForOption(_moduleOption);
        Resource = ResolvedResource(context);

        // Validate the module against the resource's supported modules
        var supportedModules = Resource.GetSupportedModules();
        if (!string.IsNullOrEmpty(Module) && !supportedModules.Contains(Module))
        {
            var errorMessage = string.Format(CultureInfo.InvariantCulture, ModuleNotSupportedByResource, Module, Resource.Name);
            context.Console.Error.WriteLine(errorMessage);
            context.ExitCode = 1;
            return;
        }

        // Continue with the command handler logic
        CommandHandlerInternal(context);
    }

    /// <summary>
    /// Handles the command logic internally.
    /// </summary>
    /// <param name="context">Invocation context containing the parsed command options.</param>
    public abstract void CommandHandlerInternal(InvocationContext context);

    /// <summary>
    /// Resolves the resource from the provided resource name in the context.
    /// </summary>
    /// <param name="context">Invocation context containing the parsed command options.</param>
    /// <returns>The resolved <see cref="BaseResource"/> instance.</returns>
    private BaseResource ResolvedResource(InvocationContext context)
    {
        // Resource option has already been validated before the command
        // handler is invoked.
        var resourceName = context.ParseResult.GetValueForOption(_resourceOption);
        Debug.Assert(!string.IsNullOrEmpty(resourceName), "Resource name must not be null or empty.");
        Debug.Assert(_resourceFactories.ContainsKey(resourceName), $"Resource '{resourceName}' is not registered.");
        return _resourceFactories[resourceName](Module);
    }
}
