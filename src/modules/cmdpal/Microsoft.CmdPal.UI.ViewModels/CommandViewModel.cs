// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using WinRT;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class CommandViewModel : ExtensionObjectViewModel
{
    public ExtensionObject<ICommand> Model { get; private set; } = new(null);

    public bool IsSet => Model.Unsafe is not null;

    protected bool IsInitialized { get; private set; }

    protected bool IsFastInitialized { get; private set; }

    public bool HasIcon => Icon.IsSet;

    // These are properties that are "observable" from the extension object
    // itself, in the sense that they get raised by PropChanged events from the
    // extension. However, we don't want to actually make them
    // [ObservableProperty]s, because PropChanged comes in off the UI thread,
    // and ObservableProperty is not smart enough to raise the PropertyChanged
    // on the UI thread.
    public string Id { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public IconInfoViewModel Icon { get; private set; }

    // UNDER NO CIRCUMSTANCES MAY SOMEONE WRITE TO THIS DICTIONARY.
    // This is our copy of the data from the extension.
    // Adding values to it does not add to the extension.
    // Modifying it will not modify the extension
    // (except it might, if the dictionary was passed by ref)
    private Dictionary<string, ExtensionObject<object>>? _properties;

    public IReadOnlyDictionary<string, ExtensionObject<object>>? Properties => _properties?.AsReadOnly();

    public CommandViewModel(ICommand? command, WeakReference<IPageContext> pageContext)
        : base(pageContext)
    {
        Model = new(command);
        Icon = new(null);
    }

    public void FastInitializeProperties()
    {
        if (IsFastInitialized)
        {
            return;
        }

        var model = Model.Unsafe;
        if (model is null)
        {
            return;
        }

        Id = model.Id ?? string.Empty;
        Name = model.Name ?? string.Empty;
        IsFastInitialized = true;
    }

    public override void InitializeProperties()
    {
        if (IsInitialized)
        {
            return;
        }

        if (!IsFastInitialized)
        {
            FastInitializeProperties();
        }

        var model = Model.Unsafe;
        if (model is null)
        {
            return;
        }

        var ico = model.Icon;
        if (ico is not null)
        {
            Icon = new(ico);
            Icon.InitializeProperties();
            UpdateProperty(nameof(Icon));
        }

<<<<<<< HEAD:src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/CommandViewModel.cs
        if (model is IExtendedAttributesProvider command2)
=======
        if (model is IHaveProperties command2)
>>>>>>> 940e71f2a (stupid levels returning to nominal values):src/modules/cmdpal/Microsoft.CmdPal.Core.ViewModels/CommandViewModel.cs
        {
            UpdatePropertiesFromExtension(command2);
        }

        if (model is IHaveProperties command3)
        {
            var p = command3.Properties;
            Debug.WriteLine($"{Name} can haz properties = {{");
            foreach (var kv in p)
            {
                Debug.WriteLine($"\t{kv.Key}: {kv.Value}");
            }

            Debug.WriteLine("}");
        }

        if (Name == "Whatever" || Name == "Revetahw" || Name == "Another")
        {
            var invokable = model as IInvokableCommand;
            var page = model as IPage;
            var props = model as IHaveProperties;
            var com2 = model as ICommand2;
            Debug.WriteLine($"{invokable},{page},{props},{com2}");

            var f = model as IWinRTObject;
            if (f != null)
            {
                var types = f.AdditionalTypeData;
                foreach (var t in types)
                {
                    Debug.WriteLine($"{t.Key}, {t.Value}");
                }

                Debug.WriteLine(string.Empty);
            }
        }

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
            ShowException(ex, Name);
        }
    }

    protected void FetchProperty(string propertyName)
    {
        var model = Model.Unsafe;
        if (model is null)
        {
            return; // throw?
        }

        switch (propertyName)
        {
            case nameof(Name):
                Name = model.Name;
                break;
            case nameof(Icon):
                var iconInfo = model.Icon;
                Icon = new(iconInfo);
                Icon.InitializeProperties();
                break;
<<<<<<< HEAD:src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/CommandViewModel.cs
=======
            case nameof(IHaveProperties.Properties):
                if (model is IHaveProperties command2)
                {
                    Properties = command2.Properties;
                }
                else
                {
                    Properties = null;
                }

                break;
>>>>>>> 940e71f2a (stupid levels returning to nominal values):src/modules/cmdpal/Microsoft.CmdPal.Core.ViewModels/CommandViewModel.cs
        }

        UpdateProperty(propertyName);
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        Icon = new(null); // necessary?

        var model = Model.Unsafe;
        if (model is not null)
        {
            model.PropChanged -= Model_PropChanged;
        }
    }

    private void UpdatePropertiesFromExtension(IExtendedAttributesProvider? model)
    {
        var propertiesFromExtension = model?.GetProperties();
        if (propertiesFromExtension == null)
        {
            _properties = null;
            return;
        }

        _properties = [];

        // COPY the properties into us.
        // The IDictionary that was passed to us may be marshalled by-ref or by-value, we _don't know_.
        //
        // If it's by-ref, the values are arbitrary objects that are out-of-proc.
        // If it's bu-value, then everything is in-proc, and we can't mutate the data.
        foreach (var property in propertiesFromExtension)
        {
            _properties.Add(property.Key, new(property.Value));
        }
    }
}
