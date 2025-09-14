// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.AI.Actions;
using Windows.AI.Actions.Hosting;
using Windows.ApplicationModel.Contacts;

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
        foreach (var action in actions)
        {
            foreach (var overload in action.GetOverloads())
            {
                try
                {
                    var inputs = overload.GetInputs();

                    var tags = inputs.AsEnumerable().Select(input => new Tag(input.Name) { Icon = GetIconForInput(input)! }).ToList();
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
}

// [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "meh")]
// public partial class CommandParameter : BaseObservable, ICommandArgument
// {
//    public virtual string Name { get; set; }

// public virtual bool Required { get; set; }

// public virtual ParameterType Type { get; set; }

// public virtual object? Value
//    {
//        get; set
//        {
//            if (field != value)
//            {
//                field = value;
//                OnPropertyChanged(nameof(Value));
//                OnPropertyChanged(nameof(DisplayName));
//            }
//        }
//    }

// public virtual string? DisplayName => Value?.ToString() ?? string.Empty;

// public virtual IIconInfo? Icon
//    {
//        get => field;
//        set
//        {
//            field = value;
//            OnPropertyChanged(nameof(Icon));
//        }
//    }

// public virtual void ShowPicker(ulong hostHwnd)
//    {
//    }

// public CommandParameter(string name = "", bool required = true, ParameterType type = ParameterType.Text)
//    {
//        Name = name;
//        Required = required;
//        Type = type;
//    }
// }

// [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "meh")]
// public abstract partial class InvokableWithParams : Command, IInvokableCommandWithParameters
// {
//    public ICommandArgument[] Parameters { get; set; } = [];

// public abstract ICommandResult InvokeWithArgs(object sender, ICommandArgument[] args);
// }
public partial class DoActionPage : ParametersPage
{
    public override IconInfo Icon => _command.Icon;

    private readonly ActionDefinition _action;
    private readonly ActionOverload _overload;
    private readonly ActionRuntime _actionRuntime;
    private readonly string _id;
    private readonly List<IParameterRun> _parameters = new();
    private readonly DoActionCommand _command;

    public DoActionPage(ActionDefinition action, ActionOverload overload, ActionRuntime actionRuntime)
    {
        _action = action;
        _overload = overload;

        var inputs = action.GetInputs();

        _actionRuntime = actionRuntime;
        _id = action.Id;

        _parameters.Add(new LabelRun(overload.DescriptionTemplate));

        // ICommandArgument[] commandParameters = inputs.AsEnumerable()
        //     .Select(input => new CommandParameter(input.Name))
        //     .ToArray();
        // Parameters = commandParameters;
        foreach (var input in inputs)
        {
            IParameterRun param = input.Kind switch
            {
                ActionEntityKind.None => new LabelRun(input.Name),
                ActionEntityKind.Document => new FilePickerParameterRun() { PlaceholderText = input.Name },
                ActionEntityKind.File => new FilePickerParameterRun() { PlaceholderText = input.Name },
                ActionEntityKind.Photo => new FilePickerParameterRun() { PlaceholderText = input.Name },
                ActionEntityKind.Text => new StringParameterRun(input.Name),

                // ActionEntityKind.StreamingText => new CommandParameter(input.Name, input.Required, ParameterType.StreamingText),
                // ActionEntityKind.RemoteFile => new CommandParameter(input.Name, input.Required, ParameterType.RemoteFile),
                // ActionEntityKind.Table => new CommandParameter(input.Name, input.Required, ParameterType.Table),
                ActionEntityKind.Contact => new StringParameterRun(input.Name),
                _ => throw new NotSupportedException($"Unsupported action entity kind: {input.Kind}"),
            };

            // var parameter = new CommandParameter(input.Name, input.Required, input.Kind.ToParameterType());
            // if (input.DefaultValue != null)
            // {
            //     parameter.Value = input.DefaultValue;
            // }
            _parameters.Add(param);
        }

        _command = new DoActionCommand(action, overload, actionRuntime, _parameters);
    }

    public override IListItem Command => new ListItem(_command) { Title = _overload.DescriptionTemplate };

    public override IParameterRun[] Parameters => _parameters.ToArray();

    private ActionEntity CreateEntity(ActionEntityRegistrationInfo i, ActionEntityFactory f, object value)
    {
        ActionEntity v = i.Kind switch
        {
            ActionEntityKind.Photo => f.CreatePhotoEntity(value as string),
            ActionEntityKind.Document => f.CreateDocumentEntity(value as string),
            ActionEntityKind.File => f.CreateFileEntity(value as string),
            ActionEntityKind.Text => f.CreateTextEntity(value as string),
            ActionEntityKind.Contact => f.CreateContactEntity(value as Contact),
            _ => throw new NotSupportedException($"Unsupported entity kind: {i.Kind}"),
        };
        return v;
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
    private readonly List<IParameterRun> _parameters;

    // public ICommandResult InvokeWithArgs(object sender, object[] args)
    // {
    //     if (args == null)
    //     {
    //         var error = new ToastStatusMessage("no args oops");
    //         error.Show();
    //         return CommandResult.KeepOpen();
    //     }
    //
    //     var s = $"{_action.DescriptionTemplate}(";
    //     foreach (var arg in args)
    //     {
    //         s += $"{arg.Name}: {arg.Value.ToString()}";
    //     }
    //
    //     s += ")";
    //
    //     // var t = new ToastStatusMessage(s);
    //     // t.Show();
    //     _ = Task.Run(async () =>
    //     {
    //         var c = _actionRuntime.CreateInvocationContext(actionId: _id);
    //         var f = _actionRuntime.EntityFactory;
    //
    //         for (var i = 0; i < args.Length; i++)
    //         {
    //             var arg = args[i];
    //             var inputParam = _action.GetInputs()[i];
    //             var entity = CreateEntity(inputParam, f, arg);
    //             c.SetInputEntity(arg.Name, entity);
    //         }
    //
    //         // foreach (var i in args)
    //         // {
    //         //     var v = f.CreatePhotoEntity(i.Value as string);
    //         //     c.SetInputEntity(i.Name, v);
    //         // }
    //         var task = _action.InvokeAsync(c);
    //         await task;
    //         var status = task.Status;
    //         var statusType = c.Result switch
    //         {
    //             ActionInvocationResult.Success => MessageState.Success,
    //             _ => MessageState.Error,
    //         };
    //         var text = c.Result switch
    //         {
    //             ActionInvocationResult.Success => $"{c.Result.ToString()}",
    //             _ => $"{c.Result.ToString()}: {c.ExtendedError}",
    //         };
    //         var resultToast = new ToastStatusMessage(new StatusMessage() { Message = text, State = statusType });
    //         resultToast.Show();
    //     });
    //
    //     return CommandResult.KeepOpen();
    // }
    public DoActionCommand(ActionDefinition action, ActionOverload overload, ActionRuntime actionRuntime, List<IParameterRun> parameters)
    {
        _overload = overload;
        _action = action;

        var inputs = overload.GetInputs();

        _actionRuntime = actionRuntime;
        _id = action.Id;
        _parameters = parameters;

        // ICommandArgument[] commandParameters = inputs.AsEnumerable()
        //     .Select(input => new CommandParameter(input.Name))
        //     .ToArray();
        // Parameters = commandParameters;
        //    foreach (var input in inputs)
        //    {
        //        var param = input.Kind switch
        //        {
        //            ActionEntityKind.None => new CommandParameter(input.Name),
        //            ActionEntityKind.Document => new CommandParameter(input.Name),
        //            ActionEntityKind.File => new CommandParameter(input.Name),
        //            ActionEntityKind.Photo => new ImageParameter(input.Name),
        //            ActionEntityKind.Text => new CommandParameter(input.Name),

        // // ActionEntityKind.StreamingText => new CommandParameter(input.Name, input.Required, ParameterType.StreamingText),
        //            // ActionEntityKind.RemoteFile => new CommandParameter(input.Name, input.Required, ParameterType.RemoteFile),
        //            // ActionEntityKind.Table => new CommandParameter(input.Name, input.Required, ParameterType.Table),
        //            ActionEntityKind.Contact => new ContactParameter(input.Name),
        //            _ => throw new NotSupportedException($"Unsupported action entity kind: {input.Kind}"),
        //        };

        // // var parameter = new CommandParameter(input.Name, input.Required, input.Kind.ToParameterType());
        //        // if (input.DefaultValue != null)
        //        // {
        //        //     parameter.Value = input.DefaultValue;
        //        // }
        //        Parameters = Parameters.Append(param).ToArray();
        //    }
    }
}

// [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "meh")]
// public partial class ImageParameter : CommandParameter
// {
//    private string? _filePath;

// public ImageParameter(string name = "", bool required = true)
//        : base(name, required, ParameterType.Custom)
//    {
//    }

// public override void ShowPicker(ulong hostHwnd)
//    {
//        var picker = new FileOpenPicker
//        {
//            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
//        };
//        picker.FileTypeFilter.Add(".jpg");
//        picker.FileTypeFilter.Add(".jpeg");
//        picker.FileTypeFilter.Add(".png");
//        picker.FileTypeFilter.Add(".gif");
//        picker.FileTypeFilter.Add(".bmp");
//        picker.FileTypeFilter.Add(".tiff");
//        picker.FileTypeFilter.Add(".webp");

// // Initialize the picker with the window handle
//        WinRT.Interop.InitializeWithWindow.Initialize(picker, (IntPtr)hostHwnd);

// _ = Task.Run(async () =>
//        {
//            try
//            {
//                var file = await picker.PickSingleFileAsync();
//                if (file != null)
//                {
//                    _filePath = file.Path;
//                    Value = _filePath;
//                    Icon = new IconInfo(_filePath);

// // TODO! update display name
//                }
//            }
//            catch (Exception ex)
//            {
//                // Handle any exceptions that might occur during file picking
//                System.Diagnostics.Debug.WriteLine($"Error picking image file: {ex.Message}");
//            }
//        });
//    }

// public override string? DisplayName
//    {
//        get { return string.IsNullOrEmpty(_filePath) ? null : Path.GetFileName(_filePath); }
//    }
// }

// [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "meh")]
// public partial class ContactParameter : CommandParameter
// {
//    private Contact? _selectedContact;

// public ContactParameter(string name = "", bool required = true)
//        : base(name, required, ParameterType.Custom)
//    {
//    }

// public override void ShowPicker(ulong hostHwnd)
//    {
//        var picker = new ContactPicker
//        {
//            // CommitButtonText = "Select",

// // SelectionMode = ContactSelectionMode.Contacts,
//        };

// // Specify which contact properties to retrieve
//        // picker.DesiredFieldsWithContactFieldType.Add(ContactFieldType.Email);
//        // picker.DesiredFieldsWithContactFieldType.Add(ContactFieldType.PhoneNumber);

// // Initialize the picker with the window handle
//        WinRT.Interop.InitializeWithWindow.Initialize(picker, (IntPtr)hostHwnd);

// _ = Task.Run(async () =>
//        {
//            try
//            {
//                var contact = await picker.PickContactAsync();
//                if (contact != null)
//                {
//                    _selectedContact = contact;
//                    Value = contact;
//                    Icon = Icons.ContactInput;
//                }
//            }
//            catch (Exception ex)
//            {
//                // Handle any exceptions that might occur during contact picking
//                System.Diagnostics.Debug.WriteLine($"Error picking contact: {ex.Message}");
//            }
//        });
//    }

// public override string? DisplayName
//    {
//        get
//        {
//            return _selectedContact == null
//                ? null
//                : !string.IsNullOrEmpty(_selectedContact.DisplayName)
//                ? _selectedContact.DisplayName
//                : _selectedContact.Name;
//        }
//    }
// }


#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
#nullable disable
