// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using PowerToys.DSC.Properties;

namespace PowerToys.DSC.Options;

/// <summary>
/// Represents an option for specifying the module name for the dsc command.
/// </summary>
public sealed class ModuleOption : Option<string?>
{
    public ModuleOption()
        : base("--module", Resources.ModuleOptionDescription)
    {
    }
}
