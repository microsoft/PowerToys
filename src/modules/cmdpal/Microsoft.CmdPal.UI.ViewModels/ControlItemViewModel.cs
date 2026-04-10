// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ControlItemViewModel : ExtensionObjectViewModel
{
    private readonly ExtensionObject<IControlItem> _model;

    public string Name { get; private set; } = string.Empty;

    public ControlType Type { get; private set; }

    public double Value
    {
        get => field;
        set
        {
            if (field != value)
            {
                field = value;
                UpdateProperty(nameof(Value));
                UpdateProperty(nameof(IsOn));

                var model = _model.Unsafe;
                if (model is not null)
                {
                    model.Value = value;
                }
            }
        }
    }

    public bool IsOn
    {
        get => Value != 0;
        set => Value = value ? 1.0 : 0.0;
    }

    public double Minimum { get; private set; }

    public double Maximum { get; private set; } = 100.0;

    public double StepValue { get; private set; } = 1.0;

    public Visibility IsToggle => Type == ControlType.Toggle ? Visibility.Visible : Visibility.Collapsed;

    public Visibility IsSlider => Type == ControlType.Slider ? Visibility.Visible : Visibility.Collapsed;

    public ControlItemViewModel(IControlItem item, WeakReference<IPageContext> context)
        : base(context)
    {
        _model = new(item);
    }

    public override void InitializeProperties()
    {
        var model = _model.Unsafe;
        if (model is null)
        {
            return;
        }

        Name = model.Name;
        Type = model.Type;
        Value = model.Value;
        Minimum = model.Minimum;
        Maximum = model.Maximum;
        StepValue = model.StepValue;

        UpdateProperty(nameof(Name));
        UpdateProperty(nameof(Type));
        UpdateProperty(nameof(Value));
        UpdateProperty(nameof(IsOn));
        UpdateProperty(nameof(Minimum));
        UpdateProperty(nameof(Maximum));
        UpdateProperty(nameof(StepValue));
        UpdateProperty(nameof(IsToggle));
        UpdateProperty(nameof(IsSlider));

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

    private void FetchProperty(string propertyName)
    {
        var model = _model.Unsafe;
        if (model is null)
        {
            return;
        }

        switch (propertyName)
        {
            case nameof(Name):
                Name = model.Name;
                break;
            case nameof(Value):
                Value = model.Value;
                break;
        }

        UpdateProperty(propertyName);
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();
        var model = _model.Unsafe;
        if (model is not null)
        {
            model.PropChanged -= Model_PropChanged;
        }
    }
}
