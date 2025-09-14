// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name
#nullable enable
public interface IRequiresHostHwnd
{
    void SetHostHwnd(nint hostHwnd);
}

public partial class LabelRun : BaseObservable, ILabelRun
{
    private string? _text = string.Empty;

    public virtual string? Text
    {
        get => _text;
        set
        {
            _text = value;
            OnPropertyChanged(nameof(Text));
        }
    }

    public LabelRun(string text)
    {
        _text = text;
    }

    public LabelRun()
    {
    }
}

public abstract partial class ParameterValueRun : BaseObservable, IParameterValueRun
{
    private string _placeholderText = string.Empty;

    public virtual string PlaceholderText
    {
        get => _placeholderText;
        set
        {
            _placeholderText = value;
            OnPropertyChanged(nameof(PlaceholderText));
        }
    }

    private bool _needsValue = true;

    // _required | _needsValue | out
    // F         | F           | T
    // F         | T           | T
    // T         | F           | F
    // T         | T           | T
    public virtual bool NeedsValue
    {
        get => !_required || _needsValue;
        set
        {
            _needsValue = value;
            OnPropertyChanged(nameof(NeedsValue));
        }
    }

    // Toolkit helper
    private bool _required = true;

    public virtual bool Required
    {
        get => _required;
        set
        {
            _required = value;
            OnPropertyChanged(nameof(NeedsValue));
        }
    }

    public abstract void ClearValue();
}

public partial class StringParameterRun : ParameterValueRun, IStringParameterRun
{
    private string _text = string.Empty;

    public virtual string Text
    {
        get => _text;
        set
        {
            _text = value;
            OnPropertyChanged(nameof(Text));
            OnPropertyChanged(nameof(NeedsValue));
        }
    }

    public override bool NeedsValue => string.IsNullOrEmpty(Text);

    public StringParameterRun()
    {
    }

    public StringParameterRun(string placeholderText)
    {
        PlaceholderText = placeholderText;
    }

    public override void ClearValue()
    {
        Text = string.Empty;
    }
}

public partial class CommandParameterRun : ParameterValueRun, ICommandParameterRun
{
    private string? _displayText;

    public virtual string? DisplayText
    {
        get => _displayText;
        set
        {
            _displayText = value;
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    private ICommand? _command;

    public virtual ICommand? Command
    {
        get => _command;
        set
        {
            _command = value;
            OnPropertyChanged(nameof(Command));
        }
    }

    private IIconInfo? _icon;

    public virtual IIconInfo? Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            OnPropertyChanged(nameof(Icon));
        }
    }

    public override bool NeedsValue => Value == null;

    public virtual ICommand? GetSelectValueCommand(ulong hostHwnd)
    {
        if (Command is IRequiresHostHwnd requiresHwnd)
        {
            requiresHwnd.SetHostHwnd((nint)hostHwnd);
        }

        return Command;
    }

    // Toolkit helper: a value for the parameter
    private object? _value;

    public virtual object? Value
    {
        get => _value;
        set
        {
            _value = value;
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(NeedsValue));
        }
    }

    public override void ClearValue()
    {
        Value = null;
    }
}

internal sealed partial class FilePickerParameterRun : CommandParameterRun
{
    public StorageFile? File { get; private set; }

    public override object? Value => File;

    public override string? DisplayText { get => File != null ? File.DisplayName : "Select a file"; }

    public FilePickerParameterRun()
    {
        var command = new FilePickerCommand();
        command.FileSelected += (s, file) =>
        {
            File = file;

            // Value = file != null ? file : (object?)null;
            // OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(NeedsValue));
            OnPropertyChanged(nameof(DisplayText));
        };
        PlaceholderText = "Select a file";
        Icon = new IconInfo("\uE710"); // Add
        Command = command;
    }

    public override void ClearValue()
    {
        File = null;
    }

    private sealed partial class FilePickerCommand : InvokableCommand, IRequiresHostHwnd
    {
        public override IconInfo Icon => new("\uE710"); // Add

        public override string Name => "Pick a file";

        public event EventHandler<StorageFile?>? FileSelected;

        private nint _hostHwnd;

        public void SetHostHwnd(nint hostHwnd)
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
            var picker = new FileOpenPicker() { };
            picker.FileTypeFilter.Add("*");

            // You need to initialize the picker with a window handle in WinUI 3 desktop apps
            // See https://learn.microsoft.com/en-us/windows/apps/design/controls/file-open-picker
            WinRT.Interop.InitializeWithWindow.Initialize(picker, (nint)_hostHwnd);

            var file = await picker.PickSingleFileAsync();
            FileSelected?.Invoke(this, file);
        }
    }
}

public partial class SelectParameterCommand<T> : InvokableCommand
{
    public event TypedEventHandler<object, T>? ValueSelected;

    private T _value;

    public T Value
    {
        get => _value; protected set { _value = value; }
    }

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

public partial class StaticParameterList<T> : ListPage
{
    public event TypedEventHandler<object, T>? ValueSelected;

    private readonly IEnumerable<T> _values;
    private readonly List<IListItem> _items = new();
    private bool _isInitialized;
    private Func<T, ListItem, ListItem> _customizeListItemsCallback;

    // ctor takes an IEnumerable<T> values, and a function to customize the ListItem's depending on the value
    public StaticParameterList(IEnumerable<T> values, Func<T, ListItem> customizeListItem)
    {
        _values = values;
        _customizeListItemsCallback = (value, listItem) =>
        {
            customizeListItem(value);
            return listItem;
        };
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

public abstract partial class ParametersPage : Page, IParametersPage
{
    public abstract IListItem Command { get; }

    public abstract IParameterRun[] Parameters { get; }
}

#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
#nullable disable
