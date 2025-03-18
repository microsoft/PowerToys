// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContentMarkdownViewModel(IMarkdownContent _markdown, WeakReference<IPageContext> context) :
    ContentViewModel(context)
{
    public ExtensionObject<IMarkdownContent> Model { get; } = new(_markdown);

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public string Body { get; protected set; } = string.Empty;

    public override void InitializeProperties()
    {
        var model = Model.Unsafe;
        if (model == null)
        {
            return;
        }

        Body = model.Body;
        UpdateProperty(nameof(Body));

        model.PropChanged += Model_PropChanged;
    }

    private void Model_PropChanged(object sender, IPropChangedEventArgs args)
    {
        try
        {
            var propName = args.PropertyName;
            FetchProperty(propName);
        }
        catch (Exception ex)
        {
            ShowException(ex);
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
            case nameof(Body):
                Body = model.Body;
                break;
        }

        UpdateProperty(propertyName);
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();
        var model = Model.Unsafe;
        if (model != null)
        {
            model.PropChanged -= Model_PropChanged;
        }
    }
}
