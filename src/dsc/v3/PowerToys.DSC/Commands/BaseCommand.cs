// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using ManagedCommon;
using PowerToys.DSC.Options;
using PowerToys.DSC.Resources;

namespace PowerToys.DSC.Commands;

internal abstract class BaseCommand : Command
{
    private readonly ModuleOption _moduleOption;
    private readonly ResourceOption _resourceOption;
    private readonly InputOption _inputOption;

    protected ModuleType Module { get; private set; }

    protected BaseResource? Resource { get; private set; }

    protected string? Input { get; private set; }

    public BaseCommand(string name, string description)
        : base(name, description)
    {
        _moduleOption = new ModuleOption();
        AddOption(_moduleOption);

        _resourceOption = new ResourceOption();
        AddOption(_resourceOption);

        _inputOption = new InputOption();
        AddOption(_inputOption);

        this.SetHandler(CommandHandler);
    }

    public void CommandHandler(InvocationContext context)
    {
        var moduleName = context.ParseResult.GetValueForOption(_moduleOption);
        var resourceName = context.ParseResult.GetValueForOption(_resourceOption);

        Input = context.ParseResult.GetValueForOption(_inputOption);
        Module = Enum.Parse<ModuleType>(moduleName!, ignoreCase: true);
        Resource = resourceName switch
        {
            SettingsResource.ResourceName => new SettingsResource(Module),
            _ => throw new ArgumentException($"Unknown resource name: {resourceName}"),
        };

        CommandHandlerInternal(context);
    }

    public abstract void CommandHandlerInternal(InvocationContext context);
}
