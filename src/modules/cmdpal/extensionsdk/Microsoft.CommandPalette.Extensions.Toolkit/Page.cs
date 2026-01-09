// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class Page : Command, IPage
{
    public virtual bool IsLoading { get; set => SetProperty(ref field, value); }

    public virtual string Title { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual OptionalColor AccentColor { get; set => SetProperty(ref field, value); }
}
