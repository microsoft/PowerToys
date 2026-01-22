// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerToysExtension.Modules;

/// <summary>
/// Base contract for a PowerToys module to expose its command palette entries.
/// </summary>
internal abstract class ModuleCommandProvider
{
    public abstract IEnumerable<ListItem> BuildCommands();
}
