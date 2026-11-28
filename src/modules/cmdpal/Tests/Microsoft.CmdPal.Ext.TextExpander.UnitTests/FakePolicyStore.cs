// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.TextExpander.Snippets;

namespace Microsoft.CmdPal.Ext.TextExpander.UnitTests;

internal sealed class FakePolicyStore : IPolicyStore
{
    public Dictionary<string, int> Values { get; } = new();

    public int? Read(string valueName) => Values.TryGetValue(valueName, out var value) ? value : null;
}
