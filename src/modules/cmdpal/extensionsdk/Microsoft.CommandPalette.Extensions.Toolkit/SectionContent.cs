// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class SectionContent : BaseObservable, ISectionContent
{
    public virtual string Title { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual IContent[] Content { get; set => SetProperty(ref field, value); } = [];

    public virtual int PreviewItemCount { get; set => SetProperty(ref field, value); } = -1;
}
