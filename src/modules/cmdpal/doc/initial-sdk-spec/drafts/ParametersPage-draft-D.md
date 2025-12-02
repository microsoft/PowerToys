---
author: Mike Griese
created on: 2025-09-04
last updated: 2025-09-08
issue id: n/a
---

## Addenda II-D: Parameters page


```c#
[uuid("a2590cc9-510c-4af7-b562-a6b56fe37f55")]
interface IParameterRun requires INotifyPropChanged
{
    
};

interface ILabelRun requires IParameterRun
{
    String Text { get; };
};

interface IParameterValueRun requires IParameterRun
{
    String PlaceholderText{ get; };
    Boolean NeedsValue{ get; }; // TODO! name is weird
};

interface IStringParameterRun requires IParameterValueRun
{
    String Text{ get; set; };

    // TODO! do we need a way to validate string inputs?
};

interface ICommandParameterRun requires IParameterValueRun
{
    String DisplayText{ get; };
    ICommand GetSelectValueCommand(UInt64 hostHwnd);
    IIconInfo Icon{ get; }; // ? maybe
};

interface IParametersPage requires IPage
{
    IParameterRun[] Parameters { get; };
    IListItem Command { get; };
};
```

When we open a `IParametersPage`, we will render the `Parameters` in the search
box. We'll move focus to the first `IParameterRun` that is not a `ILabelRun`.
What those interactions looks like depends on the type of `IParameterRun`. 

There are three basic types of inputs: strings, invokable commands, and lists.
Strings are a special case that doesn't require a command to set the value.
Lists and invokable commands are picked based on the type of the
`SelectValueCommand`. Each of these are detailed below. 

When all the parameters have `NeedsValue` set to `false`, we will display a
single item to the user - the `Command` item. 

### String parameters

These are rendered as a text box within the search box. The user can type into
it. Focus is moved to the next parameter when the user presses Enter or tab. 

### Command parameters - Invokable Commands

These are used when the `SelectValueCommand` is an `IInvokableCommand`.

These are rendered as a button within the search box. The button text is
`DisplayText` if it is set, otherwise it is `PlaceholderText`. If the user
clicks the button, we invoke the `SelectValueCommand` (and ignore the `CommandResult`).

This is good for file pickers, date pickers, color pickers, etc. Anything that
requires a custom UI to pick a value.

When the extension has picked a value, it should set the `NeedsValue` to false. 
The extension can also set the `DisplayText` and `Icon` to reflect the chosen value.

When the user presses enter with the button focused, we will also invoke the
`SelectValueCommand`.

When the user presses tab, we will move focus to the next parameter.

If the `NeedsValue` property is changed to `false` while it's focused, we will
move focus to the next parameter.

### Command parameters - List Commands

These are used when the `SelectValueCommand` is an `IListPage` - both static and
dynamic lists work similarly.

These are rendered as a text box within the search box. When the user focuses
the text box, we will display the items from the `IListPage` in the body of
CmdPal. The user can then type to filter the list. This filtering will work the
same way as any other list page in CmdPal - CmdPal will filter static lists, or
pass the query to a dynamic list.

The items in this list should all be `IListItem` objects with
`IInvokableCommands`. Putting a `IPage` into one of these items will cause the
user to navigate away from the parameters page, which would probably be
unexpected.

When the user picks an item from the list, the extension should handle that
command by bubbling an event up to the `CommandRun`, and setting the `Value`,
`DisplayText`, and `Icon` properties, and setting `NeedsValue` to false.

When the user presses enter with the text box focused, we will invoke the
command of the selected item in the list. 

When the user presses tab, we will move focus to the next parameter.

If the `NeedsValue` property is changed to `false` while it's focused, we will
move focus to the next parameter.

### Example

Lets say you had a command like "Create a note \${title} in \${folder}".
`title` is a string input, and `folder` is a static list of folders. 

The extension author can then define a `IParametersPage` with four runs in it:
* A `ILabelRun` for "Create a note"
* A `IStringParameterRun` for the `title`
* A `ILabelRun` for "in"
* A `ICommandParameterRun` for the `folder`. The `Command` will be a `IListPage`, where the items are possible folders


In this example, the user can pick the "create note" command, then type the title, hit enter/tab, and then pick a folder from the list, then hit enter to run the command.

```cs
public interface IRequiresHostHwnd
{
    void SetHostHwnd(UInt64 hostHwnd);
}

public sealed partial class CommandParameterRun : BaseObservable, ICommandParameterRun
{
    public virtual string DisplayText { get; set; } // basic projected properties here, same as throughout the toolkit
    public virtual string PlaceholderText { get; set; } // basic projected properties here, same as throughout the toolkit
    public virtual ICommand Command { get; set; } // basic projected properties here, same as throughout the toolkit
    public virtual IIconInfo Icon { get; set; } // basic projected properties here, same as throughout the toolkit
    public virtual bool NeedsValue => Value == null; // Toolkit helper: does this parameter need a value?

    public virtual ICommand GetSelectValueCommand(UInt64 hostHwnd)
    {
        if (Command is IRequiresHostHwnd requiresHwnd)
        {
            requiresHwnd.SetHostHwnd(hostHwnd);
        }
        return Command;
    }

    public object? Value { get; set; } // Toolkit helper: a value for the parameter
}

public sealed partial class CreateNoteParametersPage : ParametersPage
{
    private readonly SelectFolderPage _selectFolderPage = new SelectFolderPage();

    private readonly StringParameterRun _titleParameter = new StringParameterRun()
    {
        PlaceholderText = "Note title"
    };
    private readonly ICommandParameterRun _folderParameter = new CommandParameterRun()
    {
        PlaceholderText = "Select folder",
        Command = _selectFolderPage
    };

    private readonly List<IParameterRun> _parameters;

    private readonly CreateNoteCommand _command = new() { TitleParameter = _titleParameter, FolderParameter = _folderParameter };
    private readonly ListItem _item = new(_command);

    public IParameterRun[] Parameters => _parameters.ToArray();
    public IListItem Command => _item;

    public CreateNoteParametersPage()
    {
        _parameters = new List<IParameterRun>
        {
            new LabelRun("Create a note"),
            _titleParameter,
            new LabelRun("in"),
            _folderParameter
        };

        _selectFolderPage.FolderSelected += (s, folder) =>
        {
            _folderParameter.Value = folder;
            _folderParameter.Icon = folder.Icon;
            _folderParameter.DisplayText = folder.Name;

        };
    };
}

public sealed partial class CreateNoteCommand : BaseObservable, IInvokableCommand
{
    internal IStringParameterRun TitleParameter { get; init; } // set by the parameters page
    internal ICommandParameterRun FolderParameter { get; init; } // set by the parameters page

    public IIconInfo Icon => new IconInfo("NoteAdd");

    public override ICommandResult Invoke()
    {
        var title = TitleParameter.Text;
        if (string.IsNullOrWhiteSpace(title))
        {
            var t = new ToastStatusMessage(new StatusMessage(){ Title = "Title is required", State = MessageState.Error });
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
    public event EventHandler<Folder>? FolderSelected;

    public SelectFolderPage()
    {
        // Populate the list with folders
        var folders = FolderService.GetFolders(); // whatever your backend is
        Items = folders.Select(f => new ListItem(new SelectFolderCommand(f), f.Name, f.Icon)).ToArray();
    }

    private sealed partial class SelectFolderCommand : BaseObservable, IInvokableCommand
    {
        private readonly EventHandler<Folder> _folderSelected;
        private readonly Folder _folder;

        public IIconInfo Icon => _folder.Icon;
        public string Title => _folder.Name;

        public SelectFolderCommand(Folder folder, EventHandler<Folder> folderSelected)
        {
            _folder = folder;
            _folderSelected = folderSelected;
        }

        public override ICommandResult Invoke()
        {
            _folderSelected?.Invoke(this, _folder);
            return CommandResult.KeepOpen();
        }
    }
}

public sealed partial class FilePickerParameterRun : CommandParameterRun
{
    public StorageFile? File { get; private set;}
    public FilePickerParameterRun()
    {
        var command = new FilePickerCommand();
        command.FileSelected += (file) =>
        {
            File = file;
            if (file != null)
            {
                Value = file;
                DisplayText = file.Name;
                // Icon = new IconInfo("File");
            }
            else
            {
                Value = null;
                DisplayText = null;
                // Icon = new IconInfo("File");
            }
        };
        PlaceholderText = "Select a file";
        Icon = new IconInfo("File");
        Command = command;
    }

    private sealed partial class FilePickerCommand : InvokableCommand, IRequiresHostHwnd
    {
        public IIconInfo Icon => new IconInfo("File");
        public string Name => "Pick a file";

        public event EventHandler<StorageFile?>? FileSelected;

        private uint _hostHwnd;

        public void SetHostHwnd(uint hostHwnd)
        {
            _hostHwnd = hostHwnd;
        }

        public override ICommandResult Invoke()
        {
            PickFileAsync();
            return CommandResult.KeepOpen();
        }

        private async void PickFileAsync()
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            // You need to initialize the picker with a window handle in WinUI 3 desktop apps
            // See https://learn.microsoft.com/en-us/windows/apps/design/controls/file-open-picker
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _hostHwnd);

            var file = await picker.PickSingleFileAsync();
            FileSelected?.Invoke(this, file);
        }
    }
}

public sealed partial class SelectParameterCommand<T> : InvokableCommand
{
    public event TypedEventHandler<object, T>? ValueSelected;
    private T _value;
    public T Value { get => _value; protected set { _value = value; } }
    public SelectParameterCommand(T value)
    {
        _value = value;
    }

    public override ICommandResult Invoke()
    {
        ValueSelected?.Invoke(this, _value);
        return CommandResult.KeepOpen();
    }
}
public sealed partial class StaticParameterList<T> : ListPage
{
    public event TypedEventHandler<object, T>? ValueSelected;
    private bool _isInitialized = false;
    private readonly IEnumerable<T> _values;
    private readonly List<IListItem> _items = new List<IListItem>();
    private Func<T, ListItem, ListItem> _customizeListItemsCallback;

    // ctor takes an IEnumerable<T> values, and a function to customize the ListItem's depending on the value
    public StaticParameterList(IEnumerable<T> values, Func<T, ListItem> customizeListItem)
    {
        _values = values;
        _customizeListItemsCallback = (value, listItem) => { customizeListItem(value); return listItem; };
    }
    }
    public StaticParameterList(IEnumerable<T> values, Func<T, ListItem, ListItem> customizeListItem)
    {
        _values = values;
        _customizeListItemsCallback = customizeListItem;
    }
    public override IListItem[] GetItems()
    {
        if (!_isInitialized)
        {
            Initialize(_values, _customizeListItemsCallback);
            _isInitialized = true;
        }
        return _items.ToArray();
    }
    private void Initialize(IEnumerable<T> values, Func<T, ListItem, ListItem> customizeListItem)
    {
        foreach (var value in values)
        {
            var command = new SelectParameterCommand<T>(value);
            command.ValueSelected += (s, v) => ValueSelected?.Invoke(this, v);
            var listItem = new ListItem(command);
            var item = customizeListItem(value, listItem);
            _items.Add(item);
        }
    }
}
```




--------------------------------------------------------

## original draft starts here


### Arbitrary parameters and arguments

Something we'll want to consider soon is how to allow for arbitrary parameters
to be passed to commands. This allows for commands to require additional info from
the user _before_ they are run. In its simplest form, this is a lightweight way
to have an action accept form data inline with the query. But this also allows
for highly complex action chaining.

I had originally started to spec this out as:

```cs
enum ParameterType
{
    Text,
    File,
    Files,
    Enum,
    Entity
};

interface ICommandParameter
{
    ParameterType Type { get; };
    String Name { get; };
    Boolean Required{ get; };
    // TODO! values for enums?
    // TODO! dynamic values for enums? like GetValues(string query)
    // TODO! files might want to restrict types? but now we're a file picker and need that whole API
    // TODO! parameters with more than one value? Like, 
    //    SendMessage(People[] to, String message)
};

interface ICommandArgument
{
    String Name { get; };
    Object Value { get; };
};

interface IInvokableCommandWithParameters requires ICommand {
    ICommandParameter[] Parameters { get; };
    ICommandResult InvokeWithArgs(Object sender, ICommandArgument[] args);
};

```

TODO! Mike:
We should add like, a `CustomPicker` parameter type, which would allow
extensions to define their own custom pickers for parameters. Then when we go to fill the argument, we'd call something like `ShowPickerAsync(ICommandParameter param)` and let them fill in the value. We don't care what the value is.

So it'd be more like 

```c#
enum ParameterType
{
    Text,
    // File,
    // Files,
    Enum,
    Custom
};

// interface IArgumentEnumValue requires INotifyPropChanged
// {
//     String Name { get; };
//     IIconInfo Icon { get; };
// }
interface ICommandArgument requires INotifyPropChanged
{
    ParameterType Type { get; };
    String Name { get; };
    Boolean Required{ get; };

    Object Value { get; set; };
    String DisplayName { get; };
    IIconInfo Icon { get; };

    void ShowPicker(UInt64 hostHwnd);
    // todo
    // IArgumentEnumValue[] GetValues();
};

interface IInvokableCommandWithParameters requires ICommand {
    ICommandArgument[] Parameters { get; };
    ICommandResult InvokeWithArgs(Object sender, ICommandArgument[] args);
};
```


And `CommandParameters` would be a set of `{ type, name, required }` structs,
which would specify the parameters that the action needs. Simple types would be
`string`, `file`, `file[]`, `enum` (with possible values), etc.

But that may not be complex enough. We recently learned about Action Framework
and some of their plans there - that may be a good fit for this. My raw notes
follow - these are not part of the current SDK spec.

> [!NOTE]
>
> A thought: what if a action returns a `CommandResult.Entity`, then that takes
> devpal back home, but leaves the entity in the query box. This would allow for
> a Quicksilver-like "thing, do" flow. That command would prepopulate the
> parameters. So we would then filter top-level commands based on things that can
> accept the entity in the search box.
>
> For example: The user uses the "Search for file" list page. They find the file
> they're looking for. That file's ListItem has a context item "With
> {filename}..." that then returns a `CommandResult.Entity` with the file entity.
> The user is taken back to the main page, and a file picker badge (with that
> filename) is at the top of the search box. In that state, the only commands
> now shown are ones that can accept a File entity. This could be things like
> the "Remove background" action (from REDACTED), the "Open with" action, the
> "Send to Teams chat" (which would then ask for another entity). If they did
> the "Remove Background" one, that could then return _another_ entity.
>
> We'd need to solve for the REDACTED case specifically, cause I bet they want to
> stay in the REDACTED action page, rather than the main one.
>
> We'd also probably want the REDACTED one to be able to accept arbitrary
> entities... like, they probably want a `+` button that lets you add... any
> kind of entity to their page, rather than explicitly ask for a list of args.

However, we do not have enough visibility on how action framework actually
works, consumer-wise, to be able to specify more. As absolutely fun as chaining
actions together sounds, I've decided to leave this out of the official v1 spec.
We can ship a viable v0.1 of DevPal without it, and add it in post.