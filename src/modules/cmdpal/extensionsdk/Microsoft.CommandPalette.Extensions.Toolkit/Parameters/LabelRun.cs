// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class LabelRun : BaseObservable, ILabelRun
{
    public virtual string? Text { get; set => SetProperty(ref field, value); } = string.Empty;

    public LabelRun(string text)
    {
        Text = text;
    }

    public LabelRun()
    {
    }
}
