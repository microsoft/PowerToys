// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class ConfirmationArgs : IConfirmationArgs
{
    public string? Title { get; set; }

    public string? Description { get; set; }

    public ICommand? PrimaryCommand { get; set; }

    public bool IsPrimaryCommandCritical { get; set; }
}
