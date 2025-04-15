// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ProgressViewModel : ExtensionObjectViewModel
{
    public ExtensionObject<IProgressState> Model { get; }

    public bool IsIndeterminate { get; private set; }

    public uint ProgressPercent { get; private set; }

    public ProgressViewModel(IProgressState progress, WeakReference<IPageContext> context)
        : base(context)
    {
        Model = new(progress);
    }

    public override void InitializeProperties()
    {
        var model = Model.Unsafe;
        if (model == null)
        {
            return; // throw?
        }

        IsIndeterminate = model.IsIndeterminate;
        ProgressPercent = model.ProgressPercent;

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
        var model = this.Model.Unsafe;
        if (model == null)
        {
            return; // throw?
        }

        switch (propertyName)
        {
            case nameof(IsIndeterminate):
                this.IsIndeterminate = model.IsIndeterminate;
                break;
            case nameof(ProgressPercent):
                this.ProgressPercent = model.ProgressPercent;
                break;
        }

        UpdateProperty(propertyName);
    }
}
