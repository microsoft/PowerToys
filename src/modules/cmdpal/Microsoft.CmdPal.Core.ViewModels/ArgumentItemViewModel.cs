// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class ArgumentItemViewModel : ExtensionObjectViewModel
{
    public ExtensionObject<ICommandArgument> Model => _model;

    private readonly ExtensionObject<ICommandArgument> _model = new(null);

    public ParameterType Type { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool Required { get; private set; }

    private string ModelDisplayName { get; set; } = string.Empty;

    public string DisplayName => string.IsNullOrEmpty(ModelDisplayName) ? Name : ModelDisplayName;

    // TODO! This should be an ExtensionObject<object> since it's out-of-proc
    public object? Value
    {
        get; set
        {
            field = value;
            SafeSetValue(value);
        }
    }

    public ArgumentItemViewModel(ExtensionObject<ICommandArgument> model, WeakReference<IPageContext> pageContext)
        : base(pageContext)
    {
        _model = model;
    }

    public override void InitializeProperties()
    {
        var model = _model.Unsafe;
        if (model == null)
        {
            return;
        }

        Type = model.Type;
        Name = model.Name;
        Required = model.Required;
        Value = model.Value;
        ModelDisplayName = model.DisplayName;

        // Register for property changes
        model.PropChanged += Model_PropChanged;
    }

    private void Model_PropChanged(object sender, IPropChangedEventArgs args)
    {
        try
        {
            FetchProperty(args.PropertyName);
        }
        catch (Exception ex)
        {
            ShowException(ex);
        }
    }

    protected virtual void FetchProperty(string propertyName)
    {
        var model = this._model.Unsafe;
        if (model == null)
        {
            return; // throw?
        }

        switch (propertyName)
        {
            case nameof(ICommandArgument.Type):
                if (model.Type == Type)
                {
                    return;
                }

                Type = model.Type;
                break;
            case nameof(ICommandArgument.Name):
                if (model.Name == Name)
                {
                    return;
                }

                Name = model.Name;
                UpdateProperty(nameof(DisplayName));
                break;
            case nameof(ICommandArgument.Required):
                if (model.Required == Required)
                {
                    return;
                }

                Required = model.Required;
                break;
            case nameof(ICommandArgument.Value):
                if (model.Value == Value)
                {
                    return;
                }

                Value = model.Value;
                break;
            case nameof(ICommandArgument.DisplayName):
                if (model.DisplayName == ModelDisplayName)
                {
                    return;
                }

                ModelDisplayName = model.DisplayName;
                break;
        }

        UpdateProperty(propertyName);
    }

    private void SafeSetValue(object? value)
    {
        _ = Task.Run(() => SafeSetValueSynchronous(value));
    }

    private void SafeSetValueSynchronous(object? value)
    {
        try
        {
            var model = _model.Unsafe;
            if (model == null)
            {
                return;
            }

            model.Value = value;
        }
        catch
        {
        }
    }

    public void OpenPicker()
    {
        var model = _model.Unsafe;
        if (model == null)
        {
            return;
        }

        WeakReferenceMessenger.Default.Send<RequestOpenPickerMessage>(new(_model));
    }
}
