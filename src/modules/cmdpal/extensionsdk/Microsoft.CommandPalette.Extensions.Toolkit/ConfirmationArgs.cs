// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class ConfirmationArgs : IConfirmationArgs
{
    public virtual string? Title { get; set; }

    public virtual string? Description { get; set; }

    public virtual ICommand? PrimaryCommand { get; set; }

    public virtual bool IsPrimaryCommandCritical { get; set; }
}
