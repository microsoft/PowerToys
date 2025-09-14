// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages;

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name
#nullable enable

public sealed partial class SimpleParameterTest : ParametersPage
{
    private readonly StringParameterRun _stringParameter;
    private readonly InvokableCommand _command;

    public SimpleParameterTest()
    {
        Name = "Open";
        _stringParameter = new StringParameterRun()
        {
            PlaceholderText = "Type something",
        };

        _command = new AnonymousCommand(() =>
        {
            var input = _stringParameter.Text;
            var toast = new ToastStatusMessage(new StatusMessage() { Message = $"You entered: {input}" });
            toast.Show();
            _stringParameter.ClearValue();
        })
        {
            Name = "Submit",
            Icon = new IconInfo("\uE724"), // Send
            Result = CommandResult.KeepOpen(),
        };
    }

    public override IParameterRun[] Parameters => new IParameterRun[]
    {
        new LabelRun("Enter a value:"),
        _stringParameter,
    };

    public override IListItem Command => new ListItem(_command);
}

public sealed partial class ButtonParameterTest : ParametersPage
{
    private readonly CommandParameterRun _fileParameter;
    private readonly InvokableCommand _command;

    public ButtonParameterTest()
    {
        Name = "Open";
        _fileParameter = new FilePickerParameterRun();

        _command = new AnonymousCommand(HandleInvoke)
        {
            Name = "Submit",
            Icon = new IconInfo("\uE724"), // Send
            Result = CommandResult.KeepOpen(),
        };
    }

    public override IParameterRun[] Parameters => new IParameterRun[]
    {
        new LabelRun("Pick a file:"),
        _fileParameter,
        new LabelRun("and we'll open it"),
    };

    public override IListItem Command => new ListItem(_command);

    private void HandleInvoke()
    {
        var input = (Windows.Storage.StorageFile?)_fileParameter.Value;
        ToastStatusMessage? toast;
        if (_fileParameter.Value is Windows.Storage.StorageFile file)
        {
            toast = new ToastStatusMessage(new StatusMessage() { Message = $"You entered: '{file.Path}'", State = MessageState.Success });
            ShellHelpers.OpenInShell(file.Path);
        }
        else
        {
            toast = new ToastStatusMessage(new StatusMessage() { Message = $"no file selected", State = MessageState.Warning });
        }

        _fileParameter.ClearValue();
        toast?.Show();
    }
}

public sealed partial class CreateNoteParametersPage : ParametersPage
{
    private readonly SelectFolderPage _selectFolderPage = new();

    private readonly StringParameterRun _titleParameter;
    private readonly CommandParameterRun _folderParameter;

    private readonly List<IParameterRun> _parameters;

    private readonly CreateNoteCommand _command;
    private readonly ListItem _item;

    public override IParameterRun[] Parameters => _parameters.ToArray();

    public override IListItem Command => _item;

    public CreateNoteParametersPage()
    {
        _titleParameter = new StringParameterRun()
        {
            PlaceholderText = "Note title",
        };

        _folderParameter = new CommandParameterRun()
        {
            PlaceholderText = "Select folder",
            Command = _selectFolderPage,
        };

        _command = new() { TitleParameter = _titleParameter, FolderParameter = _folderParameter };
        _item = new(_command);

        _parameters = new List<IParameterRun>
        {
            new LabelRun("Create a note"),
            _titleParameter,
            new LabelRun("in"),
            _folderParameter,
        };

        _selectFolderPage.FolderSelected += (s, folder) =>
            {
                _folderParameter.Value = folder;
                _folderParameter.Icon = folder.Icon;
                _folderParameter.DisplayText = folder.Name;
            };
    }
}

internal sealed class Folder
{
    public string? Name { get; set; }

    public IconInfo? Icon { get; set; }
}

internal sealed partial class CreateNoteCommand : InvokableCommand
{
    internal required IStringParameterRun TitleParameter { get; init; } // set by the parameters page

    internal required CommandParameterRun FolderParameter { get; init; } // set by the parameters page

    public override IconInfo Icon => new("NoteAdd");

    public override ICommandResult Invoke()
    {
        var title = TitleParameter.Text;
        if (string.IsNullOrWhiteSpace(title))
        {
            var t = new ToastStatusMessage(new StatusMessage() { Message = "Title is required", State = MessageState.Error });
            t.Show();
            return CommandResult.KeepOpen();
        }

        var folder = FolderParameter.Value;
        if (folder is not Folder)
        {
            // This is okay, we'll create the note in the default folder
        }

        // Create the note in the specified folder
        NoteService.CreateNoteInFolder(title, folder); // whatever your backend is

        return CommandResult.Dismiss();
    }
}

public sealed partial class SelectFolderPage : ListPage
{
    internal event EventHandler<Folder>? FolderSelected;

    public SelectFolderPage()
    {
    }

    private sealed partial class SelectFolderCommand : InvokableCommand
    {
        internal event EventHandler<Folder>? FolderSelected;

        private readonly Folder _folder;

        public override IconInfo Icon => _folder?.Icon ?? new(string.Empty);

        public string Title => _folder?.Name ?? string.Empty;

        public SelectFolderCommand(Folder folder)
        {
            _folder = folder;
        }

        public override ICommandResult Invoke()
        {
            FolderSelected?.Invoke(this, _folder);
            return CommandResult.KeepOpen();
        }
    }

    public override IListItem[] GetItems()
    {
        var listItems = new List<ListItem>();

        // Populate the list with folders
        var folders = FolderService.GetFolders(); // whatever your backend is
        foreach (var value in folders)
        {
            var command = new SelectFolderCommand(value);
            command.FolderSelected += (s, v) => { this.FolderSelected?.Invoke(this, v); };
            var listItem = new ListItem(command);
            listItems.Add(listItem);
        }

        return listItems.ToArray();
    }
}

internal sealed class NoteService
{
    internal static void CreateNoteInFolder(string title, object? folder)
    {
        // Your backend logic to create a note
        var toast = new ToastStatusMessage(new StatusMessage() { Message = $"Created note '{title}'" });
        toast.Show();
    }
}

internal sealed class FolderService
{
    internal static IEnumerable<Folder> GetFolders()
    {
        // Your backend logic to get folders
        return new List<Folder>
        {
            new() { Name = "Personal", Icon = new IconInfo("\uEc25") },
            new() { Name = "Work", Icon = new IconInfo("\uE821") },
            new() { Name = "Ideas", Icon = new IconInfo("\uEA80") },
        };
    }
}


#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
#nullable disable
