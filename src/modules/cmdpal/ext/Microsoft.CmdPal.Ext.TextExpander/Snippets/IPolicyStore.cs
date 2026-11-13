// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.TextExpander.Snippets;

/// <summary>Reads PowerToys' group policy values.</summary>
internal interface IPolicyStore
{
    /// <summary>Reads a DWORD policy value, the machine's before the user's. Null when neither sets one.</summary>
    int? Read(string valueName);
}
