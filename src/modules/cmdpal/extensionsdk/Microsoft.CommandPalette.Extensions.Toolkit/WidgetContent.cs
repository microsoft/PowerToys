// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Metadata and lifecycle for Adaptive Card content that Command Palette can
/// expose through the Windows Widgets Board. Extensions are responsible for
/// persisting per-instance state using the instance ID passed to
/// CommandProvider.GetWidget.
/// </summary>
public partial class WidgetContent : BaseObservable, IWidgetContent
{
    public virtual string Id { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual string Title { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual string Description { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual IconInfo Icon { get; set => SetProperty(ref field, value); } = new();

    public virtual WidgetSize[] SupportedSizes { get; set => SetProperty(ref field, value); } = [];

    public virtual bool AllowMultiple { get; set => SetProperty(ref field, value); }

    public virtual IFormContent? Content { get; set => SetProperty(ref field, value); }

    IIconInfo IWidgetContent.Icon => Icon;

    IFormContent IWidgetContent.Content => Content!;

    public virtual void Activate()
    {
    }

    public virtual void Deactivate()
    {
    }

    public virtual void Delete()
    {
    }
}
