// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using WinRT;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class CommandViewModel : ExtensionObjectViewModel
{
    public ExtensionObject<ICommand> Model { get; private set; } = new(null);

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
        if (model == null)
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
        if (model == null)
        {
            return;
        }

        var ico = model.Icon;
        if (ico != null)
        {
            Icon = new(ico);
            Icon.InitializeProperties();
            UpdateProperty(nameof(Icon));
        }

        if (model is ICommand2 command2)
        {
            var p = command2.OtherProperties;
            Debug.WriteLine($"{Name} had properties = {{");
            foreach (var kv in p)
            {
                Debug.WriteLine($"\t{kv.Key}: {kv.Value}");
            }

            Debug.WriteLine("}");
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
        if (model == null)
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
        }

        UpdateProperty(propertyName);
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        Icon = new(null); // necessary?

        var model = Model.Unsafe;
        if (model != null)
        {
            model.PropChanged -= Model_PropChanged;
        }
    }
}
