// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.AI.Actions;
using Windows.AI.Actions.Hosting;
using Windows.ApplicationModel.Contacts;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Microsoft.CmdPal.Ext.Actions;

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

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

        var actionsDebug = string.Empty;

        foreach (var action in actions)
        {
            var overloads = action.GetOverloads();
            var overloadsTxt = string.Join("\n", overloads.Select(o => $"  - {o.DescriptionTemplate}"));
            actionsDebug += $"* `{action.Id}`: {action.Description}\n{overloadsTxt}\n";
        }

        Logger.LogDebug(actionsDebug);

        foreach (var action in actions)
        {
            var overloads = action.GetOverloads();
            foreach (var overload in action.GetOverloads())
            {
                try
                {
                    var inputs = overload.GetInputs();

                    var tags = inputs.AsEnumerable().Select(input => new Tag(input.Name) { Icon = GetIconForInput(input)! });
                    if (action.UsesGenerativeAI)
                    {
                        tags = tags.Prepend(RobotTag);
                    }

                    var command = new DoActionPage(action, overload, _actionRuntime);
                    items.Add(new ListItem(command)
                    {
                        Title = action.Description,
                        Subtitle = overload.DescriptionTemplate,
                        Tags = tags.ToArray(),
                    });
                }
                catch (Exception)
                {
                    ExtensionHost.LogMessage($"Unsupported action {overload.DescriptionTemplate}");
                }
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

    private static readonly IconInfo RobotIcon = new("\uE99A");
    private static readonly Tag RobotTag = new() { Icon = RobotIcon };
}

public partial class DoActionPage : ParametersPage
{
    public override IconInfo Icon => _command.Icon;

    private readonly ActionDefinition _action;
    private readonly ActionOverload _overload;
    private readonly ActionRuntime _actionRuntime;
    private readonly string _id;
    private readonly List<IParameterRun> _parameters = new();
    private readonly DoActionCommand _command;
    private Dictionary<string, ParameterValueRun> _actionParams = new();

    public DoActionPage(ActionDefinition action, ActionOverload overload, ActionRuntime actionRuntime)
    {
        _action = action;
        _overload = overload;
        _actionRuntime = actionRuntime;
        _id = action.Id;

        _parameters.Add(new LabelRun(overload.DescriptionTemplate));

        var inputs = action.GetInputs();
        foreach (var input in inputs)
        {
            var param = GetParameter(input);

            _parameters.Add(param);
            if (param is ParameterValueRun p)
            {
                _actionParams.Add(input.Name, p);
            }
        }

        _command = new DoActionCommand(action, overload, actionRuntime, _actionParams);
    }

    public override IListItem Command => new ListItem(_command) { Title = _overload.DescriptionTemplate };

    public override IParameterRun[] Parameters => _parameters.ToArray();

    internal static IParameterRun GetParameter(ActionEntityRegistrationInfo input)
    {
        IParameterRun param = input.Kind switch
        {
            ActionEntityKind.None => new LabelRun(input.Name),
            ActionEntityKind.Document => new FilePickerParameterRun() { PlaceholderText = input.Name },
            ActionEntityKind.File => new FilePickerParameterRun() { PlaceholderText = input.Name },
            ActionEntityKind.Photo => new PhotoFilePicker() { PlaceholderText = input.Name },
            ActionEntityKind.Text => new StringParameterRun(input.Name),

            // ActionEntityKind.StreamingText => new CommandParameter(input.Name, input.Required, ParameterType.StreamingText),
            // ActionEntityKind.RemoteFile => new CommandParameter(input.Name, input.Required, ParameterType.RemoteFile),
            // ActionEntityKind.Table => new CommandParameter(input.Name, input.Required, ParameterType.Table),
            ActionEntityKind.Contact => new StringParameterRun(input.Name),
            _ => throw new NotSupportedException($"Unsupported action entity kind: {input.Kind}"),
        };
        return param;
    }
}

public partial class DoActionCommand : InvokableCommand
{
    public override string Name => "Invoke";

    public override IconInfo Icon => new(_action.IconFullPath);

    private readonly ActionDefinition _action;
    private readonly ActionOverload _overload;
    private readonly ActionRuntime _actionRuntime;
    private readonly string _id;
    private readonly Dictionary<string, ParameterValueRun> _actionParams;

    public override ICommandResult Invoke()
    {
        // First, check that all required parameters have values.
        foreach (var input in _overload.GetInputs())
        {
            if (_actionParams.TryGetValue(input.Name, out var param))
            {
                if (param == null || param.NeedsValue)
                {
                    var error = new ToastStatusMessage($"Parameter '{input.Name}' is required.");
                    error.Show();
                    return CommandResult.KeepOpen();
                }
            }
        }

        _ = Task.Run(InvokeActionAsync);

        return CommandResult.Dismiss();
    }

    private async Task InvokeActionAsync()
    {
        try
        {
            var c = _actionRuntime.CreateInvocationContext(actionId: _id);
            var f = _actionRuntime.EntityFactory;

            var inputs = _overload.GetInputs();
            for (var i = 0; i < inputs.Length; i++)
            {
                var input = inputs[i];
                var name = input.Name;
                if (_actionParams.TryGetValue(name, out var v))
                {
                    var value = v.Value;
                    var entity = CreateEntity(input, f, v.Value!);
                    c.SetInputEntity(name, entity);
                }
            }

            var task = _overload.InvokeAsync(c);
            await task;
            var statusType = c.Result switch
            {
                ActionInvocationResult.Success => MessageState.Success,
                _ => MessageState.Error,
            };
            var text = c.Result switch
            {
                ActionInvocationResult.Success => $"{c.Result.ToString()}",
                _ => $"{c.Result.ToString()}: {c.ExtendedError}",
            };
            var resultToast = new ToastStatusMessage(new StatusMessage() { Message = text, State = statusType });
            resultToast.Show();
        }
        catch (Exception ex)
        {
            var errorToast = new ToastStatusMessage(new StatusMessage() { Message = ex.Message, State = MessageState.Error });
            errorToast.Show();
        }
    }

    public DoActionCommand(ActionDefinition action, ActionOverload overload, ActionRuntime actionRuntime, Dictionary<string, ParameterValueRun> parameters)
    {
        _overload = overload;
        _action = action;
        _actionRuntime = actionRuntime;
        _id = action.Id;
        _actionParams = parameters;
    }

    private static ActionEntity CreateEntity(ActionEntityRegistrationInfo i, ActionEntityFactory f, object value)
    {
        var input = value switch
        {
            string s => s,
            StorageFile file => file.Path,
            _ => null,
        };
        if (input == null)
        {
            throw new NotSupportedException($"Unexpected action input {value.ToString()}");
        }

        ActionEntity v = i.Kind switch
        {
            ActionEntityKind.Photo => f.CreatePhotoEntity(input),
            ActionEntityKind.Document => f.CreateDocumentEntity(input),
            ActionEntityKind.File => f.CreateFileEntity(input),
            ActionEntityKind.Text => f.CreateTextEntity(input),
            ActionEntityKind.Contact => CreateContact(input, f),
            _ => throw new NotSupportedException($"Unsupported entity kind: {i.Kind}"),
        };
        return v;
    }

    private static ContactActionEntity CreateContact(string? text, ActionEntityFactory f)
    {
        var contact = new Contact();
        var email = new ContactEmail();
        email.Address = text ?? string.Empty;
        contact.Emails.Add(email);
        return f.CreateContactEntity(contact);
    }
}

internal sealed partial class PhotoFilePicker : FilePickerParameterRun
{
    protected override void ConfigureFilePicker(object? sender, FileOpenPicker picker)
    {
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".gif");
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".tiff");
        picker.FileTypeFilter.Add(".webp");
    }
}

#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
#nullable disable
