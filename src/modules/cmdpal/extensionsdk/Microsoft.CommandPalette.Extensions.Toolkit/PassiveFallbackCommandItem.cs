// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Defines a fallback that the host formats without sending each query to the extension.
/// </summary>
public partial class PassiveFallbackCommandItem : FallbackCommandItem3
{
    public PassiveFallbackCommandItem(string displayTitle, string id)
        : base(displayTitle, id)
    {
    }

    public PassiveFallbackCommandItem(ICommand command, string displayTitle, string id)
        : base(command, displayTitle, id)
    {
    }

    public override FallbackCommandMode Mode => FallbackCommandMode.Passive;

    public override void UpdateQuery(string query)
    {
    }
}
