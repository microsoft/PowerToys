// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.AI.Actions;
using Windows.AI.Actions.Hosting;

namespace Microsoft.CmdPal.Ext.Actions;

internal sealed partial class ActionsTestPage : ListPage
{
    private readonly ActionRuntime _actionRuntime;

    public ActionsTestPage(ActionRuntime actionRuntime)
    {
        _actionRuntime = actionRuntime;
        Icon = Icons.ActionsPng;
        Title = "Actions";
        Name = "Open";
    }

    public override IListItem[] GetItems()
    {
        var actions = _actionRuntime.ActionCatalog.GetAllActions();

        var items = new List<ListItem>();
        foreach (var action in actions)
        {
            foreach (var overload in action.GetOverloads())
            {
                var inputs = overload.GetInputs();

                var tags = inputs.AsEnumerable().Select(input => new Tag(input.Name) { Icon = GetIconForInput(input)! }).ToList();

                items.Add(new ListItem(new DoActionCommand(overload) { Name = "Invoke" })
                {
                    Title = action.Description,
                    Subtitle = overload.DescriptionTemplate,
                    Icon = new IconInfo(action.IconFullPath),
                    Tags = tags.ToArray(),
                });
            }
        }

        return items.ToArray();
    }

    private static IconInfo? GetIconForInput(ActionEntityRegistrationInfo input)
    {
        return input.Kind switch
        {
            ActionEntityKind.None => null,
            ActionEntityKind.Document => Icons.DocumentInput,
            ActionEntityKind.File => Icons.FileInput,
            ActionEntityKind.Photo => Icons.PhotoInput,
            ActionEntityKind.Text => Icons.TextInput,
            ActionEntityKind.StreamingText => Icons.StreamingTextInput,
            ActionEntityKind.RemoteFile => Icons.RemoteFileInput,
            ActionEntityKind.Table => Icons.TableInput,
            ActionEntityKind.Contact => Icons.ContactInput,
            _ => null,
        };
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "meh")]
public partial class CommandParameter : ICommandParameter
{
    public string Name { get; set; }

    public bool Required { get; set; }

    public ParameterType Type { get; set; }

    public CommandParameter(string name = "", bool required = true, ParameterType type = ParameterType.Text)
    {
        Name = name;
        Required = required;
        Type = type;
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "meh")]
public abstract partial class InvokableWithParams : Command, IInvokableCommandWithParameters
{
    public ICommandParameter[] Parameters { get; set; } = [];

    public abstract ICommandResult InvokeWithArgs(object sender, ICommandArgument[] args);
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "meh")]
public partial class DoActionCommand : InvokableWithParams
{
    private readonly ActionOverload _action;

    public override ICommandResult InvokeWithArgs(object sender, ICommandArgument[] args)
    {
        if (args == null)
        {
            var error = new ToastStatusMessage("no args oops");
            error.Show();
            return CommandResult.KeepOpen();
        }

        var s = $"{_action.DescriptionTemplate}(";
        foreach (var arg in args)
        {
            s += $"{arg.Name}: {arg.Value.ToString()}";
        }

        s += ")";
        var t = new ToastStatusMessage(s);
        t.Show();
        return CommandResult.KeepOpen();
    }

    public DoActionCommand(ActionOverload action)
    {
        _action = action;

        var inputs = action.GetInputs();

        ICommandParameter[] commandParameters = inputs.AsEnumerable()
            .Select(input => new CommandParameter(input.Name))
            .ToArray();
        Parameters = commandParameters;
    }
}
